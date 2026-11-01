using System;
using System.Collections.Generic;
using CaughtGame.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PredatorPatrolI32SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Tests/PredatorPatrol_I3_2.unity";
    private const string PredatorPrefabPath = "Assets/Actors/Predator.prefab";
    private const int GridSize = 12;
    private const float CellSize = 1f;
    private static readonly Vector3 StartPosition = new Vector3(-4.5f, 0f, -4.5f);
    private static readonly Vector3 LeftWaypointPosition = new Vector3(-4.5f, 0f, -0.5f);
    private static readonly Vector3 RightWaypointPosition = new Vector3(4.5f, 0f, -0.5f);

    [MenuItem("CAUGHT-GAME/Build Predator Patrol I3.2 Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("I3.2", "Exit Play Mode before building the Patrol validation scene.", "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            EditorUtility.DisplayDialog("I3.2", $"The scene already exists at {ScenePath}. It was not overwritten.", "OK");
            return;
        }

        GameObject predatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PredatorPrefabPath);
        if (predatorPrefab == null)
        {
            EditorUtility.DisplayDialog("I3.2", $"Missing prefab: {PredatorPrefabPath}", "OK");
            return;
        }

        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            CreateLighting();
            CreateFloorAndObstacles();
            NavigationGrid grid = CreateGrid();
            CreateSafeZoneGridMarker(grid);
            NavigationGridGraphProvider provider = CreateProvider(grid);
            Transform[] waypoints = CreateWaypoints();
            Physics.SyncTransforms();
            ValidateScenario(grid, provider, waypoints);
            GameObject predator = CreatePredator(predatorPrefab, provider, waypoints);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"Unity could not save the validation scene to {ScenePath}.");

            AssetDatabase.SaveAssets();
            Selection.activeGameObject = predator;
            Debug.Log($"Built autonomous Predator Patrol validation scene at {ScenePath}.");
        }
        catch (Exception exception)
        {
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            EditorUtility.DisplayDialog("I3.2 scene build failed", exception.Message, "OK");
            Debug.LogException(exception);
        }
        finally
        {
            if (previousScene.IsValid() && previousScene.isLoaded)
                SceneManager.SetActiveScene(previousScene);
        }
    }

    private static NavigationGrid CreateGrid()
    {
        GameObject root = new GameObject("NavigationGrid_I3_2");
        root.transform.position = new Vector3(-6f, 0f, -6f);
        NavigationGrid grid = root.AddComponent<NavigationGrid>();
        if (!grid.TryApplyGridSize(GridSize, GridSize, CellSize, true, out _))
            throw new InvalidOperationException("Could not initialize the I3.2 navigation grid.");

        for (int row = 4; row <= 7; row++)
            for (int column = 5; column <= 6; column++)
                grid.SetWalkable(row, column, false);

        for (int row = 10; row <= 11; row++)
            for (int column = 10; column <= 11; column++)
                grid.SetZone(row, column, NavigationGridZone.SafeZone, "SafeZone_I2_3");

        EditorUtility.SetDirty(grid);
        return grid;
    }

    private static NavigationGridGraphProvider CreateProvider(NavigationGrid grid)
    {
        GameObject root = new GameObject("NavigationGridGraphProvider_I3_2");
        NavigationGridGraphProvider provider = root.AddComponent<NavigationGridGraphProvider>();
        LayerMask obstacleMask = LayerMask.GetMask("Obstacle");
        provider.Configure(grid, obstacleMask);
        EditorUtility.SetDirty(provider);
        return provider;
    }

    private static void CreateSafeZoneGridMarker(NavigationGrid grid)
    {
        Vector3 firstCell = grid.GridToWorldPosition(10, 10);
        Vector3 lastCell = grid.GridToWorldPosition(11, 11);
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "SafeZoneGridCells_VisualOnly_I3_2";
        marker.transform.position = new Vector3(
            (firstCell.x + lastCell.x) * 0.5f, 0.015f, (firstCell.z + lastCell.z) * 0.5f);
        marker.transform.localScale = new Vector3(2f, 0.02f, 2f);
        UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = new Color(0.15f, 0.8f, 1f, 1f);
        marker.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static Transform[] CreateWaypoints()
    {
        GameObject root = new GameObject("PatrolWaypoints_I3_2");
        Transform left = CreateMarker("Waypoint_Left", LeftWaypointPosition, Color.green, root.transform);
        Transform right = CreateMarker("Waypoint_Right", RightWaypointPosition, Color.yellow, root.transform);
        return new[] { left, right };
    }

    private static GameObject CreatePredator(GameObject prefab,
        NavigationGridGraphProvider provider, Transform[] waypoints)
    {
        GameObject predator = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (predator == null)
            throw new InvalidOperationException($"Could not instantiate prefab: {PredatorPrefabPath}");

        predator.name = "Predator_Patrol_I3_2";
        predator.SetActive(false);
        predator.transform.position = StartPosition;

        PredatorMovement movement = EnsureComponent<PredatorMovement>(predator);
        PredatorPerception perception = EnsureComponent<PredatorPerception>(predator);
        EnsureComponent<PredatorCombat>(predator);
        EnsureComponent<PredatorAnimationController>(predator);
        EnsureComponent<BoidThreat>(predator);
        GraphNavigationAgent agent = EnsureComponent<GraphNavigationAgent>(predator);
        PredatorNavigationMovementController controller =
            EnsureComponent<PredatorNavigationMovementController>(predator);
        PredatorFSM fsm = EnsureComponent<PredatorFSM>(predator);

        ConfigureController(controller, agent, movement, provider);
        ConfigureWaypoints(fsm, waypoints);
        fsm.enabled = true;

        PredatorDebugFeedback existingDebug = predator.GetComponent<PredatorDebugFeedback>();
        if (existingDebug == null) existingDebug = predator.AddComponent<PredatorDebugFeedback>();
        SerializedObject serializedDebug = new SerializedObject(existingDebug);
        serializedDebug.FindProperty("visible").boolValue = false;
        serializedDebug.ApplyModifiedPropertiesWithoutUndo();

        EnsureComponent<PredatorPatrolI32Diagnostics>(predator);
        EditorUtility.SetDirty(predator);
        predator.SetActive(true);
        return predator;
    }

    private static void ConfigureController(PredatorNavigationMovementController controller,
        GraphNavigationAgent agent, PredatorMovement movement, NavigationGridGraphProvider provider)
    {
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("navigationAgent").objectReferenceValue = agent;
        serializedController.FindProperty("movement").objectReferenceValue = movement;
        serializedController.FindProperty("graphProvider").objectReferenceValue = provider;
        serializedController.FindProperty("avoidSafeZones").boolValue = true;
        serializedController.FindProperty("agentRadius").floatValue = 0.4f;
        serializedController.FindProperty("agentHeight").floatValue = 1.8f;
        serializedController.FindProperty("chase").boolValue = false;
        serializedController.FindProperty("stoppingDistance").floatValue = 0.25f;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureWaypoints(PredatorFSM fsm, Transform[] waypoints)
    {
        SerializedObject serializedFsm = new SerializedObject(fsm);
        SerializedProperty waypointList = serializedFsm.FindProperty("waypoints");
        if (waypointList == null)
            throw new InvalidOperationException("PredatorFSM does not expose its serialized waypoint list.");

        waypointList.arraySize = waypoints.Length;
        for (int i = 0; i < waypoints.Length; i++)
            waypointList.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
        serializedFsm.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ValidateScenario(NavigationGrid grid,
        NavigationGridGraphProvider provider, Transform[] waypoints)
    {
        if (waypoints == null || waypoints.Length < 2 ||
            waypoints[0].position.x >= -1f || waypoints[1].position.x <= 1f)
            throw new InvalidOperationException("Patrol waypoints must lie on opposite sides of the obstacle wall.");

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (!grid.TryWorldToGrid(waypoints[i].position, out int row, out int column) ||
                !grid.TryGetCell(row, column, out NavigationGridCell cell) ||
                !cell.Walkable || cell.Zone == NavigationGridZone.SafeZone)
                throw new InvalidOperationException($"{waypoints[i].name} must be walkable and outside Safe Zone cells.");
        }

        if (!provider.HasDirectLineOfSight(StartPosition, LeftWaypointPosition, 0.4f, 1.8f))
            throw new InvalidOperationException("The spawn-to-left-waypoint leg should provide a direct-route comparison.");

        if (provider.HasDirectLineOfSight(LeftWaypointPosition, RightWaypointPosition, 0.4f, 1.8f))
            throw new InvalidOperationException("The cross-wall waypoint leg must require A* rather than direct movement.");

        var path = new List<Vector3>();
        if (!provider.TryFindPath(LeftWaypointPosition, RightWaypointPosition, true, path) || path.Count == 0)
            throw new InvalidOperationException("The cross-wall A* route could not be found on the configured grid.");
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static Transform CreateMarker(string name, Vector3 position, Color color, Transform parent)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = name;
        marker.transform.SetParent(parent, true);
        marker.transform.position = position;
        marker.transform.localScale = Vector3.one * 0.4f;
        UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        marker.GetComponent<Renderer>().sharedMaterial = material;
        return marker.transform;
    }

    private static void CreateFloorAndObstacles()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Ground_I3_2";
        floor.transform.position = Vector3.down * 0.1f;
        floor.transform.localScale = new Vector3(GridSize, 0.2f, GridSize);

        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer < 0)
            throw new InvalidOperationException("The project must already define its Obstacle layer; ProjectSettings are not changed by this builder.");

        for (int row = 4; row <= 7; row++)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"ObstacleWall_I3_2_{row}";
            wall.transform.position = new Vector3(0f, 0.75f, -1.5f + row - 4);
            wall.transform.localScale = new Vector3(2f, 1.5f, 1f);
            wall.layer = obstacleLayer;
        }
    }

    private static void CreateLighting()
    {
        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 15f, -12f);
        cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        camera.fieldOfView = 60f;
    }
}
