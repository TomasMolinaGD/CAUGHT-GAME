using System;
using System.Collections.Generic;
using System.Reflection;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PredatorPerceptionB3IntegrationTests
{
    private const string IntegrationScenePath =
        "Assets/Scenes/Tests/Parcial2_B3_WolfIntegration.unity";
    private const int ObstacleLayer = 12;
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(objects[i]);
    }

    [Test]
    public void ExplicitThreatWithZeroTargetLayersStillUsesRangeAndFieldOfView()
    {
        PerceptionFixture fixture = CreateFixture(new Vector3(0f, 0f, 8f));

        Invoke(fixture.Perception, "ConfigureTargets", new List<GameObject> { fixture.Target });
        Invoke(fixture.Perception, "Tick", 0.1f);

        Assert.That(Invoke<bool>(fixture.Perception, "IsExplicitThreatTarget", fixture.Target), Is.True);
        Assert.That(GetProperty<GameObject>(fixture.Perception, "Target"), Is.EqualTo(fixture.Target));
        Assert.That(GetProperty<bool>(fixture.Perception, "CanSeeTarget"), Is.True);

        fixture.Target.transform.position = new Vector3(0f, 0f, 19f);
        Invoke(fixture.Perception, "Tick", 0.1f);
        Assert.That(GetProperty<bool>(fixture.Perception, "CanSeeTarget"), Is.False);

        fixture.Target.transform.position = new Vector3(0f, 0f, -8f);
        Invoke(fixture.Perception, "Tick", 0.1f);
        Assert.That(GetProperty<bool>(fixture.Perception, "CanSeeTarget"), Is.False);
    }

    [Test]
    public void ObstacleLayerBlocksLineOfSightToExplicitThreat()
    {
        PerceptionFixture fixture = CreateFixture(new Vector3(0f, 0f, 8f));
        GameObject obstacle = CreateObject("PerceptionObstacle", new Vector3(0f, 1.5f, 4f));
        obstacle.layer = ObstacleLayer;
        obstacle.transform.localScale = new Vector3(2f, 3f, 1f);
        obstacle.AddComponent<BoxCollider>();
        Physics.SyncTransforms();

        Invoke(fixture.Perception, "ConfigureTargets", new List<GameObject> { fixture.Target });
        Invoke(fixture.Perception, "Tick", 0.1f);

        Assert.That(GetProperty<bool>(fixture.Perception, "CanSeeTarget"), Is.False);
        Assert.That(GetProperty<GameObject>(fixture.Perception, "Target"), Is.Null);
    }

    [Test]
    public void VisibleExplicitThreatTransitionsPatrolToPursuit()
    {
        FsmFixture fixture = CreateFsmFixture(new Vector3(0f, 0f, 8f));

        Invoke(fixture.Perception, "Tick", 0.1f);
        InvokePrivate(fixture.Fsm, "Update");

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Pursuit"));
        Assert.That(GetProperty<GameObject>(fixture.Fsm, "CurrentTarget"), Is.EqualTo(fixture.Target));
    }

    [Test]
    public void LosingLineOfSightTransitionsPursuitToSearch()
    {
        FsmFixture fixture = CreateFsmFixture(new Vector3(0f, 0f, 8f));
        Invoke(fixture.Perception, "Tick", 0.1f);
        InvokePrivate(fixture.Fsm, "Update");
        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Pursuit"));

        GameObject obstacle = CreateObject("PursuitObstacle", new Vector3(0f, 1.5f, 4f));
        obstacle.layer = ObstacleLayer;
        obstacle.transform.localScale = new Vector3(2f, 3f, 1f);
        obstacle.AddComponent<BoxCollider>();
        Physics.SyncTransforms();

        Invoke(fixture.Perception, "Tick", 0.1f);
        InvokePrivate(fixture.Fsm, "Update");

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Search"));
        Assert.That(GetProperty<bool>(fixture.Fsm, "HasLastKnownPosition"), Is.True);
    }

    [Test]
    public void SafeZoneVolumeHidesTargetInLegacySceneWithoutGrid()
    {
        PerceptionFixture fixture = CreateFixture(new Vector3(0f, 0f, 8f));
        GameObject safeZone = CreateObject("LegacySafeZone", fixture.Target.transform.position);
        safeZone.transform.localScale = Vector3.one * 2f;
        safeZone.AddComponent<BoxCollider>();
        Component volume = safeZone.AddComponent(Type.GetType("SafeZoneVolume, Assembly-CSharp"));
        InvokePrivate(volume, "OnEnable");
        Physics.SyncTransforms();

        Invoke(fixture.Perception, "ConfigureTargets", new List<GameObject> { fixture.Target });
        Invoke(fixture.Perception, "Tick", 0.1f);

        Assert.That(GetProperty<bool>(fixture.Perception, "CanSeeTarget"), Is.False);
        Assert.That(GetProperty<GameObject>(fixture.Perception, "Target"), Is.Null);
    }

    [Test]
    public void TargetOutsideSafeZonesRemainsVisibleInLegacySceneWithoutGrid()
    {
        PerceptionFixture fixture = CreateFixture(new Vector3(0f, 0f, 8f));

        Invoke(fixture.Perception, "ConfigureTargets", new List<GameObject> { fixture.Target });
        Invoke(fixture.Perception, "Tick", 0.1f);

        Assert.That(GetProperty<bool>(fixture.Perception, "CanSeeTarget"), Is.True);
        Assert.That(GetProperty<GameObject>(fixture.Perception, "Target"), Is.EqualTo(fixture.Target));
    }

    [Test]
    public void NavigationGridSafeZoneHidesExplicitThreat()
    {
        NavigationGrid grid = CreateGrid(1, 2, new Vector2Int(0, 1));
        FsmFixture fixture = CreateFsmFixture(grid.GridToWorldPosition(0, 1), grid);

        Invoke(fixture.Perception, "Tick", 0.1f);

        Assert.That(GetProperty<bool>(fixture.Perception, "CanSeeTarget"), Is.False);
        Assert.That(Invoke<bool>(fixture.Fsm, "IsTargetAvailable", fixture.Target), Is.False);
    }

    [Test]
    public void TargetEnteringNavigationGridSafeZoneLeavesPursuit()
    {
        NavigationGrid grid = CreateGrid(1, 2, new Vector2Int(0, 1));
        FsmFixture fixture = CreateFsmFixture(grid.GridToWorldPosition(0, 0), grid);
        fixture.Fsm.transform.position = grid.GridToWorldPosition(0, 0) + Vector3.back * 0.5f;
        fixture.Fsm.transform.forward = Vector3.forward;
        Invoke(fixture.Perception, "Tick", 0.1f);
        InvokePrivate(fixture.Fsm, "Update");
        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Pursuit"));

        fixture.Target.transform.position = grid.GridToWorldPosition(0, 1);
        Invoke(fixture.Perception, "Tick", 0.1f);
        InvokePrivate(fixture.Fsm, "Update");

        Assert.That(GetState(fixture.Fsm), Is.EqualTo("Patrol"));
        Assert.That(GetProperty<GameObject>(fixture.Fsm, "CurrentTarget"), Is.Null);
    }

    [Test]
    public void BuilderConfiguresSerializedHunterTargetBeforeEnablingFsm()
    {
        Type builderType = Type.GetType("Parcial2B3WolfIntegrationBuilder, Assembly-CSharp-Editor");
        Assert.That(builderType, Is.Not.Null);
        MethodInfo configureFsm = FindMethod(builderType, "ConfigureFsm", 3);
        Assert.That(configureFsm, Is.Not.Null,
            "The builder must accept the explicit Hunter target while the Wolf is inactive.");

        GameObject wolf = CreateObject("BuilderWolf", Vector3.zero);
        wolf.SetActive(false);
        Component fsm = wolf.AddComponent(Type.GetType("PredatorFSM, Assembly-CSharp"));
        GameObject hunter = CreateThreat("BuilderHunter", new Vector3(0f, 0f, 4f));

        configureFsm.Invoke(null, new object[] { fsm, Array.Empty<Transform>(), hunter });

        var serializedFsm = new SerializedObject(fsm);
        Assert.That(serializedFsm.FindProperty("targetReference").objectReferenceValue,
            Is.EqualTo(hunter));
        Assert.That(((Behaviour)fsm).enabled, Is.True);
    }

    [Test]
    public void IntegrationSceneEnablesPerceptionForAllWolves()
    {
        Scene scene = EditorSceneManager.OpenScene(IntegrationScenePath, OpenSceneMode.Additive);
        try
        {
            GameObject hunter = FindSceneObject(scene, "Hunter_Parcial2");
            Assert.That(hunter, Is.Not.Null);
            Assert.That(hunter.GetComponent(Type.GetType("BoidThreat, Assembly-CSharp")), Is.Not.Null);

            AssertWolfTarget(scene, "Wolf_Patrol_B3_L1_North", hunter);
            AssertWolfTarget(scene, "Wolf_Patrol_B3_L2_West", hunter);
            AssertWolfTarget(scene, "Wolf_Patrol_B3_L3_South", hunter);
            AssertWolfTarget(scene, "Wolf_Patrol_B3_L4_East", hunter);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void IntegrationSceneKeepsIndependentPerceptionAndNavigationComponents()
    {
        Scene scene = EditorSceneManager.OpenScene(IntegrationScenePath, OpenSceneMode.Additive);
        try
        {
            string[] wolfNames =
            {
                "Wolf_Patrol_B3_L1_North",
                "Wolf_Patrol_B3_L2_West",
                "Wolf_Patrol_B3_L3_South",
                "Wolf_Patrol_B3_L4_East"
            };
            Type fsmType = Type.GetType("PredatorFSM, Assembly-CSharp");
            Type perceptionType = Type.GetType("PredatorPerception, Assembly-CSharp");
            Type controllerType = Type.GetType(
                "PredatorNavigationMovementController, Assembly-CSharp");
            Type agentType = Type.GetType("GraphNavigationAgent, Assembly-CSharp");
            var fsms = new HashSet<Component>();
            var perceptions = new HashSet<Component>();
            var controllers = new HashSet<Component>();
            var agents = new HashSet<Component>();

            foreach (string wolfName in wolfNames)
            {
                GameObject wolf = FindSceneObject(scene, wolfName);
                Assert.That(wolf, Is.Not.Null, $"Missing {wolfName}.");
                Component fsm = wolf.GetComponent(fsmType);
                Component perception = wolf.GetComponent(perceptionType);
                Component controller = wolf.GetComponent(controllerType);
                Component agent = wolf.GetComponent(agentType);
                Assert.That(fsm, Is.Not.Null, $"{wolfName} is missing PredatorFSM.");
                Assert.That(perception, Is.Not.Null, $"{wolfName} is missing PredatorPerception.");
                Assert.That(controller, Is.Not.Null,
                    $"{wolfName} is missing PredatorNavigationMovementController.");
                Assert.That(agent, Is.Not.Null, $"{wolfName} is missing GraphNavigationAgent.");
                Assert.That(fsms.Add(fsm), Is.True);
                Assert.That(perceptions.Add(perception), Is.True);
                Assert.That(controllers.Add(controller), Is.True);
                Assert.That(agents.Add(agent), Is.True);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void HunterPerceptionIdentifiesEachWolfThroughItsThreatCollider()
    {
        Scene scene = EditorSceneManager.OpenScene(IntegrationScenePath, OpenSceneMode.Additive);
        string[] wolfNames =
        {
            "Wolf_Patrol_B3_L1_North",
            "Wolf_Patrol_B3_L2_West",
            "Wolf_Patrol_B3_L3_South",
            "Wolf_Patrol_B3_L4_East"
        };
        try
        {
            GameObject hunter = FindSceneObject(scene, "Hunter_Parcial2");
            Component perception = hunter.GetComponent(Type.GetType("HunterPerception, Assembly-CSharp"));
            Assert.That(perception, Is.Not.Null);

            var wolves = new List<GameObject>();
            foreach (string wolfName in wolfNames)
            {
                GameObject wolf = FindSceneObject(scene, wolfName);
                Assert.That(wolf, Is.Not.Null, $"Missing {wolfName}.");
                wolves.Add(wolf);
            }

            foreach (GameObject wolf in wolves)
                wolf.SetActive(false);

            foreach (GameObject wolf in wolves)
            {
                wolf.SetActive(true);
                hunter.transform.position = FindVisibleHunterPosition(wolf.transform.position);
                Physics.SyncTransforms();
                Invoke(perception, "ForceRefresh");

                Assert.That(GetProperty<Component>(perception, "ClosestWolf"),
                    Is.EqualTo(wolf.GetComponent(Type.GetType("PredatorFSM, Assembly-CSharp"))),
                    $"HunterPerception did not identify {wolf.name} through its trigger collider.");

                wolf.SetActive(false);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private PerceptionFixture CreateFixture(Vector3 targetPosition)
    {
        GameObject root = CreateObject("PerceptionWolf", Vector3.zero);
        Component perception = root.AddComponent(Type.GetType("PredatorPerception, Assembly-CSharp"));
        Invoke(perception, "Configure", 18f, 120f, (LayerMask)0,
            (LayerMask)(1 << ObstacleLayer), 0.02f, 1.5f);
        GameObject target = CreateThreat("HunterTarget", targetPosition);
        return new PerceptionFixture(perception, target);
    }

    private FsmFixture CreateFsmFixture(Vector3 targetPosition, NavigationGrid grid = null)
    {
        GameObject root = CreateObject("PerceptionFsmWolf", Vector3.zero);
        Component controller = null;
        if (grid != null)
        {
            controller = root.AddComponent(Type.GetType(
                "PredatorNavigationMovementController, Assembly-CSharp"));
        }
        Component fsm = root.AddComponent(Type.GetType("PredatorFSM, Assembly-CSharp"));
        Component perception = root.GetComponent(Type.GetType("PredatorPerception, Assembly-CSharp"));
        Invoke(perception, "Configure", 18f, 120f, (LayerMask)0,
            (LayerMask)(1 << ObstacleLayer), 0.02f, 1.5f);
        GameObject target = CreateThreat("FsmHunterTarget", targetPosition);
        if (grid != null)
        {
            Component provider = CreateProvider(grid);
            Invoke(controller, "ConfigureGraphProvider", provider);
        }
        InvokePrivate(fsm, "CacheDependencies");
        InvokePrivate(fsm, "InitializeStateMachine");
        fsm.GetType().GetMethod("ConfigureTargets").Invoke(fsm,
            new object[] { new List<GameObject> { target } });
        return new FsmFixture(fsm, perception, target);
    }

    private NavigationGrid CreateGrid(int rows, int columns, Vector2Int safeZone)
    {
        GameObject root = CreateObject("PerceptionSafeZoneGrid", Vector3.zero);
        NavigationGrid grid = root.AddComponent<NavigationGrid>();
        SetPrivateField(grid, "rows", rows);
        SetPrivateField(grid, "columns", columns);
        SetPrivateField(grid, "cellSize", 1f);
        var cells = new List<NavigationGridCell>(rows * columns);
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            Vector2Int coordinate = new Vector2Int(row, column);
            cells.Add(new NavigationGridCell(row, column, true,
                coordinate == safeZone ? NavigationGridZone.SafeZone : NavigationGridZone.Normal));
        }
        SetPrivateField(grid, "cells", cells);
        return grid;
    }

    private Component CreateProvider(NavigationGrid grid)
    {
        GameObject root = CreateObject("PerceptionSafeZoneProvider", Vector3.zero);
        Component provider = root.AddComponent(Type.GetType(
            "CaughtGame.Navigation.NavigationGridGraphProvider, Assembly-CSharp"));
        Invoke(provider, "Configure", grid, (LayerMask)0);
        return provider;
    }

    private GameObject CreateThreat(string name, Vector3 position)
    {
        GameObject target = CreateObject(name, position);
        target.AddComponent(Type.GetType("BoidThreat, Assembly-CSharp"));
        return target;
    }

    private GameObject CreateObject(string name, Vector3 position)
    {
        var created = new GameObject(name);
        created.transform.position = position;
        objects.Add(created);
        return created;
    }

    private static void AssertWolfTarget(Scene scene, string wolfName, GameObject expectedTarget)
    {
        GameObject wolf = FindSceneObject(scene, wolfName);
        Assert.That(wolf, Is.Not.Null, $"Missing {wolfName}.");
        Component fsm = wolf.GetComponent(Type.GetType("PredatorFSM, Assembly-CSharp"));
        Component perception = wolf.GetComponent(Type.GetType("PredatorPerception, Assembly-CSharp"));
        Assert.That(fsm, Is.Not.Null);
        Assert.That(perception, Is.Not.Null);

        var serializedFsm = new SerializedObject(fsm);
        var serializedPerception = new SerializedObject(perception);
        Assert.That(serializedFsm.FindProperty("targetReference").objectReferenceValue,
            Is.EqualTo(expectedTarget));
        Assert.That(serializedPerception.FindProperty("targetLayers").intValue, Is.Zero);
        Assert.That(serializedPerception.FindProperty("viewDistance").floatValue, Is.EqualTo(18f));
        Assert.That(serializedPerception.FindProperty("fieldOfView").floatValue, Is.EqualTo(120f));
        Assert.That(serializedPerception.FindProperty("obstacleLayers").intValue,
            Is.EqualTo(1 << ObstacleLayer));

        CapsuleCollider[] threatColliders = wolf.GetComponents<CapsuleCollider>();
        Assert.That(threatColliders, Has.Length.EqualTo(1));
        Assert.That(threatColliders[0].isTrigger, Is.True);
        Assert.That(threatColliders[0].enabled, Is.True);
        Assert.That(threatColliders[0].radius, Is.EqualTo(0.35f).Within(0.0001f));
        Assert.That(threatColliders[0].height, Is.EqualTo(1.6f).Within(0.0001f));
        Assert.That(threatColliders[0].center, Is.EqualTo(new Vector3(0f, 0.8f, 0f)));
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
                if (transforms[i].name == name) return transforms[i].gameObject;
        }
        return null;
    }

    private static Vector3 FindVisibleHunterPosition(Vector3 wolfPosition)
    {
        Vector3[] offsets =
        {
            Vector3.right * 2f,
            Vector3.left * 2f,
            Vector3.forward * 2f,
            Vector3.back * 2f
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 candidate = wolfPosition + offsets[i];
            if (!Physics.Linecast(candidate + Vector3.up, wolfPosition + Vector3.up,
                    1 << ObstacleLayer, QueryTriggerInteraction.Ignore))
            {
                return candidate;
            }
        }

        return wolfPosition + Vector3.right * 2f;
    }

    private static MethodInfo FindMethod(Type type, string name, int parameterCount)
    {
        MethodInfo[] methods = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic);
        for (int i = 0; i < methods.Length; i++)
            if (methods[i].Name == name && methods[i].GetParameters().Length == parameterCount)
                return methods[i];
        return null;
    }

    private static void InvokePrivate(Component component, string method)
    {
        component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(component, null);
    }

    private static void Invoke(Component component, string method, params object[] arguments)
    {
        component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public)
            .Invoke(component, arguments);
    }

    private static T Invoke<T>(Component component, string method, params object[] arguments)
    {
        return (T)component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public)
            .Invoke(component, arguments);
    }

    private static T GetProperty<T>(Component component, string property)
    {
        return (T)component.GetType().GetProperty(property).GetValue(component);
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private static string GetState(Component fsm)
    {
        return GetProperty<object>(fsm, "CurrentState").ToString();
    }

    private readonly struct PerceptionFixture
    {
        public PerceptionFixture(Component perception, GameObject target)
        {
            Perception = perception;
            Target = target;
        }

        public Component Perception { get; }
        public GameObject Target { get; }
    }

    private readonly struct FsmFixture
    {
        public FsmFixture(Component fsm, Component perception, GameObject target)
        {
            Fsm = fsm;
            Perception = perception;
            Target = target;
        }

        public Component Fsm { get; }
        public Component Perception { get; }
        public GameObject Target { get; }
    }
}
