using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEngine;

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

    private void SetZone(int row, int column, string zoneId, bool walkable = true)
    {
        Assert.That(grid.SetWalkable(row, column, walkable), Is.True);
        Assert.That(grid.SetZone(row, column, NavigationGridZone.SafeZone, zoneId), Is.True);
    }
}
