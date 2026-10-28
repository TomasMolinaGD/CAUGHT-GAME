using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class HunterNavigationControllerTests
{
    private Type controllerType;
    private Type intentType;
    private Type gridType;
    private GameObject gridObject;
    private GameObject hunterObject;
    private GameObject fsmaObject;
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
