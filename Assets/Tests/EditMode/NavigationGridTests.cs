using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class NavigationGridTests
{
    private GameObject gridObject;
    private NavigationGrid grid;

    [SetUp]
    public void SetUp()
    {
        gridObject = new GameObject("Navigation Grid Test");
        grid = gridObject.AddComponent<NavigationGrid>();
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
    public void NewGrid_UsesConfiguredDimensionsAndRowMajorCellCount()
    {
        Assert.That(grid.Columns, Is.EqualTo(35));
        Assert.That(grid.Rows, Is.EqualTo(27));
        Assert.That(grid.CellSize, Is.EqualTo(4f));
        Assert.That(grid.CellCount, Is.EqualTo(945));

        Assert.That(grid.TryGetCell(0, 0, out NavigationGridCell first), Is.True);
        Assert.That(grid.TryGetCell(1, 0, out NavigationGridCell nextRow), Is.True);
        Assert.That(first.Row, Is.EqualTo(0));
        Assert.That(first.Column, Is.EqualTo(0));
        Assert.That(nextRow.Row, Is.EqualTo(1));
        Assert.That(nextRow.Column, Is.EqualTo(0));
    }

    [Test]
    public void EmptySerializedGrid_IsNotRegeneratedByQueries()
    {
        SerializedObject serializedGrid = new SerializedObject(grid);
        serializedGrid.FindProperty("cells").arraySize = 0;
        serializedGrid.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(grid.CellCount, Is.Zero);
        Assert.That(grid.TryGetCell(0, 0, out _), Is.False);
        Assert.That(grid.SetWalkable(0, 0, false), Is.False);
        Assert.That(grid.TryApplyGridSize(35, 27, 4f, true, out _), Is.False);
        Assert.That(grid.CellCount, Is.Zero);
    }

    [Test]
    public void PartiallySerializedGrid_IsRejectedWithoutChangingStoredCells()
    {
        SerializedObject serializedGrid = new SerializedObject(grid);
        serializedGrid.FindProperty("cells").arraySize = 1;
        serializedGrid.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(grid.CellCount, Is.EqualTo(1));
        Assert.That(grid.TryGetCell(0, 0, out _), Is.False);
        Assert.That(grid.SetWalkable(0, 0, false), Is.False);
        Assert.That(grid.TryApplyGridSize(35, 27, 4f, true, out _), Is.False);
        Assert.That(grid.CellCount, Is.EqualTo(1));
    }

    [Test]
    public void CoordinateQueries_RejectNegativeAndOutOfRangeCoordinates()
    {
        Assert.That(grid.IsValidCoordinate(0, 0), Is.True);
        Assert.That(grid.IsValidCoordinate(-1, 0), Is.False);
        Assert.That(grid.IsValidCoordinate(0, -1), Is.False);
        Assert.That(grid.IsValidCoordinate(27, 0), Is.False);
        Assert.That(grid.IsValidCoordinate(0, 35), Is.False);
        Assert.That(grid.TryGetCell(27, 0, out _), Is.False);
        Assert.That(grid.TryGetCell(0, 35, out _), Is.False);
        Assert.Throws<System.ArgumentOutOfRangeException>(() => grid.GridToWorldPosition(27, 0));
    }

    [Test]
    public void GridWorldConversions_RespectTransformPositionRotationAndScale()
    {
        grid.transform.position = new Vector3(414f, 2f, 569f);
        grid.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        grid.transform.localScale = new Vector3(2f, 1f, 0.5f);

        Vector3 expected = grid.transform.TransformPoint(new Vector3(2f, 0f, 2f));
        Vector3 actual = grid.GridToWorldPosition(0, 0);

        Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f));
        Assert.That(grid.TryWorldToGrid(actual, out int row, out int column), Is.True);
        Assert.That(row, Is.EqualTo(0));
        Assert.That(column, Is.EqualTo(0));
        Assert.That(grid.TryWorldToGrid(grid.transform.TransformPoint(new Vector3(-0.1f, 0f, 2f)), out _, out _), Is.False);
    }

    [Test]
    public void CellEdits_ChangeWalkabilityAndZoneIdIndependently()
    {
        Assert.That(grid.SetZone(4, 7, NavigationGridZone.SafeZone, "SafeZone_A"), Is.True);
        Assert.That(grid.SetWalkable(4, 7, false), Is.True);
        Assert.That(grid.TryGetCell(4, 7, out NavigationGridCell blockedSafeCell), Is.True);
        Assert.That(blockedSafeCell.Walkable, Is.False);
        Assert.That(blockedSafeCell.Zone, Is.EqualTo(NavigationGridZone.SafeZone));
        Assert.That(blockedSafeCell.ZoneId, Is.EqualTo("SafeZone_A"));

        Assert.That(grid.SetZone(4, 7, NavigationGridZone.HuntingGround), Is.True);
        Assert.That(grid.TryGetCell(4, 7, out NavigationGridCell huntingCell), Is.True);
        Assert.That(huntingCell.Walkable, Is.False);
        Assert.That(huntingCell.Zone, Is.EqualTo(NavigationGridZone.HuntingGround));
        Assert.That(huntingCell.ZoneId, Is.Empty);
    }

    [Test]
    public void CellEdits_RejectInvalidCoordinatesAndZones()
    {
        Assert.That(grid.SetWalkable(-1, 0, false), Is.False);
        Assert.That(grid.SetZone(0, 35, NavigationGridZone.Exit), Is.False);
        Assert.That(grid.SetZone(0, 0, (NavigationGridZone)999), Is.False);
    }

    [Test]
    public void Resize_RequiresDiscardConfirmationAndPreservesRetainedCoordinates()
    {
        grid.SetZone(21, 29, NavigationGridZone.Spawn, "Retained");
        grid.SetZone(26, 34, NavigationGridZone.SafeZone, "Discarded");

        Assert.That(grid.TryApplyGridSize(30, 22, 4f, false, out int discarded), Is.False);
        Assert.That(discarded, Is.EqualTo(285));
        Assert.That(grid.Columns, Is.EqualTo(35));
        Assert.That(grid.Rows, Is.EqualTo(27));
        Assert.That(grid.CellCount, Is.EqualTo(945));

        Assert.That(grid.TryApplyGridSize(30, 22, 4f, true, out discarded), Is.True);
        Assert.That(discarded, Is.EqualTo(285));
        Assert.That(grid.Columns, Is.EqualTo(30));
        Assert.That(grid.Rows, Is.EqualTo(22));
        Assert.That(grid.CellCount, Is.EqualTo(660));
        Assert.That(grid.TryGetCell(21, 29, out NavigationGridCell retained), Is.True);
        Assert.That(retained.Zone, Is.EqualTo(NavigationGridZone.Spawn));
        Assert.That(retained.ZoneId, Is.EqualTo("Retained"));
        Assert.That(grid.TryGetCell(26, 34, out _), Is.False);
    }

    [Test]
    public void Resize_RejectsInvalidSizeWithoutChangingGrid()
    {
        Assert.That(grid.TryApplyGridSize(0, 22, 4f, true, out _), Is.False);
        Assert.That(grid.TryApplyGridSize(35, 27, 0f, true, out _), Is.False);
        Assert.That(grid.Columns, Is.EqualTo(35));
        Assert.That(grid.Rows, Is.EqualTo(27));
        Assert.That(grid.CellCount, Is.EqualTo(945));
    }

    [Test]
    public void GridData_SurvivesUnityJsonSerializationRoundTrip()
    {
        grid.transform.position = new Vector3(414f, 0f, 569f);
        grid.SetWalkable(10, 12, false);
        grid.SetZone(10, 12, NavigationGridZone.SafeZone, "SafeZone_B");
        string json = EditorJsonUtility.ToJson(grid);

        GameObject restoredObject = new GameObject("Restored Grid Test");
        try
        {
            NavigationGrid restored = restoredObject.AddComponent<NavigationGrid>();
            EditorJsonUtility.FromJsonOverwrite(json, restored);

            Assert.That(restored.Columns, Is.EqualTo(35));
            Assert.That(restored.Rows, Is.EqualTo(27));
            Assert.That(restored.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(restored.TryGetCell(10, 12, out NavigationGridCell restoredCell), Is.True);
            Assert.That(restoredCell.Walkable, Is.False);
            Assert.That(restoredCell.Zone, Is.EqualTo(NavigationGridZone.SafeZone));
            Assert.That(restoredCell.ZoneId, Is.EqualTo("SafeZone_B"));
        }
        finally
        {
            Object.DestroyImmediate(restoredObject);
        }
    }

    [Test]
    public void GridCellEdits_SupportUnityUndoAndRedo()
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.RegisterCompleteObjectUndo(grid, "Paint Navigation Grid Cell");
        grid.SetWalkable(3, 5, false);
        Undo.CollapseUndoOperations(undoGroup);

        Undo.PerformUndo();
        Assert.That(grid.TryGetCell(3, 5, out NavigationGridCell undone), Is.True);
        Assert.That(undone.Walkable, Is.True);

        Undo.PerformRedo();
        Assert.That(grid.TryGetCell(3, 5, out NavigationGridCell redone), Is.True);
        Assert.That(redone.Walkable, Is.False);
    }
}
