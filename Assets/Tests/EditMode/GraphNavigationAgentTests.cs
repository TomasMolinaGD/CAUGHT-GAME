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

    private Component CreateProvider(NavigationGrid grid)
    {
        GameObject root = new GameObject("GraphNavigationAgentTests_Provider");
        objects.Add(root);
        Type providerType = Type.GetType("CaughtGame.Navigation.NavigationGridGraphProvider, Assembly-CSharp");
        Assert.IsNotNull(providerType);
        Component provider = root.AddComponent(providerType);
        providerType.GetMethod("Configure", BindingFlags.Public | BindingFlags.Instance)
            .Invoke(provider, new object[] { grid, (LayerMask)0 });
        return provider;
    }

    private NavigationGrid CreateGrid(int rows, int columns, Vector2Int? blocked = null)
    {
        GameObject root = new GameObject("GraphNavigationAgentTests_Grid");
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

    private static void ConfigureGraph(Type agentType, Component agent, Component provider)
    {
        agentType.GetMethod("ConfigureGraphComponent").Invoke(agent, new object[] { provider });
    }

    private static bool InvokeTryMove(Type agentType, Component agent, Vector3 destination, out bool moving)
    {
        object[] args = { destination, 2f, 0.1f, false };
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
