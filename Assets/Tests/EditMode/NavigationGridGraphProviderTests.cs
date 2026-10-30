using System;
using System.Collections.Generic;
using System.Reflection;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEngine;

public sealed class NavigationGridGraphProviderTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(objects[i]);
        }
    }

    [Test]
    public void TryFindPath_CopiesWolfWaypointsInOrder()
    {
        NavigationGrid grid = CreateGrid(3, 3);
        Component provider = CreateProvider(grid);
        var path = new List<Vector3>();

        bool found = InvokeTryFindPath(provider, grid.GridToWorldPosition(0, 0),
            grid.GridToWorldPosition(2, 2), path);

        Assert.IsTrue(found);
        Assert.Greater(path.Count, 1);
        Assert.That(path[0], Is.EqualTo(grid.GridToWorldPosition(0, 0)));
        Assert.That(path[path.Count - 1], Is.EqualTo(grid.GridToWorldPosition(2, 2)));
    }

    [Test]
    public void TryFindPath_RoutesAroundBlockedCell()
    {
        NavigationGrid grid = CreateGrid(3, 3, blocked: new Vector2Int(1, 1));
        Component provider = CreateProvider(grid);
        var path = new List<Vector3>();

        bool found = InvokeTryFindPath(provider, grid.GridToWorldPosition(0, 0),
            grid.GridToWorldPosition(2, 2), path);

        Assert.IsTrue(found);
        Assert.IsFalse(path.Contains(grid.GridToWorldPosition(1, 1)));
    }

    [Test]
    public void TryFindPath_RejectsSafeZoneDestinationAndClearsOutput()
    {
        NavigationGrid grid = CreateGrid(1, 3, safeZone: new Vector2Int(0, 2));
        Component provider = CreateProvider(grid);
        var path = new List<Vector3> { Vector3.one };

        bool found = InvokeTryFindPath(provider, grid.GridToWorldPosition(0, 0),
            grid.GridToWorldPosition(0, 2), path);

        Assert.IsFalse(found);
        Assert.IsEmpty(path);
    }

    [Test]
    public void TryFindPath_RejectsWorldPositionsOutsideGrid()
    {
        NavigationGrid grid = CreateGrid(2, 2);
        Component provider = CreateProvider(grid);
        var path = new List<Vector3> { Vector3.one };

        bool found = InvokeTryFindPath(provider, new Vector3(-1f, 0f, 0f),
            grid.GridToWorldPosition(1, 1), path);

        Assert.IsFalse(found);
        Assert.IsEmpty(path);
    }

    [Test]
    public void HasDirectLineOfSight_RejectsSafeZoneInCorridor()
    {
        NavigationGrid grid = CreateGrid(1, 3, safeZone: new Vector2Int(0, 1));
        Component provider = CreateProvider(grid);

        bool visible = InvokeHasDirectLineOfSight(provider, grid.GridToWorldPosition(0, 0),
            grid.GridToWorldPosition(0, 2), 0.25f, 1.8f);

        Assert.IsFalse(visible);
    }

    private NavigationGrid CreateGrid(int rows, int columns,
        Vector2Int? blocked = null, Vector2Int? safeZone = null)
    {
        GameObject root = new GameObject("NavigationGridGraphProviderTests_Grid");
        objects.Add(root);
        NavigationGrid grid = root.AddComponent<NavigationGrid>();
        SetPrivateField(grid, "rows", rows);
        SetPrivateField(grid, "columns", columns);
        SetPrivateField(grid, "cellSize", 1f);
        var cells = new List<NavigationGridCell>(rows * columns);
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Vector2Int coordinate = new Vector2Int(row, column);
                bool walkable = !blocked.HasValue || blocked.Value != coordinate;
                NavigationGridZone zone = safeZone.HasValue && safeZone.Value == coordinate
                    ? NavigationGridZone.SafeZone
                    : NavigationGridZone.Normal;
                cells.Add(new NavigationGridCell(row, column, walkable, zone));
            }
        }

        SetPrivateField(grid, "cells", cells);
        return grid;
    }

    private Component CreateProvider(NavigationGrid grid)
    {
        GameObject root = new GameObject("NavigationGridGraphProviderTests_Provider");
        objects.Add(root);
        Type providerType = Type.GetType(
            "CaughtGame.Navigation.NavigationGridGraphProvider, Assembly-CSharp");
        Assert.IsNotNull(providerType, "NavigationGridGraphProvider must be compiled in Assembly-CSharp.");
        Component provider = root.AddComponent(providerType);
        providerType.GetMethod("Configure", BindingFlags.Public | BindingFlags.Instance)
            .Invoke(provider, new object[] { grid, (LayerMask)0 });
        return provider;
    }

    private static bool InvokeTryFindPath(Component provider, Vector3 start,
        Vector3 destination, List<Vector3> path)
    {
        MethodInfo method = provider.GetType().GetMethod("TryFindPath");
        return (bool)method.Invoke(provider, new object[] { start, destination, true, path });
    }

    private static bool InvokeHasDirectLineOfSight(Component provider, Vector3 start,
        Vector3 destination, float radius, float height)
    {
        MethodInfo method = provider.GetType().GetMethod("HasDirectLineOfSight");
        return (bool)method.Invoke(provider, new object[] { start, destination, radius, height });
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }
}
