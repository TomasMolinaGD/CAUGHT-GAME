using System;
using System.Collections.Generic;
using CaughtGame.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PredatorSearchI332SceneBuilder
{
    private const string SourceScenePath = "Assets/Scenes/Tests/PredatorPatrol_I3_2.unity";
    private const string DestinationScenePath = "Assets/Scenes/Tests/PredatorSearch_I3_3_2.unity";
    private static readonly Vector3 NormalPredatorSpawn = new Vector3(-4.5f, 0f, -4.5f);
    private static readonly Vector3 NoPathPredatorSpawn = new Vector3(4.5f, 0f, 2.5f);
    private static readonly Vector3 NoPathLastKnownPosition = new Vector3(4.5f, 0f, 4.5f);
    private static readonly Vector3 HiddenTargetPosition = new Vector3(30f, 0f, 30f);

    [MenuItem("CAUGHT-GAME/Build Predator Search I3.3.2 Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("I3.3.2", "Exit Play Mode before building the Search validation scene.", "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
        {
            EditorUtility.DisplayDialog("I3.3.2", $"Missing base scene: {SourceScenePath}", "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DestinationScenePath) != null)
        {
            EditorUtility.DisplayDialog("I3.3.2", $"The scene already exists at {DestinationScenePath}. It was not overwritten.", "OK");
            return;
        }

        if (!AssetDatabase.CopyAsset(SourceScenePath, DestinationScenePath))
        {
            EditorUtility.DisplayDialog("I3.3.2", "Unity could not create the independent scene copy.", "OK");
            return;
        }

        AssetDatabase.ImportAsset(DestinationScenePath, ImportAssetOptions.ForceSynchronousImport);
        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = default;
        try
        {
            scene = EditorSceneManager.OpenScene(DestinationScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            ConfigureScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, DestinationScenePath))
                throw new InvalidOperationException($"Unity could not save {DestinationScenePath}.");

            AssetDatabase.SaveAssets();
            ValidateUniqueGuid();
            Debug.Log($"Built isolated Predator Search validation scene at {DestinationScenePath}.");
        }
        catch (Exception exception)
        {
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.DeleteAsset(DestinationScenePath);
            EditorUtility.DisplayDialog("I3.3.2 scene build failed", exception.Message, "OK");
            Debug.LogException(exception);
        }
        finally
        {
            if (previousScene.IsValid() && previousScene.isLoaded)
                SceneManager.SetActiveScene(previousScene);
        }
    }

    private static void ConfigureScene(Scene scene)
    {
        PredatorFSM predator = FindSingleComponent<PredatorFSM>(scene);
        PredatorMovement movement = predator.GetComponent<PredatorMovement>();
        PredatorPerception perception = predator.GetComponent<PredatorPerception>();
        GraphNavigationAgent agent = predator.GetComponent<GraphNavigationAgent>();
        PredatorNavigationMovementController controller =
            predator.GetComponent<PredatorNavigationMovementController>();
        NavigationGrid grid = FindSingleComponent<NavigationGrid>(scene);
        NavigationGridGraphProvider provider = FindSingleComponent<NavigationGridGraphProvider>(scene);
        if (movement == null || perception == null || agent == null || controller == null)
            throw new InvalidOperationException("The copied Predator is missing required production components.");

        predator.gameObject.SetActive(false);
        predator.name = "Predator_Search_I3_3_2";
        predator.transform.position = NormalPredatorSpawn;

        PredatorPatrolI32Diagnostics patrolDiagnostics =
            predator.GetComponent<PredatorPatrolI32Diagnostics>();
        if (patrolDiagnostics != null) UnityEngine.Object.DestroyImmediate(patrolDiagnostics);

        ConfigurePredator(predator, perception, agent, controller, provider);
        Physics.SyncTransforms();
        Vector3 normalLastKnownPosition = FindValidatedAStarDestination(grid, provider);
        GameObject root = new GameObject("SearchHarness_I3_3_2");
        GameObject target = CreateTarget(root.transform);
        Transform normalSpawn = CreateMarker("Normal_PredatorSpawn", NormalPredatorSpawn,
            new Color(0.1f, 0.7f, 1f), root.transform);
        Transform normalLastKnown = CreateMarker("Normal_LastKnownPosition_AStar",
            normalLastKnownPosition, Color.yellow, root.transform);
        Transform noPathSpawn = CreateMarker("NoPath_PredatorSpawn", NoPathPredatorSpawn,
            new Color(0.8f, 0.2f, 1f), root.transform);
        Transform noPathLastKnown = CreateMarker("NoPath_LastKnownPosition_SafeCell",
            NoPathLastKnownPosition, Color.red, root.transform);
        Transform hiddenTarget = CreateMarker("Hidden_TargetPosition", HiddenTargetPosition,
            Color.gray, root.transform, false);

        PredatorSearchI332Harness harness = root.AddComponent<PredatorSearchI332Harness>();
        harness.Configure(predator, controller, provider, target, normalSpawn, normalLastKnown,
            noPathSpawn, noPathLastKnown, hiddenTarget);
        PredatorSearchI332Diagnostics diagnostics = root.AddComponent<PredatorSearchI332Diagnostics>();
        diagnostics.Configure(predator, controller, movement, harness);

        SerializedObject serializedFsm = new SerializedObject(predator);
        serializedFsm.FindProperty("targetReference").objectReferenceValue = target;
        serializedFsm.FindProperty("patrolSpeed").floatValue = 2.85f;
        serializedFsm.ApplyModifiedPropertiesWithoutUndo();

        predator.gameObject.SetActive(true);
        Physics.SyncTransforms();
        ValidateScenario(scene, grid, provider, predator, target, normalLastKnownPosition);

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(predator);
        Selection.activeGameObject = root;
    }

    private static void ConfigurePredator(PredatorFSM predator, PredatorPerception perception,
        GraphNavigationAgent agent, PredatorNavigationMovementController controller,
        NavigationGridGraphProvider provider)
    {
        LayerMask obstacleMask = LayerMask.GetMask("Obstacle");
        LayerMask targetMask = LayerMask.GetMask("Threat");
        if (obstacleMask.value == 0 || targetMask.value == 0)
            throw new InvalidOperationException("The existing Obstacle and Threat layers are required.");

        perception.Configure(18f, 360f, targetMask, obstacleMask, 0.05f, 1.5f);
        controller.Configure(agent, predator.GetComponent<PredatorMovement>(), false, 0.25f);
        controller.ConfigureGraphProvider(provider);

        SerializedObject serializedAgent = new SerializedObject(agent);
        serializedAgent.FindProperty("nodeReachDistance").floatValue = 0.1f;
        serializedAgent.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("avoidSafeZones").boolValue = true;
        serializedController.FindProperty("agentRadius").floatValue = 0.4f;
        serializedController.FindProperty("agentHeight").floatValue = 1.8f;
        serializedController.FindProperty("chase").boolValue = false;
        serializedController.FindProperty("stoppingDistance").floatValue = 0.25f;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateTarget(Transform parent)
    {
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        target.name = "Isolated_Search_Target";
        target.transform.SetParent(parent, true);
        target.transform.position = HiddenTargetPosition;
        target.transform.localScale = Vector3.one * 0.7f;
        target.layer = LayerMask.NameToLayer("Threat");
        target.AddComponent<BoidThreat>();

        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = new Color(1f, 0.25f, 0.1f);
        target.GetComponent<Renderer>().sharedMaterial = material;
        return target;
    }

    private static Transform CreateMarker(string name, Vector3 position, Color color,
        Transform parent, bool visible = true)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = name;
        marker.transform.SetParent(parent, true);
        marker.transform.position = position;
        marker.transform.localScale = Vector3.one * 0.3f;
        UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
        marker.SetActive(visible);
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        marker.GetComponent<Renderer>().sharedMaterial = material;
        return marker.transform;
    }

    private static void ValidateScenario(Scene scene, NavigationGrid grid,
        NavigationGridGraphProvider provider, PredatorFSM predator, GameObject target,
        Vector3 normalLastKnownPosition)
    {
        if (FindComponents<PredatorFSM>(scene).Count != 1 ||
            FindComponents<NavigationGrid>(scene).Count != 1 ||
            FindComponents<NavigationGridGraphProvider>(scene).Count != 1)
            throw new InvalidOperationException("The Search scene must contain one Predator, one grid and one provider.");

        if (target.GetComponent<BoidThreat>() == null || predator.CurrentTarget != null)
            throw new InvalidOperationException("The isolated target or initial Predator state is invalid.");

        ValidateWalkableCell(grid, NormalPredatorSpawn, false, "normal Predator spawn");
        ValidateWalkableCell(grid, normalLastKnownPosition, false, "normal LastKnownPosition");
        ValidateWalkableCell(grid, NoPathPredatorSpawn, false, "NoPath Predator spawn");
        ValidateWalkableCell(grid, NoPathLastKnownPosition, true, "NoPath LastKnownPosition");

        LayerMask obstacles = LayerMask.GetMask("Obstacle");
        if (Physics.Linecast(NormalPredatorSpawn + Vector3.up * 1.5f,
            normalLastKnownPosition + Vector3.up * 1.5f, obstacles, QueryTriggerInteraction.Ignore))
            throw new InvalidOperationException("The normal target must be visible to real Predator perception.");
        if (provider.HasDirectLineOfSight(NormalPredatorSpawn, normalLastKnownPosition, 0.4f, 1.8f))
            throw new InvalidOperationException("The normal Search destination must require A* rather than direct movement.");

        var path = new List<Vector3>();
        if (!provider.TryFindPath(NormalPredatorSpawn, normalLastKnownPosition, true, path) || path.Count == 0)
            throw new InvalidOperationException("The normal Search destination has no valid Wolf A* route.");

        if (Physics.Linecast(NoPathPredatorSpawn + Vector3.up * 1.5f,
            NoPathLastKnownPosition + Vector3.up * 1.5f, obstacles, QueryTriggerInteraction.Ignore))
            throw new InvalidOperationException("The NoPath target must first be visible to real Predator perception.");
        if (provider.HasDirectLineOfSight(NoPathPredatorSpawn, NoPathLastKnownPosition, 0.4f, 1.8f))
            throw new InvalidOperationException("The Safe Zone destination unexpectedly permits direct Wolf movement.");
        path.Clear();
        if (provider.TryFindPath(NoPathPredatorSpawn, NoPathLastKnownPosition, true, path))
            throw new InvalidOperationException("The designated Safe Zone destination unexpectedly has a Wolf route.");
    }

    private static Vector3 FindValidatedAStarDestination(NavigationGrid grid,
        NavigationGridGraphProvider provider)
    {
        LayerMask obstacles = LayerMask.GetMask("Obstacle");
        Vector3 bestPosition = default;
        float bestDistance = float.PositiveInfinity;
        bool found = false;
        var path = new List<Vector3>();

        for (int row = 0; row < 12; row++)
        {
            for (int column = 0; column < 12; column++)
            {
                if (!grid.TryGetCell(row, column, out NavigationGridCell cell) ||
                    !cell.Walkable || cell.Zone == NavigationGridZone.SafeZone)
                    continue;

                Vector3 candidate = grid.GridToWorldPosition(row, column);
                if (candidate.x < 1.5f || candidate.z < -2.5f) continue;
                if (Physics.Linecast(NormalPredatorSpawn + Vector3.up * 1.5f,
                    candidate + Vector3.up * 1.5f, obstacles, QueryTriggerInteraction.Ignore))
                    continue;
                if (provider.HasDirectLineOfSight(NormalPredatorSpawn, candidate, 0.4f, 1.8f))
                    continue;

                path.Clear();
                if (!provider.TryFindPath(NormalPredatorSpawn, candidate, true, path) || path.Count == 0)
                    continue;

                float distance = Vector3.ProjectOnPlane(
                    candidate - NormalPredatorSpawn, Vector3.up).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestPosition = candidate;
                found = true;
            }
        }

        if (!found)
            throw new InvalidOperationException(
                "No grid cell is both visible to perception and guaranteed to require A* for the Wolf capsule.");
        return bestPosition;
    }

    private static void ValidateWalkableCell(NavigationGrid grid, Vector3 position,
        bool expectSafeZone, string label)
    {
        if (!grid.TryWorldToGrid(position, out int row, out int column) ||
            !grid.TryGetCell(row, column, out NavigationGridCell cell) || !cell.Walkable)
            throw new InvalidOperationException($"The {label} is not on a walkable grid cell.");
        if ((cell.Zone == NavigationGridZone.SafeZone) != expectSafeZone)
            throw new InvalidOperationException($"The {label} has an unexpected Safe Zone classification.");
    }

    private static T FindSingleComponent<T>(Scene scene) where T : Component
    {
        List<T> components = FindComponents<T>(scene);
        if (components.Count != 1)
            throw new InvalidOperationException($"Expected exactly one {typeof(T).Name}, found {components.Count}.");
        return components[0];
    }

    private static List<T> FindComponents<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            result.AddRange(root.GetComponentsInChildren<T>(true));
        return result;
    }

    private static void ValidateUniqueGuid()
    {
        string sourceGuid = AssetDatabase.AssetPathToGUID(SourceScenePath);
        string destinationGuid = AssetDatabase.AssetPathToGUID(DestinationScenePath);
        if (string.IsNullOrEmpty(destinationGuid) || sourceGuid == destinationGuid)
            throw new InvalidOperationException("The copied Search scene does not have an independent GUID.");
    }
}
