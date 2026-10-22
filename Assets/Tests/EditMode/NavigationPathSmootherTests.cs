using System.Collections.Generic;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEngine;

public sealed class NavigationPathSmootherTests
{
    private GameObject gridObject;
    private GameObject geometryObject;
    private NavigationGrid grid;
    private GeneratedGeometryMarker geometry;
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        gridObject = new GameObject("Smoother Test Grid");
        grid = gridObject.AddComponent<NavigationGrid>();
        Assert.That(grid.TryApplyGridSize(3, 3, 2f, true, out _), Is.True);

        geometryObject = new GameObject("Smoother Test Geometry");
        geometry = geometryObject.AddComponent<GeneratedGeometryMarker>();
        geometry.Initialize(grid, 1);
        AddPart("Ground", new Vector3(3f, -0.5f, 3f), new Vector3(50f, 1f, 50f), false);
        AddPart("Wall Placeholder", new Vector3(30f, 1f, 30f), Vector3.one, false);
        Physics.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        for (int index = createdObjects.Count - 1; index >= 0; index--)
        {
            if (createdObjects[index] != null) Object.DestroyImmediate(createdObjects[index]);
        }
        createdObjects.Clear();
        if (geometryObject != null) Object.DestroyImmediate(geometryObject);
        if (gridObject != null) Object.DestroyImmediate(gridObject);
    }

    [Test]
    public void Smooth_StraightRoute_RemovesCollinearWaypoints()
    {
        NavigationPathResult original = FindPath(0, 0, 0, 2);

        NavigationPathSmoothingResult result = Smooth(original);

        Assert.That(result.IsUsable, Is.True);
        Assert.That(result.SmoothedPath.Waypoints.Count, Is.EqualTo(2));
    }

    [Test]
    public void Smooth_StaircaseRoute_UsesVisibleDirectSegment()
    {
        NavigationPathResult original = FindPath(0, 0, 2, 2);

        NavigationPathSmoothingResult result = Smooth(original);

        Assert.That(result.IsUsable, Is.True);
        Assert.That(result.SmoothedPath.Waypoints.Count, Is.EqualTo(2));
    }

    [Test]
    public void Smooth_PhysicalObstacle_PreservesSafeIntermediateWaypoints()
    {
        NavigationPathResult original = FindPath(0, 0, 2, 2);
        AddPart("Diagonal Shortcut Blocker", new Vector3(3f, 1f, 3f),
            new Vector3(0.3f, 2f, 0.3f), false);
        Physics.SyncTransforms();

        NavigationPathSmoothingResult result = Smooth(original);

        Assert.That(result.IsUsable, Is.True);
        Assert.That(result.SmoothedPath.Waypoints.Count, Is.GreaterThan(2));
        Assert.That(result.PhysicalValidation.Status, Is.EqualTo(NavigationPathValidationStatus.Clear));
    }

    [Test]
    public void Smooth_BlockedCorner_DoesNotCutThroughAdjacentCell()
    {
        Assert.That(grid.SetWalkable(0, 1, false), Is.True);
        NavigationPathResult original = FindPath(0, 0, 2, 2);

        NavigationPathSmoothingResult result = Smooth(original);

        Assert.That(original.Status, Is.EqualTo(NavigationPathStatus.Success));
        Assert.That(result.IsUsable, Is.True);
        Assert.That(result.SmoothedPath.Waypoints.Count, Is.GreaterThan(2));
    }

    [Test]
    public void Smooth_WolfCannotCrossSafeZoneButHunterCan()
    {
        Assert.That(grid.SetZone(1, 1, NavigationGridZone.SafeZone, "SafeZone_A"), Is.True);
        NavigationPathResult hunterPath = NavigationGridPathfinder.FindPath(grid, 0, 0, 2, 2,
            NavigationAgentType.Hunter);
        NavigationPathResult wolfPath = NavigationGridPathfinder.FindPath(grid, 0, 0, 2, 2,
            NavigationAgentType.Wolf);

        NavigationPathSmoothingResult hunter = Smooth(hunterPath, NavigationAgentType.Hunter);
        NavigationPathSmoothingResult wolf = Smooth(wolfPath, NavigationAgentType.Wolf);

        Assert.That(hunter.IsUsable, Is.True);
        Assert.That(hunter.SmoothedPath.Waypoints.Count, Is.EqualTo(2));
        Assert.That(wolf.IsUsable, Is.True);
        Assert.That(wolf.SmoothedPath.Waypoints.Count, Is.GreaterThan(2));
    }

    [Test]
    public void Smooth_AdditionalClearanceRejectsRouteThatBareRadiusAllows()
    {
        NavigationPathResult original = FindPath(0, 0, 0, 2);
        AddPart("Near Wall", new Vector3(3f, 1f, 1.67f),
            new Vector3(0.2f, 2f, 0.1f), false);
        Physics.SyncTransforms();

        NavigationPathSmoothingResult bareRadius = Smooth(original, NavigationAgentType.Hunter,
            0.5f, 2f, 0f);
        NavigationPathSmoothingResult withMargin = Smooth(original, NavigationAgentType.Hunter,
            0.5f, 2f, 0.25f);

        Assert.That(bareRadius.IsUsable, Is.True);
        Assert.That(withMargin.IsUsable, Is.False);
        Assert.That(withMargin.PhysicalValidation.Status, Is.EqualTo(NavigationPathValidationStatus.Blocked));
    }

    [Test]
    public void Smooth_MissingGeometry_FailsClosed()
    {
        NavigationPathSmoothingResult result = NavigationPathSmoother.Smooth(grid, null,
            FindPath(0, 0, 0, 2), NavigationAgentType.Hunter, 0.5f, 2f);

        Assert.That(result.Status, Is.EqualTo(NavigationPathSmoothingStatus.MissingGeometry));
        Assert.That(result.IsUsable, Is.False);
        Assert.That(result.SmoothedPath, Is.Null);
    }

    [Test]
    public void Smooth_PreservesEndpointsAndDoesNotMutateOriginalRoute()
    {
        NavigationPathResult original = FindPath(0, 0, 2, 2);
        var originalCells = new List<Vector2Int>(original.Cells);
        var originalWaypoints = new List<Vector3>(original.Waypoints);
        float originalCost = original.TotalCost;

        NavigationPathSmoothingResult result = Smooth(original);

        Assert.That(result.IsUsable, Is.True);
        Assert.That(result.SmoothedPath, Is.Not.SameAs(original));
        Assert.That(result.SmoothedPath.Waypoints[0], Is.EqualTo(originalWaypoints[0]));
        Assert.That(result.SmoothedPath.Waypoints[result.SmoothedPath.Waypoints.Count - 1],
            Is.EqualTo(originalWaypoints[originalWaypoints.Count - 1]));
        CollectionAssert.AreEqual(originalCells, original.Cells);
        CollectionAssert.AreEqual(originalWaypoints, original.Waypoints);
        Assert.That(original.TotalCost, Is.EqualTo(originalCost));
    }

    [Test]
    public void Smooth_SameInput_IsDeterministic()
    {
        NavigationPathResult original = FindPath(0, 0, 2, 2);

        NavigationPathSmoothingResult first = Smooth(original);
        NavigationPathSmoothingResult second = Smooth(original);

        Assert.That(first.IsUsable, Is.True);
        Assert.That(second.IsUsable, Is.True);
        CollectionAssert.AreEqual(first.SmoothedPath.Cells, second.SmoothedPath.Cells);
        CollectionAssert.AreEqual(first.SmoothedPath.Waypoints, second.SmoothedPath.Waypoints);
    }

    [Test]
    public void TestAgent_FollowsOnlyMatchingValidatedSmoothedRoute()
    {
        NavigationPathSmoothingResult smoothing = Smooth(FindPath(0, 0, 2, 2));
        var agentObject = new GameObject("Smoothed Test Agent");
        createdObjects.Add(agentObject);
        NavigationGridTestAgent agent = agentObject.AddComponent<NavigationGridTestAgent>();
        agent.Configure(grid, NavigationAgentType.Wolf, 2, 2, 3f, 0.5f);

        Assert.That(agent.FollowSmoothedPath(smoothing), Is.False);
        Assert.That(agent.LastPathResult, Is.Null);

        agent.Configure(grid, NavigationAgentType.Hunter, 2, 2, 3f, 0.5f);
        Assert.That(agent.FollowSmoothedPath(smoothing), Is.True);
        Assert.That(agent.LastPathResult, Is.SameAs(smoothing.SmoothedPath));
    }

    [Test]
    public void Smooth_InvalidDimensions_AreRejected()
    {
        NavigationPathSmoothingResult result = Smooth(FindPath(0, 0, 0, 2),
            NavigationAgentType.Hunter, 0.5f, 0.8f, 0f);

        Assert.That(result.Status, Is.EqualTo(NavigationPathSmoothingStatus.InvalidConfiguration));
    }

    [Test]
    public void Smooth_OriginalPathWithNoSafeShortcut_RemainsUnsmoothed()
    {
        Assert.That(grid.SetWalkable(0, 1, false), Is.True);
        Assert.That(grid.SetWalkable(1, 1, false), Is.True);
        Assert.That(grid.SetWalkable(2, 1, false), Is.True);
        NavigationPathResult noPath = FindPath(1, 0, 1, 2);

        NavigationPathSmoothingResult result = Smooth(noPath);

        Assert.That(noPath.Status, Is.EqualTo(NavigationPathStatus.NoPath));
        Assert.That(result.Status, Is.EqualTo(NavigationPathSmoothingStatus.InvalidPath));
        Assert.That(result.SmoothedPath, Is.Null);
    }

    private NavigationPathSmoothingResult Smooth(NavigationPathResult path,
        NavigationAgentType agentType = NavigationAgentType.Hunter,
        float radius = 0.5f, float height = 2f, float margin = 0f)
    {
        return NavigationPathSmoother.Smooth(grid, geometry, path, agentType, radius, height, margin);
    }

    private NavigationPathResult FindPath(int startRow, int startColumn, int endRow, int endColumn)
    {
        return NavigationGridPathfinder.FindPath(grid, startRow, startColumn,
            endRow, endColumn, NavigationAgentType.Hunter);
    }

    private BoxCollider AddPart(string name, Vector3 position, Vector3 size, bool trigger)
    {
        var partObject = new GameObject(name);
        partObject.transform.SetParent(geometryObject.transform);
        partObject.transform.position = position;
        GeneratedGeometryPartMarker partMarker = partObject.AddComponent<GeneratedGeometryPartMarker>();
        partMarker.Initialize(geometry);
        BoxCollider collider = partObject.AddComponent<BoxCollider>();
        collider.size = size;
        collider.isTrigger = trigger;
        createdObjects.Add(partObject);
        return collider;
    }
}
