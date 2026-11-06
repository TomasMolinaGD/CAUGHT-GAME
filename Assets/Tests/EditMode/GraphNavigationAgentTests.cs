using System;
using System.Collections.Generic;
using System.Reflection;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEngine;

public sealed class GraphNavigationAgentTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(objects[i]);
    }

    [Test]
    public void TryMoveTowards_DoesNotModifyTransform()
    {
        Component agent = CreateAgent(out Type agentType);
        Vector3 before = agent.transform.position;

        bool accepted = InvokeTryMove(agentType, agent, new Vector3(1f, 0f, 0f), out bool moving);

        Assert.IsFalse(accepted);
        Assert.IsFalse(moving);
        Assert.That(agent.transform.position, Is.EqualTo(before));
        Assert.That(GetStatus(agentType, agent), Is.EqualTo("MissingProvider"));
    }

    [Test]
    public void TryMoveTowards_SelectsDirectTargetWhenProviderReportsClearCorridor()
    {
        NavigationGrid grid = CreateGrid(1, 3);
        Component provider = CreateProvider(grid);
        Component agent = CreateAgent(out Type agentType);
        agent.transform.position = grid.GridToWorldPosition(0, 0);
        ConfigureGraph(agentType, agent, provider);
        Vector3 destination = grid.GridToWorldPosition(0, 2);
        Vector3 before = agent.transform.position;

        bool accepted = InvokeTryMove(agentType, agent, destination, out bool moving);

        Assert.IsTrue(accepted);
        Assert.IsTrue(moving);
        Assert.That(GetStatus(agentType, agent), Is.EqualTo("FollowingDirect"));
        Assert.That(GetMovementTarget(agentType, agent), Is.EqualTo(destination));
        Assert.That(agent.transform.position, Is.EqualTo(before));
    }

    [Test]
    public void FinalMovementTargetArrivesWithinStoppingTolerance()
    {
        NavigationGrid grid = CreateGrid(1, 3);
        Component provider = CreateProvider(grid);
        Component agent = CreateAgent(out Type agentType);
        Vector3 destination = grid.GridToWorldPosition(0, 2);
        ConfigureGraph(agentType, agent, provider);

        Assert.IsTrue(InvokeTryMove(agentType, agent, destination, 0.25f, out _));
        agent.transform.position = destination + Vector3.left * 0.25005f;

        Assert.IsTrue(InvokeTryMove(agentType, agent, destination, 0.25f, out bool moving));

        Assert.IsFalse(moving);
        Assert.That(GetStatus(agentType, agent), Is.EqualTo("Arrived"));
        Assert.IsFalse((bool)agentType.GetProperty("HasMovementTarget").GetValue(agent));
    }

    [Test]
    public void FinalMovementTargetOutsideStoppingToleranceDoesNotArrive()
    {
        NavigationGrid grid = CreateGrid(1, 3);
        Component provider = CreateProvider(grid);
        Component agent = CreateAgent(out Type agentType);
        Vector3 destination = grid.GridToWorldPosition(0, 2);
        ConfigureGraph(agentType, agent, provider);

        Assert.IsTrue(InvokeTryMove(agentType, agent, destination, 0.25f, out _));
        agent.transform.position = destination + Vector3.left * 0.2502f;

        Assert.IsTrue(InvokeTryMove(agentType, agent, destination, 0.25f, out bool moving));

        Assert.IsTrue(moving);
        Assert.That(GetStatus(agentType, agent), Is.EqualTo("FollowingDirect"));
        Assert.IsTrue((bool)agentType.GetProperty("HasMovementTarget").GetValue(agent));
    }

    [Test]
    public void TryMoveTowards_SelectsWaypointWhenDirectCorridorIsBlocked()
    {
        NavigationGrid grid = CreateGrid(3, 3, new Vector2Int(1, 1));
        Component provider = CreateProvider(grid);
        Component agent = CreateAgent(out Type agentType);
        agent.transform.position = grid.GridToWorldPosition(0, 0);
        ConfigureGraph(agentType, agent, provider);
        Vector3 destination = grid.GridToWorldPosition(2, 2);

        Assert.IsTrue(InvokeTryMove(agentType, agent, destination, out bool moving));
        Assert.IsTrue(moving);
        Assert.That(GetStatus(agentType, agent), Is.EqualTo("FollowingPath"));
        Assert.That(GetMovementTarget(agentType, agent), Is.Not.EqualTo(destination));
        Assert.That(agent.transform.position, Is.EqualTo(grid.GridToWorldPosition(0, 0)));
    }

    [Test]
    public void AStarRouteContinuesToStationaryHunterNearCellEdgeAfterLastCellCenter()
    {
        NavigationGrid grid = CreateGrid(3, 3, new Vector2Int(1, 1), 3f);
        Component provider = CreateProvider(grid, 1 << 12);
        Component agent = CreateAgent(out Type agentType);
        Vector3 hunterPosition = grid.GridToWorldPosition(2, 2) +
            Vector3.right * 0.95f + Vector3.forward * 0.95f;
        Vector3 lastCellCenter = grid.GridToWorldPosition(2, 2);
        agent.transform.position = grid.GridToWorldPosition(0, 0);
        agentType.GetMethod("Configure").Invoke(agent, new object[] { false, 0.1f, 0.2f });
        ConfigureGraph(agentType, agent, provider);

        GameObject offCenterBlocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        objects.Add(offCenterBlocker);
        offCenterBlocker.layer = 12;
        offCenterBlocker.transform.position = lastCellCenter + new Vector3(0.65f, 1f, 0.3f);
        offCenterBlocker.transform.localScale = new Vector3(0.1f, 2f, 0.1f);
        Physics.SyncTransforms();

        Assert.IsTrue(InvokeTryMove(agentType, agent, hunterPosition, 0.315f, out bool moving));
        Assert.IsTrue(moving);
        Assert.That(GetStatus(agentType, agent), Is.EqualTo("FollowingPath"));

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector3 nextWaypoint = GetMovementTarget(agentType, agent);
            if (nextWaypoint == lastCellCenter) break;

            agent.transform.position = nextWaypoint;
            Assert.IsTrue(InvokeTryMove(agentType, agent, hunterPosition, 0.315f, out _));
        }

        Assert.That(GetMovementTarget(agentType, agent), Is.EqualTo(lastCellCenter));
        Assert.That(Vector3.Distance(lastCellCenter, hunterPosition), Is.GreaterThan(0.9f));

        // From an off-center position, do not cut across the blocked final approach.
        agent.transform.position = lastCellCenter + new Vector3(0.35f, 0f, -0.35f);
        Assert.IsTrue(InvokeTryMove(agentType, agent, hunterPosition, 0.315f, out moving));

        Assert.IsTrue(moving);
        Assert.That(GetStatus(agentType, agent), Is.EqualTo("FollowingPath"));
        Assert.That(GetMovementTarget(agentType, agent), Is.EqualTo(lastCellCenter));

        // The Hunter remains stationary while the Wolf reaches the final A* cell center.
        agent.transform.position = lastCellCenter;
        Assert.IsTrue(InvokeTryMove(agentType, agent, hunterPosition, 0.315f, out moving));

        Assert.IsTrue(moving);
        Assert.That(GetStatus(agentType, agent), Is.EqualTo("FollowingPath"));
        Assert.That(GetMovementTarget(agentType, agent), Is.EqualTo(hunterPosition));
    }

    [Test]
    public void TryMoveTowards_OffsetStartChoosesInitialCellCenterBeforeNextWaypoint()
    {
        NavigationGrid grid = CreateGrid(3, 3, new Vector2Int(1, 1));
        Component provider = CreateProvider(grid);
        Component agent = CreateAgent(out Type agentType);
        agent.transform.position = new Vector3(0.1f, 0f, 0.1f);
        ConfigureGraph(agentType, agent, provider);
        Vector3 destination = grid.GridToWorldPosition(2, 2);

        Assert.IsTrue(InvokeTryMove(agentType, agent, destination, out bool moving));
        Assert.IsTrue(moving);
        Assert.That(GetStatus(agentType, agent), Is.EqualTo("FollowingPath"));
        Assert.That(GetMovementTarget(agentType, agent), Is.EqualTo(grid.GridToWorldPosition(0, 0)));
        Assert.That(agent.transform.position, Is.EqualTo(new Vector3(0.1f, 0f, 0.1f)));
    }

    [Test]
    public void InvalidatePath_RemovesImmediateTargetWithoutMovingAgent()
    {
        NavigationGrid grid = CreateGrid(1, 3);
        Component provider = CreateProvider(grid);
        Component agent = CreateAgent(out Type agentType);
        agent.transform.position = grid.GridToWorldPosition(0, 0);
        ConfigureGraph(agentType, agent, provider);
        Vector3 destination = grid.GridToWorldPosition(0, 2);
        Vector3 before = agent.transform.position;
        InvokeTryMove(agentType, agent, destination, out _);

        agentType.GetMethod("InvalidatePath").Invoke(agent, null);

        Assert.That(GetStatus(agentType, agent), Is.EqualTo("Idle"));
        Assert.IsFalse((bool)agentType.GetProperty("HasMovementTarget").GetValue(agent));
        Assert.That(agent.transform.position, Is.EqualTo(before));
    }

    [Test]
    public void TryMoveTowards_NoPathDoesNotEnableDirectMovement()
    {
        NavigationGrid grid = CreateGrid(1, 3, new Vector2Int(0, 1));
        Component provider = CreateProvider(grid);
        Component agent = CreateAgent(out Type agentType);
        ConfigureGraph(agentType, agent, provider);
        Vector3 destination = grid.GridToWorldPosition(0, 2);

        Assert.IsFalse(InvokeTryMove(agentType, agent, destination, out bool moving));
        Assert.IsFalse(moving);
        Assert.That(GetStatus(agentType, agent), Is.EqualTo("NoPath"));
        Assert.IsFalse((bool)agentType.GetProperty("HasMovementTarget").GetValue(agent));
    }

    private Component CreateAgent(out Type agentType)
    {
        GameObject root = new GameObject("GraphNavigationAgentTests_Agent");
        objects.Add(root);
        agentType = Type.GetType("GraphNavigationAgent, Assembly-CSharp");
        Assert.IsNotNull(agentType);
        return root.AddComponent(agentType);
    }

    private Component CreateProvider(NavigationGrid grid, int obstacleLayerMask = 0)
    {
        GameObject root = new GameObject("GraphNavigationAgentTests_Provider");
        objects.Add(root);
        Type providerType = Type.GetType("CaughtGame.Navigation.NavigationGridGraphProvider, Assembly-CSharp");
        Assert.IsNotNull(providerType);
        Component provider = root.AddComponent(providerType);
        providerType.GetMethod("Configure", BindingFlags.Public | BindingFlags.Instance)
            .Invoke(provider, new object[] { grid, (LayerMask)obstacleLayerMask });
        return provider;
    }

    private NavigationGrid CreateGrid(int rows, int columns, Vector2Int? blocked = null, float cellSize = 1f)
    {
        GameObject root = new GameObject("GraphNavigationAgentTests_Grid");
        objects.Add(root);
        NavigationGrid grid = root.AddComponent<NavigationGrid>();
        SetPrivateField(grid, "rows", rows);
        SetPrivateField(grid, "columns", columns);
        SetPrivateField(grid, "cellSize", cellSize);
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

    private static void ConfigureGraph(Type agentType, Component agent, Component provider)
    {
        agentType.GetMethod("ConfigureGraphComponent").Invoke(agent, new object[] { provider });
    }

    private static bool InvokeTryMove(Type agentType, Component agent, Vector3 destination,
        out bool moving)
    {
        return InvokeTryMove(agentType, agent, destination, 0.1f, out moving);
    }

    private static bool InvokeTryMove(Type agentType, Component agent, Vector3 destination,
        float stoppingDistance, out bool moving)
    {
        object[] args = { destination, 2f, stoppingDistance, false };
        bool result = (bool)agentType.GetMethod("TryMoveTowards").Invoke(agent, args);
        moving = (bool)args[3];
        return result;
    }

    private static string GetStatus(Type agentType, Component agent)
    {
        return agentType.GetProperty("Status").GetValue(agent).ToString();
    }

    private static Vector3 GetMovementTarget(Type agentType, Component agent)
    {
        return (Vector3)agentType.GetProperty("MovementTarget").GetValue(agent);
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
