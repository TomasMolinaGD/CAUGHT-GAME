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
    private const int PatrolRow = 25;
    private const int LeftPatrolColumn = 5;
    private const int RightPatrolColumn = 33;

    [MenuItem("CAUGHT-GAME/Integrate Wolf Patrol Into B3 Copy")]
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

        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        NavigationGrid grid = RequireSingleSceneComponent<NavigationGrid>(scene, "NavigationGrid");

        if (FindSceneComponents<NavigationGridGraphProvider>(scene).Count != 0)
            throw new InvalidOperationException("The B3 copy already contains a NavigationGridGraphProvider.");
        if (FindSceneComponents<PredatorFSM>(scene).Count != 0)
            throw new InvalidOperationException("The B3 copy already contains a Predator/Wolf FSM.");
        if (!ContainsComponentNamed(scene, "FSMAgent"))
            throw new InvalidOperationException("The copied scene no longer contains the Alien FSM.");
        if (GameObject.Find("BoidRespawnArea_B3") == null)
            throw new InvalidOperationException("The copied scene no longer contains BoidRespawnArea_B3.");

        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer < 0)
            throw new InvalidOperationException("The existing Obstacle layer is required.");
        LayerMask obstacleMask = 1 << obstacleLayer;

        NavigationGridGraphProvider provider = CreateProvider(scene, grid, obstacleMask);
        Transform[] waypoints = CreateWaypoints(scene, grid);
        Physics.SyncTransforms();
        ValidatePatrolRoute(grid, provider, waypoints);
        CreateWolf(scene, predatorPrefab, provider, waypoints, obstacleMask);

        if (FindSceneComponents<PredatorFSM>(scene).Count != 1)
            throw new InvalidOperationException("The integration must contain exactly one PredatorFSM.");

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, TargetScenePath))
            throw new InvalidOperationException("Unity could not save the Wolf integration scene.");

        AssetDatabase.SaveAssets();
        Debug.Log($"Integrated autonomous Wolf patrol into preserved B3 copy: {TargetScenePath}");
    }

    private static NavigationGridGraphProvider CreateProvider(
        Scene scene, NavigationGrid grid, LayerMask obstacleMask)
    {
        GameObject root = new GameObject("NavigationGridGraphProvider_WolfIntegration");
        SceneManager.MoveGameObjectToScene(root, scene);
        NavigationGridGraphProvider provider = root.AddComponent<NavigationGridGraphProvider>();
        provider.Configure(grid, obstacleMask);
        EditorUtility.SetDirty(provider);
        return provider;
    }

    private static Transform[] CreateWaypoints(Scene scene, NavigationGrid grid)
    {
        GameObject root = new GameObject("WolfPatrolWaypoints_B3");
        SceneManager.MoveGameObjectToScene(root, scene);

        Transform left = CreateWaypoint(
            "WolfWaypoint_Left", grid.GridToWorldPosition(PatrolRow, LeftPatrolColumn), Color.green, root.transform);
        Transform right = CreateWaypoint(
            "WolfWaypoint_Right", grid.GridToWorldPosition(PatrolRow, RightPatrolColumn), Color.yellow, root.transform);
        return new[] { left, right };
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

    private static void ValidatePatrolRoute(
        NavigationGrid grid, NavigationGridGraphProvider provider, Transform[] waypoints)
    {
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (!grid.TryWorldToGrid(waypoints[i].position, out int row, out int column) ||
                !grid.TryGetCell(row, column, out NavigationGridCell cell) ||
                !cell.Walkable || cell.Zone == NavigationGridZone.SafeZone)
                throw new InvalidOperationException($"{waypoints[i].name} must be walkable and outside Safe Zones.");
        }

        if (provider.HasDirectLineOfSight(waypoints[0].position, waypoints[1].position, 0.4f, 1.8f))
            throw new InvalidOperationException("The selected Patrol leg does not require A*.");

        var path = new List<Vector3>();
        if (!provider.TryFindPath(waypoints[0].position, waypoints[1].position, true, path) || path.Count == 0)
            throw new InvalidOperationException("No Wolf A* path exists between the selected Patrol waypoints.");

        for (int i = 0; i < path.Count; i++)
        {
            if (!grid.TryWorldToGrid(path[i], out int row, out int column) ||
                !grid.TryGetCell(row, column, out NavigationGridCell cell) ||
                !cell.Walkable || cell.Zone == NavigationGridZone.SafeZone)
                throw new InvalidOperationException("The selected A* route enters an invalid or Safe Zone cell.");
        }
    }

    private static void CreateWolf(Scene scene, GameObject prefab,
        NavigationGridGraphProvider provider, Transform[] waypoints, LayerMask obstacleMask)
    {
        GameObject wolf = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (wolf == null)
            throw new InvalidOperationException("Could not instantiate the Predator prefab.");

        wolf.name = "Wolf_Patrol_B3";
        wolf.SetActive(false);
        wolf.transform.position = waypoints[0].position;

        PredatorMovement movement = EnsureComponent<PredatorMovement>(wolf);
        PredatorPerception perception = EnsureComponent<PredatorPerception>(wolf);
        EnsureComponent<PredatorCombat>(wolf);
        EnsureComponent<PredatorAnimationController>(wolf);
        EnsureComponent<BoidThreat>(wolf);
        GraphNavigationAgent agent = EnsureComponent<GraphNavigationAgent>(wolf);
        PredatorNavigationMovementController controller =
            EnsureComponent<PredatorNavigationMovementController>(wolf);
        PredatorFSM fsm = EnsureComponent<PredatorFSM>(wolf);

        ConfigurePerception(perception, obstacleMask);
        ConfigureAgent(agent, obstacleMask);
        ConfigureController(controller, agent, movement, provider);
        ConfigureFsm(fsm, waypoints);

        PredatorDebugFeedback debug = EnsureComponent<PredatorDebugFeedback>(wolf);
        SerializedObject serializedDebug = new SerializedObject(debug);
        serializedDebug.FindProperty("visible").boolValue = false;
        serializedDebug.ApplyModifiedPropertiesWithoutUndo();
        EnsureComponent<PredatorPatrolI32Diagnostics>(wolf);

        EditorUtility.SetDirty(wolf);
        wolf.SetActive(true);
    }

    private static void ConfigurePerception(PredatorPerception perception, LayerMask obstacleMask)
    {
        SerializedObject serialized = new SerializedObject(perception);
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
        serialized.FindProperty("nodeReachDistance").floatValue = 0.1f;
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
        serialized.FindProperty("stoppingDistance").floatValue = 0.25f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureFsm(PredatorFSM fsm, Transform[] waypoints)
    {
        SerializedObject serialized = new SerializedObject(fsm);
        serialized.FindProperty("targetReference").objectReferenceValue = null;
        SerializedProperty waypointList = serialized.FindProperty("waypoints");
        waypointList.arraySize = waypoints.Length;
        for (int i = 0; i < waypoints.Length; i++)
            waypointList.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        fsm.enabled = true;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
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
