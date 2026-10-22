using System.IO;
using CaughtGame.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NavigationMovementTestSceneBuilder
{
    private const string MenuPath = "Tools/CAUGHT/Build Navigation Movement Test Scene";
    private const string ScenePath = "Assets/Scenes/Tests/NavigationMovement_Test.unity";
    private const float HunterSpeed = 3f;
    private const float WolfSpeed = 2.5f;
    private const float PrototypeRadius = 0.5f;

    [MenuItem(MenuPath)]
    private static void BuildTestScene()
    {
        if (File.Exists(ScenePath))
        {
            EditorUtility.DisplayDialog("Navigation test scene exists",
                $"The scene already exists and will not be overwritten:\n{ScenePath}", "OK");
            return;
        }

        NavigationGrid sourceGrid = Selection.activeGameObject == null
            ? null
            : Selection.activeGameObject.GetComponent<NavigationGrid>();
        if (sourceGrid == null)
        {
            EditorUtility.DisplayDialog("Select a NavigationGrid",
                "Select the GameObject containing the painted NavigationGrid, then run this menu again.", "OK");
            return;
        }

        if (!TryFindCell(sourceGrid, NavigationGridZone.Spawn, null, out Vector2Int hunterStart) ||
            !TryFindCell(sourceGrid, NavigationGridZone.Spawn, hunterStart, out Vector2Int wolfStart) ||
            !TryFindCell(sourceGrid, NavigationGridZone.SafeZone, "SafeZone_A", out Vector2Int hunterDestination) ||
            !TryFindCell(sourceGrid, NavigationGridZone.HuntingGround, null, out Vector2Int wolfDestination))
        {
            EditorUtility.DisplayDialog("Navigation test setup incomplete",
                "The grid needs two walkable Spawn cells, one SafeZone_A cell, and one walkable HuntingGround cell.", "OK");
            return;
        }

        if (!HasRoute(sourceGrid, hunterStart, hunterDestination, NavigationAgentType.Hunter) ||
            !HasRoute(sourceGrid, wolfStart, wolfDestination, NavigationAgentType.Wolf))
        {
            EditorUtility.DisplayDialog("Navigation test routes unavailable",
                "The selected prototype start and destination cells do not have valid routes for their agent types.", "OK");
            return;
        }

        Scene originalActiveScene = SceneManager.GetActiveScene();
        Scene testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        if (!testScene.IsValid())
        {
            EditorUtility.DisplayDialog("Could not create test scene", "Unity did not create an additive scene.", "OK");
            return;
        }

        SceneManager.SetActiveScene(testScene);
        GameObject gridCopyObject = Object.Instantiate(sourceGrid.gameObject);
        gridCopyObject.name = "NavigationGrid_Test_Copy";
        SceneManager.MoveGameObjectToScene(gridCopyObject, testScene);
        NavigationGrid copiedGrid = gridCopyObject.GetComponent<NavigationGrid>();

        NavigationGridPathPreview copiedPreview = gridCopyObject.GetComponent<NavigationGridPathPreview>();
        if (copiedPreview != null)
        {
            SerializedObject serializedPreview = new SerializedObject(copiedPreview);
            serializedPreview.FindProperty("grid").objectReferenceValue = copiedGrid;
            serializedPreview.ApplyModifiedPropertiesWithoutUndo();
        }

        CreatePrototype("HunterPrototype", copiedGrid, NavigationAgentType.Hunter,
            hunterStart, hunterDestination, HunterSpeed, new Color(0.12f, 0.45f, 1f));
        CreatePrototype("WolfPrototype", copiedGrid, NavigationAgentType.Wolf,
            wolfStart, wolfDestination, WolfSpeed, new Color(0.9f, 0.12f, 0.12f));

        EditorSceneManager.MarkSceneDirty(testScene);
        if (!EditorSceneManager.SaveScene(testScene, ScenePath))
        {
            EditorSceneManager.CloseScene(testScene, true);
            if (originalActiveScene.IsValid()) SceneManager.SetActiveScene(originalActiveScene);
            EditorUtility.DisplayDialog("Could not save test scene",
                "The test scene was not saved. The source scene was left unchanged.", "OK");
            return;
        }

        EditorUtility.DisplayDialog("Navigation test scene created",
            $"Created an isolated copy at:\n{ScenePath}\n\nThe source scene was not modified. Prototype colliders are triggers; this validates logical path following only, not physical collision avoidance.",
            "OK");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateBuildTestScene()
    {
        return Selection.activeGameObject != null &&
               Selection.activeGameObject.GetComponent<NavigationGrid>() != null;
    }

    private static bool TryFindCell(NavigationGrid grid, NavigationGridZone zone, string zoneId,
        out Vector2Int cell)
    {
        return TryFindCell(grid, zone, zoneId, new Vector2Int(-1, -1), out cell);
    }

    private static bool TryFindCell(NavigationGrid grid, NavigationGridZone zone, Vector2Int exclude,
        out Vector2Int cell)
    {
        return TryFindCell(grid, zone, null, exclude, out cell);
    }

    private static bool TryFindCell(NavigationGrid grid, NavigationGridZone zone, string zoneId,
        Vector2Int exclude, out Vector2Int cell)
    {
        for (int row = 0; row < grid.Rows; row++)
        {
            for (int column = 0; column < grid.Columns; column++)
            {
                if (!grid.TryGetCell(row, column, out NavigationGridCell candidate) ||
                    !candidate.Walkable || candidate.Zone != zone ||
                    (zoneId != null && candidate.ZoneId != zoneId))
                {
                    continue;
                }

                Vector2Int coordinates = new Vector2Int(column, row);
                if (coordinates == exclude) continue;
                cell = coordinates;
                return true;
            }
        }

        cell = default;
        return false;
    }

    private static bool HasRoute(NavigationGrid grid, Vector2Int start, Vector2Int destination,
        NavigationAgentType type)
    {
        NavigationPathResult result = NavigationGridPathfinder.FindPath(grid,
            start.y, start.x, destination.y, destination.x, type);
        return result.Status == NavigationPathStatus.Success;
    }

    private static void CreatePrototype(string objectName, NavigationGrid grid,
        NavigationAgentType agentType, Vector2Int start, Vector2Int destination,
        float movementSpeed, Color color)
    {
        GameObject prototype = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        prototype.name = objectName;
        prototype.transform.position = grid.GridToWorldPosition(start.y, start.x) + Vector3.up;

        CapsuleCollider capsule = prototype.GetComponent<CapsuleCollider>();
        capsule.radius = PrototypeRadius;
        capsule.height = 2f;
        capsule.isTrigger = true;

        PathFollower follower = prototype.AddComponent<PathFollower>();
        follower.Speed = movementSpeed;
        NavigationGridTestAgent testAgent = prototype.AddComponent<NavigationGridTestAgent>();
        testAgent.Configure(grid, agentType, destination.y, destination.x,
            movementSpeed, PrototypeRadius);

        Renderer meshRenderer = prototype.GetComponent<Renderer>();
        var propertyBlock = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", color);
        propertyBlock.SetColor("_Color", color);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }
}
