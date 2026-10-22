using System.Collections.Generic;
using CaughtGame.Navigation;
using CaughtGame.Navigation.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BlockedCellRectangleMergerTests
{
    [Test]
    public void Merge_IsDeterministicAndCoversEveryBlockedCellExactlyOnce()
    {
        NavigationGrid grid = CreateGrid(4, 3);
        try
        {
            grid.SetWalkable(0, 0, false);
            grid.SetWalkable(0, 1, false);
            grid.SetWalkable(1, 0, false);
            grid.SetWalkable(2, 2, false);
            grid.SetWalkable(2, 3, false);

            IReadOnlyList<GridCellRectangle> first = BlockedCellRectangleMerger.Merge(grid);
            IReadOnlyList<GridCellRectangle> second = BlockedCellRectangleMerger.Merge(grid);

            CollectionAssert.AreEqual(first, second);
            Assert.That(CountCovered(first), Is.EqualTo(5));
            AssertExactCoverage(grid, first);
        }
        finally
        {
            Object.DestroyImmediate(grid.gameObject);
        }
    }

    [Test]
    public void Merge_DoesNotIncludeWalkableCellsOrOverlapRectangles()
    {
        NavigationGrid grid = CreateGrid(3, 3);
        try
        {
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                    grid.SetWalkable(row, column, false);
            grid.SetWalkable(1, 1, true);

            IReadOnlyList<GridCellRectangle> rectangles = BlockedCellRectangleMerger.Merge(grid);
            Assert.That(CountCovered(rectangles), Is.EqualTo(8));
            AssertExactCoverage(grid, rectangles);
        }
        finally
        {
            Object.DestroyImmediate(grid.gameObject);
        }
    }

    [Test]
    public void GeometryPlan_UsesGridTransformAndExpectedBoxDimensions()
    {
        NavigationGrid grid = CreateGrid(2, 1);
        try
        {
            grid.transform.SetPositionAndRotation(new Vector3(10f, 2f, -4f), Quaternion.Euler(0f, 90f, 0f));
            grid.SetWalkable(0, 0, false);

            GrayboxGeometryPlan plan = GrayboxGeometryBuilder.CreatePlan(grid, 4f, 0.5f);

            Assert.That(plan.Walls.Count, Is.EqualTo(1));
            AssertVector3Within(plan.Walls[0].Size, new Vector3(4f, 4f, 4f), 0.0001f, "Wall size");
            AssertVector3Within(plan.Floor.Size, new Vector3(8f, 0.5f, 4f), 0.0001f, "Floor size");
            Assert.That(plan.Floor.Center, Is.EqualTo(grid.transform.TransformPoint(new Vector3(4f, -0.25f, 2f))));
            Assert.That(plan.Walls[0].Center, Is.EqualTo(grid.transform.TransformPoint(new Vector3(2f, 2f, 2f))));
        }
        finally
        {
            Object.DestroyImmediate(grid.gameObject);
        }
    }

    [Test]
    public void FindExistingGeneration_RejectsDuplicatesAndLeavesManualObjectsUntouched()
    {
        var gridObject = new GameObject("Grid");
        var container = new GameObject("GeneratedGeometry");
        var manual = new GameObject("ManualObject");
        manual.transform.SetParent(container.transform);
        var generatedA = new GameObject("GenerationA");
        generatedA.transform.SetParent(container.transform);
        GeneratedGeometryMarker markerA = generatedA.AddComponent<GeneratedGeometryMarker>();
        markerA.Initialize(gridObject.AddComponent<NavigationGrid>(), 1);
        var generatedB = new GameObject("GenerationB");
        generatedB.transform.SetParent(container.transform);
        GeneratedGeometryMarker markerB = generatedB.AddComponent<GeneratedGeometryMarker>();
        markerB.Initialize(gridObject.GetComponent<NavigationGrid>(), 1);

        try
        {
            Assert.That(GrayboxGeometryBuilder.TryFindExistingGeneration(container.transform,
                gridObject.GetComponent<NavigationGrid>(), out _, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(manual != null, Is.True);
            Assert.That(manual.transform.parent, Is.EqualTo(container.transform));
        }
        finally
        {
            Object.DestroyImmediate(container);
            Object.DestroyImmediate(gridObject);
        }
    }

    [Test]
    public void FindExistingGeneration_SelectsOnlyTheMarkedGenerationForTheSameGrid()
    {
        var gridObject = new GameObject("Grid");
        NavigationGrid grid = gridObject.AddComponent<NavigationGrid>();
        var container = new GameObject("GeneratedGeometry");
        var manual = new GameObject("ManualObject");
        manual.transform.SetParent(container.transform);
        var generated = new GameObject("OwnedGeneration");
        generated.transform.SetParent(container.transform);
        GeneratedGeometryMarker marker = generated.AddComponent<GeneratedGeometryMarker>();
        marker.Initialize(grid, 1);

        try
        {
            Assert.That(GrayboxGeometryBuilder.TryFindExistingGeneration(container.transform, grid,
                out GeneratedGeometryMarker found, out string error), Is.True, error);
            Assert.That(found, Is.EqualTo(marker));
            Assert.That(manual != null, Is.True);
            Assert.That(manual.transform.parent, Is.EqualTo(container.transform));
        }
        finally
        {
            Object.DestroyImmediate(container);
            Object.DestroyImmediate(gridObject);
        }
    }

    [Test]
    public void RegenerationSafety_RejectsManualObjectsInsideOwnedGeneration()
    {
        var gridObject = new GameObject("Grid");
        NavigationGrid grid = gridObject.AddComponent<NavigationGrid>();
        var container = new GameObject("GeneratedGeometry");
        var generated = new GameObject("OwnedGeneration");
        generated.transform.SetParent(container.transform);
        GeneratedGeometryMarker owner = generated.AddComponent<GeneratedGeometryMarker>();
        owner.Initialize(grid, 1);
        var generatedPart = new GameObject("Ground");
        generatedPart.transform.SetParent(generated.transform);
        GeneratedGeometryPartMarker partMarker = generatedPart.AddComponent<GeneratedGeometryPartMarker>();
        partMarker.Initialize(owner);
        var manual = new GameObject("Manual Child");
        manual.transform.SetParent(generated.transform);

        try
        {
            Assert.That(GrayboxGeometryBuilder.TryValidateOwnedGeneration(owner, out string error), Is.False);
            Assert.That(error, Does.Contain("unmarked"));
            Assert.That(manual != null, Is.True);
            Assert.That(manual.transform.parent, Is.EqualTo(generated.transform));
        }
        finally
        {
            Object.DestroyImmediate(container);
            Object.DestroyImmediate(gridObject);
        }
    }

    [Test]
    public void Generation_ReplacesOnlyOwnedOutputSupportsUndoAndPreservesPreviousOutputOnFailure()
    {
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        Scene previousScene = SceneManager.GetActiveScene();
        var originalScenes = new Scene[SceneManager.sceneCount];
        var originalDirtyStates = new bool[originalScenes.Length];
        for (int index = 0; index < originalScenes.Length; index++)
        {
            originalScenes[index] = SceneManager.GetSceneAt(index);
            originalDirtyStates[index] = originalScenes[index].isDirty;
        }

        Assert.That(originalSetup, Is.Not.Null,
            "Inspect the open Editor scene setup before creating the temporary test scene.");
        Assert.That(previousScene.IsValid() && previousScene.isLoaded, Is.True,
            "The active Editor scene must remain loaded while the test runs.");

        Undo.IncrementCurrentGroup();
        int testUndoGroup = Undo.GetCurrentGroup();
        Scene testScene = default;
        try
        {
            testScene = EditorSceneManager.NewPreviewScene();
            Assert.That(testScene.IsValid() && testScene.isLoaded && EditorSceneManager.IsPreviewScene(testScene), Is.True,
                "Unity could not create the isolated preview test scene.");
            GameObject gridObject = new GameObject("Temporary Grid");
            SceneManager.MoveGameObjectToScene(gridObject, testScene);
            NavigationGrid grid = gridObject.AddComponent<NavigationGrid>();
            Assert.That(grid.TryApplyGridSize(2, 1, 4f, true, out _), Is.True);
            Assert.That(grid.SetWalkable(0, 0, false), Is.True);

            Assert.That(GrayboxGeometryBuilder.GenerateInSceneForTesting(testScene, grid, 4f, 0.5f,
                null, null, null, out string firstMessage), Is.True, firstMessage);
            Transform container = FindGeneratedContainer(testScene);
            Assert.That(container, Is.Not.Null);
            var manual = new GameObject("Manual Sibling");
            SceneManager.MoveGameObjectToScene(manual, testScene);
            Undo.RegisterCreatedObjectUndo(manual, "Create manual test sibling");
            Undo.SetTransformParent(manual.transform, container, "Parent manual test sibling");
            Assert.That(FindGeneration(testScene), Is.Not.Null);

            Assert.That(GrayboxGeometryBuilder.GenerateInSceneForTesting(testScene, grid, 4f, 0.5f,
                null, null, null, out string secondMessage), Is.True, secondMessage);
            GeneratedGeometryMarker secondGeneration = FindGeneration(testScene);
            Assert.That(secondGeneration, Is.Not.Null);
            Assert.That(FindGenerations(testScene).Length, Is.EqualTo(1));
            Assert.That(manual != null && manual.transform.parent == container, Is.True);
            Assert.That(secondGeneration.GetComponentsInChildren<GeneratedGeometryPartMarker>().Length, Is.EqualTo(2));

            Undo.PerformUndo();
            GeneratedGeometryMarker restoredGeneration = FindGeneration(testScene);
            Assert.That(restoredGeneration, Is.Not.Null);
            Assert.That(restoredGeneration.SourceGrid, Is.EqualTo(grid));
            Assert.That(restoredGeneration.transform.childCount, Is.EqualTo(2));
            Assert.That(manual != null && manual.transform.parent == container, Is.True);

            Assert.That(GrayboxGeometryBuilder.GenerateInSceneForTesting(testScene, grid, 4f, 0.5f,
                null, null, _ => throw new System.InvalidOperationException("Injected staging failure."),
                out string failedMessage), Is.False);
            Assert.That(failedMessage, Does.Contain("previous generation was preserved"));
            Assert.That(FindGeneration(testScene).SourceGrid, Is.EqualTo(grid));
            Assert.That(FindGenerations(testScene).Length, Is.EqualTo(1));
            Assert.That(manual != null, Is.True);
        }
        finally
        {
            try
            {
                Undo.RevertAllDownToGroup(testUndoGroup);
            }
            finally
            {
                if (testScene.IsValid() && testScene.isLoaded)
                {
                    EditorSceneManager.ClosePreviewScene(testScene);
                }

                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(previousScene),
                    "The test must not change the active Editor scene.");

                Assert.That(EditorSceneManager.GetSceneManagerSetup().Length, Is.EqualTo(originalSetup.Length),
                    "The Editor scene setup must be restored after the temporary scene is closed.");
                for (int index = 0; index < originalScenes.Length; index++)
                {
                    Assert.That(originalScenes[index].IsValid() && originalScenes[index].isLoaded, Is.True,
                        "A scene that was open before the test must remain loaded.");
                    Assert.That(originalScenes[index].isDirty, Is.EqualTo(originalDirtyStates[index]),
                        "The test must not change the dirty state of a pre-existing scene.");
                }
            }
        }
    }

    private static void AssertVector3Within(Vector3 actual, Vector3 expected, float tolerance, string label)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance),
            $"{label} X differs: expected {expected.x:R}, actual {actual.x:R}.");
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance),
            $"{label} Y differs: expected {expected.y:R}, actual {actual.y:R}.");
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(tolerance),
            $"{label} Z differs: expected {expected.z:R}, actual {actual.z:R}.");
    }

    private static NavigationGrid CreateGrid(int columns, int rows)
    {
        var gameObject = new GameObject("Grid Test");
        NavigationGrid grid = gameObject.AddComponent<NavigationGrid>();
        Assert.That(grid.TryApplyGridSize(columns, rows, 4f, true, out _), Is.True);
        return grid;
    }

    private static Transform FindGeneratedContainer(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "Level")
                for (int index = 0; index < root.transform.childCount; index++)
                    if (root.transform.GetChild(index).name == "GeneratedGeometry")
                        return root.transform.GetChild(index);
        return null;
    }

    private static GeneratedGeometryMarker[] FindGenerations(Scene scene)
    {
        var markers = new List<GeneratedGeometryMarker>();
        foreach (GameObject root in scene.GetRootGameObjects())
            markers.AddRange(root.GetComponentsInChildren<GeneratedGeometryMarker>(true));
        return markers.ToArray();
    }

    private static GeneratedGeometryMarker FindGeneration(Scene scene)
    {
        GeneratedGeometryMarker[] markers = FindGenerations(scene);
        return markers.Length == 1 ? markers[0] : null;
    }

    private static int CountCovered(IReadOnlyList<GridCellRectangle> rectangles)
    {
        int count = 0;
        foreach (GridCellRectangle rectangle in rectangles)
            count += rectangle.Columns * rectangle.Rows;
        return count;
    }

    private static void AssertExactCoverage(NavigationGrid grid, IReadOnlyList<GridCellRectangle> rectangles)
    {
        var covered = new HashSet<Vector2Int>();
        foreach (GridCellRectangle rectangle in rectangles)
        {
            for (int row = rectangle.Row; row < rectangle.Row + rectangle.Rows; row++)
            {
                for (int column = rectangle.Column; column < rectangle.Column + rectangle.Columns; column++)
                {
                    Assert.That(covered.Add(new Vector2Int(column, row)), Is.True, "Rectangle overlap detected.");
                    Assert.That(grid.TryGetCell(row, column, out NavigationGridCell cell), Is.True);
                    Assert.That(cell.Walkable, Is.False, "A walkable cell was merged into wall geometry.");
                }
            }
        }

        for (int row = 0; row < grid.Rows; row++)
            for (int column = 0; column < grid.Columns; column++)
            {
                Assert.That(grid.TryGetCell(row, column, out NavigationGridCell cell), Is.True);
                Assert.That(covered.Contains(new Vector2Int(column, row)), Is.EqualTo(!cell.Walkable));
            }
    }
}
