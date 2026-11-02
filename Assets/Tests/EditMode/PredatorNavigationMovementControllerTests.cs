using System;
using System.Collections.Generic;
using System.Reflection;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEngine;

public sealed class PredatorNavigationMovementControllerTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(objects[i]);
    }

    [Test]
    public void ControllerWithoutDestinationDoesNotStopLegacyMovement()
    {
        GameObject root = CreateRoot();
        Component controller = Add(root, "PredatorNavigationMovementController");
        Component movement = root.GetComponent(Type.GetType("PredatorMovement, Assembly-CSharp"));

        Invoke(movement, "MoveTowards", new Vector3(2f, 0f, 0f), false, 0f);
        Vector3 velocityBeforeTick = GetVelocity(movement);
        Assert.That(velocityBeforeTick.sqrMagnitude, Is.GreaterThan(0f));

        Invoke(controller, "TickNavigation");

        Assert.That(GetVelocity(movement), Is.EqualTo(velocityBeforeTick));
        Assert.IsFalse((bool)controller.GetType().GetProperty("HasDestination").GetValue(controller));
    }

    [Test]
    public void MissingProviderStopsMovementWithoutWritingTransform()
    {
        GameObject root = CreateRoot();
        Component controller = Add(root, "PredatorNavigationMovementController");
        Component agent = root.GetComponent(Type.GetType("GraphNavigationAgent, Assembly-CSharp"));
        Vector3 before = root.transform.position;

        Invoke(controller, "RequestDestination", new Vector3(2f, 0f, 0f));
        Invoke(controller, "TickNavigation");

        Assert.That(GetEnum(controller, "Status"), Is.EqualTo("MissingProvider"));
        Assert.That(root.transform.position, Is.EqualTo(before));
        Assert.That(agent, Is.Not.Null);
    }

    [Test]
    public void CancelDestinationInvalidatesNavigationAndStopsMovement()
    {
        GameObject root = CreateRoot();
        Component controller = Add(root, "PredatorNavigationMovementController");
        Component movement = root.GetComponent(Type.GetType("PredatorMovement, Assembly-CSharp"));

        Invoke(controller, "RequestDestination", new Vector3(2f, 0f, 0f));
        Invoke(movement, "MoveTowards", new Vector3(2f, 0f, 0f), false, 0f);
        Assert.That(GetVelocity(movement).sqrMagnitude, Is.GreaterThan(0f));
        Vector3 positionBeforeCancel = root.transform.position;
        Invoke(controller, "CancelDestination");

        Assert.IsFalse((bool)controller.GetType().GetProperty("HasDestination").GetValue(controller));
        Assert.That(GetEnum(controller, "Status"), Is.EqualTo("Idle"));
        Assert.That(GetVelocity(movement), Is.EqualTo(Vector3.zero));
        Assert.That(root.transform.position, Is.EqualTo(positionBeforeCancel));
    }

    [Test]
    public void DirectRouteDelegatesToMovementWithoutNavigationWritingTransform()
    {
        NavigationGrid grid = CreateGrid(1, 3);
        Component provider = CreateProvider(grid);
        GameObject root = CreateRoot();
        Component controller = Add(root, "PredatorNavigationMovementController");
        Invoke(controller, "ConfigureGraphProvider", provider);
        root.transform.position = grid.GridToWorldPosition(0, 0);
        Vector3 before = root.transform.position;
        Vector3 destination = grid.GridToWorldPosition(0, 2);

        Component agent = root.GetComponent(Type.GetType("GraphNavigationAgent, Assembly-CSharp"));
        Type agentType = agent.GetType();
        bool navigationAccepted = InvokeTryMove(agentType, agent, destination, out bool navigationMoving);
        Assert.IsTrue(navigationAccepted);
        Assert.IsTrue(navigationMoving);
        Assert.That(agentType.GetProperty("MovementTarget").GetValue(agent), Is.EqualTo(destination));
        Assert.That(root.transform.position, Is.EqualTo(before));

        Invoke(controller, "RequestDestination", destination);
        Invoke(controller, "TickNavigation");

        Assert.That(GetEnum(controller, "Status"), Is.EqualTo("FollowingDirect"));
        Assert.That(Vector3.Distance(root.transform.position, before), Is.GreaterThan(0f));
        Assert.That(root.transform.position.z, Is.EqualTo(before.z));
    }

    [Test]
    public void NoPathStopsMovementAndDoesNotEnableDirectFallback()
    {
        NavigationGrid grid = CreateGrid(1, 3, new Vector2Int(0, 1));
        Component provider = CreateProvider(grid);
        GameObject root = CreateRoot();
        Component controller = Add(root, "PredatorNavigationMovementController");
        Invoke(controller, "ConfigureGraphProvider", provider);
        root.transform.position = grid.GridToWorldPosition(0, 0);

        Invoke(controller, "RequestDestination", grid.GridToWorldPosition(0, 2));
        Invoke(controller, "TickNavigation");

        Assert.That(GetEnum(controller, "Status"), Is.EqualTo("NoPath"));
        Assert.That(root.transform.position, Is.EqualTo(grid.GridToWorldPosition(0, 0)));
    }

    [Test]
    public void IntermediateWaypointUsesAgentToleranceInsteadOfFinalStoppingDistance()
    {
        NavigationGrid grid = CreateGrid(3, 3, new Vector2Int(1, 1));
        Component provider = CreateProvider(grid);
        GameObject root = CreateRoot();
        Component controller = Add(root, "PredatorNavigationMovementController");
        Component agent = root.GetComponent(Type.GetType("GraphNavigationAgent, Assembly-CSharp"));
        Invoke(controller, "ConfigureGraphProvider", provider);
        SetPrivateField(agent, "nodeReachDistance", 0.1f);
        SetPrivateField(controller, "stoppingDistance", 0.25f);
        root.transform.position = new Vector3(0.36f, 0f, 0.5f);
        Vector3 before = root.transform.position;

        Invoke(controller, "RequestDestination", grid.GridToWorldPosition(2, 2));
        Invoke(controller, "TickNavigation");

        Assert.That(Vector3.Distance(before, grid.GridToWorldPosition(0, 0)), Is.EqualTo(0.14f).Within(0.001f));
        Assert.That(GetEnum(controller, "Status"), Is.EqualTo("FollowingPath"));
        Assert.IsFalse((bool)agent.GetType().GetProperty("IsFinalMovementTarget").GetValue(agent));
        Assert.That(root.transform.position.x, Is.GreaterThan(before.x));
    }

    [Test]
    public void FinalDestinationArrivesWithinSharedStoppingTolerance()
    {
        NavigationGrid grid = CreateGrid(1, 3);
        Component provider = CreateProvider(grid);
        GameObject root = CreateRoot();
        Component controller = Add(root, "PredatorNavigationMovementController");
        Component agent = root.GetComponent(Type.GetType("GraphNavigationAgent, Assembly-CSharp"));
        Invoke(controller, "ConfigureGraphProvider", provider);
        SetPrivateField(agent, "nodeReachDistance", 0.1f);
        SetPrivateField(controller, "stoppingDistance", 0.25f);
        Vector3 destination = grid.GridToWorldPosition(0, 2);
        root.transform.position = destination + Vector3.left * 0.25005f;
        Invoke(controller, "RequestDestination", destination);

        Invoke(controller, "TickNavigation");

        Assert.That(GetEnum(controller, "Status"), Is.EqualTo("Arrived"));
        Assert.IsFalse((bool)controller.GetType().GetProperty("HasDestination").GetValue(controller));
        Assert.That(root.transform.position, Is.EqualTo(destination + Vector3.left * 0.25005f));
    }

    [Test]
    public void FinalDestinationImmediatelyOutsideSharedStoppingToleranceDoesNotArrive()
    {
        NavigationGrid grid = CreateGrid(1, 3);
        Component provider = CreateProvider(grid);
        GameObject root = CreateRoot();
        Component controller = Add(root, "PredatorNavigationMovementController");
        Component agent = root.GetComponent(Type.GetType("GraphNavigationAgent, Assembly-CSharp"));
        Invoke(controller, "ConfigureGraphProvider", provider);
        SetPrivateField(agent, "nodeReachDistance", 0.1f);
        SetPrivateField(controller, "stoppingDistance", 0.25f);
        Vector3 destination = grid.GridToWorldPosition(0, 2);
        root.transform.position = destination + Vector3.left * 0.2502f;
        Invoke(controller, "RequestDestination", destination);

        Invoke(controller, "TickNavigation");

        Assert.That(GetEnum(controller, "Status"), Is.EqualTo("FollowingDirect"));
        Assert.IsTrue((bool)controller.GetType().GetProperty("HasDestination").GetValue(controller));
    }

    private GameObject CreateRoot()
    {
        GameObject root = new GameObject("PredatorNavigationMovementControllerTests_Root");
        objects.Add(root);
        root.AddComponent(Type.GetType("GraphNavigationAgent, Assembly-CSharp"));
        root.AddComponent(Type.GetType("PredatorMovement, Assembly-CSharp"));
        return root;
    }

    private static Component Add(GameObject root, string typeName)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        Assert.IsNotNull(type);
        return root.AddComponent(type);
    }

    private Component CreateProvider(NavigationGrid grid)
    {
        GameObject root = new GameObject("PredatorNavigationMovementControllerTests_Provider");
        objects.Add(root);
        Type type = Type.GetType("CaughtGame.Navigation.NavigationGridGraphProvider, Assembly-CSharp");
        Assert.IsNotNull(type);
        Component provider = root.AddComponent(type);
        type.GetMethod("Configure", BindingFlags.Public | BindingFlags.Instance)
            .Invoke(provider, new object[] { grid, (LayerMask)0 });
        return provider;
    }

    private NavigationGrid CreateGrid(int rows, int columns, Vector2Int? blocked = null)
    {
        GameObject root = new GameObject("PredatorNavigationMovementControllerTests_Grid");
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
            cells.Add(new NavigationGridCell(row, column,
                !blocked.HasValue || blocked.Value != coordinate,
                NavigationGridZone.Normal));
        }
        SetPrivateField(grid, "cells", cells);
        return grid;
    }

    private static void Invoke(Component component, string method, params object[] args)
    {
        component.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance)
            .Invoke(component, args);
    }

    private static bool InvokeTryMove(Type agentType, Component agent, Vector3 destination, out bool moving)
    {
        object[] args = { destination, 2f, 0.1f, false };
        bool result = (bool)agentType.GetMethod("TryMoveTowards").Invoke(agent, args);
        moving = (bool)args[3];
        return result;
    }

    private static string GetEnum(Component component, string property)
    {
        return component.GetType().GetProperty(property).GetValue(component).ToString();
    }

    private static Vector3 GetVelocity(Component movement)
    {
        return (Vector3)movement.GetType().GetProperty("Velocity").GetValue(movement);
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
