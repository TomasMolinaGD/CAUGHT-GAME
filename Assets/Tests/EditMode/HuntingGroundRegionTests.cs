using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class HuntingGroundRegionTests
{
    private GameObject gridObject;
    private NavigationGrid grid;
    private GameObject regionObject;
    private HuntingGroundRegion region;

    [SetUp]
    public void SetUp()
    {
        gridObject = new GameObject("Hunting Ground Grid Test");
        grid = gridObject.AddComponent<NavigationGrid>();
        Assert.That(grid.TryApplyGridSize(4, 3, 2f, true, out _), Is.True);

        regionObject = new GameObject("Hunting Ground Region Test");
        region = regionObject.AddComponent<HuntingGroundRegion>();
        SerializedObject serializedRegion = new SerializedObject(region);
        serializedRegion.FindProperty("navigationGrid").objectReferenceValue = grid;
        serializedRegion.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        if (regionObject != null) Object.DestroyImmediate(regionObject);
        if (gridObject != null) Object.DestroyImmediate(gridObject);
    }

    [Test]
    public void ContainsWorldPosition_ReturnsTrueForWalkableHuntingGroundCell()
    {
        SetCell(1, 2, true, NavigationGridZone.HuntingGround);

        Assert.That(region.ContainsWorldPosition(grid.GridToWorldPosition(1, 2)), Is.True);
    }

    [Test]
    public void ContainsWorldPosition_ReturnsFalseForBlockedHuntingGroundCell()
    {
        SetCell(1, 2, false, NavigationGridZone.HuntingGround);

        Assert.That(region.ContainsWorldPosition(grid.GridToWorldPosition(1, 2)), Is.False);
    }

    [Test]
    public void ContainsWorldPosition_ReturnsFalseForWalkableCellOutsideHuntingGround()
    {
        SetCell(1, 2, true, NavigationGridZone.Normal);

        Assert.That(region.ContainsWorldPosition(grid.GridToWorldPosition(1, 2)), Is.False);
    }

    [Test]
    public void ContainsWorldPosition_ReturnsFalseOutsideGridBounds()
    {
        Vector3 outsideGrid = grid.transform.TransformPoint(new Vector3(-0.01f, 0f, 1f));

        Assert.That(region.ContainsWorldPosition(outsideGrid), Is.False);
    }

    [Test]
    public void ContainsWorldPosition_UsesGridTranslationAndRotation()
    {
        SetCell(1, 2, true, NavigationGridZone.HuntingGround);
        grid.transform.SetPositionAndRotation(new Vector3(10f, 3f, -5f), Quaternion.Euler(0f, 90f, 0f));

        Assert.That(region.ContainsWorldPosition(grid.GridToWorldPosition(1, 2)), Is.True);
    }

    [Test]
    public void ContainsWorldPosition_SupportsNonRectangularAndDisconnectedCells()
    {
        SetCell(0, 0, true, NavigationGridZone.HuntingGround);
        SetCell(0, 2, true, NavigationGridZone.HuntingGround);
        SetCell(2, 1, true, NavigationGridZone.HuntingGround);

        Assert.That(region.ContainsWorldPosition(grid.GridToWorldPosition(0, 0)), Is.True);
        Assert.That(region.ContainsWorldPosition(grid.GridToWorldPosition(0, 2)), Is.True);
        Assert.That(region.ContainsWorldPosition(grid.GridToWorldPosition(2, 1)), Is.True);
        Assert.That(region.ContainsWorldPosition(grid.GridToWorldPosition(1, 1)), Is.False);
    }

    [Test]
    public void TryGetRandomWorldPosition_ReturnsFalseForEmptyRegion()
    {
        Assert.That(region.TryGetRandomWorldPosition(out Vector3 position), Is.False);
        Assert.That(position, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void TryGetRandomWorldPosition_AlwaysReturnsAValidCellCenter()
    {
        SetCell(0, 0, true, NavigationGridZone.HuntingGround);
        SetCell(2, 3, true, NavigationGridZone.HuntingGround);

        for (int i = 0; i < 20; i++)
        {
            Assert.That(region.TryGetRandomWorldPosition(out Vector3 position), Is.True);
            Assert.That(region.ContainsWorldPosition(position), Is.True);
            Assert.That(grid.TryWorldToGrid(position, out int row, out int column), Is.True);
            Assert.That(grid.TryGetCell(row, column, out NavigationGridCell cell), Is.True);
            Assert.That(cell.Zone, Is.EqualTo(NavigationGridZone.HuntingGround));
            Assert.That(cell.Walkable, Is.True);
        }
    }

    [Test]
    public void ContainsWorldPosition_UsesConsistentHalfOpenCellEdges()
    {
        SetCell(0, 0, true, NavigationGridZone.HuntingGround);
        SetCell(0, 1, true, NavigationGridZone.Normal);
        Vector3 sharedEdge = grid.transform.TransformPoint(new Vector3(2f, 0f, 1f));
        Vector3 outerEdge = grid.transform.TransformPoint(new Vector3(8f, 0f, 1f));

        Assert.That(region.ContainsWorldPosition(sharedEdge), Is.False);
        Assert.That(region.ContainsWorldPosition(outerEdge), Is.False);
    }

    [Test]
    public void ContainsWorldPosition_ReflectsGridChangesWithoutRebuildingCache()
    {
        SetCell(1, 2, true, NavigationGridZone.HuntingGround);
        Vector3 position = grid.GridToWorldPosition(1, 2);
        Assert.That(region.ContainsWorldPosition(position), Is.True);

        Assert.That(grid.SetWalkable(1, 2, false), Is.True);

        Assert.That(region.ContainsWorldPosition(position), Is.False);
    }

    private void SetCell(int row, int column, bool walkable, NavigationGridZone zone)
    {
        Assert.That(grid.SetWalkable(row, column, walkable), Is.True);
        Assert.That(grid.SetZone(row, column, zone), Is.True);
    }
}
