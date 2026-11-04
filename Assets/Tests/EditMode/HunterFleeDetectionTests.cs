using System;
using System.Collections.Generic;
using System.Reflection;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEngine;

public sealed class HunterFleeDetectionTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject instance in objects)
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }

        objects.Clear();
    }

    [Test]
    public void DetectsClosestWolfWithinRangeAndLineOfSight()
    {
        Component perception = CreatePerception(5f);
        Component fartherWolf = CreateWolf(new Vector3(4f, 0f, 0f));
        Component closerWolf = CreateWolf(new Vector3(2f, 0f, 0f));

        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.SameAs(closerWolf));
        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Not.SameAs(fartherWolf));
    }

    [Test]
    public void WolfOutsideDetectionRadiusIsNotDetected()
    {
        Component perception = CreatePerception(3f);
        CreateWolf(new Vector3(4f, 0f, 0f));

        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Null);
    }

    [Test]
    public void ObstacleBlocksWolfLineOfSight()
    {
        Component perception = CreatePerception(5f);
        CreateWolf(new Vector3(4f, 0f, 0f));
        GameObject obstacle = CreateObject(
            "HunterFleeDetectionTests.Obstacle", new Vector3(2f, 1f, 0f));
        obstacle.layer = 12;
        obstacle.AddComponent<BoxCollider>().size = new Vector3(1f, 3f, 1f);

        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Null);
    }

    [Test]
    public void BlockedWolfDoesNotHideDifferentVisibleWolf()
    {
        Component perception = CreatePerception(6f);
        CreateWolf(new Vector3(4f, 0f, 0f));
        Component visibleWolf = CreateWolf(new Vector3(0f, 0f, 4f));
        GameObject obstacle = CreateObject(
            "HunterFleeDetectionTests.Obstacle", new Vector3(2f, 1f, 0f));
        obstacle.layer = 12;
        obstacle.AddComponent<BoxCollider>().size = new Vector3(1f, 3f, 1f);

        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.SameAs(visibleWolf));
    }

    [Test]
    public void WolfDetectionDoesNotReplaceBoidDetection()
    {
        Component perception = CreatePerception(5f);
        CreateWolf(new Vector3(2f, 0f, 0f));
        Component boid = CreateLivingBoid(new Vector3(0f, 0f, 2f));

        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Not.Null);
        Assert.That(GetProperty<object>(perception, "ClosestLivingBoid"), Is.SameAs(boid));
    }

    [Test]
    public void DetectedWolfInterruptsAttackAndEntersFleeOnlyOnce()
    {
        Component hunter = CreateHunterAgent();
        CreateWolf(new Vector3(2f, 0f, 0f));
        object machine = GetPrivateField(hunter, "stateMachine");
        Type policeState = FindProjectType("PoliceState");
        Assert.That((bool)Invoke(machine, "ChangeState", Enum.Parse(policeState, "Attack")), Is.True);

        Physics.SyncTransforms();
        Component perception = (Component)GetProperty<object>(hunter, "Perception");
        Invoke(perception, "ForceRefresh");
        InvokePrivate(hunter, "Update");
        object fleeState = GetProperty<object>(machine, "CurrentState");
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
        Assert.That(GetProperty<object>(machine, "CurrentState"), Is.SameAs(fleeState));
    }

    [Test]
    public void DetectedWolfDoesNotInterruptDeadState()
    {
        Component hunter = CreateHunterAgent();
        CreateWolf(new Vector3(2f, 0f, 0f));
        object machine = GetPrivateField(hunter, "stateMachine");
        Type policeState = FindProjectType("PoliceState");

        object patrolState = GetProperty<object>(machine, "CurrentState");
        Assert.That((bool)Invoke(machine, "ChangeState", Enum.Parse(policeState, "Attack")), Is.True);
        Invoke(machine, "RegisterState", Enum.Parse(policeState, "Dead"), patrolState);
        Assert.That((bool)Invoke(machine, "ChangeState", Enum.Parse(policeState, "Dead")), Is.True);

        Physics.SyncTransforms();
        Component perception = (Component)GetProperty<object>(hunter, "Perception");
        Invoke(perception, "ForceRefresh");
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Dead"));
    }

    [Test]
    public void WallPreventsSearchFromEnteringFlee()
    {
        Component hunter = CreateHunterAgent(true);
        CreateWolf(new Vector3(4f, 0f, 0f));
        CreateObstacleBetweenHunterAndWolf();

        Physics.SyncTransforms();
        Component perception = (Component)GetProperty<object>(hunter, "Perception");
        Invoke(perception, "ForceRefresh");
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Null);
        Assert.That(GetProperty<bool>(perception, "HasRecentWolfThreat"), Is.False);
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Search"));
    }

    [Test]
    public void BriefLineOfSightLossKeepsCurrentFleeStateWithoutVisibleWolf()
    {
        Component hunter = CreateHunterAgent(true, 0.5f);
        CreateWolf(new Vector3(4f, 0f, 0f));
        Component perception = (Component)GetProperty<object>(hunter, "Perception");

        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        InvokePrivate(hunter, "Update");
        CreateObstacleBetweenHunterAndWolf();
        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        Invoke(perception, "Tick", 0.1f);
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Null);
        Assert.That(GetProperty<bool>(perception, "HasRecentWolfThreat"), Is.True);
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
    }

    [Test]
    public void SustainedLineOfSightLossDoesNotCancelActiveFlee()
    {
        Component hunter = CreateHunterAgent(true, 0.5f);
        CreateWolf(new Vector3(4f, 0f, 0f));
        Component perception = (Component)GetProperty<object>(hunter, "Perception");

        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        InvokePrivate(hunter, "Update");
        CreateObstacleBetweenHunterAndWolf();
        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        Invoke(perception, "Tick", 0.6f);
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Null);
        Assert.That(GetProperty<bool>(perception, "HasRecentWolfThreat"), Is.False);
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
    }

    [Test]
    public void RecoveredLineOfSightRefreshesFleeGracePeriod()
    {
        Component hunter = CreateHunterAgent(true, 0.5f);
        CreateWolf(new Vector3(4f, 0f, 0f));
        Component perception = (Component)GetProperty<object>(hunter, "Perception");

        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        InvokePrivate(hunter, "Update");
        GameObject obstacle = CreateObstacleBetweenHunterAndWolf();
        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        Invoke(perception, "Tick", 0.3f);

        obstacle.SetActive(false);
        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        Invoke(perception, "Tick", 0.3f);
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Not.Null);
        Assert.That(GetProperty<bool>(perception, "HasRecentWolfThreat"), Is.True);
        Assert.That(GetProperty<float>(perception, "WolfLossGraceRemaining"), Is.EqualTo(0.5f));
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
    }

    [Test]
    public void DisabledLossGraceDoesNotCancelActiveFlee()
    {
        Component hunter = CreateHunterAgent(true, 0f);
        CreateWolf(new Vector3(4f, 0f, 0f));
        Component perception = (Component)GetProperty<object>(hunter, "Perception");

        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        InvokePrivate(hunter, "Update");
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));

        CreateObstacleBetweenHunterAndWolf();
        Physics.SyncTransforms();
        Invoke(perception, "ForceRefresh");
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Null);
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
    }

    [Test]
    public void SearchToFleeCancelsPreviousRouteAndChoosesLowestCostSafeZone()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 0, 4, "SafeZone_A");
        SetSafeZone(grid, 2, 2, "SafeZone_B");
        Assert.That(grid.SetWalkable(2, 1, false), Is.True);
        Assert.That(grid.SetWalkable(1, 2, false), Is.True);
        Assert.That(grid.SetWalkable(3, 2, false), Is.True);
        Component hunter = CreateHunterAgent(true, 0.3f, grid, grid.GridToWorldPosition(2, 0));

        ulong searchRequestId = (ulong)Invoke(
            hunter, "RequestNavigation", grid.GridToWorldPosition(4, 4), 0.25f, 1f);
        ChangeState(hunter, "Flee");
        object request = GetActiveNavigationRequest(hunter);

        Assert.That(GetProperty<ulong>(request, "Id"), Is.Not.EqualTo(searchRequestId));
        Assert.That(GetProperty<Vector3>(request, "Destination"),
            Is.EqualTo(grid.GridToWorldPosition(0, 4)));
    }

    [Test]
    public void FleeKeepsOneRequestAfterLosingLineOfSight()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "SafeZone_A");
        Component hunter = CreateHunterAgent(true, 0f, grid, grid.GridToWorldPosition(2, 0));
        ChangeState(hunter, "Flee");
        ulong requestId = GetProperty<ulong>(GetActiveNavigationRequest(hunter), "Id");

        InvokePrivate(hunter, "Update");
        int calculationCount = GetProperty<int>(
            GetProperty<object>(hunter, "NavigationController"), "PathCalculationCount");
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
        Assert.That(GetProperty<ulong>(GetActiveNavigationRequest(hunter), "Id"), Is.EqualTo(requestId));
        Assert.That(GetProperty<int>(
            GetProperty<object>(hunter, "NavigationController"), "PathCalculationCount"),
            Is.EqualTo(calculationCount));
    }

    [Test]
    public void FleeArrivalStopsInsideSafeZoneAndKeepsProtectedState()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "SafeZone_A");
        Component hunter = CreateHunterAgent(true, 0.3f, grid, grid.GridToWorldPosition(2, 0));
        ChangeState(hunter, "Flee");
        Vector3 destination = GetProperty<Vector3>(GetActiveNavigationRequest(hunter), "Destination");

        InvokePrivate(hunter, "Update");
        hunter.transform.position = destination;
        Physics.SyncTransforms();
        InvokePrivate(hunter, "Update");
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
        Assert.That(GetProperty<bool>(GetProperty<object>(hunter, "NavigationIntent"),
            "HasActiveRequest"), Is.False);
        Assert.That(grid.TryWorldToGrid(hunter.transform.position, out int row, out int column), Is.True);
        Assert.That(grid.TryGetCell(row, column, out NavigationGridCell cell), Is.True);
        Assert.That(cell.Zone, Is.EqualTo(NavigationGridZone.SafeZone));
        Assert.That(GetProperty<Vector3>(GetProperty<object>(hunter, "Movement"), "Velocity"),
            Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void FleeNoPathCancelsOnceWithoutRetrying()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "SafeZone_A");
        Component hunter = CreateHunterAgent(true, 0.3f, grid, grid.GridToWorldPosition(2, 0));
        ChangeState(hunter, "Flee");
        for (int row = 0; row < grid.Rows; row++)
        {
            Assert.That(grid.SetWalkable(row, 2, false), Is.True);
        }

        InvokePrivate(hunter, "Update");
        object controller = GetProperty<object>(hunter, "NavigationController");
        int calculationCount = GetProperty<int>(controller, "PathCalculationCount");
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<bool>(GetProperty<object>(hunter, "NavigationIntent"),
            "HasActiveRequest"), Is.False);
        Assert.That(calculationCount, Is.EqualTo(1));
        Assert.That(GetProperty<int>(controller, "PathCalculationCount"), Is.EqualTo(calculationCount));
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
    }

    [Test]
    public void FleeWithoutGridStopsWithoutCreatingRequests()
    {
        Component hunter = CreateHunterAgent(true);
        ChangeState(hunter, "Flee");

        InvokePrivate(hunter, "Update");
        InvokePrivate(hunter, "Update");

        Assert.That(GetProperty<bool>(GetProperty<object>(hunter, "NavigationIntent"),
            "HasActiveRequest"), Is.False);
        Assert.That(GetProperty<int>(GetProperty<object>(hunter, "NavigationController"),
            "PathCalculationCount"), Is.EqualTo(0));
        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Flee"));
    }

    [Test]
    public void DeadInterruptsFleeAndCancelsOwnedNavigation()
    {
        NavigationGrid grid = CreateGrid();
        SetSafeZone(grid, 2, 4, "SafeZone_A");
        Component hunter = CreateHunterAgent(true, 0.3f, grid, grid.GridToWorldPosition(2, 0));
        object machine = GetPrivateField(hunter, "stateMachine");
        Type policeState = FindProjectType("PoliceState");
        object patrolState = GetProperty<object>(machine, "CurrentState");
        ChangeState(hunter, "Flee");
        Invoke(machine, "RegisterState", Enum.Parse(policeState, "Dead"), patrolState);

        Assert.That((bool)Invoke(machine, "ChangeState", Enum.Parse(policeState, "Dead")), Is.True);

        Assert.That(GetProperty<Enum>(hunter, "CurrentState").ToString(), Is.EqualTo("Dead"));
        Assert.That(GetProperty<bool>(GetProperty<object>(hunter, "NavigationIntent"),
            "HasActiveRequest"), Is.False);
    }

    private Component CreatePerception(float wolfRadius)
    {
        GameObject hunter = CreateObject("HunterFleeDetectionTests.Perception", Vector3.zero);
        Component perception = (Component)hunter.AddComponent(FindProjectType("HunterPerception"));
        SetPrivateField(perception, "wolfDetectionRadius", wolfRadius);
        SetPrivateField(perception, "wolfLayerMask", (LayerMask)~0);
        return perception;
    }

    private Component CreateHunterAgent(
        bool gridNavigation = false,
        float lossGraceDuration = 0.3f,
        NavigationGrid grid = null,
        Vector3? position = null)
    {
        GameObject hunter = CreateObject(
            "HunterFleeDetectionTests.Agent", position ?? Vector3.zero);
        hunter.SetActive(false);
        Component agent = (Component)hunter.AddComponent(FindProjectType("FSMAgent"));
        SetPrivateField(agent, "enableGridNavigation", gridNavigation);
        SetPrivateField(agent, "navigationGrid", grid);
        InvokePrivate(agent, "Awake");
        Component perception = (Component)GetProperty<object>(agent, "Perception");
        SetPrivateField(perception, "wolfDetectionRadius", 5f);
        SetPrivateField(perception, "wolfLossGraceDuration", lossGraceDuration);
        SetPrivateField(perception, "wolfLayerMask", (LayerMask)~0);
        return agent;
    }

    private NavigationGrid CreateGrid()
    {
        GameObject gridObject = CreateObject("HunterFleeDetectionTests.Grid", Vector3.zero);
        NavigationGrid grid = gridObject.AddComponent<NavigationGrid>();
        Assert.That(grid.TryApplyGridSize(5, 5, 4f, true, out _), Is.True);
        return grid;
    }

    private static void SetSafeZone(
        NavigationGrid grid, int row, int column, string zoneId)
    {
        Assert.That(grid.SetWalkable(row, column, true), Is.True);
        Assert.That(grid.SetZone(row, column, NavigationGridZone.SafeZone, zoneId), Is.True);
    }

    private static void ChangeState(Component hunter, string stateName)
    {
        object machine = GetPrivateField(hunter, "stateMachine");
        Type policeState = FindProjectType("PoliceState");
        Assert.That((bool)Invoke(machine, "ChangeState", Enum.Parse(policeState, stateName)), Is.True);
    }

    private static object GetActiveNavigationRequest(Component hunter)
    {
        object intent = GetProperty<object>(hunter, "NavigationIntent");
        Type requestType = FindProjectType("HunterNavigationRequest");
        object[] arguments = { Activator.CreateInstance(requestType) };
        Assert.That((bool)Invoke(intent, "TryGetCurrent", arguments), Is.True);
        return arguments[0];
    }

    private Component CreateWolf(Vector3 position)
    {
        GameObject wolf = CreateObject("HunterFleeDetectionTests.Wolf", position);
        wolf.SetActive(false);
        wolf.AddComponent<SphereCollider>().radius = 0.5f;
        Component fsm = (Component)wolf.AddComponent(FindProjectType("PredatorFSM"));
        wolf.SetActive(true);
        return fsm;
    }

    private Component CreateLivingBoid(Vector3 position)
    {
        GameObject boid = CreateObject("HunterFleeDetectionTests.Boid", position);
        boid.layer = 9;
        boid.AddComponent<SphereCollider>().radius = 0.5f;
        return (Component)boid.AddComponent(FindProjectType("BoidLife"));
    }

    private GameObject CreateObstacleBetweenHunterAndWolf()
    {
        GameObject obstacle = CreateObject(
            "HunterFleeDetectionTests.Wall", new Vector3(2f, 1f, 0f));
        obstacle.layer = 12;
        obstacle.AddComponent<BoxCollider>().size = new Vector3(1f, 3f, 1f);
        return obstacle;
    }

    private GameObject CreateObject(string name, Vector3 position)
    {
        GameObject instance = new GameObject(name);
        instance.transform.position = position;
        objects.Add(instance);
        return instance;
    }

    private static object Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName);
        Assert.That(method, Is.Not.Null, "Missing method " + methodName + ".");
        return method.Invoke(target, arguments);
    }

    private static object InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Missing method " + methodName + ".");
        return method.Invoke(target, null);
    }

    private static T GetProperty<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null, "Missing property " + propertyName + ".");
        return (T)property.GetValue(target);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field " + fieldName + ".");
        return field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field " + fieldName + ".");
        field.SetValue(target, value);
    }

    private static Type FindProjectType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName, false);
            if (type != null)
            {
                return type;
            }
        }

        Assert.Fail("Could not locate project type " + typeName + ".");
        return null;
    }
}
