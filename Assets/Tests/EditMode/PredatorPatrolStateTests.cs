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

    private NavigationGrid CreateGrid(int rows, int columns)
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
            cells.Add(new NavigationGridCell(row, column, true, NavigationGridZone.Normal));
        SetPrivateField(grid, "cells", cells);
        return grid;
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
