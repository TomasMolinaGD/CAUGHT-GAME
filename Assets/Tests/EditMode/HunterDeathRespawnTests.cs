using System;
using System.Collections.Generic;
using System.Reflection;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEngine;

public sealed class HunterDeathRespawnTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(objects[i]);
    }

    [Test]
    public void CaptureEntersDeadWithoutDisablingHunterAndClearsNavigation()
    {
        Component hunter = CreateHunter(CreateGridWithSpawn(), 5f);
        Component movement = (Component)GetProperty<object>(hunter, "Movement");
        Invoke(movement, "BeginMovementFrame");
        SetPrivateField(movement, "<Velocity>k__BackingField", Vector3.right * 3f);
        Assert.That(GetProperty<Vector3>(movement, "Velocity"), Is.Not.EqualTo(Vector3.zero));
        CreateWolf(new Vector3(2f, 0f, 0f));
        Component perception = (Component)GetProperty<object>(hunter, "Perception");
        Invoke(perception, "ForceRefresh");
        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Not.Null);
        Invoke(hunter, "RequestNavigation", new Vector3(4f, 0f, 4f), 0.5f, 1f, null);
        Assert.That(GetProperty<bool>(GetProperty<object>(hunter, "NavigationIntent"),
            "HasActiveRequest"), Is.True);

        Assert.That(TryRegistryCapture(hunter.gameObject), Is.True);

        Assert.That(hunter.gameObject.activeInHierarchy, Is.True);
        Assert.That(GetState(hunter), Is.EqualTo("Dead"));
        Assert.That(GetProperty<bool>(hunter, "IsCaptureAvailable"), Is.False);
        Assert.That(GetProperty<bool>(GetProperty<object>(hunter, "NavigationIntent"),
            "HasActiveRequest"), Is.False);
        Assert.That(GetProperty<object>(GetProperty<object>(hunter, "NavigationController"),
            "Status").ToString(), Is.EqualTo("Disabled"));
        Assert.That(GetProperty<Vector3>(movement, "Velocity"), Is.EqualTo(Vector3.zero));
        Assert.That(GetProperty<object>(perception, "ClosestWolf"), Is.Null);
        Assert.That(GetProperty<float>(hunter, "RespawnTime"), Is.EqualTo(5f));
    }

    [Test]
    public void DeadWaitsForConfiguredRespawnTime()
    {
        Component hunter = CreateHunter(CreateGridWithSpawn(), 5f);
        Vector3 deathPosition = hunter.transform.position;
        Assert.That(TryRegistryCapture(hunter.gameObject), Is.True);
        object deadState = GetCurrentStateObject(hunter);

        InvokePrivate(deadState, "UpdateRespawn", 4f);

        Assert.That(GetState(hunter), Is.EqualTo("Dead"));
        Assert.That(hunter.transform.position, Is.EqualTo(deathPosition));

        InvokePrivate(deadState, "UpdateRespawn", 1f);

        Assert.That(GetState(hunter), Is.EqualTo("Search"));
    }

    [Test]
    public void GridRespawnPreservesGatherProgressWhileDeadAndResetsAfterRespawn()
    {
        NavigationGrid grid = CreateGridWithSpawn();
        Component hunter = CreateHunter(grid, 1f);
        InvokePrivate(hunter, "RegisterGatherCompletion");
        InvokePrivate(hunter, "RegisterGatherCompletion");
        Vector3 expectedSpawn = grid.GridToWorldPosition(2, 2);
        expectedSpawn.y = GetProperty<float>(GetProperty<object>(hunter, "Movement"), "GroundHeight");

        Assert.That(TryRegistryCapture(hunter.gameObject), Is.True);
        Assert.That(GetState(hunter), Is.EqualTo("Dead"));
        Assert.That(GetProperty<int>(hunter, "CompletedGathers"), Is.EqualTo(2));

        InvokePrivate(GetCurrentStateObject(hunter), "UpdateRespawn", 1f);

        Assert.That(GetState(hunter), Is.EqualTo("Search"));
        Assert.That(hunter.transform.position, Is.EqualTo(expectedSpawn));
        Assert.That(GetProperty<int>(hunter, "CompletedGathers"), Is.EqualTo(0));
        Assert.That(GetProperty<bool>(hunter, "IsCaptureAvailable"), Is.True);
    }

    [Test]
    public void LegacyRespawnReturnsToPatrolWithoutGrid()
    {
        Component hunter = CreateHunter(null, 1f);
        Vector3 originalPosition = hunter.transform.position;

        Assert.That(TryRegistryCapture(hunter.gameObject), Is.True);
        InvokePrivate(GetCurrentStateObject(hunter), "UpdateRespawn", 1f);

        Assert.That(GetState(hunter), Is.EqualTo("Patrol"));
        Assert.That(hunter.transform.position, Is.EqualTo(originalPosition));
        Assert.That(GetProperty<bool>(hunter, "IsCaptureAvailable"), Is.True);
    }

    [Test]
    public void HunterCanBeCapturedAgainAfterRespawn()
    {
        Component hunter = CreateHunter(CreateGridWithSpawn(), 1f);

        Assert.That(TryRegistryCapture(hunter.gameObject), Is.True);
        InvokePrivate(GetCurrentStateObject(hunter), "UpdateRespawn", 1f);
        Assert.That(GetState(hunter), Is.EqualTo("Search"));

        Assert.That(TryRegistryCapture(hunter.gameObject), Is.True);
        Assert.That(GetState(hunter), Is.EqualTo("Dead"));
    }

    [Test]
    public void SafeZoneVolumeStillPreventsHunterCapture()
    {
        Component hunter = CreateHunter(null, 1f);
        Component unprotectedHunter = CreateHunter(null, 1f);
        unprotectedHunter.transform.position = new Vector3(4f, 0f, 0f);
        GameObject safeZone = CreateObject("HunterDeathRespawnTests.SafeZone", hunter.transform.position);
        safeZone.transform.localScale = Vector3.one * 2f;
        safeZone.AddComponent<BoxCollider>();
        Component safeZoneVolume = safeZone.AddComponent(FindProjectType("SafeZoneVolume"));
        InvokePrivate(safeZoneVolume, "OnEnable");
        Physics.SyncTransforms();

        GameObject predator = CreateObject("HunterDeathRespawnTests.Predator", Vector3.zero);
        Component combat = predator.AddComponent(FindProjectType("PredatorCombat"));
        GameObject controlPredator = CreateObject(
            "HunterDeathRespawnTests.ControlPredator", Vector3.zero);
        Component controlCombat = controlPredator.AddComponent(FindProjectType("PredatorCombat"));

        bool controlCaptured = (bool)Invoke(
            controlCombat, "TryCapture", unprotectedHunter.gameObject, 0.9f, 0.5f, false);
        bool captured = (bool)Invoke(combat, "TryCapture", hunter.gameObject, 0.9f, 0.5f, false);

        Assert.That(controlCaptured, Is.True,
            "The same public combat path must capture an equivalent Hunter outside the Safe Zone.");
        Assert.That(captured, Is.False);
        Assert.That(GetState(hunter), Is.EqualTo("Patrol"));
        Assert.That(hunter.gameObject.activeInHierarchy, Is.True);
    }

    private Component CreateHunter(NavigationGrid grid, float respawnTime)
    {
        GameObject root = CreateObject("HunterDeathRespawnTests.Hunter", Vector3.zero);
        root.SetActive(false);
        Component hunter = root.AddComponent(FindProjectType("FSMAgent"));
        Component movement = root.GetComponent(FindProjectType("HunterMovement"));
        if (grid != null)
        {
            SetPrivateField(movement, "boundsPolicy", Enum.Parse(
                movement.GetType().GetNestedType("BoundsPolicy"), "NavigationGrid"));
        }
        SetPrivateField(hunter, "navigationGrid", grid);
        SetPrivateField(hunter, "enableGridNavigation", grid != null);
        SetPrivateField(hunter, "respawnTime", respawnTime);
        InvokePrivate(hunter, "Awake");
        root.SetActive(true);
        return hunter;
    }

    private NavigationGrid CreateGridWithSpawn()
    {
        GameObject root = CreateObject("HunterDeathRespawnTests.Grid", Vector3.zero);
        NavigationGrid grid = root.AddComponent<NavigationGrid>();
        Assert.That(grid.TryApplyGridSize(3, 3, 2f, true, out _), Is.True);
        Assert.That(grid.SetWalkable(2, 2, true), Is.True);
        Assert.That(grid.SetZone(2, 2, NavigationGridZone.Spawn, "HunterRespawn"), Is.True);
        return grid;
    }

    private void CreateWolf(Vector3 position)
    {
        GameObject wolf = CreateObject("HunterDeathRespawnTests.Wolf", position);
        wolf.SetActive(false);
        wolf.AddComponent<SphereCollider>().radius = 0.5f;
        wolf.AddComponent(FindProjectType("PredatorFSM"));
        wolf.SetActive(true);
    }

    private static bool TryRegistryCapture(GameObject target)
    {
        return (bool)FindProjectType("PredatorCaptureRegistry")
            .GetMethod("TryCapture", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { target, false });
    }

    private static object GetCurrentStateObject(Component hunter) =>
        GetProperty<object>(GetPrivateField<object>(hunter, "stateMachine"), "CurrentState");

    private static string GetState(Component hunter) =>
        GetProperty<Enum>(hunter, "CurrentState").ToString();

    private GameObject CreateObject(string name, Vector3 position)
    {
        GameObject instance = new GameObject(name);
        instance.transform.position = position;
        objects.Add(instance);
        return instance;
    }

    private static object Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.That(method, Is.Not.Null, "Missing method " + methodName + ".");
        return method.Invoke(target, arguments);
    }

    private static object InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Missing method " + methodName + ".");
        return method.Invoke(target, arguments);
    }

    private static T GetProperty<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null, "Missing property " + propertyName + ".");
        return (T)property.GetValue(target);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field " + fieldName + ".");
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field " + fieldName + ".");
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
