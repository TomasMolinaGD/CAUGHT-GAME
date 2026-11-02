using System;
using System.Collections.Generic;
using System.Reflection;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEngine;

public sealed class PredatorSearchStateTests
{
    private const float SearchRadius = 2f;
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(objects[i]);
    }

    [Test]
    public void NavigationSearchRequestsLastKnownOnceAndDoesNotMoveDirectly()
    {
        NavigationGrid grid = CreateGrid(3, 5);
        Vector3 start = grid.GridToWorldPosition(1, 0);
        Vector3 lastKnown = grid.GridToWorldPosition(1, 4);
        SearchFixture fixture = CreateFixture(true, start, lastKnown, grid);
        Vector3 beforeSearchTick = fixture.Root.transform.position;

        Assert.That(GetProperty<bool>(fixture.Controller, "HasDestination"), Is.True);
        Assert.That(GetProperty<Vector3>(fixture.Controller, "RequestedDestination"), Is.EqualTo(lastKnown));

        TickStateMachine(fixture.Fsm);

        Assert.That(fixture.Root.transform.position, Is.EqualTo(beforeSearchTick));
        Assert.That(GetVelocity(fixture.Movement), Is.EqualTo(Vector3.zero));

        Invoke(fixture.Controller, "TickNavigation");
        Assert.That(GetProperty<bool>(fixture.Controller, "LastTickCommandedMovement"), Is.True);
        Assert.That(GetVelocity(fixture.Movement).magnitude, Is.EqualTo(2.85f).Within(0.001f));
        Assert.That(GetPrivateField<bool>(fixture.NavigationAgent, "pathValid"), Is.True);

        TickStateMachine(fixture.Fsm);

        Assert.That(GetPrivateField<bool>(fixture.NavigationAgent, "pathValid"), Is.True,
            "Search must not invalidate and repeat an active destination request.");
    }

    [Test]
    public void MissingProviderProcessesLastKnownOnceThenCompletesEightRadialAttempts()
    {
        Vector3 lastKnown = new Vector3(4f, 0f, 3f);
        SearchFixture fixture = CreateFixture(true, Vector3.zero, lastKnown);

        FailCurrentDestinationAndAdvance(fixture);

        Assert.That(GetProperty<Vector3>(fixture.Controller, "RequestedDestination"),
            Is.EqualTo(lastKnown + Vector3.right * SearchRadius));
        Assert.That(GetCurrentState(fixture.Fsm), Is.EqualTo("Search"));

        TickStateMachine(fixture.Fsm);
        Assert.That(GetProperty<Vector3>(fixture.Controller, "RequestedDestination"),
            Is.EqualTo(lastKnown + Vector3.right * SearchRadius),
            "A stale failure status must not skip a destination before its controller tick.");

        for (int i = 0; i < 7; i++)
        {
            Vector3 previousDestination = GetProperty<Vector3>(fixture.Controller, "RequestedDestination");
            FailCurrentDestinationAndAdvance(fixture);
            Assert.That(GetProperty<Vector3>(fixture.Controller, "RequestedDestination"),
                Is.Not.EqualTo(previousDestination));
        }

        FailCurrentDestinationAndAdvance(fixture);

        Assert.That(GetCurrentState(fixture.Fsm), Is.EqualTo("Patrol"));
        Assert.That(GetProperty<bool>(fixture.Controller, "HasDestination"), Is.False);
    }

    [Test]
    public void SafeZoneNoPathAdvancesFromLastKnownToFirstRadialPoint()
    {
        NavigationGrid grid = CreateGrid(3, 5, safeZone: new Vector2Int(1, 2));
        Vector3 start = grid.GridToWorldPosition(1, 0);
        Vector3 lastKnown = grid.GridToWorldPosition(1, 2);
        SearchFixture fixture = CreateFixture(true, start, lastKnown, grid, true);

        Invoke(fixture.Controller, "TickNavigation");
        Assert.That(GetEnum(fixture.Controller, "Status"), Is.EqualTo("NoPath"));

        TickStateMachine(fixture.Fsm);

        Assert.That(GetProperty<Vector3>(fixture.Controller, "RequestedDestination"),
            Is.EqualTo(lastKnown + Vector3.right * SearchRadius));
        Assert.That(GetCurrentState(fixture.Fsm), Is.EqualTo("Search"));
    }

    [Test]
    public void ArrivedAtLastKnownRequestsFirstRadialPoint()
    {
        NavigationGrid grid = CreateGrid(3, 3);
        Vector3 lastKnown = grid.GridToWorldPosition(1, 1);
        SearchFixture fixture = CreateFixture(true, lastKnown, lastKnown, grid);

        Invoke(fixture.Controller, "TickNavigation");
        Assert.That(GetEnum(fixture.Controller, "Status"), Is.EqualTo("Arrived"));

        TickStateMachine(fixture.Fsm);

        Assert.That(GetProperty<Vector3>(fixture.Controller, "RequestedDestination"),
            Is.EqualTo(lastKnown + Vector3.right * SearchRadius));
        Assert.That(GetProperty<bool>(fixture.Controller, "HasDestination"), Is.True);
    }

    [Test]
    public void VisibleTargetTransitionsToPursuitAndCancelsOwnedNavigation()
    {
        SearchFixture fixture = CreateFixture(true, Vector3.zero, new Vector3(4f, 0f, 0f));
        SetPrivateField(fixture.Perception, "target", fixture.Target);
        SetBackingField(fixture.Perception, "CanSeeTarget", true);

        TickStateMachine(fixture.Fsm);

        Assert.That(GetCurrentState(fixture.Fsm), Is.EqualTo("Pursuit"));
        Assert.That(GetProperty<bool>(fixture.Controller, "HasDestination"), Is.False);
        Assert.That(GetVelocity(fixture.Movement), Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void SearchWithoutControllerPreservesLegacyMovement()
    {
        SearchFixture fixture = CreateFixture(false, Vector3.zero, new Vector3(4f, 0f, 0f));

        TickStateMachine(fixture.Fsm);

        Assert.That(fixture.Root.transform.position.x, Is.GreaterThan(0f));
        Assert.That(GetVelocity(fixture.Movement).sqrMagnitude, Is.GreaterThan(0f));
        Assert.That(GetCurrentState(fixture.Fsm), Is.EqualTo("Search"));
    }

    private SearchFixture CreateFixture(bool withController, Vector3 start, Vector3 lastKnown,
        NavigationGrid grid = null, bool avoidSafeZones = false)
    {
        GameObject root = new GameObject("PredatorSearchStateTests_Root");
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
            {
                Component provider = CreateProvider(grid);
                Invoke(controller, "ConfigureGraphProvider", provider);
            }
            if (avoidSafeZones)
                Invoke(navigationAgent, "Configure", true, 0.4f, 1.8f);
        }

        GameObject target = new GameObject("PredatorSearchStateTests_Target");
        objects.Add(target);
        Add(target, "BoidThreat");
        target.transform.position = new Vector3(20f, 0f, 20f);
        Invoke(fsm, "ConfigureTargets", new List<GameObject> { target });
        Invoke(fsm, "SetTarget", target);
        Invoke(perception, "ReceiveAlert", target, lastKnown);
        ChangeState(fsm, "Search");

        return new SearchFixture(root, fsm, movement, perception, controller, navigationAgent, target);
    }

    private NavigationGrid CreateGrid(int rows, int columns, Vector2Int? safeZone = null)
    {
        GameObject root = new GameObject("PredatorSearchStateTests_Grid");
        objects.Add(root);
        NavigationGrid grid = root.AddComponent<NavigationGrid>();
        Assert.That(grid.TryApplyGridSize(columns, rows, 1f, true, out _), Is.True);
        if (safeZone.HasValue)
            Assert.That(grid.SetZone(safeZone.Value.x, safeZone.Value.y,
                NavigationGridZone.SafeZone, "SearchSafeZone"), Is.True);
        return grid;
    }

    private Component CreateProvider(NavigationGrid grid)
    {
        GameObject root = new GameObject("PredatorSearchStateTests_Provider");
        objects.Add(root);
        Component provider = Add(root, "CaughtGame.Navigation.NavigationGridGraphProvider");
        Invoke(provider, "Configure", grid, (LayerMask)0);
        return provider;
    }

    private static void FailCurrentDestinationAndAdvance(SearchFixture fixture)
    {
        Invoke(fixture.Controller, "TickNavigation");
        Assert.That(GetEnum(fixture.Controller, "Status"), Is.EqualTo("MissingProvider"));
        TickStateMachine(fixture.Fsm);
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

    private static string GetCurrentState(Component fsm)
    {
        return fsm.GetType().GetProperty("CurrentState").GetValue(fsm).ToString();
    }

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

    private static T GetProperty<T>(Component component, string name)
    {
        return (T)component.GetType().GetProperty(name).GetValue(component);
    }

    private static string GetEnum(Component component, string name)
    {
        return component.GetType().GetProperty(name).GetValue(component).ToString();
    }

    private static Vector3 GetVelocity(Component movement)
    {
        return GetProperty<Vector3>(movement, "Velocity");
    }

    private static T GetPrivateField<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(target);
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private static void SetBackingField(object target, string propertyName, object value)
    {
        SetPrivateField(target, $"<{propertyName}>k__BackingField", value);
    }

    private sealed class SearchFixture
    {
        public readonly GameObject Root;
        public readonly Component Fsm;
        public readonly Component Movement;
        public readonly Component Perception;
        public readonly Component Controller;
        public readonly Component NavigationAgent;
        public readonly GameObject Target;

        public SearchFixture(GameObject root, Component fsm, Component movement, Component perception,
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
