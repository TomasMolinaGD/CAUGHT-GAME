using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CaughtGame.Navigation;

public sealed class HunterNavigationControllerTests
{
    private Type controllerType;
    private Type intentType;
    private Type gridType;
    private GameObject gridObject;
    private GameObject hunterObject;
    private GameObject fsmaObject;
    private GameObject regionObject;
    private GameObject targetObject;
    private readonly List<GameObject> targetObjects = new List<GameObject>();
    private object intent;
    private object controller;

    [SetUp]
    public void SetUp()
    {
        controllerType = FindProjectType("HunterNavigationController");
        intentType = FindProjectType("HunterNavigationIntent");
        gridType = FindProjectType("CaughtGame.Navigation.NavigationGrid");
        intent = Activator.CreateInstance(intentType);

        gridObject = new GameObject("HunterNavigationControllerTests.Grid");
        Component grid = (Component)gridObject.AddComponent(gridType);
        InvokePrivate(grid, "Reset");

        hunterObject = new GameObject("HunterNavigationControllerTests.Hunter");
        Type navigationGridType = gridType;
        Type hunterIntentType = intentType;
        ConstructorInfo constructor = controllerType.GetConstructor(new[]
        {
            typeof(Transform), hunterIntentType, navigationGridType
        });
        controller = constructor.Invoke(new[] { hunterObject.transform, intent, grid });
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(hunterObject);
        UnityEngine.Object.DestroyImmediate(gridObject);
        UnityEngine.Object.DestroyImmediate(fsmaObject);
        UnityEngine.Object.DestroyImmediate(regionObject);
        foreach (GameObject target in targetObjects)
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
        targetObjects.Clear();
    }

    [Test]
    public void ChangedRequestCalculatesOneHunterRouteAndExposesWaypoint()
    {
        Invoke(intent, "RequestNavigation", new Vector3(8f, 0f, 0f), 0.1f, 1f);
        Invoke(controller, "SetEnabled", true);
        Invoke(controller, "Tick");

        Assert.That(GetProperty<Enum>(controller, "Status").ToString(), Is.EqualTo("InProgress"));
        Assert.That(GetProperty<int>(controller, "PathCalculationCount"), Is.EqualTo(1));
        Assert.That((bool)Invoke(controller, "TryGetNextWaypoint", new object[] { null }), Is.True);
    }

    [Test]
    public void RepeatedTicksDoNotRecalculateUnchangedRequest()
    {
        Invoke(intent, "RequestNavigation", new Vector3(8f, 0f, 0f), 0.1f, 1f);
        Invoke(controller, "SetEnabled", true);
        Invoke(controller, "SetEnabled", true);
        Invoke(controller, "Tick");
        Invoke(controller, "Tick");

        Assert.That(GetProperty<int>(controller, "PathCalculationCount"), Is.EqualTo(1));
    }

    [Test]
    public void ReplacementDropsOldRouteAndCalculatesNewRequest()
    {
        ulong firstId = (ulong)Invoke(intent, "RequestNavigation", new Vector3(8f, 0f, 0f), 0.1f, 1f);
        Invoke(controller, "SetEnabled", true);
        Invoke(controller, "Tick");
        object[] replacement = { firstId, new Vector3(0f, 0f, 8f), 0.1f, 1f, null };
        Assert.That((bool)Invoke(intent, "ReplaceNavigation", replacement), Is.True);
        Assert.That(GetProperty<object>(controller, "CurrentPath"), Is.Null);
        Invoke(controller, "Tick");

        Assert.That(GetProperty<int>(controller, "PathCalculationCount"), Is.EqualTo(2));
        Assert.That(GetProperty<object>(controller, "CurrentPath"), Is.Not.Null);
    }

    [Test]
    public void CancellationClearsCachedRoute()
    {
        ulong id = (ulong)Invoke(intent, "RequestNavigation", new Vector3(8f, 0f, 0f), 0.1f, 1f);
        Invoke(controller, "SetEnabled", true);
        Invoke(controller, "Tick");
        Assert.That((bool)Invoke(intent, "CancelNavigation", id), Is.True);
        Assert.That(GetProperty<object>(controller, "CurrentPath"), Is.Null);
        Invoke(controller, "Tick");

        Assert.That(GetProperty<Enum>(controller, "Status").ToString(), Is.EqualTo("WaitingForRequest"));
        Assert.That(GetProperty<object>(controller, "CurrentPath"), Is.Null);
    }

    [Test]
    public void BlockedDestinationIsReportedWithoutDirectVisibilityShortcut()
    {
        ulong id = (ulong)Invoke(intent, "RequestNavigation", new Vector3(8f, 0f, 0f), 0.1f, 1f);
        Component grid = (Component)gridObject.GetComponent(gridType);
        Invoke(grid, "SetWalkable", 0, 2, false);
        Invoke(controller, "SetEnabled", true);
        Invoke(controller, "Tick");

        Assert.That(GetProperty<Enum>(controller, "Status").ToString(), Is.EqualTo("DestinationNotTraversable"));
        Assert.That(GetProperty<object>(controller, "CurrentPath"), Is.Null);
        Assert.That(id, Is.GreaterThan(0UL));
    }

