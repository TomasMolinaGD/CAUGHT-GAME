using System;
using System.Collections.Generic;
using CaughtGame.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Parcial2B3WolfIntegrationBuilder
{
    private const string TargetScenePath = "Assets/Scenes/Tests/Parcial2_B3_WolfIntegration.unity";
    private const string PredatorPrefabPath = "Assets/Actors/Predator.prefab";
    private const string WolfControllerPath =
        "Assets/Animations/Predator/Predator_WolfIntegration.controller";
    private const string ProviderName = "NavigationGridGraphProvider_WolfIntegration";
    private const string WaypointRootName = "WolfPatrolWaypoints_B3";
    private const string WolfNamePrefix = "Wolf_Patrol_B3_";
    private const float PatrolSpeed = 2.85f;
    private const float NodeReachDistance = 0.1f;
    private const float StoppingDistance = 0.25f;
    private const float ViewDistance = 18f;
    private const float FieldOfView = 120f;
    private const string HunterName = "Hunter_Parcial2";

    private sealed class PatrolDefinition
    {
        public PatrolDefinition(string id, string regionName, Color color, params Vector3[] waypointPositions)
        {
            Id = id;
            RegionName = regionName;
            Color = color;
            WaypointPositions = waypointPositions;
        }

        public string Id { get; }
        public string RegionName { get; }
        public Color Color { get; }
        public Vector3[] WaypointPositions { get; }
    }

    private sealed class PatrolSetup
    {
        public PatrolSetup(PatrolDefinition definition, Transform[] waypoints)
        {
            Definition = definition;
            Waypoints = waypoints;
        }

        public PatrolDefinition Definition { get; }
        public Transform[] Waypoints { get; }
    }

    [MenuItem("CAUGHT-GAME/Integrate Four Wolf Patrols Into B3 Copy")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before integrating the Wolf patrol scene.");

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath) == null)
            throw new InvalidOperationException($"The preserved B3 copy is missing: {TargetScenePath}");

        GameObject predatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PredatorPrefabPath);
        if (predatorPrefab == null)
            throw new InvalidOperationException($"Missing Predator prefab: {PredatorPrefabPath}");

        RuntimeAnimatorController wolfController =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(WolfControllerPath);
        if (wolfController == null)
            throw new InvalidOperationException($"Missing Wolf Animator Controller: {WolfControllerPath}");

        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        NavigationGrid grid = RequireSingleSceneComponent<NavigationGrid>(scene, "NavigationGrid");
        GameObject hunterTarget = FindSceneGameObject(scene, HunterName);
        if (hunterTarget == null || hunterTarget.GetComponent<BoidThreat>() == null)
            throw new InvalidOperationException(
                $"The copied scene requires {HunterName} with BoidThreat before configuring L1.");
        if (!ContainsGameObjectNamed(scene, "BoidRespawnArea_B3"))
            throw new InvalidOperationException("The copied scene no longer contains BoidRespawnArea_B3.");

        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer < 0)
            throw new InvalidOperationException("The existing Obstacle layer is required.");
        LayerMask obstacleMask = 1 << obstacleLayer;

        RemoveGeneratedWolfPatrols(scene);
        NavigationGridGraphProvider provider = GetOrCreateProvider(scene, grid, obstacleMask);
        PatrolSetup[] patrols = CreatePatrols(scene);
        Physics.SyncTransforms();
        ValidatePatrolRoutes(grid, provider, patrols);

        for (int i = 0; i < patrols.Length; i++)
        {
            GameObject targetReference = patrols[i].Definition.Id == "L1" ? hunterTarget : null;
            CreateWolf(scene, predatorPrefab, wolfController, provider, patrols[i], obstacleMask,
                targetReference, i == 0);
        }

        ValidateSceneConfiguration(scene, grid, provider, wolfController, patrols, hunterTarget,
            obstacleMask);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, TargetScenePath))
            throw new InvalidOperationException("Unity could not save the Wolf integration scene.");

        AssetDatabase.SaveAssets();
        Debug.Log($"Integrated four regional Wolf patrols into preserved B3 copy: {TargetScenePath}");
    }

    private static PatrolDefinition[] CreatePatrolDefinitions()
    {
        return new[]
        {
            new PatrolDefinition("L1", "North", Color.green,
                new Vector3(138.5f, 0f, 34.6f), new Vector3(14f, 0f, 33.9f)),
            new PatrolDefinition("L2", "West", Color.cyan,
                new Vector3(10.3f, 0f, 102.3f), new Vector3(25.7f, 0f, 102.1f),
                new Vector3(26.3f, 0f, 142.7f), new Vector3(10.59f, 0f, 142.1f)),
            new PatrolDefinition("L3", "South", Color.yellow,
                new Vector3(138.5f, 0f, 205.4f), new Vector3(14f, 0f, 206.1f)),
            new PatrolDefinition("L4", "East", Color.magenta,
                new Vector3(149.7f, 0f, 102.3f), new Vector3(134.3f, 0f, 102.1f),
                new Vector3(133.7f, 0f, 142.7f), new Vector3(149.41f, 0f, 142.1f))
        };
    }

    private static void RemoveGeneratedWolfPatrols(Scene scene)
    {
        List<PredatorFSM> wolves = FindSceneComponents<PredatorFSM>(scene);
        for (int i = 0; i < wolves.Count; i++)
        {
            string wolfName = wolves[i].gameObject.name;
            if (wolfName != "Wolf_Patrol_B3" &&
                !wolfName.StartsWith(WolfNamePrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Refusing to replace unrelated PredatorFSM object: {wolfName}");
            }
        }

        for (int i = wolves.Count - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(wolves[i].gameObject);

        GameObject waypointRoot = FindSceneGameObject(scene, WaypointRootName);
        if (waypointRoot != null)
            UnityEngine.Object.DestroyImmediate(waypointRoot);
    }

    private static NavigationGridGraphProvider GetOrCreateProvider(
        Scene scene, NavigationGrid grid, LayerMask obstacleMask)
    {
        List<NavigationGridGraphProvider> providers =
            FindSceneComponents<NavigationGridGraphProvider>(scene);
        if (providers.Count > 1)
            throw new InvalidOperationException(
                $"Expected at most one NavigationGridGraphProvider, found {providers.Count}.");

        NavigationGridGraphProvider provider;
        if (providers.Count == 1)
        {
            provider = providers[0];
            provider.gameObject.name = ProviderName;
        }
        else
        {
            GameObject root = new GameObject(ProviderName);
            SceneManager.MoveGameObjectToScene(root, scene);
            provider = root.AddComponent<NavigationGridGraphProvider>();
        }

        provider.Configure(grid, obstacleMask);
        EditorUtility.SetDirty(provider);
        return provider;
    }

    private static PatrolSetup[] CreatePatrols(Scene scene)
    {
        PatrolDefinition[] definitions = CreatePatrolDefinitions();
        var setups = new PatrolSetup[definitions.Length];
        GameObject root = new GameObject(WaypointRootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        for (int i = 0; i < definitions.Length; i++)
        {
            PatrolDefinition definition = definitions[i];
            GameObject patrolRoot = new GameObject($"{definition.Id}_{definition.RegionName}_Waypoints");
            patrolRoot.transform.SetParent(root.transform, false);
            var waypoints = new Transform[definition.WaypointPositions.Length];

            for (int waypointIndex = 0; waypointIndex < definition.WaypointPositions.Length; waypointIndex++)
            {
                waypoints[waypointIndex] = CreateWaypoint(
                    $"Wolf_{definition.Id}_Waypoint_{waypointIndex + 1:00}",
                    definition.WaypointPositions[waypointIndex],
                    definition.Color,
                    patrolRoot.transform);
            }

            setups[i] = new PatrolSetup(definition, waypoints);
        }

        return setups;
    }

    private static Transform CreateWaypoint(string name, Vector3 position, Color color, Transform parent)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = name;
        marker.transform.SetParent(parent, true);
        marker.transform.position = position;
        marker.transform.localScale = Vector3.one * 1.2f;
        UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());

        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        marker.GetComponent<Renderer>().sharedMaterial = material;
        return marker.transform;
    }

    private static void ValidatePatrolRoutes(
        NavigationGrid grid, NavigationGridGraphProvider provider, PatrolSetup[] patrols)
    {
        var routeOwners = new Dictionary<Vector2Int, string>();

        for (int patrolIndex = 0; patrolIndex < patrols.Length; patrolIndex++)
        {
            PatrolSetup patrol = patrols[patrolIndex];
            if (patrol.Waypoints.Length < 2)
                throw new InvalidOperationException($"{patrol.Definition.Id} requires at least two waypoints.");

            var waypointCells = new HashSet<Vector2Int>();
            for (int i = 0; i < patrol.Waypoints.Length; i++)
            {
                Transform waypoint = patrol.Waypoints[i];
                if (!grid.TryWorldToGrid(waypoint.position, out int row, out int column) ||
                    !grid.TryGetCell(row, column, out NavigationGridCell cell) ||
                    !cell.Walkable || cell.Zone == NavigationGridZone.SafeZone)
                {
                    throw new InvalidOperationException(
                        $"{waypoint.name} must match a walkable non-SafeZone grid cell.");
                }

                Vector2Int waypointCell = new Vector2Int(column, row);
                if (!waypointCells.Add(waypointCell))
                    throw new InvalidOperationException(
                        $"{patrol.Definition.Id} contains duplicate waypoint cell {waypointCell}.");
            }

            var patrolRouteCells = new HashSet<Vector2Int>();
            int directSegments = 0;
            int pathSegments = 0;
            for (int i = 0; i < patrol.Waypoints.Length; i++)
            {
                Transform start = patrol.Waypoints[i];
                Transform end = patrol.Waypoints[(i + 1) % patrol.Waypoints.Length];
                if (start.position == end.position)
                    throw new InvalidOperationException(
                        $"{patrol.Definition.Id} contains consecutive duplicate waypoints.");

                if (provider.HasDirectLineOfSight(start.position, end.position, 0.4f, 1.8f))
                    directSegments++;
                else
                    pathSegments++;

                var path = new List<Vector3>();
                if (!provider.TryFindPath(start.position, end.position, true, path) || path.Count == 0)
                    throw new InvalidOperationException(
                        $"No Wolf A* path exists for {patrol.Definition.Id}: {start.name} -> {end.name}.");

                for (int pathIndex = 0; pathIndex < path.Count; pathIndex++)
                {
                    if (!grid.TryWorldToGrid(path[pathIndex], out int row, out int column) ||
                        !grid.TryGetCell(row, column, out NavigationGridCell cell) ||
                        !cell.Walkable || cell.Zone == NavigationGridZone.SafeZone)
                    {
                        throw new InvalidOperationException(
                            $"{patrol.Definition.Id} route enters an invalid or SafeZone cell.");
                    }

                    patrolRouteCells.Add(new Vector2Int(column, row));
                }
            }

            foreach (Vector2Int cell in patrolRouteCells)
            {
                if (routeOwners.TryGetValue(cell, out string owner))
                    throw new InvalidOperationException(
                        $"Patrol routes {owner} and {patrol.Definition.Id} overlap at grid cell {cell}.");
                routeOwners.Add(cell, patrol.Definition.Id);
            }

            Debug.Log(
                $"Validated {patrol.Definition.Id} {patrol.Definition.RegionName}: " +
                $"{patrol.Waypoints.Length} waypoints, {directSegments} direct segments, " +
                $"{pathSegments} A* segments.");
        }
    }

    private static void CreateWolf(Scene scene, GameObject prefab,
        RuntimeAnimatorController wolfController, NavigationGridGraphProvider provider,
        PatrolSetup patrol, LayerMask obstacleMask, GameObject targetReference,
        bool addDiagnostics)
    {
        GameObject wolf = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (wolf == null)
            throw new InvalidOperationException("Could not instantiate the Predator prefab.");

        wolf.name = $"{WolfNamePrefix}{patrol.Definition.Id}_{patrol.Definition.RegionName}";
        wolf.SetActive(false);
        wolf.transform.position = patrol.Waypoints[0].position;

        Animator animator = wolf.GetComponentInChildren<Animator>(true);
        if (animator == null)
            throw new InvalidOperationException($"{wolf.name} has no child Animator.");
        animator.runtimeAnimatorController = wolfController;
        animator.applyRootMotion = false;

        PredatorMovement movement = EnsureSingleComponent<PredatorMovement>(wolf);
        PredatorPerception perception = EnsureSingleComponent<PredatorPerception>(wolf);
        EnsureSingleComponent<PredatorCombat>(wolf);
        EnsureSingleComponent<PredatorAnimationController>(wolf);
        EnsureSingleComponent<BoidThreat>(wolf);
        GraphNavigationAgent agent = EnsureSingleComponent<GraphNavigationAgent>(wolf);
        PredatorNavigationMovementController controller =
            EnsureSingleComponent<PredatorNavigationMovementController>(wolf);
        PredatorFSM fsm = EnsureSingleComponent<PredatorFSM>(wolf);

        ConfigurePerception(perception, obstacleMask);
        ConfigureAgent(agent, obstacleMask);
        ConfigureController(controller, agent, movement, provider);
        ConfigureFsm(fsm, patrol.Waypoints, targetReference);

        PredatorDebugFeedback debug = EnsureSingleComponent<PredatorDebugFeedback>(wolf);
        SerializedObject serializedDebug = new SerializedObject(debug);
        serializedDebug.FindProperty("visible").boolValue = false;
        serializedDebug.ApplyModifiedPropertiesWithoutUndo();
        if (addDiagnostics)
            EnsureSingleComponent<PredatorPatrolI32Diagnostics>(wolf);

        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(wolf);
        wolf.SetActive(true);
    }

    private static void ConfigurePerception(PredatorPerception perception, LayerMask obstacleMask)
    {
        SerializedObject serialized = new SerializedObject(perception);
        serialized.FindProperty("viewDistance").floatValue = ViewDistance;
        serialized.FindProperty("fieldOfView").floatValue = FieldOfView;
        serialized.FindProperty("targetLayers").intValue = 0;
        serialized.FindProperty("obstacleLayers").intValue = obstacleMask.value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureAgent(GraphNavigationAgent agent, LayerMask obstacleMask)
    {
        SerializedObject serialized = new SerializedObject(agent);
        serialized.FindProperty("avoidSafeZones").boolValue = true;
        serialized.FindProperty("obstacleLayers").intValue = obstacleMask.value;
        serialized.FindProperty("agentRadius").floatValue = 0.4f;
        serialized.FindProperty("agentHeight").floatValue = 1.8f;
        serialized.FindProperty("nodeReachDistance").floatValue = NodeReachDistance;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureController(PredatorNavigationMovementController controller,
        GraphNavigationAgent agent, PredatorMovement movement, NavigationGridGraphProvider provider)
    {
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("navigationAgent").objectReferenceValue = agent;
        serialized.FindProperty("movement").objectReferenceValue = movement;
        serialized.FindProperty("graphProvider").objectReferenceValue = provider;
        serialized.FindProperty("avoidSafeZones").boolValue = true;
        serialized.FindProperty("agentRadius").floatValue = 0.4f;
        serialized.FindProperty("agentHeight").floatValue = 1.8f;
        serialized.FindProperty("chase").boolValue = false;
        serialized.FindProperty("stoppingDistance").floatValue = StoppingDistance;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureFsm(PredatorFSM fsm, Transform[] waypoints,
        GameObject targetReference)
    {
        SerializedObject serialized = new SerializedObject(fsm);
        serialized.FindProperty("targetReference").objectReferenceValue = targetReference;
        serialized.FindProperty("patrolSpeed").floatValue = PatrolSpeed;
        SerializedProperty waypointList = serialized.FindProperty("waypoints");
        waypointList.arraySize = waypoints.Length;
        for (int i = 0; i < waypoints.Length; i++)
            waypointList.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        fsm.enabled = true;
    }

    private static void ValidateSceneConfiguration(Scene scene, NavigationGrid grid,
        NavigationGridGraphProvider provider, RuntimeAnimatorController wolfController,
        PatrolSetup[] patrols, GameObject hunterTarget, LayerMask obstacleMask)
    {
        if (FindSceneComponents<NavigationGrid>(scene).Count != 1)
            throw new InvalidOperationException("The integration scene must contain exactly one NavigationGrid.");
        if (FindSceneComponents<NavigationGridGraphProvider>(scene).Count != 1)
            throw new InvalidOperationException(
                "The integration scene must contain exactly one NavigationGridGraphProvider.");

        SerializedObject serializedProvider = new SerializedObject(provider);
        if (serializedProvider.FindProperty("navigationGrid").objectReferenceValue != grid)
            throw new InvalidOperationException("The shared graph provider is not connected to the scene grid.");

        List<PredatorFSM> fsms = FindSceneComponents<PredatorFSM>(scene);
        List<PredatorNavigationMovementController> controllers =
            FindSceneComponents<PredatorNavigationMovementController>(scene);
        List<GraphNavigationAgent> agents = FindSceneComponents<GraphNavigationAgent>(scene);
        List<PredatorMovement> movements = FindSceneComponents<PredatorMovement>(scene);
        if (fsms.Count != 4 || controllers.Count != 4 || agents.Count != 4 || movements.Count != 4)
        {
            throw new InvalidOperationException(
                $"Expected four Wolf component sets, found FSM={fsms.Count}, " +
                $"Controller={controllers.Count}, Agent={agents.Count}, Movement={movements.Count}.");
        }

        var assignedWaypoints = new HashSet<Transform>();
        for (int i = 0; i < patrols.Length; i++)
        {
            PatrolSetup patrol = patrols[i];
            string wolfName = $"{WolfNamePrefix}{patrol.Definition.Id}_{patrol.Definition.RegionName}";
            GameObject wolf = FindSceneGameObject(scene, wolfName);
            if (wolf == null)
                throw new InvalidOperationException($"Missing configured Wolf: {wolfName}");

            PredatorFSM fsm = wolf.GetComponent<PredatorFSM>();
            GraphNavigationAgent agent = wolf.GetComponent<GraphNavigationAgent>();
            PredatorMovement movement = wolf.GetComponent<PredatorMovement>();
            PredatorNavigationMovementController controller =
                wolf.GetComponent<PredatorNavigationMovementController>();
            PredatorPerception perception = wolf.GetComponent<PredatorPerception>();
            if (fsm == null || agent == null || movement == null ||
                controller == null || perception == null)
            {
                throw new InvalidOperationException($"{wolfName} is missing a required component.");
            }

            if (fsm.Waypoints.Count != patrol.Waypoints.Length)
                throw new InvalidOperationException($"{wolfName} has an invalid waypoint list.");
            for (int waypointIndex = 0; waypointIndex < fsm.Waypoints.Count; waypointIndex++)
            {
                if (fsm.Waypoints[waypointIndex] != patrol.Waypoints[waypointIndex] ||
                    !assignedWaypoints.Add(fsm.Waypoints[waypointIndex]))
                {
                    throw new InvalidOperationException(
                        $"{wolfName} does not own an independent ordered waypoint list.");
                }
            }

            SerializedObject serializedController = new SerializedObject(controller);
            if (serializedController.FindProperty("navigationAgent").objectReferenceValue != agent ||
                serializedController.FindProperty("movement").objectReferenceValue != movement ||
                serializedController.FindProperty("graphProvider").objectReferenceValue != provider)
            {
                throw new InvalidOperationException($"{wolfName} navigation references are incomplete.");
            }

            SerializedObject serializedPerception = new SerializedObject(perception);
            if (serializedPerception.FindProperty("targetLayers").intValue != 0 ||
                serializedPerception.FindProperty("viewDistance").floatValue != ViewDistance ||
                serializedPerception.FindProperty("fieldOfView").floatValue != FieldOfView ||
                serializedPerception.FindProperty("obstacleLayers").intValue != obstacleMask.value)
            {
                throw new InvalidOperationException($"{wolfName} perception settings are invalid.");
            }

            SerializedObject serializedFsm = new SerializedObject(fsm);
            GameObject expectedTarget = patrol.Definition.Id == "L1" ? hunterTarget : null;
            if (serializedFsm.FindProperty("targetReference").objectReferenceValue != expectedTarget)
                throw new InvalidOperationException($"{wolfName} has an invalid perception target.");

            Animator animator = wolf.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.runtimeAnimatorController != wolfController ||
                animator.applyRootMotion)
            {
                throw new InvalidOperationException($"{wolfName} Animator is not configured correctly.");
            }
        }

        if (!ContainsComponentNamed(scene, "FSMAgent") ||
            !ContainsGameObjectNamed(scene, "BoidRespawnArea_B3"))
        {
            throw new InvalidOperationException("Alien or Boid integration was not preserved.");
        }
    }

    private static T EnsureSingleComponent<T>(GameObject target) where T : Component
    {
        T[] components = target.GetComponents<T>();
        if (components.Length > 1)
            throw new InvalidOperationException(
                $"{target.name} contains duplicate {typeof(T).Name} components.");
        return components.Length == 1 ? components[0] : target.AddComponent<T>();
    }

    private static T RequireSingleSceneComponent<T>(Scene scene, string label) where T : Component
    {
        List<T> components = FindSceneComponents<T>(scene);
        if (components.Count != 1)
            throw new InvalidOperationException($"Expected exactly one {label}, found {components.Count}.");
        return components[0];
    }

    private static List<T> FindSceneComponents<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            result.AddRange(roots[i].GetComponentsInChildren<T>(true));
        return result;
    }

    private static GameObject FindSceneGameObject(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
                if (transforms[j].name == objectName) return transforms[j].gameObject;
        }
        return null;
    }

    private static bool ContainsGameObjectNamed(Scene scene, string objectName)
    {
        return FindSceneGameObject(scene, objectName) != null;
    }

    private static bool ContainsComponentNamed(Scene scene, string typeName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            MonoBehaviour[] components = roots[i].GetComponentsInChildren<MonoBehaviour>(true);
            for (int j = 0; j < components.Length; j++)
                if (components[j] != null && components[j].GetType().Name == typeName) return true;
        }
        return false;
    }
}
