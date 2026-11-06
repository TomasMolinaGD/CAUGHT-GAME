using CaughtGame.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PredatorNavigationI23SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Tests/PredatorNavigation_I2_3.unity";
    private const string PredatorPrefabPath = "Assets/Actors/Predator.prefab";

    [MenuItem("CAUGHT-GAME/Build Predator Navigation I2.3 Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("I2.3", "Exit Play Mode before building the diagnostic scene.", "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            EditorUtility.DisplayDialog("I2.3", $"The scene already exists at {ScenePath}. It was not overwritten.", "OK");
            return;
        }

        GameObject predatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PredatorPrefabPath);
        if (predatorPrefab == null)
        {
            EditorUtility.DisplayDialog("I2.3", $"Missing prefab: {PredatorPrefabPath}", "OK");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateLighting();
        CreateFloorAndObstacles();
        NavigationGrid grid = CreateGrid();
        NavigationGridGraphProvider provider = CreateProvider(grid);
        GameObject predator = CreatePredator(predatorPrefab, provider);
        CreateTargets(predator);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = predator;
        Debug.Log($"Built Predator navigation diagnostic scene at {ScenePath}.");
    }

    private static NavigationGrid CreateGrid()
    {
        GameObject root = new GameObject("NavigationGrid_I2_3");
        root.transform.position = new Vector3(-6f, 0f, -6f);
        NavigationGrid grid = root.AddComponent<NavigationGrid>();
        grid.TryApplyGridSize(12, 12, 1f, true, out _);

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
        GameObject root = new GameObject("NavigationGridGraphProvider_I2_3");
        NavigationGridGraphProvider provider = root.AddComponent<NavigationGridGraphProvider>();
        LayerMask obstacleMask = LayerMask.GetMask("Obstacle");
        provider.Configure(grid, obstacleMask);
        EditorUtility.SetDirty(provider);
        return provider;
    }

    private static GameObject CreatePredator(GameObject prefab, NavigationGridGraphProvider provider)
    {
        GameObject predator = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        predator.name = "Predator_I2_3";
        predator.transform.position = new Vector3(-4.5f, 0f, -4.5f);

        PredatorFSM fsm = predator.GetComponent<PredatorFSM>();
        if (fsm != null) fsm.enabled = false;

        GraphNavigationAgent agent = predator.GetComponent<GraphNavigationAgent>();
        if (agent == null) agent = predator.AddComponent<GraphNavigationAgent>();
        PredatorMovement movement = predator.GetComponent<PredatorMovement>();
        if (movement == null) movement = predator.AddComponent<PredatorMovement>();
        PredatorNavigationMovementController controller =
            predator.GetComponent<PredatorNavigationMovementController>();
        if (controller == null) controller = predator.AddComponent<PredatorNavigationMovementController>();

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("navigationAgent").objectReferenceValue = agent;
        serializedController.FindProperty("movement").objectReferenceValue = movement;
        serializedController.FindProperty("graphProvider").objectReferenceValue = provider;
        serializedController.FindProperty("avoidSafeZones").boolValue = true;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(predator);
        return predator;
    }

    private static void CreateTargets(GameObject predator)
    {
        GameObject harnessObject = new GameObject("PredatorNavigationScenario");
        PredatorNavigationTestHarness harness = harnessObject.AddComponent<PredatorNavigationTestHarness>();
        Transform direct = CreateMarker("DirectTarget", new Vector3(4.5f, 0f, -4.5f), Color.green);
        Transform obstacle = CreateMarker("ObstacleTarget", new Vector3(4.5f, 0f, 3.5f), Color.yellow);
        Transform alternate = CreateMarker("AlternateTarget", new Vector3(-4.5f, 0f, 4.5f), Color.magenta);
        Transform safe = CreateMarker("SafeZoneTarget", new Vector3(5.5f, 0f, 5.5f), Color.cyan);
        Transform outside = CreateMarker("OutOfGridTarget", new Vector3(20f, 0f, 20f), Color.red);

        SerializedObject serializedHarness = new SerializedObject(harness);
        serializedHarness.FindProperty("controller").objectReferenceValue =
            predator.GetComponent<PredatorNavigationMovementController>();
        serializedHarness.FindProperty("destination").objectReferenceValue = direct;
        serializedHarness.FindProperty("obstacleDestination").objectReferenceValue = obstacle;
        serializedHarness.FindProperty("alternateDestination").objectReferenceValue = alternate;
        serializedHarness.FindProperty("safeZoneDestination").objectReferenceValue = safe;
        serializedHarness.FindProperty("outOfGridDestination").objectReferenceValue = outside;
        serializedHarness.FindProperty("requestOnStart").boolValue = true;
        serializedHarness.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("I2.3 targets created: DirectTarget, ObstacleTarget, AlternateTarget, SafeZoneTarget, OutOfGridTarget.");
        _ = obstacle;
        _ = safe;
        _ = outside;
    }

    private static Transform CreateMarker(string name, Vector3 position, Color color)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = name;
        marker.transform.position = position;
        marker.transform.localScale = Vector3.one * 0.35f;
        Object.DestroyImmediate(marker.GetComponent<Collider>());
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        marker.GetComponent<Renderer>().sharedMaterial = material;
        return marker.transform;
    }

    private static void CreateFloorAndObstacles()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Ground_I2_3";
        floor.transform.position = Vector3.down * 0.1f;
        floor.transform.localScale = new Vector3(12f, 0.2f, 12f);

        for (int row = 4; row <= 7; row++)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"ObstacleWall_I2_3_{row}";
            wall.transform.position = new Vector3(0f, 0.75f, -1.5f + row - 4);
            wall.transform.localScale = new Vector3(2f, 1.5f, 1f);
            wall.layer = LayerMask.NameToLayer("Obstacle");
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
        cameraObject.transform.position = new Vector3(0f, 12f, -12f);
        cameraObject.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        camera.fieldOfView = 60f;
    }
}
