using System;
using System.Collections.Generic;
using CaughtGame.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CaughtGame.Navigation.Editor
{
    public sealed class GrayboxGeometryPlan
    {
        public BoxSpecification Floor { get; }
        public IReadOnlyList<BoxSpecification> Walls { get; }

        internal GrayboxGeometryPlan(BoxSpecification floor, IReadOnlyList<BoxSpecification> walls)
        {
            Floor = floor;
            Walls = walls;
        }
    }

    public struct BoxSpecification
    {
        public Vector3 Center { get; }
        public Vector3 Size { get; }

        public BoxSpecification(Vector3 center, Vector3 size)
        {
            Center = center;
            Size = size;
        }
    }

    public static class GrayboxGeometryBuilder
    {
        private const int GeneratorVersion = 1;
        private const string LevelName = "Level";
        private const string ContainerName = "GeneratedGeometry";
        private const string GenerationName = "GrayboxGeneration";

        public static GrayboxGeometryPlan CreatePlan(NavigationGrid grid, float wallHeight, float floorThickness)
        {
            ValidateGrid(grid, wallHeight, floorThickness);
            IReadOnlyList<GridCellRectangle> rectangles = BlockedCellRectangleMerger.Merge(grid);
            Vector3 scale = grid.transform.lossyScale;
            float cellSize = grid.CellSize;
            Vector3 floorCenter = grid.transform.TransformPoint(new Vector3(
                grid.Columns * cellSize * 0.5f, -floorThickness * 0.5f, grid.Rows * cellSize * 0.5f));
            Vector3 floorSize = new Vector3(grid.Columns * cellSize * scale.x,
                floorThickness * scale.y, grid.Rows * cellSize * scale.z);

            var walls = new List<BoxSpecification>(rectangles.Count);
            foreach (GridCellRectangle rectangle in rectangles)
            {
                Vector3 localCenter = new Vector3(
                    (rectangle.Column + rectangle.Columns * 0.5f) * cellSize,
                    wallHeight * 0.5f,
                    (rectangle.Row + rectangle.Rows * 0.5f) * cellSize);
                Vector3 center = grid.transform.TransformPoint(localCenter);
                Vector3 size = new Vector3(rectangle.Columns * cellSize * scale.x,
                    wallHeight * scale.y, rectangle.Rows * cellSize * scale.z);
                walls.Add(new BoxSpecification(center, size));
            }

            return new GrayboxGeometryPlan(new BoxSpecification(floorCenter, floorSize), walls.AsReadOnly());
        }

        public static bool TryFindExistingGeneration(Transform container, NavigationGrid grid,
            out GeneratedGeometryMarker marker, out string error)
        {
            marker = null;
            error = null;
            if (container == null || grid == null)
            {
                error = "A GeneratedGeometry container and NavigationGrid are required.";
                return false;
            }

            GeneratedGeometryMarker[] candidates = container.GetComponentsInChildren<GeneratedGeometryMarker>(true);
            foreach (GeneratedGeometryMarker candidate in candidates)
            {
                if (candidate.SourceGrid != grid)
                    continue;
                if (candidate.transform.parent != container)
                {
                    error = "A matching generated-geometry marker is nested unexpectedly; resolve the hierarchy manually.";
                    return false;
                }
                if (marker != null)
                {
                    error = "Multiple generated-geometry markers reference this grid; regeneration was stopped.";
                    return false;
                }
                marker = candidate;
            }
            return true;
        }

        public static bool GenerateInOpenScene(SceneAsset selectedScene, NavigationGrid grid,
            float wallHeight, float floorThickness, Material floorMaterial, Material wallMaterial,
            out string message)
        {
            message = null;
            if (!ValidateTargetScene(selectedScene, grid, out message))
                return false;

            return GenerateCore(grid.gameObject.scene, grid, wallHeight, floorThickness, floorMaterial, wallMaterial,
                marker => EditorUtility.DisplayDialog("Replace generated graybox?",
                    "A graybox generated from this NavigationGrid already exists. Replace only that marked generation?",
                    "Replace", "Cancel"), null, out message);
        }

        internal static bool GenerateInSceneForTesting(Scene targetScene, NavigationGrid grid, float wallHeight,
            float floorThickness, Material floorMaterial, Material wallMaterial,
            Action<GameObject> afterStagingForTesting, out string message)
        {
            return GenerateCore(targetScene, grid, wallHeight, floorThickness, floorMaterial, wallMaterial,
                marker => true, afterStagingForTesting, out message);
        }

        private static bool GenerateCore(Scene targetScene, NavigationGrid grid, float wallHeight, float floorThickness,
            Material floorMaterial, Material wallMaterial,
            Func<GeneratedGeometryMarker, bool> confirmReplacement,
            Action<GameObject> afterStagingForTesting, out string message)
        {
            message = null;
            if (!targetScene.IsValid() || !targetScene.isLoaded || grid == null ||
                grid.gameObject.scene != targetScene)
            {
                message = "The NavigationGrid must belong to the specified loaded scene.";
                return false;
            }

            GrayboxGeometryPlan plan;
            try
            {
                plan = CreatePlan(grid, wallHeight, floorThickness);
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return false;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Generate Graybox Geometry");
            if (!TryGetContainer(targetScene, out Transform container, out message))
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return false;
            }
            if (!TryFindExistingGeneration(grid.gameObject.scene, container, grid,
                    out GeneratedGeometryMarker oldMarker, out message))
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return false;
            }
            if (oldMarker != null && !TryValidateOwnedGeneration(oldMarker, out message))
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return false;
            }
            if (oldMarker != null && !confirmReplacement(oldMarker))
            {
                Undo.RevertAllDownToGroup(undoGroup);
                message = "Generation cancelled; existing geometry was preserved.";
                return false;
            }

            GameObject stagedRoot = null;
            try
            {
                stagedRoot = CreateGameObjectInScene("__GrayboxGeneration_Staging", targetScene);
                Undo.RegisterCreatedObjectUndo(stagedRoot, "Create staged graybox");
                Undo.SetTransformParent(stagedRoot.transform, container, "Parent staged graybox");
                stagedRoot.transform.SetPositionAndRotation(grid.transform.position, grid.transform.rotation);
                stagedRoot.transform.localScale = grid.transform.lossyScale;
                GeneratedGeometryMarker newMarker = Undo.AddComponent<GeneratedGeometryMarker>(stagedRoot);
                newMarker.Initialize(grid, GeneratorVersion);
                EditorUtility.SetDirty(newMarker);

                CreateBox(stagedRoot.transform, "Ground", plan.Floor, floorMaterial, newMarker);
                for (int index = 0; index < plan.Walls.Count; index++)
                    CreateBox(stagedRoot.transform, "Wall_" + index.ToString("D3"),
                        plan.Walls[index], wallMaterial, newMarker);

                if (!ValidateGeneratedRoot(stagedRoot, grid, plan, out message))
                    throw new InvalidOperationException(message);

                afterStagingForTesting?.Invoke(stagedRoot);

                if (oldMarker != null)
                    Undo.DestroyObjectImmediate(oldMarker.gameObject);
                Undo.RecordObject(stagedRoot, "Finalize graybox generation");
                stagedRoot.name = GenerationName;
                EditorSceneManager.MarkSceneDirty(targetScene);
                Undo.CollapseUndoOperations(undoGroup);
                message = "Graybox generated. The scene is modified in memory; save it explicitly when ready.";
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                message = "Graybox generation failed; the previous generation was preserved. " + exception.Message;
                return false;
            }
        }

        private static void CreateBox(Transform parent, string name, BoxSpecification specification,
            Material material, GeneratedGeometryMarker owner)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (box.scene != parent.gameObject.scene)
                SceneManager.MoveGameObjectToScene(box, parent.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(box, "Create graybox primitive");
            Undo.SetTransformParent(box.transform, parent, "Parent graybox primitive");
            box.name = name;
            box.transform.localPosition = parent.InverseTransformPoint(specification.Center);
            box.transform.localRotation = Quaternion.identity;
            Vector3 parentScale = parent.lossyScale;
            box.transform.localScale = new Vector3(specification.Size.x / parentScale.x,
                specification.Size.y / parentScale.y, specification.Size.z / parentScale.z);
            if (material != null)
            {
                MeshRenderer renderer = box.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
            }
            GeneratedGeometryPartMarker partMarker = Undo.AddComponent<GeneratedGeometryPartMarker>(box);
            partMarker.Initialize(owner);
            EditorUtility.SetDirty(partMarker);
        }

        private static bool ValidateGeneratedRoot(GameObject root, NavigationGrid grid,
            GrayboxGeometryPlan plan, out string error)
        {
            error = null;
            if (root == null || root.GetComponent<GeneratedGeometryMarker>() == null ||
                root.GetComponent<GeneratedGeometryMarker>().SourceGrid != grid)
            {
                error = "Generated root marker validation failed.";
                return false;
            }

            BoxCollider[] colliders = root.GetComponentsInChildren<BoxCollider>(true);
            if (colliders.Length != plan.Walls.Count + 1 || root.transform.childCount != plan.Walls.Count + 1)
            {
                error = "Generated collider count does not match the planned ground and walls.";
                return false;
            }

            for (int index = 0; index < colliders.Length; index++)
            {
                BoxCollider collider = colliders[index];
                BoxSpecification expected = index == 0 ? plan.Floor : plan.Walls[index - 1];
                if (collider.size.x <= 0f || collider.size.y <= 0f || collider.size.z <= 0f ||
                    Vector3.Distance(collider.transform.position, expected.Center) > 0.001f ||
                    (collider.transform.lossyScale - expected.Size).sqrMagnitude > 0.000001f ||
                    collider.transform.childCount != 0 ||
                    collider.GetComponent<GeneratedGeometryPartMarker>() == null ||
                    collider.GetComponent<GeneratedGeometryPartMarker>().Owner != root.GetComponent<GeneratedGeometryMarker>())
                {
                    error = "Generated collider dimensions or position do not match the validated geometry plan.";
                    return false;
                }
            }
            return true;
        }

        public static bool TryValidateOwnedGeneration(GeneratedGeometryMarker generation, out string error)
        {
            error = null;
            if (generation == null)
            {
                error = "A generated-geometry marker is required.";
                return false;
            }

            Transform root = generation.transform;
            for (int index = 0; index < root.childCount; index++)
            {
                Transform child = root.GetChild(index);
                GeneratedGeometryPartMarker part = child.GetComponent<GeneratedGeometryPartMarker>();
                if (part == null || part.Owner != generation || child.childCount != 0)
                {
                    error = "The previous generated root contains unmarked or nested objects. Move manual objects elsewhere before regenerating.";
                    return false;
                }
            }
            return true;
        }

        public static bool TryFindExistingGeneration(Scene scene, Transform container, NavigationGrid grid,
            out GeneratedGeometryMarker marker, out string error)
        {
            marker = null;
            error = null;
            if (!scene.IsValid() || !scene.isLoaded || container == null || grid == null)
            {
                error = "A loaded scene, GeneratedGeometry container, and NavigationGrid are required.";
                return false;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GeneratedGeometryMarker[] sceneMarkers = root.GetComponentsInChildren<GeneratedGeometryMarker>(true);
                foreach (GeneratedGeometryMarker candidate in sceneMarkers)
                {
                    if (candidate.SourceGrid != grid)
                        continue;
                    if (candidate.transform.parent != container)
                    {
                        error = "A marker for this grid exists outside the direct GeneratedGeometry children; resolve it manually.";
                        return false;
                    }
                    if (marker != null)
                    {
                        error = "Multiple generated-geometry markers reference this grid; regeneration was stopped.";
                        return false;
                    }
                    marker = candidate;
                }
            }
            return true;
        }

        private static bool ValidateTargetScene(SceneAsset selectedScene, NavigationGrid grid, out string error)
        {
            error = null;
            if (selectedScene == null || grid == null)
            {
                error = "Select both the target scene asset and a NavigationGrid.";
                return false;
            }

            string selectedPath = AssetDatabase.GetAssetPath(selectedScene).Replace('\\', '/');
            if (!string.Equals(selectedPath, "Assets/Scenes/Parcial2_Main_Grid.unity", StringComparison.Ordinal))
            {
                error = "For safety, this generator is restricted to Assets/Scenes/Parcial2_Main_Grid.unity.";
                return false;
            }
            if (EditorSceneManager.GetActiveScene().path != selectedPath || grid.gameObject.scene != EditorSceneManager.GetActiveScene())
            {
                error = "Open the selected main scene and select its NavigationGrid before generating.";
                return false;
            }
            if (!grid.gameObject.scene.IsValid() || !grid.gameObject.scene.isLoaded)
            {
                error = "The selected NavigationGrid must belong to the loaded target scene.";
                return false;
            }
            if (!IsSupportedGridTransform(grid.transform))
            {
                error = "NavigationGrid must have a positive, uniform world scale; non-uniform scale can distort BoxColliders.";
                return false;
            }
            return true;
        }

        private static bool TryGetContainer(Scene scene, out Transform container, out string error)
        {
            container = null;
            error = null;
            GameObject level = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != LevelName)
                    continue;
                if (level != null)
                {
                    error = "Multiple root objects named Level make the output hierarchy ambiguous.";
                    return false;
                }
                level = root;
            }

            if (level == null)
            {
                level = CreateGameObjectInScene(LevelName, scene);
                Undo.RegisterCreatedObjectUndo(level, "Create Level container");
            }
            if (!IsIdentityTransform(level.transform))
            {
                error = "Level must have an identity transform so generated BoxColliders remain axis-aligned to the grid frame.";
                return false;
            }

            Transform found = null;
            for (int index = 0; index < level.transform.childCount; index++)
            {
                Transform child = level.transform.GetChild(index);
                if (child.name != ContainerName)
                    continue;
                if (found != null)
                {
                    error = "Multiple GeneratedGeometry containers make the output hierarchy ambiguous.";
                    return false;
                }
                found = child;
            }

            if (found == null)
            {
                var created = CreateGameObjectInScene(ContainerName, scene);
                Undo.RegisterCreatedObjectUndo(created, "Create GeneratedGeometry container");
                Undo.SetTransformParent(created.transform, level.transform, "Parent GeneratedGeometry container");
                found = created.transform;
            }
            if (!IsIdentityTransform(found))
            {
                error = "GeneratedGeometry must have an identity transform; existing contents will not be moved.";
                return false;
            }
            container = found;
            return true;
        }

        private static GameObject CreateGameObjectInScene(string name, Scene scene)
        {
            var gameObject = new GameObject(name);
            if (gameObject.scene != scene)
                SceneManager.MoveGameObjectToScene(gameObject, scene);
            return gameObject;
        }

        private static void ValidateGrid(NavigationGrid grid, float wallHeight, float floorThickness)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));
            if (grid.Rows <= 0 || grid.Columns <= 0 || grid.CellSize <= 0f ||
                float.IsNaN(grid.CellSize) || float.IsInfinity(grid.CellSize))
                throw new InvalidOperationException("NavigationGrid dimensions and cell size must be valid.");
            if (wallHeight <= 0f || floorThickness <= 0f || float.IsNaN(wallHeight) ||
                float.IsNaN(floorThickness) || float.IsInfinity(wallHeight) || float.IsInfinity(floorThickness))
                throw new ArgumentOutOfRangeException(nameof(wallHeight), "Wall height and floor thickness must be finite and greater than zero.");
            if (!IsSupportedGridTransform(grid.transform))
                throw new InvalidOperationException("NavigationGrid must have a positive, uniform world scale.");
        }

        private static bool IsSupportedGridTransform(Transform transform)
        {
            Vector3 scale = transform.lossyScale;
            if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f ||
                !Mathf.Approximately(scale.x, scale.y) || !Mathf.Approximately(scale.y, scale.z))
                return false;

            Vector3 right = transform.TransformVector(Vector3.right).normalized;
            Vector3 up = transform.TransformVector(Vector3.up).normalized;
            Vector3 forward = transform.TransformVector(Vector3.forward).normalized;
            return Mathf.Abs(Vector3.Dot(right, up)) < 0.001f &&
                   Mathf.Abs(Vector3.Dot(right, forward)) < 0.001f &&
                   Mathf.Abs(Vector3.Dot(up, forward)) < 0.001f;
        }

        private static bool IsIdentityTransform(Transform transform) =>
            transform.position == Vector3.zero && Quaternion.Angle(transform.rotation, Quaternion.identity) < 0.001f &&
            (transform.lossyScale - Vector3.one).sqrMagnitude < 0.000001f;
    }
}