    [Test]
    public void DisconnectedDestinationReportsNoPath()
    {
        for (int column = 0; column < 35; column++)
        {
            Invoke(gridObject.GetComponent(gridType), "SetWalkable", 1, column, false);
        }

        Invoke(intent, "RequestNavigation", new Vector3(0f, 0f, 8f), 0.1f, 1f);
        Invoke(controller, "SetEnabled", true);
        Invoke(controller, "Tick");

        Assert.That(GetProperty<Enum>(controller, "Status").ToString(), Is.EqualTo("NoPath"));
        Assert.That(GetProperty<object>(controller, "CurrentPath"), Is.Null);
    }

    [Test]
    public void DestinationOutsideGridIsReportedAsInvalid()
    {
        Invoke(intent, "RequestNavigation", new Vector3(-1f, 0f, -1f), 0.1f, 1f);
        Invoke(controller, "SetEnabled", true);
        Invoke(controller, "Tick");

        Assert.That(GetProperty<Enum>(controller, "Status").ToString(), Is.EqualTo("InvalidDestination"));
        Assert.That(GetProperty<object>(controller, "CurrentPath"), Is.Null);
    }

    [Test]
    public void DestinationAtGridCellCenterDoesNotCreateDuplicateTerminalWaypoint()
    {
        Vector3 destination = new Vector3(10f, 0f, 2f);
        ulong requestId = (ulong)Invoke(intent, "RequestNavigation", destination, 0.1f, 1f);
        Invoke(controller, "SetEnabled", true);
        Invoke(controller, "Tick");

        Assert.That(GetProperty<bool>(controller, "HasTerminalDestination"), Is.False);
        object[] arguments = { requestId, GetProperty<ulong>(controller, "CurrentRouteVersion"), null };
        Assert.That((bool)Invoke(controller, "TryGetTerminalDestination", arguments), Is.False);
    }

    [Test]
    public void DescenteredDestinationIsExposedAsTerminalSegment()
    {
        Vector3 destination = new Vector3(9f, 0f, 2f);
        ulong requestId = (ulong)Invoke(intent, "RequestNavigation", destination, 0.1f, 1f);
        Invoke(controller, "SetEnabled", true);
        Invoke(controller, "Tick");

        ulong version = GetProperty<ulong>(controller, "CurrentRouteVersion");
        object[] arguments = { requestId, version, null };
        Assert.That(GetProperty<bool>(controller, "HasTerminalDestination"), Is.True);
        Assert.That((bool)Invoke(controller, "TryGetTerminalDestination", arguments), Is.True);
        Assert.That((Vector3)arguments[2], Is.EqualTo(destination));
    }

    [Test]
    public void ReplacedRequestInvalidatesThePreviousTerminalSegment()
    {
        Vector3 firstDestination = new Vector3(9f, 0f, 2f);
        ulong firstId = (ulong)Invoke(intent, "RequestNavigation", firstDestination, 0.1f, 1f);
        Invoke(controller, "SetEnabled", true);
        Invoke(controller, "Tick");
        ulong oldVersion = GetProperty<ulong>(controller, "CurrentRouteVersion");

        object[] replacement = { firstId, new Vector3(10f, 0f, 2f), 0.1f, 1f, null };
        Assert.That((bool)Invoke(intent, "ReplaceNavigation", replacement), Is.True);
        object[] oldTerminal = { firstId, oldVersion, null };
        Assert.That((bool)Invoke(controller, "TryGetTerminalDestination", oldTerminal), Is.False);

        Invoke(controller, "Tick");
        Assert.That(GetProperty<bool>(controller, "HasTerminalDestination"), Is.False);
    }

    [Test]
    public void WaypointsAdvanceInOrderAndReachTheDestination()
    {
        ulong requestId = (ulong)Invoke(intent, "RequestNavigation", new Vector3(8f, 0f, 0f), 0.1f, 1f);
        Invoke(controller, "SetEnabled", true);
        Invoke(controller, "Tick");
        ulong version = GetProperty<ulong>(controller, "CurrentRouteVersion");

        Assert.That((bool)Invoke(controller, "TryAdvanceWaypoint", requestId, version, 1), Is.True);
        Assert.That((bool)Invoke(controller, "TryAdvanceWaypoint", requestId, version, 1), Is.False);
        Assert.That((bool)Invoke(controller, "TryAdvanceWaypoint", requestId, version, 2), Is.True);
        Assert.That(GetProperty<Enum>(controller, "Status").ToString(), Is.EqualTo("InProgress"));
        Assert.That((bool)Invoke(controller, "TryAdvanceWaypoint", requestId, version, 3), Is.False);
        Assert.That((bool)Invoke(controller, "TryAdvanceWaypoint", requestId, version, -1), Is.False);
        Assert.That(GetProperty<int>(controller, "CurrentWaypointIndex"), Is.EqualTo(3));

        hunterObject.transform.position = new Vector3(8f, 3f, 0f);
        Invoke(controller, "Tick");
        Assert.That(GetProperty<Enum>(controller, "Status").ToString(), Is.EqualTo("DestinationReached"));
    }

