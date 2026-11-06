using System.Collections.Generic;
using System.IO;
using CaughtGame.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildNavigationTestScene
{
    private const string ScenePath = "Assets/Scenes/Tests/Navigation_Test.unity";

    [MenuItem("Tools/CAUGHT/Build Navigation Test Scene")]
    public static void Build()
    {
        if (File.Exists(ScenePath))
        {
            Debug.LogError($"Navigation test scene already exists at '{ScenePath}'. It was not overwritten.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateEnvironment();
        NavigationGraph graph = CreateNavigationGraph();
        IReadOnlyList<string> validationIssues = graph.Validate();
        if (validationIssues.Count > 0)
        {
            foreach (string issue in validationIssues)
            {
                Debug.LogError(issue, graph);
            }

            return;
        }

        if (File.Exists(ScenePath))
        {
            Debug.LogError($"Navigation test scene appeared at '{ScenePath}' while building. It was not overwritten.");
            return;
        }

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            Debug.LogError($"Failed to save navigation test scene at '{ScenePath}'.");
            return;
        }

        AssetDatabase.Refresh();
        Debug.Log($"Navigation test scene created and graph validated: {ScenePath}");
    }

    private static void CreateEnvironment()
    {
        GameObject environment = new GameObject("Test Environment");

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Ground";
        floor.transform.SetParent(environment.transform, false);
        floor.transform.localScale = new Vector3(1.4f, 1f, 1f);

        // Room boundaries and the central barrier leave a northern corridor around the wall.
        CreateBox(environment.transform, "West Room North Wall", new Vector3(-4.5f, 1f, 2.5f), new Vector3(3f, 2f, 0.2f));
        CreateBox(environment.transform, "West Room South Wall", new Vector3(-4f, 1f, -2.5f), new Vector3(4f, 2f, 0.2f));
        CreateBox(environment.transform, "West Room West Wall", new Vector3(-6f, 1f, 0f), new Vector3(0.2f, 2f, 5f));
        CreateBox(environment.transform, "East Room North Wall", new Vector3(4.5f, 1f, 2.5f), new Vector3(3f, 2f, 0.2f));
        CreateBox(environment.transform, "East Room South Wall", new Vector3(4f, 1f, -2.5f), new Vector3(4f, 2f, 0.2f));
        CreateBox(environment.transform, "East Room East Wall", new Vector3(6f, 1f, 0f), new Vector3(0.2f, 2f, 5f));
        CreateBox(environment.transform, "Blocking Wall", Vector3.up, new Vector3(0.3f, 2f, 5f));

        // These short side walls mark the passage that routes around the blocking wall.
        CreateBox(environment.transform, "Corridor North Edge", new Vector3(0f, 0.5f, 4f), new Vector3(8f, 1f, 0.2f));
        CreateBox(environment.transform, "Corridor South Edge West", new Vector3(-2.5f, 0.5f, 3f), new Vector3(3f, 1f, 0.2f));
        CreateBox(environment.transform, "Corridor South Edge East", new Vector3(2.5f, 0.5f, 3f), new Vector3(3f, 1f, 0.2f));
    }

    private static NavigationGraph CreateNavigationGraph()
    {
        GameObject graphObject = new GameObject("Navigation Graph");
        NavigationGraph graph = graphObject.AddComponent<NavigationGraph>();

        NavigationNode westRoom = CreateNode("Node West Room", new Vector3(-4f, 0.1f, 0f), NavigationZone.Normal);
        NavigationNode safeZone = CreateNode("Node Safe Zone", new Vector3(-4f, 0.1f, 1.5f), NavigationZone.SafeZone);
        NavigationNode corridorWest = CreateNode("Node Corridor West", new Vector3(-1.5f, 0.1f, 3.5f), NavigationZone.Normal);
        NavigationNode corridorEast = CreateNode("Node Corridor East", new Vector3(1.5f, 0.1f, 3.5f), NavigationZone.Normal);
        NavigationNode eastRoom = CreateNode("Node East Room", new Vector3(4f, 0.1f, 0f), NavigationZone.Normal);

        Connect(westRoom, safeZone);
        Connect(safeZone, corridorWest);
        Connect(corridorWest, corridorEast);
        Connect(corridorEast, eastRoom);

        SerializedObject serializedGraph = new SerializedObject(graph);
        SerializedProperty nodes = serializedGraph.FindProperty("nodes");
        nodes.arraySize = 5;
        nodes.GetArrayElementAtIndex(0).objectReferenceValue = westRoom;
        nodes.GetArrayElementAtIndex(1).objectReferenceValue = safeZone;
        nodes.GetArrayElementAtIndex(2).objectReferenceValue = corridorWest;
        nodes.GetArrayElementAtIndex(3).objectReferenceValue = corridorEast;
        nodes.GetArrayElementAtIndex(4).objectReferenceValue = eastRoom;
        serializedGraph.ApplyModifiedPropertiesWithoutUndo();

        return graph;
    }

    private static NavigationNode CreateNode(string objectName, Vector3 position, NavigationZone zone)
    {
        GameObject nodeObject = new GameObject(objectName);
        nodeObject.transform.position = position;
        NavigationNode node = nodeObject.AddComponent<NavigationNode>();

        SerializedObject serializedNode = new SerializedObject(node);
        serializedNode.FindProperty("zone").enumValueIndex = (int)zone;
        serializedNode.ApplyModifiedPropertiesWithoutUndo();
        return node;
    }

    private static void Connect(NavigationNode from, NavigationNode to)
    {
        if (!from.TryConnectBidirectionally(to))
        {
            Debug.LogError($"Could not create reciprocal connection between '{from.name}' and '{to.name}'.", from);
        }
    }

    private static GameObject CreateBox(Transform parent, string objectName, Vector3 position, Vector3 size)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = size;
        return box;
    }
}
