using System;
using System.Collections.Generic;
using System.Reflection;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEngine;

public sealed class PredatorChaseStateTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(objects[i]);
    }

    [Test]
    public void ClearCorridorUsesDirectNavigationAtChaseSpeedAndStoppingDistance()
    {
        NavigationGrid grid = CreateGrid(3, 3);
        ChaseFixture fixture = CreateFixture(true, grid.GridToWorldPosition(0, 0),
            grid.GridToWorldPosition(0, 2), grid);

        TickController(fixture);

        Assert.That(GetEnum(fixture.Controller, "Status"), Is.EqualTo("FollowingDirect"));
        Assert.That(GetProperty<bool>(fixture.Controller, "UsesChaseMovement"), Is.True);
        Assert.That(GetProperty<float>(fixture.Controller, "StoppingDistance"),
            Is.EqualTo(0.9f * 0.35f).Within(0.0001f));
        Assert.That(GetVelocity(fixture.Movement).magnitude, Is.EqualTo(5.4f).Within(0.001f));
    }

    [Test]
    public void BlockedCorridorUsesExistingAStarPath()
    {
        NavigationGrid grid = CreateGrid(3, 3, new Vector2Int(1, 1));
        ChaseFixture fixture = CreateFixture(true, grid.GridToWorldPosition(0, 0),
            grid.GridToWorldPosition(2, 2), grid);

        TickController(fixture);

        Assert.That(GetEnum(fixture.Controller, "Status"), Is.EqualTo("FollowingPath"));
        Assert.That(GetProperty<Vector3>(fixture.Controller, "MovementTarget"),
            Is.Not.EqualTo(fixture.Target.transform.position));
    }

    [Test]
    public void NavigationControllerOwnsMovementUntilItsTick()
    {
        NavigationGrid grid = CreateGrid(3, 3);
        ChaseFixture fixture = CreateFixture(true, grid.GridToWorldPosition(0, 0),
            grid.GridToWorldPosition(0, 2), grid);
        Vector3 start = fixture.Root.transform.position;

        TickStateMachine(fixture.Fsm);

        Assert.That(fixture.Root.transform.position, Is.EqualTo(start));
        Assert.That(GetVelocity(fixture.Movement), Is.EqualTo(Vector3.zero));
        Assert.That(GetProperty<bool>(fixture.Controller, "HasDestination"), Is.True);
    }

    [Test]
    public void MovingTargetRefreshesOnlyAfterDistanceAndCadenceThresholds()
    {
        NavigationGrid grid = CreateGrid(3, 5);
        ChaseFixture fixture = CreateFixture(true, grid.GridToWorldPosition(1, 0),
            grid.GridToWorldPosition(1, 3), grid);
        TickController(fixture);
        Assert.That(GetPrivateField<bool>(fixture.NavigationAgent, "pathValid"), Is.True);
        Vector3 originalDestination = GetProperty<Vector3>(fixture.Controller, "RequestedDestination");

        fixture.Target.transform.position = originalDestination + Vector3.right * 0.25f;
        TickChaseDestinationRefresh(fixture, 0.1f);

        Assert.That(GetProperty<Vector3>(fixture.Controller, "RequestedDestination"),
            Is.EqualTo(originalDestination));
        Assert.That(GetPrivateField<bool>(fixture.NavigationAgent, "pathValid"), Is.True);

        fixture.Target.transform.position = originalDestination + Vector3.right;
        TickChaseDestinationRefresh(fixture, 0.05f);

        Assert.That(GetProperty<Vector3>(fixture.Controller, "RequestedDestination"),
            Is.EqualTo(originalDestination));
        Assert.That(GetPrivateField<bool>(fixture.NavigationAgent, "pathValid"), Is.True);

        TickChaseDestinationRefresh(fixture, 0.05f);

        Assert.That(GetProperty<Vector3>(fixture.Controller, "RequestedDestination"),
            Is.EqualTo(fixture.Target.transform.position));
        Assert.That(GetPrivateField<bool>(fixture.NavigationAgent, "pathValid"), Is.False,
            "A due mobile-target refresh must invalidate the cached route exactly when it requests again.");
    }

    [Test]
    public void AttackTransitionCancelsChaseOwnershipAndRestoresControllerConfiguration()
    {
        NavigationGrid grid = CreateGrid(1, 3);
        ChaseFixture fixture = CreateFixture(true, grid.GridToWorldPosition(0, 0),
            grid.GridToWorldPosition(0, 2), grid);
        fixture.Target.transform.position = fixture.Root.transform.position + Vector3.right * 0.5f;

        TickStateMachine(fixture.Fsm);

        Assert.That(GetCurrentState(fixture.Fsm), Is.EqualTo("Attack"));
        Assert.That(GetProperty<bool>(fixture.Controller, "HasDestination"), Is.False);
        Assert.That(GetProperty<bool>(fixture.Controller, "UsesChaseMovement"), Is.False);
        Assert.That(GetProperty<float>(fixture.Controller, "StoppingDistance"),
            Is.EqualTo(0.25f).Within(0.0001f));
    }

    [Test]
    public void LostSightTransfersOwnershipToSearchAtLastKnownPosition()
    {
        NavigationGrid grid = CreateGrid(3, 5);
        Vector3 lastKnown = grid.GridToWorldPosition(1, 3);
        ChaseFixture fixture = CreateFixture(true, grid.GridToWorldPosition(1, 0), lastKnown, grid);
        SetBackingField(fixture.Perception, "CanSeeTarget", false);

        TickStateMachine(fixture.Fsm);

        Assert.That(GetCurrentState(fixture.Fsm), Is.EqualTo("Search"));
        Assert.That(GetProperty<bool>(fixture.Controller, "HasDestination"), Is.True);
        Assert.That(GetProperty<Vector3>(fixture.Controller, "RequestedDestination"),
            Is.EqualTo(lastKnown));
        Assert.That(GetProperty<bool>(fixture.Controller, "UsesChaseMovement"), Is.False);
    }

    [Test]
    public void TargetInsideGridSafeZoneReturnsToPatrolAndCancelsChaseOwnership()
    {
        NavigationGrid grid = CreateGrid(3, 3);
        Assert.That(grid.SetZone(1, 2, NavigationGridZone.SafeZone, "ChaseSafeZone"), Is.True);
        ChaseFixture fixture = CreateFixture(true, grid.GridToWorldPosition(1, 0),
            grid.GridToWorldPosition(1, 2), grid);

        TickStateMachine(fixture.Fsm);

        Assert.That(GetCurrentState(fixture.Fsm), Is.EqualTo("Patrol"));
        Assert.That(GetProperty<bool>(fixture.Controller, "HasDestination"), Is.False);
        Assert.That(GetProperty<bool>(fixture.Controller, "UsesChaseMovement"), Is.False);
    }

    [Test]
    public void ChaseWithoutControllerPreservesLegacyMovement()
    {
        ChaseFixture fixture = CreateFixture(false, Vector3.zero, new Vector3(4f, 0f, 0f));

        TickStateMachine(fixture.Fsm);

        Assert.That(GetVelocity(fixture.Movement).magnitude, Is.EqualTo(5.4f).Within(0.001f));
        Assert.That(GetCurrentState(fixture.Fsm), Is.EqualTo("Pursuit"));
    }

    private ChaseFixture CreateFixture(bool withController, Vector3 start, Vector3 targetPosition,
        NavigationGrid grid = null)
    {
        GameObject root = new GameObject("PredatorChaseStateTests_Root");
        objects.Add(root);
        root.transform.position = start;

        Component controller = withController ? Add(root, "PredatorNavigationMovementController") : null;
        Component fsm = Add(root, "PredatorFSM");
        Component movement = root.GetComponent(Type.GetType("PredatorMovement, Assembly-CSharp"));
        Component perception = root.GetComponent(Type.GetType("PredatorPerception, Assembly-CSharp"));
        Component navigationAgent = root.GetComponent(Type.GetType("GraphNavigationAgent, Assembly-CSharp"));

        InvokePrivate(fsm, "CacheDependencies");
        InvokePrivate(fsm, "InitializeStateMachine");
        Invoke(movement, "Configure", 2.85f, 5.4f, 8f, 5.4f, (LayerMask)0);

        if (controller != null)
        {
            Invoke(controller, "Configure", navigationAgent, movement, false, 0.25f);
            if (grid != null)
                Invoke(controller, "ConfigureGraphProvider", CreateProvider(grid));
        }

        GameObject target = new GameObject("PredatorChaseStateTests_Target");
        objects.Add(target);
        Add(target, "BoidThreat");
        target.transform.position = targetPosition;
        Invoke(fsm, "ConfigureTargets", new List<GameObject> { target });
        Invoke(fsm, "SetTarget", target);
        SetPrivateField(perception, "target", target);
        SetBackingField(perception, "CanSeeTarget", true);
        Invoke(perception, "UpdateTrackedPosition", target);
        ChangeState(fsm, "Pursuit");

        return new ChaseFixture(root, fsm, movement, perception, controller, navigationAgent, target);
    }

    private NavigationGrid CreateGrid(int rows, int columns, Vector2Int? blocked = null)
    {
        GameObject root = new GameObject("PredatorChaseStateTests_Grid");
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
                !blocked.HasValue || blocked.Value != coordinate, NavigationGridZone.Normal));
        }
        SetPrivateField(grid, "cells", cells);
        return grid;
    }

    private Component CreateProvider(NavigationGrid grid)
    {
        GameObject root = new GameObject("PredatorChaseStateTests_Provider");
        objects.Add(root);
        Component provider = Add(root, "CaughtGame.Navigation.NavigationGridGraphProvider");
        Invoke(provider, "Configure", grid, (LayerMask)0);
        return provider;
    }

    private static void TickController(ChaseFixture fixture)
    {
        Invoke(fixture.Controller, "TickNavigation");
    }

    private static void TickChaseDestinationRefresh(ChaseFixture fixture, float deltaTime)
    {
        object stateMachine = GetPrivateField<object>(fixture.Fsm, "stateMachine");
        object state = stateMachine.GetType().GetProperty("CurrentState").GetValue(stateMachine);
        state.GetType().GetMethod("UpdateNavigationDestination",
                BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(state, new object[] { fixture.Target.transform.position, deltaTime, false });
    }

    private static void TickStateMachine(Component fsm)
    {
        object stateMachine = GetPrivateField<object>(fsm, "stateMachine");
        stateMachine.GetType().GetMethod("Update").Invoke(stateMachine, null);
    }

    private static void ChangeState(Component fsm, string stateName)
    {
        object stateMachine = GetPrivateField<object>(fsm, "stateMachine");
        Type stateType = fsm.GetType().GetProperty("CurrentState").PropertyType;
        object state = Enum.Parse(stateType, stateName);
        stateMachine.GetType().GetMethod("ChangeState").Invoke(stateMachine, new[] { state });
    }

    private static string GetCurrentState(Component fsm) =>
        fsm.GetType().GetProperty("CurrentState").GetValue(fsm).ToString();

    private static Component Add(GameObject root, string typeName)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, typeName);
        return root.AddComponent(type);
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

    private static T GetProperty<T>(Component component, string name) =>
        (T)component.GetType().GetProperty(name).GetValue(component);

    private static string GetEnum(Component component, string name) =>
        component.GetType().GetProperty(name).GetValue(component).ToString();

    private static Vector3 GetVelocity(Component movement) => GetProperty<Vector3>(movement, "Velocity");

    private static T GetPrivateField<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(target);

    private static void SetPrivateField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private static void SetBackingField(object target, string propertyName, object value) =>
        SetPrivateField(target, $"<{propertyName}>k__BackingField", value);

    private sealed class ChaseFixture
    {
        public readonly GameObject Root;
        public readonly Component Fsm;
        public readonly Component Movement;
        public readonly Component Perception;
        public readonly Component Controller;
        public readonly Component NavigationAgent;
        public readonly GameObject Target;

        public ChaseFixture(GameObject root, Component fsm, Component movement, Component perception,
            Component controller, Component navigationAgent, GameObject target)
        {
            Root = root;
            Fsm = fsm;
            Movement = movement;
            Perception = perception;
            Controller = controller;
            NavigationAgent = navigationAgent;
            Target = target;
        }
    }
}