    [Test]
    public void RecalculationVersionRejectsCallbacksForThePreviousRoute()
    {
        ulong requestId = (ulong)Invoke(intent, "RequestNavigation", new Vector3(8f, 0f, 0f), 0.1f, 1f);
        Invoke(controller, "SetEnabled", true);
        Invoke(controller, "Tick");
        ulong oldVersion = GetProperty<ulong>(controller, "CurrentRouteVersion");

        Invoke(controller, "RequestRepath");
        Invoke(controller, "Tick");
        ulong newVersion = GetProperty<ulong>(controller, "CurrentRouteVersion");

        Assert.That(newVersion, Is.Not.EqualTo(oldVersion));
        Assert.That((bool)Invoke(controller, "TryAdvanceWaypoint", requestId, oldVersion, 1), Is.False);
        Assert.That((bool)Invoke(controller, "TryAdvanceWaypoint", requestId, newVersion, 1), Is.True);
    }

    [Test]
    public void NavigationControllerNeverWritesHunterPosition()
    {
        hunterObject.transform.position = new Vector3(2f, 3f, 2f);
        Vector3 beforeTick = hunterObject.transform.position;
        Invoke(intent, "RequestNavigation", new Vector3(8f, 0f, 0f), 0.1f, 1f);
        Invoke(controller, "SetEnabled", true);
        Invoke(controller, "Tick");

        Assert.That(hunterObject.transform.position, Is.EqualTo(beforeTick));
    }

    [Test]
    public void FSMAgentCreatesControllerOnlyWhenExplicitlyConfigured()
    {
        fsmaObject = new GameObject("HunterNavigationControllerTests.FSMAgent");
        fsmaObject.SetActive(false);
        Component fsm = (Component)fsmaObject.AddComponent(FindProjectType("FSMAgent"));
        Component grid = (Component)gridObject.GetComponent(gridType);

        Assert.That(GetProperty<object>(fsm, "NavigationController"), Is.Null);
        Invoke(fsm, "ConfigureNavigation", grid, true);
        object configuredController = GetProperty<object>(fsm, "NavigationController");
        Assert.That(configuredController, Is.Not.Null);
        Assert.That(GetProperty<bool>(fsm, "GridNavigationEnabled"), Is.True);

        Invoke(fsm, "RequestNavigation", new Vector3(8f, 0f, 0f), 0.1f, 1f);
        Invoke(configuredController, "Tick");
        Assert.That(GetProperty<Enum>(configuredController, "Status").ToString(), Is.EqualTo("InProgress"));
    }

    [Test]
    public void GridNavigationStartsInSearchAndCreatesOneRequest()
    {
        regionObject = new GameObject("HunterNavigationControllerTests.HuntingGround");
        Component region = (Component)regionObject.AddComponent(FindProjectType("CaughtGame.Navigation.HuntingGroundRegion"));
        Component grid = (Component)gridObject.GetComponent(gridType);
        SetPrivateField(region, "navigationGrid", grid);
        Invoke(grid, "SetZone", 0, 1,
            Enum.Parse(FindProjectType("CaughtGame.Navigation.NavigationGridZone"), "HuntingGround"), null);

        fsmaObject = new GameObject("HunterNavigationControllerTests.Search");
        fsmaObject.SetActive(false);
        fsmaObject.transform.position = Vector3.zero;
        Component fsm = (Component)fsmaObject.AddComponent(FindProjectType("FSMAgent"));
        Component movement = (Component)fsmaObject.GetComponent(FindProjectType("HunterMovement"));
        SetPrivateField(movement, "boundsPolicy", Enum.Parse(
            movement.GetType().GetNestedType("BoundsPolicy"), "NavigationGrid"));
        SetPrivateField(fsm, "navigationGrid", grid);
        SetPrivateField(fsm, "huntingGroundRegion", region);
        SetPrivateField(fsm, "enableGridNavigation", true);

        InvokePrivate(fsm, "Awake");

        Assert.That(GetProperty<Enum>(fsm, "CurrentState").ToString(), Is.EqualTo("Search"));
        Assert.That(GetProperty<bool>(GetProperty<object>(fsm, "NavigationIntent"), "HasActiveRequest"), Is.True);

        InvokePrivate(fsm, "Update");
        int calculations = GetProperty<int>(GetProperty<object>(fsm, "NavigationController"), "PathCalculationCount");
        InvokePrivate(fsm, "Update");

        Assert.That(calculations, Is.EqualTo(1));
        Assert.That(GetProperty<int>(GetProperty<object>(fsm, "NavigationController"), "PathCalculationCount"),
            Is.EqualTo(1));
    }

    [Test]
    public void LeavingSearchCancelsOnlyItsActiveRequest()
    {
        regionObject = new GameObject("HunterNavigationControllerTests.SearchExitRegion");
        Component region = (Component)regionObject.AddComponent(FindProjectType("CaughtGame.Navigation.HuntingGroundRegion"));
        Component grid = (Component)gridObject.GetComponent(gridType);
        SetPrivateField(region, "navigationGrid", grid);
        Invoke(grid, "SetZone", 0, 1,
            Enum.Parse(FindProjectType("CaughtGame.Navigation.NavigationGridZone"), "HuntingGround"), null);

        fsmaObject = new GameObject("HunterNavigationControllerTests.SearchExit");
        fsmaObject.SetActive(false);
        Component fsm = (Component)fsmaObject.AddComponent(FindProjectType("FSMAgent"));
        Component movement = (Component)fsmaObject.GetComponent(FindProjectType("HunterMovement"));
        SetPrivateField(movement, "boundsPolicy", Enum.Parse(
            movement.GetType().GetNestedType("BoundsPolicy"), "NavigationGrid"));
        SetPrivateField(fsm, "navigationGrid", grid);
        SetPrivateField(fsm, "huntingGroundRegion", region);
        SetPrivateField(fsm, "enableGridNavigation", true);
        InvokePrivate(fsm, "Awake");

        object stateMachine = GetPrivateField(fsm, "stateMachine");
        Assert.That((bool)Invoke(stateMachine, "ChangeState", Enum.Parse(
            FindProjectType("PoliceState"), "Patrol")), Is.True);
        Assert.That(GetProperty<bool>(GetProperty<object>(fsm, "NavigationIntent"), "HasActiveRequest"), Is.False);
    }

