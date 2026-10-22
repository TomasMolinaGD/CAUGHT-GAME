using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEngine;

public sealed class NavigationGridTestAgentTests
{
    private GameObject gridObject;
    private NavigationGrid grid;
    private GameObject hunterObject;
    private GameObject wolfObject;

    [SetUp]
    public void SetUp()
    {
        gridObject = new GameObject("Navigation Test Agent Grid");
        grid = gridObject.AddComponent<NavigationGrid>();
        Assert.That(grid.TryApplyGridSize(5, 1, 4f, true, out _), Is.True);
        Assert.That(grid.SetZone(0, 3, NavigationGridZone.SafeZone, "SafeZone_A"), Is.True);

        hunterObject = CreateAgentObject("Hunter Test Agent");
        hunterObject.transform.position = grid.GridToWorldPosition(0, 0) + Vector3.up;
        hunterObject.GetComponent<NavigationGridTestAgent>().Configure(
            grid, NavigationAgentType.Hunter, 0, 3, 3f, 0.5f);

        wolfObject = CreateAgentObject("Wolf Test Agent");
        wolfObject.transform.position = grid.GridToWorldPosition(0, 0) + Vector3.up;
        wolfObject.GetComponent<NavigationGridTestAgent>().Configure(
            grid, NavigationAgentType.Wolf, 0, 3, 2.5f, 0.5f);
    }

    [TearDown]
    public void TearDown()
    {
        if (hunterObject != null) Object.DestroyImmediate(hunterObject);
        if (wolfObject != null) Object.DestroyImmediate(wolfObject);
        if (gridObject != null) Object.DestroyImmediate(gridObject);
    }

    [Test]
    public void RequestPathToConfiguredCell_UsesAgentRestrictionsWithoutChangingGrid()
    {
        NavigationGridTestAgent hunter = hunterObject.GetComponent<NavigationGridTestAgent>();
        NavigationGridTestAgent wolf = wolfObject.GetComponent<NavigationGridTestAgent>();
        int originalCellCount = grid.CellCount;

        NavigationPathResult hunterResult = hunter.RequestPathToConfiguredCell();
        NavigationPathResult wolfResult = wolf.RequestPathToConfiguredCell();

        Assert.That(hunterResult.Status, Is.EqualTo(NavigationPathStatus.Success));
        Assert.That(hunter.Follower.IsMoving, Is.True);
        Assert.That(wolfResult.Status, Is.EqualTo(NavigationPathStatus.DestinationNotTraversable));
        Assert.That(wolf.Follower.IsMoving, Is.False);
        Assert.That(grid.CellCount, Is.EqualTo(originalCellCount));
        Assert.That(grid.TryGetCell(0, 3, out NavigationGridCell safeCell), Is.True);
        Assert.That(safeCell.Walkable, Is.True);
        Assert.That(safeCell.Zone, Is.EqualTo(NavigationGridZone.SafeZone));
        Assert.That(safeCell.ZoneId, Is.EqualTo("SafeZone_A"));
    }

    private static GameObject CreateAgentObject(string objectName)
    {
        var agentObject = new GameObject(objectName);
        agentObject.AddComponent<CapsuleCollider>();
        agentObject.AddComponent<NavigationGridTestAgent>();
        return agentObject;
    }
}
