using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SafeZonePathSelectorTests
{
    private GameObject gridObject;
    private NavigationGrid grid;

    [SetUp]
    public void SetUp()
    {
        gridObject = new GameObject("Safe Zone Selector Test Grid");
        grid = gridObject.AddComponent<NavigationGrid>();
        Assert.That(grid.TryApplyGridSize(5, 5, 4f, true, out _), Is.True);
    }

    [TearDown]
    public void TearDown()
    {
        if (gridObject != null)
        {
            Object.DestroyImmediate(gridObject);
        }
    }

    [Test]
    public void SelectsReachableSafeZoneUsingHunterPath()
    {
        SetZone(2, 4, "SafeZone_A");

        Assert.That(SafeZonePathSelector.TrySelect(grid, grid.GridToWorldPosition(2, 0), out SafeZonePathSelection selection), Is.True);
        Assert.That(selection.ZoneId, Is.EqualTo("SafeZone_A"));
        Assert.That(selection.DestinationCell, Is.EqualTo(new Vector2Int(4, 2)));
        Assert.That(selection.Path.Status, Is.EqualTo(NavigationPathStatus.Success));
    }

    [Test]
    public void SelectsLowerAStarCostEvenWhenAnotherZoneIsCloserInStraightLine()
    {
        SetZone(0, 4, "SafeZone_A");
        SetZone(2, 2, "SafeZone_B");
        Assert.That(grid.SetWalkable(2, 1, false), Is.True);
        Assert.That(grid.SetWalkable(1, 2, false), Is.True);
        Assert.That(grid.SetWalkable(3, 2, false), Is.True);

        Assert.That(SafeZonePathSelector.TrySelect(grid, grid.GridToWorldPosition(2, 0), out SafeZonePathSelection selection), Is.True);
        Assert.That(selection.ZoneId, Is.EqualTo("SafeZone_A"));
        Assert.That(selection.Path.TotalCost, Is.EqualTo(24f).Within(0.0001f));
    }

    [Test]
    public void IgnoresBlockedSafeZoneCells()
    {
        SetZone(2, 4, "SafeZone_A", false);

        Assert.That(SafeZonePathSelector.TrySelect(grid, grid.GridToWorldPosition(2, 0), out _), Is.False);
    }

    [Test]
    public void IgnoresSafeZonesWithoutAPath()
    {
        SetZone(2, 4, "SafeZone_A");
        for (int row = 0; row < grid.Rows; row++)
        {
            Assert.That(grid.SetWalkable(row, 2, false), Is.True);
        }

        Assert.That(SafeZonePathSelector.TrySelect(grid, grid.GridToWorldPosition(2, 0), out _), Is.False);
    }

    [Test]
    public void ResolvesEqualCostDestinationsDeterministically()
    {
        SetZone(2, 4, "SafeZone_B");
        SetZone(4, 2, "SafeZone_A");

        Assert.That(SafeZonePathSelector.TrySelect(grid, grid.GridToWorldPosition(2, 0), out SafeZonePathSelection selection), Is.True);
        Assert.That(selection.ZoneId, Is.EqualTo("SafeZone_A"));
        Assert.That(selection.DestinationCell, Is.EqualTo(new Vector2Int(2, 4)));
    }

    [Test]
    public void ReturnsFalseWhenNoSafeZoneIsReachable()
    {
        Assert.That(SafeZonePathSelector.TrySelect(grid, grid.GridToWorldPosition(0, 0), out SafeZonePathSelection selection), Is.False);
        Assert.That(selection, Is.Null);
    }

    [Test]
    public void NearestDestinationWinsButItsExecutedRouteCanDetour()
    {
        SetZone(2, 4, "SafeZone_ShortRisky");
        SetZone(0, 0, "SafeZone_LongSafe");
        Vector3 hunter = grid.GridToWorldPosition(2, 2);
        Vector3 wolf = Vector3.Lerp(
            hunter, grid.GridToWorldPosition(2, 3), 0.5f);

        Assert.That(SafeZonePathSelector.TrySelect(
            grid, hunter, wolf, 0.6f, 0.2f, out SafeZonePathSelection selection), Is.True);

        Assert.That(selection.ZoneId, Is.EqualTo("SafeZone_ShortRisky"));
        Assert.That(selection.DestinationCost, Is.EqualTo(8f));
        Assert.That(selection.Path.TotalCost, Is.GreaterThan(selection.DestinationCost));
        Assert.That(selection.CrossesCaptureRadius, Is.False);
    }

    [Test]
    public void AllowsTemporaryApproachThatRemainsOutsideDangerRadius()
    {
        SetZone(2, 4, "SafeZone_A");
        // Single bent corridor: its first move toward the Wolf is unavoidable.
        for (int r = 0; r < 5; r++)
            for (int c = 0; c < 5; c++)
                grid.SetWalkable(r, c, (r == 2 && c != 2) || (r == 3 && c >= 1 && c <= 3));
        Vector3 hunter = grid.GridToWorldPosition(2, 0);
        Vector3 wolf = grid.GridToWorldPosition(0, 2);
        float startDistance = Vector3.Distance(hunter, wolf);

        Assert.That(SafeZonePathSelector.TrySelect(
            grid, hunter, wolf, 0.5f, 0.25f, out SafeZonePathSelection selection), Is.True);

        Assert.That(selection.CrossesDangerRadius, Is.False);
        Assert.That(selection.MinimumThreatDistance, Is.LessThan(startDistance));
    }

    [Test]
    public void EqualCostRoutesUseRiskToPreferMarginOnlyOverCaptureCrossing()
    {
        SetZone(2, 4, "SafeZone_CaptureCrossing");
        SetZone(0, 2, "SafeZone_MarginOnly");
        Vector3 hunter = grid.GridToWorldPosition(2, 2);
        Vector3 wolf = Vector3.Lerp(
            hunter, grid.GridToWorldPosition(2, 3), 0.5f);

        Assert.That(SafeZonePathSelector.TrySelect(
            grid, hunter, wolf, 1f, 2f, out SafeZonePathSelection selection), Is.True);

        Assert.That(selection.ZoneId, Is.EqualTo("SafeZone_MarginOnly"));
        Assert.That(selection.Path.TotalCost, Is.EqualTo(8f));
        Assert.That(selection.CrossesDangerRadius, Is.True);
        Assert.That(selection.CrossesCaptureRadius, Is.False);
    }

    [Test]
    public void AvoidsDangerousInitialBacktrackingWhenSafeAlternativeExists()
    {
        SetZone(2, 0, "SafeZone_Backtrack");
        SetZone(2, 4, "SafeZone_Away");
        Vector3 hunter = grid.GridToWorldPosition(2, 2);
        Vector3 wolf = grid.GridToWorldPosition(2, 0);

        Assert.That(SafeZonePathSelector.TrySelect(
            grid, hunter, wolf, 0.6f, 0.3f, out SafeZonePathSelection selection), Is.True);

        Assert.That(selection.ZoneId, Is.EqualTo("SafeZone_Away"));
        Assert.That(selection.CrossesDangerRadius, Is.False);
    }

    [Test]
    public void DiagnosticsAndNormalSelectionUseSameDetourForMidSegmentApproach()
    {
        SetZone(2, 3, "Short");
        SetZone(2, 0, "Away");
        Vector3 hunter = grid.GridToWorldPosition(2, 2);
        Vector3 wolf = hunter + new Vector3(2f, 0f, 1.4f);
        Assert.That(SafeZonePathSelector.TrySelect(grid, hunter, wolf, 0.9f, 0.35f,
            out var plain), Is.True);
        var away = new System.Collections.Generic.List<Vector2Int>();
        Assert.That(SafeZonePathSelector.TrySelectWithDiagnostics(grid, hunter, wolf, 0.9f, 0.35f,
            out var traced, away), Is.True);

        Assert.That(traced.ZoneId, Is.EqualTo("Short"));
        Assert.That(traced.Path.Waypoints, Is.EqualTo(plain.Path.Waypoints));
        Assert.That(away, Is.EquivalentTo(new[] { new Vector2Int(0, 2) }));
        var direct = NavigationGridPathfinder.FindPath(grid, 2, 2, 2, 3, NavigationAgentType.Hunter);
        Vector3 first = direct.Waypoints[1];
        Assert.That(SafeZonePathSelector.FirstSegmentMinimumDistance(hunter, first, wolf),
            Is.EqualTo(1.4f).Within(0.0001f));
        Assert.That(Vector3.Distance(hunter, wolf), Is.EqualTo(Vector3.Distance(first, wolf)).Within(0.0001f));
        Assert.That(SafeZonePathSelector.EvaluateThreat(direct, hunter, wolf, 0.9f, 0.35f, 1)
            .InitialApproach, Is.GreaterThan(1f));
        Assert.That(SafeZonePathSelector.EvaluateThreat(traced.Path, hunter, wolf, 0.9f, 0.35f, 1)
            .InitialApproach, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void B3LoggedFleeChoosesSameNearestCellButLeavesAwayFromL1()
    {
        const string path = "Assets/Scenes/Tests/Parcial2_B3_WolfIntegration.unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            NavigationGrid b3 = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if ((b3 = root.GetComponentInChildren<NavigationGrid>()) != null) break;
            Assert.That(b3, Is.Not.Null);
            var hunter = new Vector3(82f, 0f, 43.135f);
            var wolf = new Vector3(97.649f, 0f, 34.341f);
            Assert.That(SafeZonePathSelector.TrySelect(b3, hunter, out var shortest), Is.True);
            Assert.That(shortest.DestinationCell, Is.EqualTo(new Vector2Int(26, 13)));
            Assert.That(shortest.Path.Waypoints[1].x, Is.EqualTo(86f));
            Assert.That(shortest.Path.Waypoints[1].z, Is.EqualTo(42f));
            Assert.That(SafeZonePathSelector.TrySelect(b3, hunter, wolf, 0.9f, 0.35f, out var flee), Is.True);
            Assert.That(flee.ZoneId, Is.EqualTo("SafeZone_B"));
            Assert.That(flee.DestinationCell, Is.EqualTo(shortest.DestinationCell));
            Assert.That(flee.DestinationCost, Is.EqualTo(shortest.Path.TotalCost));
            Assert.That(flee.UsesDetour, Is.True);
            Assert.That(Vector3.Dot(hunter - wolf, flee.Path.Waypoints[1] - hunter), Is.GreaterThanOrEqualTo(0f));
            Assert.That(flee.Path.Cells[flee.Path.Cells.Count - 1], Is.EqualTo(shortest.DestinationCell));
            for (int i = 1; i < flee.Path.Cells.Count; i++)
            {
                Vector2Int step = flee.Path.Cells[i] - flee.Path.Cells[i - 1];
                Assert.That(Mathf.Abs(step.x) + Mathf.Abs(step.y), Is.EqualTo(1));
                Assert.That(b3.TryGetCell(flee.Path.Cells[i].y, flee.Path.Cells[i].x, out var cell) && cell.Walkable, Is.True);
            }
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void ForcedCorridorKeepsReachablePathAndLeavesOtherAgentRulesUntouched()
    {
        for (int r = 0; r < 5; r++)
            for (int c = 0; c < 5; c++) grid.SetWalkable(r, c, r == 2);
        SetZone(2, 4, "OnlyExit");
        Vector3 start = grid.GridToWorldPosition(2, 0);
        Vector3 wolf = grid.GridToWorldPosition(2, 1);
        Assert.That(SafeZonePathSelector.TrySelect(grid, start, wolf, 0.9f, 0.35f, out var escape), Is.True);
        Assert.That(escape.UsesDetour, Is.False);
        Assert.That(escape.CrossesCaptureRadius, Is.True);
        var normal = NavigationGridPathfinder.FindPath(grid, 2, 0, 2, 4, NavigationAgentType.Hunter);
        Assert.That(escape.Path.Cells, Is.EqualTo(normal.Cells));
        var pursuit = NavigationGridPathfinder.FindPath(grid, 2, 0, 2, 3, NavigationAgentType.Wolf);
        Assert.That(pursuit.Status, Is.EqualTo(NavigationPathStatus.Success));
        Assert.That(pursuit.TotalCost, Is.EqualTo(12f));
        Assert.That(NavigationGridPathfinder.FindPath(grid, 2, 0, 2, 4, NavigationAgentType.Wolf).Status,
            Is.EqualTo(NavigationPathStatus.DestinationNotTraversable));
    }

    private void SetZone(int row, int column, string zoneId, bool walkable = true)
    {
        Assert.That(grid.SetWalkable(row, column, walkable), Is.True);
        Assert.That(grid.SetZone(row, column, NavigationGridZone.SafeZone, zoneId), Is.True);
    }
}
