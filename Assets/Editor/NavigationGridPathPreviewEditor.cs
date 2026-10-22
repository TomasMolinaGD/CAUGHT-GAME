using CaughtGame.Navigation;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NavigationGridPathPreview))]
public sealed class NavigationGridPathPreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        NavigationGridPathPreview preview = (NavigationGridPathPreview)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "A* and smoothing run only after explicit button presses. The original A* route remains unchanged.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(preview.Grid == null ||
                                           (preview.MetricsRecorder != null && preview.MetricsRecorder.IsRecording)))
        {
            if (GUILayout.Button("Calculate A* Path"))
            {
                preview.CalculatePath();
                SceneView.RepaintAll();
                Repaint();
            }
        }

        NavigationPathResult result = preview.LastResult;
        if (result == null)
        {
            EditorGUILayout.LabelField("Result", "Not calculated");
            return;
        }

        EditorGUILayout.LabelField("Result", result.Status.ToString());
        EditorGUILayout.LabelField("Visited Cells", result.VisitedCellCount.ToString());
        if (result.Status == NavigationPathStatus.Success)
        {
            EditorGUILayout.LabelField("Total Cost", result.TotalCost.ToString("F3"));
            EditorGUILayout.LabelField("Steps", result.StepCount.ToString());
        }
        else if (result.Status == NavigationPathStatus.NoPath)
        {
            EditorGUILayout.HelpBox("No traversable path exists between the selected cells for this agent.",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox($"Path calculation could not run: {result.Status}.", MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Physical Clearance", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(result == null || result.Status != NavigationPathStatus.Success))
        {
            if (GUILayout.Button("Validate Physical Clearance"))
            {
                preview.ValidatePhysicalPath();
                SceneView.RepaintAll();
                Repaint();
            }
        }

        NavigationPathValidationResult validation = preview.LastPhysicalValidation;
        if (validation == null)
        {
            EditorGUILayout.LabelField("Validation", "Not run or stale");
        }
        else
        {
            MessageType messageType = validation.Status == NavigationPathValidationStatus.Clear
                ? MessageType.Info
                : MessageType.Warning;
            EditorGUILayout.HelpBox($"{validation.Status}: {validation.Message}", messageType);
            if (validation.FirstBlockingCollider != null)
            {
                EditorGUILayout.ObjectField("First Obstacle", validation.FirstBlockingCollider,
                    typeof(Collider), true);
                EditorGUILayout.LabelField("Problem Segment",
                    validation.FirstProblematicSegmentIndex.ToString());
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Optional Path Smoothing", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(result.Status != NavigationPathStatus.Success ||
                                           (preview.MetricsRecorder != null && preview.MetricsRecorder.IsRecording)))
        {
            if (GUILayout.Button("Smooth & Validate A* Path"))
            {
                preview.SmoothLastPath();
                SceneView.RepaintAll();
                Repaint();
            }
        }

        NavigationPathSmoothingResult smoothing = preview.LastSmoothingResult;
        if (smoothing == null)
        {
            EditorGUILayout.LabelField("Smoothing", "Not run or stale");
        }
        else
        {
            MessageType smoothingMessage = smoothing.IsUsable ? MessageType.Info : MessageType.Warning;
            string validationStatus = smoothing.PhysicalValidation == null
                ? "No physical result"
                : smoothing.PhysicalValidation.Status.ToString();
            EditorGUILayout.HelpBox(
                $"{smoothing.Status} — physical clearance: {validationStatus}. " +
                (smoothing.PhysicalValidation == null ? string.Empty : smoothing.PhysicalValidation.Message),
                smoothingMessage);
            EditorGUILayout.LabelField("Waypoints",
                $"{smoothing.OriginalWaypointCount} → {smoothing.SmoothedWaypointCount}");
            EditorGUILayout.LabelField("Distance",
                $"{smoothing.OriginalDistance:F3} → {smoothing.SmoothedDistance:F3} units");
            if (smoothing.PhysicalValidation != null &&
                smoothing.PhysicalValidation.FirstBlockingCollider != null)
            {
                EditorGUILayout.ObjectField("First Smoothing Obstacle",
                    smoothing.PhysicalValidation.FirstBlockingCollider, typeof(Collider), true);
                EditorGUILayout.LabelField("Problem Segment",
                    smoothing.PhysicalValidation.FirstProblematicSegmentIndex.ToString());
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Route Metrics", EditorStyles.boldLabel);
        NavigationRouteMetrics metrics = preview.LastMetrics;
        if (metrics == null)
        {
            EditorGUILayout.LabelField("Metrics", "Calculate a successful route first");
        }
        else
        {
            EditorGUILayout.LabelField("Route", metrics.Identifier);
            EditorGUILayout.LabelField("Agent", metrics.AgentType.ToString());
            EditorGUILayout.LabelField("Cells", $"{metrics.StartCell} → {metrics.DestinationCell}");
            EditorGUILayout.LabelField("A* Status / Steps", $"{metrics.PathStatus} / {metrics.StepCount}");
            EditorGUILayout.LabelField("Planned Distance", $"{metrics.PlannedDistance:F3} units");
            EditorGUILayout.LabelField("Configured Speed", $"{metrics.ConfiguredSpeed:F3} units/s");
            EditorGUILayout.LabelField("Theoretical Time", metrics.HasValidSpeed
                ? $"{metrics.TheoreticalTime:F3} s (distance / speed)"
                : "Invalid speed");
            EditorGUILayout.LabelField("Physical Validation", metrics.PhysicalValidationStatus.ToString());
            EditorGUILayout.LabelField("Observed Movement Time", metrics.HasActualTime
                ? $"{metrics.ActualMovementTime:F3} s"
                : "Not measured");
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Optional Play Mode Timing", EditorStyles.boldLabel);
        if (preview.MetricsRecorder == null)
        {
            EditorGUILayout.HelpBox(
                "A recorder is added only after an explicit click to a scene object. This does not create a GameObject or save the scene.",
                MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying || EditorUtility.IsPersistent(preview) ||
                                               !preview.gameObject.scene.IsValid()))
            {
                if (GUILayout.Button("Add Metrics Recorder to This Object"))
                {
                    NavigationRouteMetricsRecorder recorder = Undo.AddComponent<NavigationRouteMetricsRecorder>(
                        preview.gameObject);
                    Undo.RecordObject(preview, "Assign Navigation Metrics Recorder");
                    var serializedPreview = new SerializedObject(preview);
                    serializedPreview.FindProperty("metricsRecorder").objectReferenceValue = recorder;
                    serializedPreview.ApplyModifiedProperties();
                    EditorUtility.SetDirty(preview);
                }
            }
        }
        else
        {
            EditorGUILayout.ObjectField("Movement Test Agent", preview.MovementTestAgent,
                typeof(NavigationGridTestAgent), true);
            EditorGUILayout.ObjectField("Movement Follower", preview.EffectiveMovementFollower,
                typeof(PathFollower), true);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || result == null ||
                result.Status != NavigationPathStatus.Success || preview.EffectiveMovementFollower == null ||
                (preview.UseSmoothedPathForMovement && (smoothing == null || !smoothing.IsUsable)) ||
                preview.MetricsRecorder.IsRecording ||
                !Mathf.Approximately(preview.EffectiveMovementFollower.Speed, preview.ConfiguredSpeed)))
            {
                if (GUILayout.Button("Follow Current Path & Measure"))
                {
                    preview.StartMeasuredMovement();
                    Repaint();
                }
            }

            if (Application.isPlaying && preview.EffectiveMovementFollower != null &&
                !Mathf.Approximately(preview.EffectiveMovementFollower.Speed, preview.ConfiguredSpeed))
            {
                EditorGUILayout.HelpBox(
                    "The follower speed must match Configured Speed before starting a measured run.",
                    MessageType.Warning);
            }
        }
    }

    private void OnSceneGUI()
    {
        NavigationGridPathPreview preview = (NavigationGridPathPreview)target;
        if (preview.Grid == null) return;

        DrawCellLabel(preview.Grid, preview.StartRow, preview.StartColumn, "Start", new Color(0.15f, 0.85f, 0.25f));
        DrawCellLabel(preview.Grid, preview.DestinationRow, preview.DestinationColumn, "Destination",
            new Color(0.95f, 0.15f, 0.15f));
        NavigationPathValidationResult validation = preview.LastPhysicalValidation;
        if (validation != null && validation.FirstBlockingCollider != null)
        {
            Handles.Label(validation.FirstProblemPosition + Vector3.up * 0.5f,
                $"Blocked: {validation.FirstBlockingCollider.name}");
        }
        // This editor only draws labels; it intentionally does not consume Scene View input,
        // so the Navigation Grid Paint Mode and normal Unity selection retain control.
    }

    private static void DrawCellLabel(NavigationGrid grid, int row, int column, string label, Color color)
    {
        if (!grid.TryGetCell(row, column, out _)) return;
        Vector3 position = grid.GridToWorldPosition(row, column) + Vector3.up * 0.35f;
        Color previousColor = Handles.color;
        try
        {
            Handles.color = color;
            Handles.Label(position, $"{label} ({row}, {column})");
        }
        finally
        {
            Handles.color = previousColor;
        }
    }
}