    [Test]
    public void FSMAgentDisableEnableRestartsPatrolWithoutReinitializingStateMachine()
    {
        fsmaObject = new GameObject("HunterNavigationControllerTests.FSMAgentLifecycle");
        fsmaObject.SetActive(false);
        Component fsm = (Component)fsmaObject.AddComponent(FindProjectType("FSMAgent"));
        InvokePrivate(fsm, "Awake");

        object stateMachine = GetPrivateField(fsm, "stateMachine");
        Assert.That(GetProperty<object>(stateMachine, "CurrentState"), Is.Not.Null);
        Assert.That(GetProperty<Enum>(stateMachine, "CurrentStateKey").ToString(), Is.EqualTo("Patrol"));

        InvokePrivate(fsm, "OnDisable");
        Assert.That(GetProperty<object>(stateMachine, "CurrentState"), Is.Null);

        InvokePrivate(fsm, "OnEnable");
        Assert.That(GetPrivateField(fsm, "stateMachine"), Is.SameAs(stateMachine));
        Assert.That(GetProperty<object>(stateMachine, "CurrentState"), Is.Not.Null);
        Assert.That(GetProperty<Enum>(stateMachine, "CurrentStateKey").ToString(), Is.EqualTo("Patrol"));
    }

    [Test]
    public void HunterMovementAcceptsOnlyOnePhysicalCommandPerFrame()
    {
        GameObject movementObject = new GameObject("HunterNavigationControllerTests.Movement");
        try
        {
            Component movement = movementObject.AddComponent(FindProjectType("HunterMovement"));
            Invoke(movement, "BeginMovementFrame");

            Invoke(movement, "MoveTowardsNavigation", new Vector3(8f, 0f, 0f), 0.1f, 1f);
            bool legacyCommandAccepted = (bool)Invoke(
                movement, "MoveTowards", new Vector3(0f, 0f, 8f), 0.1f, 1f);

            Assert.That(legacyCommandAccepted, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(movementObject);
        }
    }

    [Test]
    public void LegacyRectangleKeepsClampingInitialPosition()
    {
        GameObject movementObject = new GameObject("HunterNavigationControllerTests.LegacyBounds");
        try
        {
            movementObject.SetActive(false);
            Component movement = movementObject.AddComponent(FindProjectType("HunterMovement"));
            movementObject.transform.position = new Vector3(82f, 0.05f, 14f);

            InvokePrivate(movement, "Awake");

            Assert.That(movementObject.transform.position, Is.EqualTo(new Vector3(435.5f, 0.05f, 590.5f)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(movementObject);
        }
    }

    [Test]
    public void NavigationGridKeepsInitialPositionOutsideLegacyRectangle()
    {
        GameObject movementObject = new GameObject("HunterNavigationControllerTests.GridBounds");
        try
        {
            movementObject.SetActive(false);
            Component movement = movementObject.AddComponent(FindProjectType("HunterMovement"));
            SetPrivateField(movement, "boundsPolicy", Enum.Parse(
                movement.GetType().GetNestedType("BoundsPolicy"), "NavigationGrid"));
            movementObject.transform.position = new Vector3(82f, 0.05f, 14f);

            InvokePrivate(movement, "Awake");

            Assert.That(movementObject.transform.position, Is.EqualTo(new Vector3(82f, 0.05f, 14f)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(movementObject);
        }
    }

    [Test]
    public void NavigationGridMovementDoesNotApplyLegacyRectangleClamp()
    {
        GameObject movementObject = new GameObject("HunterNavigationControllerTests.GridMovementBounds");
        try
        {
            movementObject.SetActive(false);
            Component movement = movementObject.AddComponent(FindProjectType("HunterMovement"));
            SetPrivateField(movement, "boundsPolicy", Enum.Parse(
                movement.GetType().GetNestedType("BoundsPolicy"), "NavigationGrid"));
            movementObject.transform.position = new Vector3(82f, 0.05f, 14f);
            InvokePrivate(movement, "Awake");

            Invoke(movement, "MoveTowards", new Vector3(84f, 0f, 14f), 0f, 1f);

            Assert.That(movementObject.transform.position.x, Is.LessThan(435.5f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(movementObject);
        }
    }

    [Test]
    public void SpawnSelectorChoosesFirstWalkableSpawnDeterministically()
    {
        NavigationGrid grid = (NavigationGrid)gridObject.GetComponent(gridType);
        grid.SetWalkable(0, 0, false);
        grid.SetZone(0, 1, NavigationGridZone.Spawn);
        grid.SetZone(1, 1, NavigationGridZone.Spawn);

        Assert.That(HunterSpawnSelector.TrySelectSpawn(grid, 0.05f, out Vector3 position), Is.True);
        Vector3 expected = grid.GridToWorldPosition(0, 1);
        Assert.That(position.x, Is.EqualTo(expected.x));
        Assert.That(position.z, Is.EqualTo(expected.z));
        Assert.That(position.y, Is.EqualTo(0.05f));
    }

    [Test]
    public void SpawnSelectorRejectsBlockedOrMissingSpawnWithoutMutation()
    {
        NavigationGrid grid = (NavigationGrid)gridObject.GetComponent(gridType);
        grid.SetWalkable(0, 0, false);
        grid.SetZone(0, 0, NavigationGridZone.Spawn);

        Assert.That(HunterSpawnSelector.TrySelectSpawn(grid, 0.05f, out _), Is.False);
        grid.SetWalkable(0, 0, true);
        grid.SetZone(0, 0, NavigationGridZone.Normal);
        Assert.That(HunterSpawnSelector.TrySelectSpawn(grid, 0.05f, out _), Is.False);
    }

    [Test]
    public void SpawnSelectorRejectsNonFiniteHeight()
    {
        NavigationGrid grid = (NavigationGrid)gridObject.GetComponent(gridType);
        grid.SetZone(0, 0, NavigationGridZone.Spawn);

        Assert.That(HunterSpawnSelector.TrySelectSpawn(grid, float.NaN, out _), Is.False);
    }

    [Test]
    public void StopAndPlaceClearsMotionAndRejectsStaleCommand()
    {
        GameObject movementObject = new GameObject("HunterNavigationControllerTests.StopAndPlace");
        try
        {
            Component movement = movementObject.AddComponent(FindProjectType("HunterMovement"));
            Rigidbody body = movementObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            SetPrivateField(movement, "boundsPolicy", Enum.Parse(
                movement.GetType().GetNestedType("BoundsPolicy"), "NavigationGrid"));
            Invoke(movement, "BeginMovementFrame");
            Invoke(movement, "MoveTowardsNavigation", new Vector3(8f, 0f, 0f), 0.1f, 1f);

            object result = Invoke(movement, "TryStopAndPlace", new Vector3(3f, 9f, 4f));
            Assert.That(result.ToString(), Is.EqualTo("Placed"));
            Assert.That(movementObject.transform.position, Is.EqualTo(new Vector3(3f, 0.05f, 4f)));
            Assert.That(body.position, Is.EqualTo(movementObject.transform.position));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That((bool)Invoke(movement, "MoveTowardsNavigation", new Vector3(9f, 0f, 4f), 0.1f, 1f), Is.False);
            Assert.That(GetProperty<Vector3>(movement, "Velocity"), Is.EqualTo(Vector3.zero));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(movementObject);
        }
    }

    [Test]
    public void StopAndPlacePreservesLegacyRectangleClamping()
    {
        GameObject movementObject = new GameObject("HunterNavigationControllerTests.LegacyPlacement");
        try
        {
            Component movement = movementObject.AddComponent(FindProjectType("HunterMovement"));
            object result = Invoke(movement, "TryStopAndPlace", new Vector3(82f, 0.05f, 14f));
            Assert.That(result.ToString(), Is.EqualTo("Placed"));
            Assert.That(movementObject.transform.position, Is.EqualTo(new Vector3(435.5f, 0.05f, 590.5f)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(movementObject);
        }
    }

    [Test]
    public void StopAndPlaceRejectsNonFinitePositionWithoutMutation()
    {
        GameObject movementObject = new GameObject("HunterNavigationControllerTests.InvalidPlacement");
        try
        {
            Component movement = movementObject.AddComponent(FindProjectType("HunterMovement"));
            movementObject.transform.position = new Vector3(2f, 0.05f, 3f);
            object result = Invoke(movement, "TryStopAndPlace", new Vector3(float.NaN, 0f, 0f));
            Assert.That(result.ToString(), Is.EqualTo("InvalidPosition"));
            Assert.That(movementObject.transform.position, Is.EqualTo(new Vector3(2f, 0.05f, 3f)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(movementObject);
        }
    }

    [Test]
    public void SearchTransitionsToGatherBeforeAttackWhenBothTargetsAreDetected()
    {
        Component fsm = CreateSearchAgent();
        Component dead = CreateDetectedBoid(false);
        CreateDetectedBoid(true);
        Invoke(fsm.GetComponent(FindProjectType("HunterPerception")), "ForceRefresh");

        InvokePrivate(fsm, "Update");

        Assert.That(GetProperty<Enum>(fsm, "CurrentState").ToString(), Is.EqualTo("Gather"));
        Assert.That(GetProperty<object>(fsm, "CurrentTarget"), Is.SameAs(dead));
        Assert.That(GetProperty<bool>(GetProperty<object>(fsm, "NavigationIntent"), "HasActiveRequest"), Is.False);
    }

    [Test]
    public void SearchTransitionsToAttackWhenLivingTargetIsDetectedAndReady()
    {
        Component fsm = CreateSearchAgent();
        Component target = CreateDetectedBoid(true);
        Invoke(fsm.GetComponent(FindProjectType("HunterPerception")), "ForceRefresh");

        InvokePrivate(fsm, "Update");

        Assert.That(GetProperty<Enum>(fsm, "CurrentState").ToString(), Is.EqualTo("Attack"));
        Assert.That(GetProperty<object>(fsm, "CurrentTarget"), Is.SameAs(target));
    }

    [Test]
    public void SearchStaysActiveWhenCachedTargetIsInvalidated()
    {
        Component fsm = CreateSearchAgent();
        Component target = CreateDetectedBoid(false);
        Invoke(fsm.GetComponent(FindProjectType("HunterPerception")), "ForceRefresh");
        Invoke(target, "Collect");

        InvokePrivate(fsm, "Update");

        Assert.That(GetProperty<Enum>(fsm, "CurrentState").ToString(), Is.EqualTo("Search"));
        Assert.That(GetProperty<object>(fsm, "CurrentTarget"), Is.Null);
    }

    [Test]
    public void SearchContinuesWhenLivingTargetIsDetectedButAttackIsCoolingDown()
    {
        Component fsm = CreateSearchAgent();
        CreateDetectedBoid(true);
        Component combat = fsm.GetComponent(FindProjectType("HunterCombat"));
        SetPrivateField(combat, "cooldownRemaining", 1f);
        Invoke(fsm.GetComponent(FindProjectType("HunterPerception")), "ForceRefresh");

        InvokePrivate(fsm, "Update");

        Assert.That(GetProperty<Enum>(fsm, "CurrentState").ToString(), Is.EqualTo("Search"));
        Assert.That(GetProperty<bool>(GetProperty<object>(fsm, "NavigationIntent"), "HasActiveRequest"), Is.True);
    }

    [Test]
    public void GridNavigationEngagementsReturnToSearch()
    {
        Component fsm = CreateSearchAgent();

        Assert.That(GetProperty<Enum>(fsm, "EngagementReturnState").ToString(), Is.EqualTo("Search"));
    }

    [Test]
    public void LegacyEngagementsReturnToPatrol()
    {
        fsmaObject = new GameObject("HunterNavigationControllerTests.LegacyReturn");
        fsmaObject.SetActive(false);
        Component fsm = (Component)fsmaObject.AddComponent(FindProjectType("FSMAgent"));
        SetPrivateField(fsm, "enableGridNavigation", false);
        InvokePrivate(fsm, "Awake");

        Assert.That(GetProperty<Enum>(fsm, "EngagementReturnState").ToString(), Is.EqualTo("Patrol"));
    }

    [Test]
    public void GatherProgressStartsAtZeroWithConfigurableRequirement()
    {
        Component fsm = CreateSearchAgent();

        Assert.That(GetProperty<int>(fsm, "CompletedGathers"), Is.EqualTo(0));
        Assert.That(GetProperty<int>(fsm, "RequiredGathers"), Is.EqualTo(3));
        Assert.That(GetProperty<bool>(fsm, "HasReachedRequiredGathers"), Is.False);
    }

    [Test]
    public void RequiredGathersCanBeConfigured()
    {
        fsmaObject = new GameObject("HunterNavigationControllerTests.RequiredGathers");
        fsmaObject.SetActive(false);
        Component fsm = (Component)fsmaObject.AddComponent(FindProjectType("FSMAgent"));
        SetPrivateField(fsm, "requiredGathers", 5);
        InvokePrivate(fsm, "Awake");

        Assert.That(GetProperty<int>(fsm, "RequiredGathers"), Is.EqualTo(5));
    }

    [Test]
    public void SuccessfulGatherRegistersOnceAndCapsAtRequirement()
    {
        Component fsm = CreateSearchAgent();

        InvokePrivate(fsm, "RegisterGatherCompletion");
        InvokePrivate(fsm, "RegisterGatherCompletion");
        InvokePrivate(fsm, "RegisterGatherCompletion");
        InvokePrivate(fsm, "RegisterGatherCompletion");

        Assert.That(GetProperty<int>(fsm, "CompletedGathers"), Is.EqualTo(3));
        Assert.That(GetProperty<bool>(fsm, "HasReachedRequiredGathers"), Is.True);
    }

    [Test]
    public void GatherProgressCanBeResetExplicitly()
    {
        Component fsm = CreateSearchAgent();
        InvokePrivate(fsm, "RegisterGatherCompletion");
        Invoke(fsm, "ResetGatherProgress");

        Assert.That(GetProperty<int>(fsm, "CompletedGathers"), Is.EqualTo(0));
        Assert.That(GetProperty<bool>(fsm, "HasReachedRequiredGathers"), Is.False);
    }

    [Test]
    public void ExternalBoidCollectionDoesNotRegisterHunterProgress()
    {
        Component fsm = CreateSearchAgent();
        Component target = CreateDetectedBoid(false);
        Invoke(target, "Collect");

        Assert.That(GetProperty<int>(fsm, "CompletedGathers"), Is.EqualTo(0));
    }

    [Test]
    public void ExitStateSelectsWalkableExitAndDoesNotRecalculateEveryFrame()
    {
        NavigationGrid grid = (NavigationGrid)gridObject.GetComponent(gridType);
        grid.SetZone(0, 2, NavigationGridZone.Exit);
        Component fsm = CreateSearchAgent();
        object stateMachine = GetPrivateField(fsm, "stateMachine");
        Type policeState = FindProjectType("PoliceState");
        Invoke(stateMachine, "ChangeState", Enum.Parse(policeState, "Exit"));

        InvokePrivate(fsm, "Update");
        int calculations = GetProperty<int>(GetProperty<object>(fsm, "NavigationController"), "PathCalculationCount");
        InvokePrivate(fsm, "Update");

        Assert.That(GetProperty<Enum>(fsm, "CurrentState").ToString(), Is.EqualTo("Exit"));
        Assert.That(GetProperty<bool>(GetProperty<object>(fsm, "NavigationIntent"), "HasActiveRequest"), Is.True);
        Assert.That(GetProperty<int>(GetProperty<object>(fsm, "NavigationController"), "PathCalculationCount"),
            Is.EqualTo(calculations));
    }

    [Test]
    public void ReachedExitRestartsAtSpawnAndRequestsFreshSearchRoute()
    {
        NavigationGrid grid = (NavigationGrid)gridObject.GetComponent(gridType);
        grid.SetZone(0, 0, NavigationGridZone.Spawn);
        grid.SetZone(0, 2, NavigationGridZone.Exit);
        Component fsm = CreateSearchAgent();
        Component movement = fsm.GetComponent(FindProjectType("HunterMovement"));
        fsm.transform.position = grid.GridToWorldPosition(0, 2);
        InvokePrivate(fsm, "RegisterGatherCompletion");
        InvokePrivate(fsm, "RegisterGatherCompletion");
        InvokePrivate(fsm, "RegisterGatherCompletion");
        Component target = CreateDetectedBoid(false);
        Invoke(fsm, "SetCurrentTarget", target);
        Invoke(fsm.GetComponent(FindProjectType("HunterPerception")), "ForceRefresh");

        object stateMachine = GetPrivateField(fsm, "stateMachine");
        Type policeState = FindProjectType("PoliceState");
        Invoke(stateMachine, "ChangeState", Enum.Parse(policeState, "Exit"));
        ulong previousRequestId = GetActiveRequestId(GetProperty<object>(fsm, "NavigationIntent"));

        InvokePrivate(fsm, "Update");

        Assert.That(GetProperty<Enum>(fsm, "CurrentState").ToString(), Is.EqualTo("Search"));
        Assert.That(GetProperty<int>(fsm, "CompletedGathers"), Is.EqualTo(0));
        Assert.That(GetProperty<object>(fsm, "CurrentTarget"), Is.Null);
        Assert.That(GetProperty<object>(fsm.GetComponent(FindProjectType("HunterPerception")), "ClosestDeadBoid"), Is.Null);
        Assert.That(fsm.transform.position, Is.EqualTo(new Vector3(2f, 0.05f, 2f)));
        Assert.That(GetProperty<Vector3>(movement, "Velocity"), Is.EqualTo(Vector3.zero));
        Assert.That(GetProperty<bool>(GetProperty<object>(fsm, "NavigationIntent"), "HasActiveRequest"), Is.True);
        Assert.That(GetActiveRequestId(GetProperty<object>(fsm, "NavigationIntent")), Is.GreaterThan(previousRequestId));
    }

    [Test]
    public void MissingSpawnDefersExitRestartWithoutPartialMutation()
    {
        NavigationGrid grid = (NavigationGrid)gridObject.GetComponent(gridType);
        grid.SetZone(0, 2, NavigationGridZone.Exit);
        Component fsm = CreateSearchAgent();
        fsm.transform.position = grid.GridToWorldPosition(0, 2);
        InvokePrivate(fsm, "RegisterGatherCompletion");
        InvokePrivate(fsm, "RegisterGatherCompletion");
        InvokePrivate(fsm, "RegisterGatherCompletion");
        object stateMachine = GetPrivateField(fsm, "stateMachine");
        Type policeState = FindProjectType("PoliceState");
        Invoke(stateMachine, "ChangeState", Enum.Parse(policeState, "Exit"));
        Vector3 previousPosition = fsm.transform.position;
        ulong previousRequestId = GetActiveRequestId(GetProperty<object>(fsm, "NavigationIntent"));

        LogAssert.Expect(LogType.Warning, "Hunter Exit restart deferred: No valid Hunter Spawn is available.");
        InvokePrivate(fsm, "Update");

        Assert.That(GetProperty<Enum>(fsm, "CurrentState").ToString(), Is.EqualTo("Exit"));
        Assert.That(GetProperty<int>(fsm, "CompletedGathers"), Is.EqualTo(3));
        Assert.That(fsm.transform.position, Is.EqualTo(previousPosition));
        Assert.That(GetActiveRequestId(GetProperty<object>(fsm, "NavigationIntent")), Is.EqualTo(previousRequestId));
    }

    [Test]
    public void SearchDestinationSelectorWithSameSeedProducesSameSequence()
    {
        Component gridComponent = gridObject.GetComponent(gridType);
        NavigationGrid grid = (NavigationGrid)gridComponent;
        regionObject = new GameObject("HunterNavigationControllerTests.SelectorRegion");
        HuntingGroundRegion region = regionObject.AddComponent<HuntingGroundRegion>();
        SetPrivateField(region, "navigationGrid", grid);
        for (int row = 0; row < grid.Rows; row++)
        {
            for (int column = 0; column < grid.Columns; column++)
            {
                grid.SetZone(row, column, NavigationGridZone.HuntingGround);
            }
        }

        SearchDestinationSelector first = new SearchDestinationSelector(42);
        SearchDestinationSelector second = new SearchDestinationSelector(42);
        Vector3 origin = grid.GridToWorldPosition(0, 0);
        int firstIndex = -1;
        int secondIndex = -1;
        for (int count = 0; count < 5; count++)
        {
            Assert.That(first.TrySelect(grid, region, origin, firstIndex, 0f, 100f, 24,
                null, out _, out int selectedFirst), Is.True);
            Assert.That(second.TrySelect(grid, region, origin, secondIndex, 0f, 100f, 24,
                null, out _, out int selectedSecond), Is.True);
            Assert.That(selectedFirst, Is.EqualTo(selectedSecond));
            firstIndex = selectedFirst;
            secondIndex = selectedSecond;
        }
    }

    [Test]
    public void SearchDestinationSelectorDoesNotChangeUnityRandomState()
    {
        Component gridComponent = gridObject.GetComponent(gridType);
        NavigationGrid grid = (NavigationGrid)gridComponent;
        regionObject = new GameObject("HunterNavigationControllerTests.RandomRegion");
        HuntingGroundRegion region = regionObject.AddComponent<HuntingGroundRegion>();
        SetPrivateField(region, "navigationGrid", grid);
        grid.SetZone(0, 1, NavigationGridZone.HuntingGround);

        UnityEngine.Random.InitState(9876);
        float expected = UnityEngine.Random.value;
        UnityEngine.Random.InitState(9876);
        SearchDestinationSelector selector = new SearchDestinationSelector(7);
        selector.TrySelect(grid, region, grid.GridToWorldPosition(0, 0), -1, 0f, 100f, 8,
            null, out _, out _);

        Assert.That(UnityEngine.Random.value, Is.EqualTo(expected));
    }

    [Test]
    public void SearchDestinationSelectorUsesOnlyValidCellsAndBoundedAttempts()
    {
        Component gridComponent = gridObject.GetComponent(gridType);
        NavigationGrid grid = (NavigationGrid)gridComponent;
        regionObject = new GameObject("HunterNavigationControllerTests.ValidRegion");
        HuntingGroundRegion region = regionObject.AddComponent<HuntingGroundRegion>();
        SetPrivateField(region, "navigationGrid", grid);
        for (int row = 0; row < grid.Rows; row++)
        {
            for (int column = 0; column < grid.Columns; column++)
            {
                grid.SetZone(row, column, NavigationGridZone.HuntingGround);
            }
        }

        SearchDestinationSelector selector = new SearchDestinationSelector(11);
        Assert.That(selector.TrySelect(grid, region, grid.GridToWorldPosition(0, 0), -1,
            0f, 100f, 64, null, out Vector3 destination, out int index), Is.True);
        Assert.That(index, Is.GreaterThanOrEqualTo(0));
        Assert.That(region.ContainsWorldPosition(destination), Is.True);
    }

    private Component CreateSearchAgent()
    {
        regionObject = new GameObject("HunterNavigationControllerTests.SearchRegion");
        HuntingGroundRegion region = regionObject.AddComponent<HuntingGroundRegion>();
        NavigationGrid grid = (NavigationGrid)gridObject.GetComponent(gridType);
        SetPrivateField(region, "navigationGrid", grid);
        grid.SetZone(0, 1, NavigationGridZone.HuntingGround);

        fsmaObject = new GameObject("HunterNavigationControllerTests.SearchAgent");
        fsmaObject.SetActive(false);
        Component fsm = (Component)fsmaObject.AddComponent(FindProjectType("FSMAgent"));
        Component movement = (Component)fsmaObject.GetComponent(FindProjectType("HunterMovement"));
        SetPrivateField(movement, "boundsPolicy", Enum.Parse(
            movement.GetType().GetNestedType("BoundsPolicy"), "NavigationGrid"));
        SetPrivateField(fsm, "navigationGrid", grid);
        SetPrivateField(fsm, "huntingGroundRegion", region);
        SetPrivateField(fsm, "enableGridNavigation", true);
        InvokePrivate(fsm, "Awake");
        return fsm;
    }

    private Component CreateDetectedBoid(bool alive)
    {
        targetObject = new GameObject("HunterNavigationControllerTests.Target");
        targetObjects.Add(targetObject);
        targetObject.layer = 9;
        targetObject.transform.position = new Vector3(2f, 0f, 2f);
        targetObject.AddComponent<SphereCollider>().radius = 0.5f;
        Component life = (Component)targetObject.AddComponent(FindProjectType("BoidLife"));
        if (!alive)
        {
            Invoke(life, "Kill");
        }

        return life;
    }

    private static object Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName);
        Assert.That(method, Is.Not.Null, "Missing method " + methodName + ".");
        return method.Invoke(target, arguments);
    }

    private static object InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Missing private method " + methodName + ".");
        return method.Invoke(target, null);
    }

    private static T GetProperty<T>(object target, string propertyName) =>
        (T)target.GetType().GetProperty(propertyName).GetValue(target);

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing private field " + fieldName + ".");
        return field.GetValue(target);
    }

    private static ulong GetActiveRequestId(object intent)
    {
        object activeRequest = GetPrivateField(intent, "activeRequest");
        Assert.That(activeRequest, Is.Not.Null);
        return (ulong)activeRequest.GetType().GetProperty("Id").GetValue(activeRequest);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing private field " + fieldName + ".");
        field.SetValue(target, value);
    }

    private static Type FindProjectType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName, false);
            if (type != null) return type;
        }

        Assert.Fail("Could not locate project type " + typeName + ".");
        return null;
    }
}
