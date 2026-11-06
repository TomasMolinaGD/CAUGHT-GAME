using System.Collections.Generic;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class NavigationPathDiagnosticsTests
{
    private GameObject gridObject;
    private GameObject generationObject;
    private readonly List<GameObject> extraObjects = new List<GameObject>();
    private NavigationGrid grid;
    private GeneratedGeometryMarker generation;

    [SetUp]
    public void SetUp()
    {
        gridObject = new GameObject("Navigation Diagnostics Test Grid");
        grid = gridObject.AddComponent<NavigationGrid>();
        Assert.That(grid.TryApplyGridSize(3, 3, 2f, true, out _), Is.True);

        generationObject = new GameObject("Navigation Diagnostics Test Geometry");
        generation = generationObject.AddComponent<GeneratedGeometryMarker>();
        generation.Initialize(grid, 1);
        AddPart("Ground", new Vector3(3f, -0.5f, 3f), new Vector3(20f, 0.5f, 20f), false);
        AddPart("Test Wall", new Vector3(30f, 2f, 30f), Vector3.one, false);
    }

    [TearDown]
    public void TearDown()
    {
        for (int index = extraObjects.Count - 1; index >= 0; index--)
        {
            if (extraObjects[index] != null) Object.DestroyImmediate(extraObjects[index]);
        }
        extraObjects.Clear();
        if (generationObject != null) Object.DestroyImmediate(generationObject);
        if (gridObject != null) Object.DestroyImmediate(gridObject);
    }

    [Test]
    public void Validate_ClearRoute_ReturnsClear()
    {
        NavigationPathResult path = FindPath(0, 0, 0, 2);

        NavigationPathValidationResult result = Validate(path);

        Assert.That(result.Status, Is.EqualTo(NavigationPathValidationStatus.Clear));
        Assert.That(result.Segments.Count, Is.EqualTo(path.StepCount));
        Assert.That(result.FirstBlockingCollider, Is.Null);
    }

    [Test]
    public void Validate_InitialOverlap_ReportsBlockingCollider()
    {
        BoxCollider wall = AddPart("Start Overlap", new Vector3(1f, 1f, 1f), Vector3.one, false);
        Physics.SyncTransforms();

        NavigationPathValidationResult result = Validate(FindPath(0, 0, 0, 2));

        Assert.That(result.Status, Is.EqualTo(NavigationPathValidationStatus.Blocked));
        Assert.That(result.FirstBlockingCollider, Is.SameAs(wall));
        Assert.That(result.FirstProblematicSegmentIndex, Is.EqualTo(0));
    }

    [Test]
    public void Validate_BlockedSegment_ReportsFirstBlockedSegment()
    {
        BoxCollider wall = AddPart("Segment Blocker", new Vector3(3f, 1f, 1f),
            new Vector3(0.2f, 2f, 0.2f), false);
        Physics.SyncTransforms();

        NavigationPathValidationResult result = Validate(FindPath(0, 0, 0, 2));

        Assert.That(result.Status, Is.EqualTo(NavigationPathValidationStatus.Blocked));
        Assert.That(result.FirstBlockingCollider, Is.SameAs(wall));
        Assert.That(result.FirstProblematicSegmentIndex, Is.EqualTo(0));
    }

    [Test]
    public void Validate_ObstacleNearTurn_RejectsInsufficientClearance()
    {
        NavigationPathResult path = FindPath(0, 0, 2, 2);
        int turnIndex = FindTurnIndex(path);
        Vector3 turn = path.Waypoints[turnIndex];
        BoxCollider wall = AddPart("Turn Clearance Blocker", turn + new Vector3(0.4f, 1f, 0.4f),
            new Vector3(0.2f, 2f, 0.2f), false);
        Physics.SyncTransforms();

        NavigationPathValidationResult result = Validate(path);

        Assert.That(result.Status, Is.EqualTo(NavigationPathValidationStatus.Blocked));
        Assert.That(result.FirstBlockingCollider, Is.SameAs(wall));
        Assert.That(result.FirstProblematicSegmentIndex, Is.GreaterThanOrEqualTo(turnIndex - 1));
    }

    [Test]
    public void Validate_TriggerOnRoute_IsIgnored()
    {
        AddPart("Trigger", new Vector3(3f, 1f, 1f), new Vector3(0.2f, 2f, 0.2f), true);
        Physics.SyncTransforms();

        Assert.That(Validate(FindPath(0, 0, 0, 2)).Status,
            Is.EqualTo(NavigationPathValidationStatus.Clear));
    }

    [Test]
    public void Validate_GroundOverlappingRoute_IsIgnored()
    {
        AddPart("Ground", new Vector3(3f, 0f, 1f), new Vector3(20f, 1f, 20f), false);
        Physics.SyncTransforms();

        Assert.That(Validate(FindPath(0, 0, 0, 2)).Status,
            Is.EqualTo(NavigationPathValidationStatus.Clear));
    }

    [Test]
    public void Validate_UnownedColliderOnRoute_IsIgnored()
    {
        GameObject unrelatedObject = new GameObject("Unrelated Collider");
        extraObjects.Add(unrelatedObject);
        unrelatedObject.transform.position = new Vector3(3f, 1f, 1f);
        BoxCollider unrelated = unrelatedObject.AddComponent<BoxCollider>();
        unrelated.size = new Vector3(0.2f, 2f, 0.2f);
        GameObject unmarkedChild = new GameObject("Hand Authored Child Collider");
        extraObjects.Add(unmarkedChild);
        unmarkedChild.transform.SetParent(generationObject.transform.Find("Test Wall"), false);
        unmarkedChild.transform.position = new Vector3(3f, 1f, 1f);
        BoxCollider childCollider = unmarkedChild.AddComponent<BoxCollider>();
        childCollider.size = new Vector3(0.2f, 2f, 0.2f);
        Physics.SyncTransforms();

        NavigationPathValidationResult result = Validate(FindPath(0, 0, 0, 2));

        Assert.That(result.Status, Is.EqualTo(NavigationPathValidationStatus.Clear));
    }

    [Test]
    public void Validate_MissingGeneration_FailsClosed()
    {
        NavigationPathValidationResult result = NavigationPathValidator.Validate(
            grid, null, FindPath(0, 0, 0, 2), 0.5f, 2f);

        Assert.That(result.Status, Is.EqualTo(NavigationPathValidationStatus.MissingGeometry));
    }

    [Test]
    public void Preview_CachesMissingGeometryResultForInspectorAndMetrics()
    {
        GameObject otherGridObject = new GameObject("Different Source Grid");
        extraObjects.Add(otherGridObject);
        NavigationGrid otherGrid = otherGridObject.AddComponent<NavigationGrid>();
        var wrongGenerationObject = new GameObject("Wrong Source Generation");
        extraObjects.Add(wrongGenerationObject);
        GeneratedGeometryMarker wrongGeneration = wrongGenerationObject.AddComponent<GeneratedGeometryMarker>();
        wrongGeneration.Initialize(otherGrid, 1);

        GameObject previewObject = new GameObject("Navigation Diagnostics Preview");
        extraObjects.Add(previewObject);
        NavigationGridPathPreview preview = previewObject.AddComponent<NavigationGridPathPreview>();
        var serializedPreview = new SerializedObject(preview);
        serializedPreview.FindProperty("grid").objectReferenceValue = grid;
        serializedPreview.FindProperty("startRow").intValue = 0;
        serializedPreview.FindProperty("startColumn").intValue = 0;
        serializedPreview.FindProperty("destinationRow").intValue = 0;
        serializedPreview.FindProperty("destinationColumn").intValue = 2;
        serializedPreview.FindProperty("geometryGeneration").objectReferenceValue = wrongGeneration;
        serializedPreview.ApplyModifiedPropertiesWithoutUndo();

        preview.CalculatePath();
        NavigationPathValidationResult returned = preview.ValidatePhysicalPath();

        Assert.That(returned.Status, Is.EqualTo(NavigationPathValidationStatus.MissingGeometry));
        Assert.That(preview.LastPhysicalValidation, Is.SameAs(returned));
        Assert.That(preview.LastMetrics.PhysicalValidationStatus,
            Is.EqualTo(NavigationPathValidationStatus.MissingGeometry));
    }

    [Test]
    public void Validate_InvalidCapsuleConfiguration_IsRejected()
    {
        NavigationPathValidationResult result = NavigationPathValidator.Validate(
            grid, generation, FindPath(0, 0, 0, 2), 0.5f, 0.8f);

        Assert.That(result.Status, Is.EqualTo(NavigationPathValidationStatus.InvalidConfiguration));
    }

    [Test]
    public void Validate_FailedPath_IsReportedAsInvalidPath()
    {
        Assert.That(grid.SetWalkable(0, 1, false), Is.True);
        Assert.That(grid.SetWalkable(1, 1, false), Is.True);
        Assert.That(grid.SetWalkable(2, 1, false), Is.True);
        NavigationPathResult failedPath = FindPath(1, 0, 1, 2);

        NavigationPathValidationResult result = Validate(failedPath);

        Assert.That(failedPath.Status, Is.EqualTo(NavigationPathStatus.NoPath));
        Assert.That(result.Status, Is.EqualTo(NavigationPathValidationStatus.InvalidPath));
    }

    [Test]
    public void Metrics_FromPath_ReportsWorldDistanceAndTheoreticalTime()
    {
        NavigationPathResult path = FindPath(0, 0, 0, 2);
        NavigationPathValidationResult validation = Validate(path);

        NavigationRouteMetrics metrics = NavigationRouteMetrics.FromPath(
            "test-route", NavigationAgentType.Hunter, path, 2f, validation.Status);

        Assert.That(metrics.Identifier, Is.EqualTo("test-route"));
        Assert.That(metrics.StartCell, Is.EqualTo(new Vector2Int(0, 0)));
        Assert.That(metrics.DestinationCell, Is.EqualTo(new Vector2Int(2, 0)));
        Assert.That(metrics.StepCount, Is.EqualTo(2));
        Assert.That(metrics.PlannedDistance, Is.EqualTo(4f).Within(0.0001f));
        Assert.That(metrics.TheoreticalTime, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(metrics.HasActualTime, Is.False);
    }

    [Test]
    public void MetricsRecorder_RepeatedRunStartsWithoutPreviousActualTime()
    {
        NavigationPathResult path = FindPath(0, 0, 0, 2);
        var followerObject = new GameObject("Measured Test Follower");
        extraObjects.Add(followerObject);
        followerObject.transform.position = path.Waypoints[0] + Vector3.up;
        PathFollower follower = followerObject.AddComponent<PathFollower>();
        follower.Speed = 2f;
        var recorderObject = new GameObject("Metrics Recorder Test");
        extraObjects.Add(recorderObject);
        NavigationRouteMetricsRecorder recorder = recorderObject.AddComponent<NavigationRouteMetricsRecorder>();
        NavigationRouteMetrics metrics = NavigationRouteMetrics.FromPath("timed-route",
            NavigationAgentType.Hunter, path, follower.Speed, NavigationPathValidationStatus.Clear);

        Assert.That(follower.FollowPath(path), Is.True);
        Assert.That(recorder.Begin(metrics, follower), Is.True);
        follower.Advance(10f);
        follower.Advance(10f);
        follower.Advance(10f);
        Assert.That(recorder.RecordCompletionIfReady(), Is.True);
        NavigationRouteMetrics completed = recorder.CurrentMetrics;
        Assert.That(completed.HasActualTime, Is.True);

        Assert.That(follower.FollowPath(path), Is.True);
        Assert.That(recorder.Begin(completed, follower), Is.True);

        Assert.That(recorder.IsRecording, Is.True);
        Assert.That(recorder.CurrentMetrics.HasActualTime, Is.False);
    }

    private NavigationPathResult FindPath(int startRow, int startColumn, int endRow, int endColumn)
    {
        return NavigationGridPathfinder.FindPath(grid, startRow, startColumn,
            endRow, endColumn, NavigationAgentType.Hunter);
    }

    private NavigationPathValidationResult Validate(NavigationPathResult path)
    {
        return NavigationPathValidator.Validate(grid, generation, path, 0.5f, 2f);
    }

    private BoxCollider AddPart(string name, Vector3 position, Vector3 size, bool isTrigger)
    {
        var partObject = new GameObject(name);
        extraObjects.Add(partObject);
        partObject.transform.SetParent(generationObject.transform, false);
        partObject.transform.position = position;
        GeneratedGeometryPartMarker part = partObject.AddComponent<GeneratedGeometryPartMarker>();
        part.Initialize(generation);
        BoxCollider collider = partObject.AddComponent<BoxCollider>();
        collider.size = size;
        collider.isTrigger = isTrigger;
        return collider;
    }

    private static int FindTurnIndex(NavigationPathResult path)
    {
        for (int index = 1; index < path.Waypoints.Count - 1; index++)
        {
            Vector3 incoming = path.Waypoints[index] - path.Waypoints[index - 1];
            Vector3 outgoing = path.Waypoints[index + 1] - path.Waypoints[index];
            if (Vector3.Dot(incoming.normalized, outgoing.normalized) < 0.99f)
            {
                return index;
            }
        }

        Assert.Fail("Expected the selected path to contain a turn.");
        return -1;
    }
}
