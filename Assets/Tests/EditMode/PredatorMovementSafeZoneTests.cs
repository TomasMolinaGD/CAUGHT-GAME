using System;
using System.Collections.Generic;
using System.Reflection;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEngine;

public sealed class PredatorMovementSafeZoneTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(objects[i]);
    }

    [Test]
    public void ChaseSegmentOutsideSafeZoneRemainsTraversable()
    {
        NavigationGrid grid = CreateGrid(2, 5, new Vector2Int(1, 4));
        Component movement = CreateMovementWithGrid(grid, new Vector3(0.5f, 0f, 0.5f), 0.2f);

        Assert.That(CanMove(movement, new Vector3(2f, 0f, 0f)), Is.True);
    }

    [Test]
    public void FrontalEntryIntoSafeZoneIsBlocked()
    {
        NavigationGrid grid = CreateGrid(1, 4, new Vector2Int(0, 2));
        Component movement = CreateMovementWithGrid(grid, grid.GridToWorldPosition(0, 0), 0.2f);

        Assert.That(CanMove(movement, new Vector3(2.1f, 0f, 0f)), Is.False);
    }

    [Test]
    public void DiagonalEntryIntoSafeZoneIsBlocked()
    {
        NavigationGrid grid = CreateGrid(3, 3, new Vector2Int(1, 1));
        Component movement = CreateMovementWithGrid(grid, grid.GridToWorldPosition(0, 0), 0.2f);

        Assert.That(CanMove(movement, new Vector3(1.2f, 0f, 1.2f)), Is.False);
    }

    [Test]
    public void SegmentCrossingSafeZoneIsBlockedEvenWhenDestinationIsNormal()
    {
        NavigationGrid grid = CreateGrid(1, 5, new Vector2Int(0, 2));
        Component movement = CreateMovementWithGrid(grid, grid.GridToWorldPosition(0, 0), 0.2f);

        Assert.That(CanMove(movement, new Vector3(4f, 0f, 0f)), Is.False);
    }

    [Test]
    public void TangentialMovementOutsideWolfRadiusRemainsTraversable()
    {
        NavigationGrid grid = CreateGrid(2, 5, new Vector2Int(1, 2));
        Component movement = CreateMovementWithGrid(grid, grid.GridToWorldPosition(0, 0), 0.2f);

        Assert.That(CanMove(movement, new Vector3(4f, 0f, 0f)), Is.True);
    }

    [Test]
    public void WolfRadiusOverlappingSafeZoneBlocksMovement()
    {
        NavigationGrid grid = CreateGrid(2, 5, new Vector2Int(1, 2));
        Component movement = CreateMovementWithGrid(grid, grid.GridToWorldPosition(0, 0), 0.55f);

        Assert.That(CanMove(movement, new Vector3(4f, 0f, 0f)), Is.False);
    }

    [Test]
    public void WolfRadiusCornerOverlapWithSafeZoneBlocksMovement()
    {
        NavigationGrid grid = CreateGrid(2, 3, new Vector2Int(1, 2));
        Component movement = CreateMovementWithGrid(grid,
            new Vector3(1.59f, 0f, 0.85f), 0.4f);

        Assert.That(CanMove(movement, new Vector3(0.06f, 0f, 0f)), Is.False);
    }

    [Test]
    public void MovementWithoutNavigationGridKeepsLegacyBehavior()
    {
        GameObject root = CreateObject("LegacyPredatorMovement", Vector3.zero);
        Component movement = root.AddComponent(Type.GetType("PredatorMovement, Assembly-CSharp"));
        SetPrivateField(movement, "collisionRadius", 0.2f);

        Assert.That(CanMove(movement, Vector3.right), Is.True);
    }

    [Test]
    public void ProviderWithoutNavigationGridKeepsLegacyBehavior()
    {
        GameObject root = CreateObject("UnconfiguredGridPredatorMovement", Vector3.zero);
        Component controller = root.AddComponent(Type.GetType(
            "PredatorNavigationMovementController, Assembly-CSharp"));
        Component movement = root.GetComponent(Type.GetType("PredatorMovement, Assembly-CSharp"));
        SetPrivateField(movement, "collisionRadius", 0.2f);
        GameObject providerRoot = CreateObject("UnconfiguredGridProvider", Vector3.zero);
        Component provider = providerRoot.AddComponent(Type.GetType(
            "CaughtGame.Navigation.NavigationGridGraphProvider, Assembly-CSharp"));
        Invoke(controller, "ConfigureGraphProvider", provider);

        Assert.That(CanMove(movement, Vector3.right), Is.True);
    }

    [Test]
    public void SafeZoneVolumeStillBlocksMovement()
    {
        GameObject root = CreateObject("VolumePredatorMovement", Vector3.zero);
        Component movement = root.AddComponent(Type.GetType("PredatorMovement, Assembly-CSharp"));
        SetPrivateField(movement, "collisionRadius", 0.2f);
        GameObject zone = CreateObject("MovementSafeZoneVolume", new Vector3(1f, 0f, 0f));
        zone.transform.localScale = new Vector3(1f, 2f, 1f);
        zone.AddComponent<BoxCollider>();
        Component volume = zone.AddComponent(Type.GetType("SafeZoneVolume, Assembly-CSharp"));
        InvokePrivate(volume, "OnEnable");
        Physics.SyncTransforms();

        Assert.That(CanMove(movement, new Vector3(1.5f, 0f, 0f)), Is.False);
    }

    [Test]
    public void SafeZoneRecoveryFindsNormalWalkableExit()
    {
        NavigationGrid grid = CreateGridWithZones(1, 3,
            new[] { new Vector2Int(0, 0) });
        Component provider = CreateProvider(grid);
        var recoveryPath = new List<Vector3>();

        bool found = InvokeRecoveryPath(provider, grid.GridToWorldPosition(0, 0),
            0.2f, 1.6f, recoveryPath);

        Assert.That(found, Is.True);
        Assert.That(recoveryPath, Is.Not.Empty);
        Assert.That(recoveryPath[recoveryPath.Count - 1],
            Is.EqualTo(grid.GridToWorldPosition(0, 1)));
    }

    [Test]
    public void SafeZoneRecoveryRejectsObstacleBlockedExit()
    {
        NavigationGrid grid = CreateGridWithZones(1, 2,
            new[] { new Vector2Int(0, 0) });
        Component provider = CreateProvider(grid, 1 << 12);
        GameObject obstacle = CreateObject("RecoveryExitObstacle", grid.GridToWorldPosition(0, 1));
        obstacle.layer = 12;
        obstacle.AddComponent<BoxCollider>().size = new Vector3(0.8f, 2f, 0.8f);
        Physics.SyncTransforms();
        var recoveryPath = new List<Vector3>();

        bool found = InvokeRecoveryPath(provider, grid.GridToWorldPosition(0, 0),
            0.2f, 1.6f, recoveryPath);

        Assert.That(found, Is.False);
        Assert.That(recoveryPath, Is.Empty);
    }

    [Test]
    public void SafeZoneRecoveryRejectsGridWithoutNormalExit()
    {
        NavigationGrid grid = CreateGridWithZones(1, 2,
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1) });
        Component provider = CreateProvider(grid);
        var recoveryPath = new List<Vector3>();

        bool found = InvokeRecoveryPath(provider, grid.GridToWorldPosition(0, 0),
            0.2f, 1.6f, recoveryPath);

        Assert.That(found, Is.False);
        Assert.That(recoveryPath, Is.Empty);
    }

    [Test]
    public void ControllerRecoversInvalidOriginAndResumesRequestedNavigation()
    {
        NavigationGrid grid = CreateGridWithZones(1, 4,
            new[] { new Vector2Int(0, 0) });
        GameObject root = CreateObject("RecoveringPredator", grid.GridToWorldPosition(0, 0));
        Component controller = root.AddComponent(Type.GetType(
            "PredatorNavigationMovementController, Assembly-CSharp"));
        Component provider = CreateProvider(grid);
        Invoke(controller, "ConfigureGraphProvider", provider);
        Vector3 destination = grid.GridToWorldPosition(0, 3);
        Invoke(controller, "RequestDestination", destination);

        Invoke(controller, "TickNavigation");

        Assert.That(GetProperty<bool>(controller, "IsRecoveringFromInvalidOrigin"), Is.True);
        Assert.That(GetProperty<bool>(controller, "HasDestination"), Is.True);

        root.transform.position = grid.GridToWorldPosition(0, 1);
        Invoke(controller, "TickNavigation");
        Invoke(controller, "TickNavigation");

        Assert.That(GetProperty<bool>(controller, "IsRecoveringFromInvalidOrigin"), Is.False);
        Assert.That(GetProperty<object>(controller, "Status").ToString(),
            Is.Not.EqualTo("NoPath"));
        Assert.That(root.transform.position.x, Is.GreaterThan(grid.GridToWorldPosition(0, 1).x));
        Component movement = root.GetComponent(Type.GetType("PredatorMovement, Assembly-CSharp"));
        Assert.That(CanMove(movement, Vector3.left * 1.1f), Is.False,
            "Recovery permission must not allow a later Chase movement back into Safe Zone.");
    }

    [Test]
    public void ControllerAttemptsUnavailableRecoveryOnlyOncePerDestination()
    {
        NavigationGrid grid = CreateGridWithZones(1, 2,
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1) });
        GameObject root = CreateObject("TrappedPredator", grid.GridToWorldPosition(0, 0));
        Component controller = root.AddComponent(Type.GetType(
            "PredatorNavigationMovementController, Assembly-CSharp"));
        Invoke(controller, "ConfigureGraphProvider", CreateProvider(grid));
        Invoke(controller, "RequestDestination", grid.GridToWorldPosition(0, 1));
        Vector3 before = root.transform.position;

        Invoke(controller, "TickNavigation");
        Invoke(controller, "TickNavigation");

        Assert.That(GetProperty<object>(controller, "Status").ToString(), Is.EqualTo("NoPath"));
        Assert.That(GetProperty<bool>(controller, "IsRecoveringFromInvalidOrigin"), Is.False);
        Assert.That(GetPrivateField<bool>(controller, "recoveryAttemptedForDestination"), Is.True);
        Assert.That(root.transform.position, Is.EqualTo(before));
    }

    [Test]
    public void ControllerBuildsRecoveryForPhysicalMovementRadius()
    {
        NavigationGrid grid = CreateGridWithZones(3, 5,
            new[] { new Vector2Int(1, 1) });
        GameObject root = CreateObject("PhysicalRadiusRecoveryPredator",
            grid.GridToWorldPosition(1, 1));
        Component controller = root.AddComponent(Type.GetType(
            "PredatorNavigationMovementController, Assembly-CSharp"));
        Component movement = root.GetComponent(Type.GetType("PredatorMovement, Assembly-CSharp"));
        SetPrivateField(controller, "agentRadius", 0.2f);
        SetPrivateField(movement, "collisionRadius", 0.55f);
        Invoke(controller, "ConfigureGraphProvider", CreateProvider(grid));
        Invoke(controller, "RequestDestination", grid.GridToWorldPosition(1, 3));

        Invoke(controller, "TickNavigation");

        List<Vector3> recoveryPath = GetPrivateField<List<Vector3>>(
            controller, "safeZoneRecoveryPath");
        Assert.That(recoveryPath, Is.Not.Empty);
        Assert.That(recoveryPath[recoveryPath.Count - 1],
            Is.EqualTo(grid.GridToWorldPosition(1, 3)));
    }

    [Test]
    public void ControllerReevaluatesSafeZoneOriginAfterRelocationDuringFollowingPath()
    {
        NavigationGrid grid = CreateGridWithZones(3, 5,
            new[] { new Vector2Int(0, 2) });
        GameObject root = CreateObject("RelocatedFollowingPathPredator",
            grid.GridToWorldPosition(1, 0));
        Component controller = root.AddComponent(Type.GetType(
            "PredatorNavigationMovementController, Assembly-CSharp"));
        Component provider = CreateProvider(grid, 1 << 12);
        GameObject obstacle = CreateObject("RelocationPathObstacle",
            grid.GridToWorldPosition(1, 2));
        obstacle.layer = 12;
        obstacle.AddComponent<BoxCollider>().size = new Vector3(0.8f, 2f, 0.8f);
        Physics.SyncTransforms();
        Invoke(controller, "ConfigureGraphProvider", provider);
        Invoke(controller, "RequestDestination", grid.GridToWorldPosition(1, 4));
        Invoke(controller, "TickNavigation");
        Assert.That(GetProperty<object>(controller, "Status").ToString(),
            Is.EqualTo("FollowingPath"));

        root.transform.position = grid.GridToWorldPosition(0, 2);
        Invoke(controller, "TickNavigation");

        Assert.That(GetProperty<bool>(controller, "IsRecoveringFromInvalidOrigin"), Is.True);
        Assert.That(GetProperty<bool>(controller, "HasDestination"), Is.True);
    }

    private Component CreateMovementWithGrid(NavigationGrid grid, Vector3 position, float radius)
    {
        GameObject root = CreateObject("GridPredatorMovement", position);
        Type controllerType = Type.GetType(
            "PredatorNavigationMovementController, Assembly-CSharp");
        Component controller = root.AddComponent(controllerType);
        Component movement = root.GetComponent(Type.GetType("PredatorMovement, Assembly-CSharp"));
        SetPrivateField(movement, "collisionRadius", radius);
        Component provider = CreateProvider(grid);
        Invoke(controller, "ConfigureGraphProvider", provider);
        return movement;
    }

    private NavigationGrid CreateGrid(int rows, int columns, Vector2Int safeZone)
    {
        GameObject root = CreateObject("PredatorMovementSafeZoneGrid", Vector3.zero);
        NavigationGrid grid = root.AddComponent<NavigationGrid>();
        SetPrivateField(grid, "rows", rows);
        SetPrivateField(grid, "columns", columns);
        SetPrivateField(grid, "cellSize", 1f);
        var cells = new List<NavigationGridCell>(rows * columns);
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            NavigationGridZone zone = new Vector2Int(row, column) == safeZone
                ? NavigationGridZone.SafeZone
                : NavigationGridZone.Normal;
            cells.Add(new NavigationGridCell(row, column, true, zone));
        }
        SetPrivateField(grid, "cells", cells);
        return grid;
    }

    private Component CreateProvider(NavigationGrid grid, int obstacleMask = 0)
    {
        GameObject root = CreateObject("PredatorMovementSafeZoneProvider", Vector3.zero);
        Component provider = root.AddComponent(Type.GetType(
            "CaughtGame.Navigation.NavigationGridGraphProvider, Assembly-CSharp"));
        Invoke(provider, "Configure", grid, (LayerMask)obstacleMask);
        return provider;
    }

    private NavigationGrid CreateGridWithZones(int rows, int columns,
        IEnumerable<Vector2Int> safeZones)
    {
        GameObject root = CreateObject("PredatorMovementRecoveryGrid", Vector3.zero);
        NavigationGrid grid = root.AddComponent<NavigationGrid>();
        SetPrivateField(grid, "rows", rows);
        SetPrivateField(grid, "columns", columns);
        SetPrivateField(grid, "cellSize", 1f);
        var safe = new HashSet<Vector2Int>(safeZones);
        var cells = new List<NavigationGridCell>(rows * columns);
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            Vector2Int coordinate = new Vector2Int(row, column);
            cells.Add(new NavigationGridCell(row, column, true,
                safe.Contains(coordinate) ? NavigationGridZone.SafeZone : NavigationGridZone.Normal));
        }
        SetPrivateField(grid, "cells", cells);
        return grid;
    }

    private static bool InvokeRecoveryPath(Component provider, Vector3 start, float radius,
        float height, List<Vector3> path)
    {
        MethodInfo method = provider.GetType().GetMethod("TryBuildSafeZoneRecoveryPath",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(provider, new object[] { start, radius, height, path });
    }

    private GameObject CreateObject(string name, Vector3 position)
    {
        var created = new GameObject(name);
        created.transform.position = position;
        objects.Add(created);
        return created;
    }

    private static bool CanMove(Component movement, Vector3 displacement)
    {
        return (bool)movement.GetType().GetMethod(
            "CanMove", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(movement, new object[] { displacement });
    }

    private static void Invoke(Component component, string method, params object[] arguments)
    {
        component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public)
            .Invoke(component, arguments);
    }

    private static void InvokePrivate(Component component, string method)
    {
        component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(component, null);
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private static T GetProperty<T>(object target, string name)
    {
        PropertyInfo property = target.GetType().GetProperty(name);
        Assert.That(property, Is.Not.Null);
        return (T)property.GetValue(target);
    }

    private static T GetPrivateField<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(target);
    }
}
