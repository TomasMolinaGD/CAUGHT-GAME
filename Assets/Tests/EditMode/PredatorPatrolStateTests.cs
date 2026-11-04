using System;
using System.Collections.Generic;
using System.Reflection;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEngine;

public sealed class PredatorPatrolStateTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(objects[i]);
    }

    [Test]
    public void PatrolRequestsWaypointThroughNavigationController()
    {
        GameObject root = CreateRoot(true);
        Component fsm = root.GetComponent(Type.GetType("PredatorFSM, Assembly-CSharp"));
        Component controller = root.GetComponent(Type.GetType("PredatorNavigationMovementController, Assembly-CSharp"));
        GameObject waypoint = CreateWaypoint(new Vector3(2f, 0f, 0f));

        InitializePatrol(fsm, waypoint.transform);
        InvokePrivate(fsm, "Update");

        Assert.IsTrue((bool)controller.GetType().GetProperty("HasDestination").GetValue(controller));
        Assert.That(controller.GetType().GetProperty("RequestedDestination").GetValue(controller),
            Is.EqualTo(waypoint.transform.position));
    }

    [Test]
    public void PatrolKeepsLegacyMovementWithoutNavigationController()
    {
        GameObject root = CreateRoot(false);
        Component fsm = root.GetComponent(Type.GetType("PredatorFSM, Assembly-CSharp"));
        GameObject waypoint = CreateWaypoint(new Vector3(2f, 0f, 0f));

        InitializePatrol(fsm, waypoint.transform);
        InvokePrivate(fsm, "Update");

        Assert.IsNull(root.GetComponent(Type.GetType("PredatorNavigationMovementController, Assembly-CSharp")));
        Assert.That(GetEnum(fsm, "CurrentState"), Is.EqualTo("Patrol"));
    }

    [Test]
    public void PatrolDoesNotRetryMissingProviderEveryFrame()
    {
        GameObject root = CreateRoot(true);
        Component fsm = root.GetComponent(Type.GetType("PredatorFSM, Assembly-CSharp"));
        Component controller = root.GetComponent(Type.GetType("PredatorNavigationMovementController, Assembly-CSharp"));
        GameObject waypoint = CreateWaypoint(new Vector3(2f, 0f, 0f));

        InitializePatrol(fsm, waypoint.transform);
        InvokePrivate(fsm, "Update");
        Invoke(controller, "TickNavigation");
        InvokePrivate(fsm, "Update");

        Assert.IsFalse((bool)controller.GetType().GetProperty("HasDestination").GetValue(controller));
        InvokePrivate(fsm, "Update");
        Assert.IsFalse((bool)controller.GetType().GetProperty("HasDestination").GetValue(controller));
    }

    [Test]
    public void PatrolRequestsNextWaypointAfterNavigationArrivesAtFinalStoppingDistance()
    {
        NavigationGrid grid = CreateGrid(1, 3);
        Component provider = CreateProvider(grid);
        GameObject root = CreateRoot(true);
        Component fsm = root.GetComponent(Type.GetType("PredatorFSM, Assembly-CSharp"));
        Component controller = root.GetComponent(Type.GetType("PredatorNavigationMovementController, Assembly-CSharp"));
        Invoke(controller, "ConfigureGraphProvider", provider);
        Vector3 firstPosition = grid.GridToWorldPosition(0, 2);
        Vector3 secondPosition = grid.GridToWorldPosition(0, 0);
        root.transform.position = firstPosition + Vector3.left * 0.25005f;
        GameObject first = CreateWaypoint(firstPosition);
        GameObject second = CreateWaypoint(secondPosition);

        InitializePatrol(fsm, first.transform, second.transform);
        Invoke(controller, "TickNavigation");
        Assert.That(GetEnum(controller, "Status"), Is.EqualTo("Arrived"));

        InvokePrivate(fsm, "Update");

        Assert.That(controller.GetType().GetProperty("RequestedDestination").GetValue(controller),
            Is.EqualTo(secondPosition));
        Assert.IsTrue((bool)controller.GetType().GetProperty("HasDestination").GetValue(controller));
    }

    [Test]
    public void PatrolUsesAStarToReturnWhenBlockedCellPreventsDirectTravel()
    {
        NavigationGrid grid = CreateGrid(3, 3, blocked: new Vector2Int(1, 1));
        Component provider = CreateProvider(grid);
        GameObject root = CreateRoot(true);
        Component fsm = root.GetComponent(Type.GetType("PredatorFSM, Assembly-CSharp"));
        Component controller = root.GetComponent(Type.GetType(
            "PredatorNavigationMovementController, Assembly-CSharp"));
        Invoke(controller, "ConfigureGraphProvider", provider);
        root.transform.position = grid.GridToWorldPosition(1, 0);
        GameObject waypoint = CreateWaypoint(grid.GridToWorldPosition(1, 2));

        InitializePatrol(fsm, waypoint.transform);
        Invoke(controller, "TickNavigation");

        Assert.That(GetEnum(controller, "Status"), Is.EqualTo("FollowingPath"));
        Assert.That(controller.GetType().GetProperty("MovementTarget").GetValue(controller),
            Is.Not.EqualTo(waypoint.transform.position));
    }

    [Test]
    public void PatrolSkipsNoPathWaypointAndRequestsNextReachableWaypoint()
    {
        NavigationGrid grid = CreateGrid(2, 3, safeZones: new[] { new Vector2Int(0, 1) });
        Component provider = CreateProvider(grid);
        GameObject root = CreateRoot(true);
        Component fsm = root.GetComponent(Type.GetType("PredatorFSM, Assembly-CSharp"));
        Component controller = root.GetComponent(Type.GetType(
            "PredatorNavigationMovementController, Assembly-CSharp"));
        Invoke(controller, "ConfigureGraphProvider", provider);
        root.transform.position = grid.GridToWorldPosition(0, 0);
        GameObject unreachable = CreateWaypoint(grid.GridToWorldPosition(0, 1));
        GameObject reachable = CreateWaypoint(grid.GridToWorldPosition(0, 2));

        InitializePatrol(fsm, unreachable.transform, reachable.transform);
        Invoke(controller, "TickNavigation");
        Assert.That(GetEnum(controller, "Status"), Is.EqualTo("NoPath"));

        InvokePrivate(fsm, "Update");

        Assert.That(controller.GetType().GetProperty("RequestedDestination").GetValue(controller),
            Is.EqualTo(reachable.transform.position));
        Assert.That(controller.GetType().GetProperty("HasDestination").GetValue(controller), Is.True);
        Invoke(controller, "TickNavigation");
        Assert.That(GetEnum(controller, "Status"), Is.EqualTo("FollowingPath"));
    }

    [Test]
    public void PatrolStopsAfterEveryWaypointReturnsNoPath()
    {
        NavigationGrid grid = CreateGrid(1, 3, safeZones: new[]
        {
            new Vector2Int(0, 1),
            new Vector2Int(0, 2)
        });
        Component provider = CreateProvider(grid);
        GameObject root = CreateRoot(true);
        Component fsm = root.GetComponent(Type.GetType("PredatorFSM, Assembly-CSharp"));
        Component controller = root.GetComponent(Type.GetType(
            "PredatorNavigationMovementController, Assembly-CSharp"));
        Invoke(controller, "ConfigureGraphProvider", provider);
        root.transform.position = grid.GridToWorldPosition(0, 0);
        GameObject first = CreateWaypoint(grid.GridToWorldPosition(0, 1));
        GameObject second = CreateWaypoint(grid.GridToWorldPosition(0, 2));

        InitializePatrol(fsm, first.transform, second.transform);
        Invoke(controller, "TickNavigation");
        InvokePrivate(fsm, "Update");
        Invoke(controller, "TickNavigation");
        InvokePrivate(fsm, "Update");

        Assert.That(controller.GetType().GetProperty("HasDestination").GetValue(controller), Is.False);
        Vector3 finalRequest = (Vector3)controller.GetType()
            .GetProperty("RequestedDestination").GetValue(controller);
        InvokePrivate(fsm, "Update");
        Assert.That(controller.GetType().GetProperty("HasDestination").GetValue(controller), Is.False);
        Assert.That(controller.GetType().GetProperty("RequestedDestination").GetValue(controller),
            Is.EqualTo(finalRequest));
    }

    [Test]
    public void PatrolDoesNotCancelActiveSafeZoneRecovery()
    {
        NavigationGrid grid = CreateGrid(3, 5,
            safeZones: new[] { new Vector2Int(1, 1) });
        Component provider = CreateProvider(grid);
        GameObject root = CreateRoot(true);
        Component fsm = root.GetComponent(Type.GetType("PredatorFSM, Assembly-CSharp"));
        Component controller = root.GetComponent(Type.GetType(
            "PredatorNavigationMovementController, Assembly-CSharp"));
        Invoke(controller, "ConfigureGraphProvider", provider);
        root.transform.position = grid.GridToWorldPosition(1, 1);
        GameObject waypoint = CreateWaypoint(grid.GridToWorldPosition(1, 4));
        InitializePatrol(fsm, waypoint.transform);
        Invoke(controller, "TickNavigation");
        Assert.That(controller.GetType().GetProperty("IsRecoveringFromInvalidOrigin")
            .GetValue(controller), Is.True);

        InvokePrivate(fsm, "Update");

        Assert.That(controller.GetType().GetProperty("IsRecoveringFromInvalidOrigin")
            .GetValue(controller), Is.True);
        Assert.That(controller.GetType().GetProperty("HasDestination").GetValue(controller),
            Is.True);
    }

    private GameObject CreateRoot(bool withNavigationController)
    {
        GameObject root = new GameObject("PredatorPatrolStateTests_Root");
        objects.Add(root);
        Type fsmType = Type.GetType("PredatorFSM, Assembly-CSharp");
        Assert.IsNotNull(fsmType);
        if (withNavigationController)
            root.AddComponent(Type.GetType("PredatorNavigationMovementController, Assembly-CSharp"));
        root.AddComponent(fsmType);
        return root;
    }

    private GameObject CreateWaypoint(Vector3 position)
    {
        GameObject waypoint = new GameObject("PredatorPatrolStateTests_Waypoint");
        waypoint.transform.position = position;
        objects.Add(waypoint);
        return waypoint;
    }

    private static void InitializePatrol(Component fsm, params Transform[] waypoints)
    {
        Invoke(fsm, "ConfigureWaypoints", new List<Transform>(waypoints));
        InvokePrivate(fsm, "CacheDependencies");
        InvokePrivate(fsm, "InitializeStateMachine");
    }

    private Component CreateProvider(NavigationGrid grid)
    {
        GameObject root = new GameObject("PredatorPatrolStateTests_Provider");
        objects.Add(root);
        Type type = Type.GetType("CaughtGame.Navigation.NavigationGridGraphProvider, Assembly-CSharp");
        Assert.IsNotNull(type);
        Component provider = root.AddComponent(type);
        type.GetMethod("Configure", BindingFlags.Public | BindingFlags.Instance)
            .Invoke(provider, new object[] { grid, (LayerMask)0 });
        return provider;
    }

    private NavigationGrid CreateGrid(int rows, int columns,
        Vector2Int? blocked = null, IReadOnlyCollection<Vector2Int> safeZones = null)
    {
        GameObject root = new GameObject("PredatorPatrolStateTests_Grid");
        objects.Add(root);
        NavigationGrid grid = root.AddComponent<NavigationGrid>();
        SetPrivateField(grid, "rows", rows);
        SetPrivateField(grid, "columns", columns);
        SetPrivateField(grid, "cellSize", 1f);
        var cells = new List<NavigationGridCell>(rows * columns);
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            Vector2Int coordinate = new Vector2Int(row, column);
            bool walkable = !blocked.HasValue || blocked.Value != coordinate;
            NavigationGridZone zone = Contains(safeZones, coordinate)
                ? NavigationGridZone.SafeZone
                : NavigationGridZone.Normal;
            cells.Add(new NavigationGridCell(row, column, walkable, zone));
        }
        SetPrivateField(grid, "cells", cells);
        return grid;
    }

    private static bool Contains(IReadOnlyCollection<Vector2Int> coordinates, Vector2Int value)
    {
        if (coordinates == null) return false;
        foreach (Vector2Int coordinate in coordinates)
            if (coordinate == value) return true;
        return false;
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static void Invoke(Component component, string method, params object[] args)
    {
        component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public)
            .Invoke(component, args);
    }

    private static void InvokePrivate(Component component, string method)
    {
        component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(component, null);
    }

    private static string GetEnum(Component component, string property)
    {
        return component.GetType().GetProperty(property).GetValue(component).ToString();
    }
}
