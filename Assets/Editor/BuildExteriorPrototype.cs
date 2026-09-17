using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildExteriorPrototype
{
    private const string SourceScene = "Assets/LastSave.unity";
    private const string TargetScene = "Assets/Scenes/LastSave_ExteriorPrototype.unity";
    private const int ObstacleLayer = 12;

    private static readonly Vector3 LevelCenter = new Vector3(484f, 0f, 623f);

    [InitializeOnLoadMethod]
    private static void BuildOnNextEditorOpen()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(TargetScene) || !File.ReadAllText(TargetScene).Contains("Exterior Mining Outpost (Kenney v8)"))
            {
                Build();
            }
        };
    }

    [MenuItem("Tools/Level Design/Rebuild Exterior Prototype")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        Scene scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
        EditorSceneManager.SaveScene(scene, TargetScene, true);

        scene = SceneManager.GetActiveScene();
        RemovePreviousPrototype();

        GameObject prototype = new GameObject("Exterior Mining Outpost (Kenney v8)");
        GameObject boundaries = CreateGroup("01 - Perimeter", prototype.transform);
        GameObject landingZone = CreateGroup("02 - Landing Zone", prototype.transform);
        GameObject sideHangars = CreateGroup("03 - Side Hangars", prototype.transform);
        GameObject markers = CreateGroup("04 - Design Markers", prototype.transform);

        BuildPerimeter(boundaries.transform, scene);
        SetLayerRecursively(boundaries, ObstacleLayer);
        BuildLandingZone(landingZone.transform, scene);
        BuildSideHangars(sideHangars.transform, scene);
        SetLayerRecursively(sideHangars, ObstacleLayer);
        DressInterestObject(scene);
        BuildMarkers(markers.transform);
        FrameGameplayCamera();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, TargetScene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Exterior prototype created at {TargetScene}");
    }

    private static void BuildPerimeter(Transform parent, Scene scene)
    {
        GameObject wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Dzeruza/MinimalScifiPack/3DModels/Walls/Prefabs/SM_Trim_Wall_3x8M_B1.prefab");

        float west = LevelCenter.x - 72f;
        float east = LevelCenter.x + 72f;
        float south = LevelCenter.z - 56f;
        float north = LevelCenter.z + 56f;

        int index = 0;
        for (float x = west + 4f; x <= east - 4f; x += 8f)
        {
            InstantiatePrefab(wallPrefab, $"South Wall {++index:00}", new Vector3(x, 0f, south), Quaternion.identity, parent, scene);
            InstantiatePrefab(wallPrefab, $"North Wall {++index:00}", new Vector3(x, 0f, north), Quaternion.Euler(0f, 180f, 0f), parent, scene);
        }

        for (float z = south + 4f; z <= north - 4f; z += 8f)
        {
            InstantiatePrefab(wallPrefab, $"West Wall {++index:00}", new Vector3(west, 0f, z), Quaternion.Euler(0f, 90f, 0f), parent, scene);
            InstantiatePrefab(wallPrefab, $"East Wall {++index:00}", new Vector3(east, 0f, z), Quaternion.Euler(0f, -90f, 0f), parent, scene);
        }

        CreateInvisibleBoundary("South Boundary Collider", new Vector3(LevelCenter.x, 2.5f, south), new Vector3(145f, 5f, 1f), parent);
        CreateInvisibleBoundary("North Boundary Collider", new Vector3(LevelCenter.x, 2.5f, north), new Vector3(145f, 5f, 1f), parent);
        CreateInvisibleBoundary("West Boundary Collider", new Vector3(west, 2.5f, LevelCenter.z), new Vector3(1f, 5f, 113f), parent);
        CreateInvisibleBoundary("East Boundary Collider", new Vector3(east, 2.5f, LevelCenter.z), new Vector3(1f, 5f, 113f), parent);
    }

    private static void BuildLandingZone(Transform parent, Scene scene)
    {
        GameObject floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Dzeruza/MinimalScifiPack/3DModels/Floor/Prefabs/SM_Trim_Floor_8x8M_A2.prefab");

        Vector3 padCenter = new Vector3(LevelCenter.x + 32f, 0.03f, LevelCenter.z + 20f);
        for (int x = -2; x <= 2; x++)
        {
            for (int z = -2; z <= 2; z++)
            {
                InstantiatePrefab(floorPrefab, $"Landing Pad {x + 3}-{z + 3}",
                    padCenter + new Vector3(x * 8f, 0f, z * 8f), Quaternion.identity, parent, scene);
            }
        }

        CreatePointLight("Landing Beacon Orange", padCenter + new Vector3(-18f, 4f, 15f),
            new Color(1f, 0.42f, 0.08f), 16f, 4f, parent);
        CreatePointLight("Landing Beacon Blue", padCenter + new Vector3(18f, 4f, 15f),
            new Color(0.15f, 0.65f, 1f), 16f, 4f, parent);
    }

    private static void BuildSideHangars(Transform parent, Scene scene)
    {
        PlaceKenney("hangar_smallA", "West Side Hangar", new Vector3(420f, 0f, LevelCenter.z),
            4.5f, 90f, parent, scene, true);
        PlaceKenney("hangar_smallB", "East Side Hangar", new Vector3(548f, 0f, LevelCenter.z),
            4.5f, -90f, parent, scene, true);
    }

    private static void DressInterestObject(Scene scene)
    {
        GameObject interest = GameObject.Find("InterestObject");
        if (interest == null)
        {
            return;
        }

        MeshRenderer placeholder = interest.GetComponent<MeshRenderer>();
        if (placeholder != null)
        {
            placeholder.enabled = false;
        }

        GameObject beacon = InstantiatePrefab(LoadKenneyModel("machine_wireless"), "Astra Attraction Beacon",
            interest.transform.position, Quaternion.identity, interest.transform, scene, Vector3.one * 2.5f);
        if (beacon != null)
        {
            beacon.transform.localPosition = Vector3.zero;
            beacon.transform.localRotation = Quaternion.identity;
        }
        CreatePointLight("Attraction Beacon Glow", interest.transform.position + Vector3.up * 2.2f,
            new Color(1f, 0.1f, 0.8f), 12f, 4f, interest.transform);
    }

    private static void BuildMarkers(Transform parent)
    {
        CreateMarker("HERD START AREA", new Vector3(484f, 0.1f, 620f), parent);
        CreateMarker("INTEREST OBJECT AREA", new Vector3(483f, 0.1f, 639f), parent);
        CreateMarker("ALIEN APPROACH", new Vector3(456f, 0.1f, 615f), parent);
        CreateMarker("WIDE FLOCKING CORRIDOR", new Vector3(485f, 0.1f, 610f), parent);
    }

    private static void FrameGameplayCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        camera.transform.position = new Vector3(LevelCenter.x, 95f, LevelCenter.z - 105f);
        camera.transform.LookAt(LevelCenter + new Vector3(0f, 0f, 6f));
        camera.fieldOfView = 48f;
        camera.farClipPlane = 1000f;
    }

    private static GameObject CreateGroup(string name, Transform parent)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);
        return group;
    }

    private static GameObject InstantiatePrefab(GameObject prefab, string name, Vector3 position, Quaternion rotation,
        Transform parent, Scene scene)
    {
        return InstantiatePrefab(prefab, name, position, rotation, parent, scene, Vector3.one);
    }

    private static GameObject InstantiatePrefab(GameObject prefab, string name, Vector3 position, Quaternion rotation,
        Transform parent, Scene scene, Vector3 scale)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"Could not load prefab for {name}");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = name;
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.localScale = scale;
        instance.transform.SetParent(parent, true);
        return instance;
    }

    private static GameObject LoadKenneyModel(string modelName)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/ThirdParty/Kenney/SpaceKit/Models/{modelName}.fbx");
    }

    private static GameObject PlaceKenney(string modelName, string name, Vector3 position, float scale, float yaw,
        Transform parent, Scene scene, bool addCollider)
    {
        GameObject instance = InstantiatePrefab(LoadKenneyModel(modelName), name, position,
            Quaternion.Euler(0f, yaw, 0f), parent, scene, Vector3.one * scale);
        if (addCollider)
        {
            AddBoundsCollider(instance, 0.84f);
            SetLayerRecursively(instance, ObstacleLayer);
        }
        return instance;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static void AddBoundsCollider(GameObject instance, float horizontalInset)
    {
        if (instance == null)
        {
            return;
        }

        if (!TryGetLocalVisualBounds(instance, out Bounds localBounds))
        {
            return;
        }

        BoxCollider collider = instance.AddComponent<BoxCollider>();
        collider.center = localBounds.center;
        collider.size = new Vector3(
            localBounds.size.x * horizontalInset,
            localBounds.size.y,
            localBounds.size.z * horizontalInset);
    }

    private static bool TryGetLocalVisualBounds(GameObject instance, out Bounds localBounds)
    {
        localBounds = default;
        bool hasBounds = false;

        foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null)
            {
                continue;
            }

            Matrix4x4 toInstance = instance.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            EncapsulateTransformedBounds(filter.sharedMesh.bounds, toInstance, ref localBounds, ref hasBounds);
        }

        foreach (SkinnedMeshRenderer renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Matrix4x4 toInstance = instance.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            EncapsulateTransformedBounds(renderer.localBounds, toInstance, ref localBounds, ref hasBounds);
        }

        return hasBounds;
    }

    private static void EncapsulateTransformedBounds(Bounds source, Matrix4x4 matrix,
        ref Bounds destination, ref bool hasBounds)
    {
        Vector3 center = source.center;
        Vector3 extents = source.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 transformedCorner = matrix.MultiplyPoint3x4(corner);
                    if (!hasBounds)
                    {
                        destination = new Bounds(transformedCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        destination.Encapsulate(transformedCorner);
                    }
                }
            }
        }
    }

    private static void CreatePointLight(string name, Vector3 position, Color color, float range, float intensity,
        Transform parent)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.SetParent(parent, true);
        lightObject.transform.position = position;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
    }

    private static void CreateInvisibleBoundary(string name, Vector3 position, Vector3 size, Transform parent)
    {
        GameObject boundary = new GameObject(name);
        boundary.transform.SetParent(parent, true);
        boundary.transform.position = position;
        BoxCollider collider = boundary.AddComponent<BoxCollider>();
        collider.size = size;
    }

    private static GameObject CreateCube(string name, Vector3 localPosition, Vector3 scale, Material material, Transform parent)
    {
        return CreateCube(name, localPosition, scale, material, parent, Quaternion.identity, true);
    }

    private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material, Transform parent, Quaternion rotation)
    {
        return CreateCube(name, position, scale, material, parent, rotation, false);
    }

    private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material, Transform parent,
        Quaternion rotation, bool useLocalPosition)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        if (useLocalPosition)
        {
            cube.transform.localPosition = position;
            cube.transform.localRotation = rotation;
        }
        else
        {
            cube.transform.SetPositionAndRotation(position, rotation);
        }
        cube.transform.localScale = scale;

        if (material != null)
        {
            cube.GetComponent<Renderer>().sharedMaterial = material;
        }

        return cube;
    }

    private static void CreateMarker(string name, Vector3 position, Transform parent)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(parent, true);
        marker.transform.position = position;
    }

    private static void RemovePreviousPrototype()
    {
        GameObject existing = GameObject.Find("Exterior Prototype - Landing Site");
        if (existing == null)
        {
            existing = GameObject.Find("Exterior Prototype - Landing Site (Large)");
        }
        if (existing == null)
        {
            existing = GameObject.Find("Exterior Prototype - Landing Site (Kenney)");
        }
        if (existing == null)
        {
            existing = GameObject.Find("Exterior Mining Outpost (Kenney v2)");
        }
        if (existing == null)
        {
            existing = GameObject.Find("Exterior Mining Outpost (Kenney v3)");
        }
        if (existing == null)
        {
            existing = GameObject.Find("Exterior Mining Outpost (Kenney v4)");
        }
        if (existing == null)
        {
            existing = GameObject.Find("Exterior Mining Outpost (Kenney v5)");
        }
        if (existing == null)
        {
            existing = GameObject.Find("Exterior Mining Outpost (Kenney v6)");
        }
        if (existing == null)
        {
            existing = GameObject.Find("Exterior Mining Outpost (Kenney v7)");
        }
        if (existing == null)
        {
            existing = GameObject.Find("Exterior Mining Outpost (Kenney v8)");
        }
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }
    }
}
