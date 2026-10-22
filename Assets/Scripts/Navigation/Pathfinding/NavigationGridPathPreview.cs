using System;
using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>
    /// Stores explicit path-preview inputs and draws the most recently calculated result.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NavigationGridPathPreview : MonoBehaviour
    {
        [SerializeField] private NavigationGrid grid;
        [SerializeField, Min(0)] private int startRow;
        [SerializeField, Min(0)] private int startColumn;
        [SerializeField, Min(0)] private int destinationRow;
        [SerializeField, Min(0)] private int destinationColumn;
        [SerializeField] private NavigationAgentType agentType = NavigationAgentType.Hunter;
        [SerializeField] private GeneratedGeometryMarker geometryGeneration;
        [SerializeField] private string routeIdentifier = "Navigation Route";
        [SerializeField, Min(0.01f)] private float configuredSpeed = 3f;
        [SerializeField, Min(0.01f)] private float capsuleRadius = 0.5f;
        [SerializeField, Min(0.01f)] private float capsuleHeight = 2f;
        [Tooltip("Extra physical clearance used only for the smoothing query; it does not change agent dimensions.")]
        [SerializeField, Min(0f)] private float additionalClearance = 0.25f;
        [Tooltip("When enabled, measured test movement uses only a successfully smoothed and validated route.")]
        [SerializeField] private bool useSmoothedPathForMovement;
        [SerializeField] private LayerMask collisionLayers = Physics.AllLayers;
        [SerializeField] private PathFollower movementFollower;
        [SerializeField] private NavigationGridTestAgent movementTestAgent;
        [SerializeField] private NavigationRouteMetricsRecorder metricsRecorder;

        [NonSerialized] private NavigationPathResult lastResult;
        [NonSerialized] private NavigationGrid calculatedGrid;
        [NonSerialized] private int calculatedStartRow;
        [NonSerialized] private int calculatedStartColumn;
        [NonSerialized] private int calculatedDestinationRow;
        [NonSerialized] private int calculatedDestinationColumn;
        [NonSerialized] private NavigationAgentType calculatedAgentType;
        [NonSerialized] private NavigationPathValidationResult lastPhysicalValidation;
        [NonSerialized] private NavigationPathResult validatedPath;
        [NonSerialized] private GeneratedGeometryMarker validatedGeneration;
        [NonSerialized] private GeneratedGeometryMarker validatedExplicitGeneration;
        [NonSerialized] private float validatedRadius;
        [NonSerialized] private float validatedHeight;
        [NonSerialized] private int validatedLayerMask;
        [NonSerialized] private bool hasCachedValidation;
        [NonSerialized] private NavigationPathSmoothingResult lastSmoothingResult;
        [NonSerialized] private NavigationPathResult smoothedSourcePath;
        [NonSerialized] private NavigationGrid smoothedGrid;
        [NonSerialized] private GeneratedGeometryMarker smoothedGeneration;
        [NonSerialized] private GeneratedGeometryMarker smoothedExplicitGeneration;
        [NonSerialized] private NavigationAgentType smoothedAgentType;
        [NonSerialized] private float smoothedRadius;
        [NonSerialized] private float smoothedHeight;
        [NonSerialized] private float smoothedMargin;
        [NonSerialized] private int smoothedLayerMask;

        public NavigationGrid Grid => grid;
        public int StartRow => startRow;
        public int StartColumn => startColumn;
        public int DestinationRow => destinationRow;
        public int DestinationColumn => destinationColumn;
        public NavigationAgentType AgentType => agentType;
        public GeneratedGeometryMarker GeometryGeneration => geometryGeneration;
        public string RouteIdentifier => routeIdentifier;
        public float ConfiguredSpeed => configuredSpeed;
        public float CapsuleRadius => capsuleRadius;
        public float CapsuleHeight => capsuleHeight;
        public float AdditionalClearance => additionalClearance;
        public bool UseSmoothedPathForMovement => useSmoothedPathForMovement;
        public int CollisionLayers => collisionLayers.value;
        public PathFollower MovementFollower => movementFollower;
        public NavigationGridTestAgent MovementTestAgent => movementTestAgent;
        public PathFollower EffectiveMovementFollower => movementTestAgent != null
            ? movementTestAgent.Follower
            : movementFollower;
        public NavigationRouteMetricsRecorder MetricsRecorder => metricsRecorder;
        public NavigationPathResult LastResult => HasCurrentResult ? lastResult : null;
        public NavigationPathValidationResult LastPhysicalValidation => HasCurrentResult && hasCachedValidation &&
            validatedPath == lastResult &&
            validatedExplicitGeneration == geometryGeneration &&
            Mathf.Approximately(validatedRadius, capsuleRadius) &&
            Mathf.Approximately(validatedHeight, capsuleHeight) && validatedLayerMask == collisionLayers.value &&
            (validatedGeneration != null || (lastPhysicalValidation != null &&
                lastPhysicalValidation.Status != NavigationPathValidationStatus.Clear &&
                lastPhysicalValidation.Status != NavigationPathValidationStatus.Blocked))
                ? lastPhysicalValidation
                : null;
        public NavigationPathSmoothingResult LastSmoothingResult => HasCurrentSmoothingResult
            ? lastSmoothingResult
            : null;
        public NavigationRouteMetrics LastMetrics
        {
            get
            {
                NavigationPathResult path = LastResult;
                if (path == null) return null;
                NavigationRouteMetrics recorded = metricsRecorder == null ? null : metricsRecorder.CurrentMetrics;
                if (recorded != null && recorded.Identifier == routeIdentifier &&
                    recorded.AgentType == agentType && recorded.ConfiguredSpeed == configuredSpeed &&
                    recorded.StartCell == path.Cells[0] && recorded.DestinationCell == path.Cells[path.Cells.Count - 1])
                {
                    return recorded;
                }

                NavigationPathValidationResult validation = LastPhysicalValidation;
                return NavigationRouteMetrics.FromPath(routeIdentifier, agentType, path, configuredSpeed,
                    validation == null ? NavigationPathValidationStatus.NotValidated : validation.Status);
            }
        }
        public bool HasCurrentResult => lastResult != null && calculatedGrid == grid &&
            calculatedStartRow == startRow && calculatedStartColumn == startColumn &&
            calculatedDestinationRow == destinationRow && calculatedDestinationColumn == destinationColumn &&
            calculatedAgentType == agentType;

        private void Reset()
        {
            grid = GetComponent<NavigationGrid>();
        }

        public NavigationPathResult CalculatePath()
        {
            lastPhysicalValidation = null;
            validatedPath = null;
            hasCachedValidation = false;
            ClearSmoothingResult();
            if (metricsRecorder != null) metricsRecorder.Clear();
            lastResult = NavigationGridPathfinder.FindPath(grid, startRow, startColumn,
                destinationRow, destinationColumn, agentType);
            calculatedGrid = grid;
            calculatedStartRow = startRow;
            calculatedStartColumn = startColumn;
            calculatedDestinationRow = destinationRow;
            calculatedDestinationColumn = destinationColumn;
            calculatedAgentType = agentType;
            return lastResult;
        }

        /// <summary>Runs the physical queries once and caches their result for the Inspector and Gizmos.</summary>
        public NavigationPathValidationResult ValidatePhysicalPath()
        {
            NavigationPathResult path = LastResult;
            if (path == null || path.Status != NavigationPathStatus.Success)
            {
                lastPhysicalValidation = new NavigationPathValidationResult(
                    NavigationPathValidationStatus.InvalidPath,
                    "Calculate a successful route before validating physical clearance.");
                validatedPath = path;
                validatedGeneration = null;
                validatedExplicitGeneration = geometryGeneration;
                validatedRadius = capsuleRadius;
                validatedHeight = capsuleHeight;
                validatedLayerMask = collisionLayers.value;
                hasCachedValidation = true;
            }
            else
            {
                GeneratedGeometryMarker source = ResolveGeneration();
                lastPhysicalValidation = NavigationPathValidator.Validate(grid, source, path,
                    capsuleRadius, capsuleHeight, collisionLayers.value);
                validatedPath = path;
                validatedGeneration = source;
                validatedExplicitGeneration = geometryGeneration;
                validatedRadius = capsuleRadius;
                validatedHeight = capsuleHeight;
                validatedLayerMask = collisionLayers.value;
                hasCachedValidation = true;
            }

            return lastPhysicalValidation;
        }

        /// <summary>Explicitly smooths and physically validates the last successful A* result.</summary>
        public NavigationPathSmoothingResult SmoothLastPath()
        {
            NavigationPathResult path = LastResult;
            GeneratedGeometryMarker source = ResolveGeneration();
            lastSmoothingResult = NavigationPathSmoother.Smooth(grid, source, path, agentType,
                capsuleRadius, capsuleHeight, additionalClearance, collisionLayers.value);
            smoothedSourcePath = path;
            smoothedGrid = grid;
            smoothedGeneration = source;
            smoothedExplicitGeneration = geometryGeneration;
            smoothedAgentType = agentType;
            smoothedRadius = capsuleRadius;
            smoothedHeight = capsuleHeight;
            smoothedMargin = additionalClearance;
            smoothedLayerMask = collisionLayers.value;
            return lastSmoothingResult;
        }

        /// <summary>Starts the already-calculated route on the assigned follower and times its completion.</summary>
        public bool StartMeasuredMovement()
        {
            NavigationPathResult path = LastResult;
            PathFollower follower = EffectiveMovementFollower;
            NavigationPathSmoothingResult smoothing = null;
            if (useSmoothedPathForMovement)
            {
                // Revalidate against the current grid and colliders when movement is explicitly requested.
                smoothing = SmoothLastPath();
                if (smoothing == null || !smoothing.IsUsable) return false;
                if (movementTestAgent != null && (movementTestAgent.Grid != grid ||
                    movementTestAgent.AgentType != agentType ||
                    (movementFollower != null && movementFollower != movementTestAgent.Follower))) return false;
                path = smoothing.SmoothedPath;
            }

            if (path == null || path.Status != NavigationPathStatus.Success || follower == null ||
                metricsRecorder == null || metricsRecorder.IsRecording ||
                !Mathf.Approximately(follower.Speed, configuredSpeed))
            {
                return false;
            }

            bool started = useSmoothedPathForMovement && movementTestAgent != null
                ? movementTestAgent.FollowSmoothedPath(smoothing)
                : follower.FollowPath(path);
            if (!started) return false;
            NavigationPathValidationStatus physicalStatus = useSmoothedPathForMovement
                ? smoothing.PhysicalValidation.Status
                : LastPhysicalValidation == null
                    ? NavigationPathValidationStatus.NotValidated
                    : LastPhysicalValidation.Status;
            NavigationRouteMetrics metrics = NavigationRouteMetrics.FromPath(routeIdentifier,
                agentType, path, configuredSpeed, physicalStatus);
            return metricsRecorder.Begin(metrics, follower);
        }

        private bool HasCurrentSmoothingResult => lastSmoothingResult != null && HasCurrentResult &&
            lastSmoothingResult.OriginalPath == lastResult && smoothedSourcePath == lastResult &&
            smoothedGrid == grid && smoothedExplicitGeneration == geometryGeneration &&
            smoothedAgentType == agentType && Mathf.Approximately(smoothedRadius, capsuleRadius) &&
            Mathf.Approximately(smoothedHeight, capsuleHeight) &&
            Mathf.Approximately(smoothedMargin, additionalClearance) &&
            smoothedLayerMask == collisionLayers.value &&
            (smoothedGeneration != null || lastSmoothingResult.Status == NavigationPathSmoothingStatus.MissingGeometry ||
             lastSmoothingResult.Status == NavigationPathSmoothingStatus.InvalidPath ||
             lastSmoothingResult.Status == NavigationPathSmoothingStatus.InvalidConfiguration);

        private void ClearSmoothingResult()
        {
            lastSmoothingResult = null;
            smoothedSourcePath = null;
            smoothedGrid = null;
            smoothedGeneration = null;
            smoothedExplicitGeneration = null;
        }

        private GeneratedGeometryMarker ResolveGeneration()
        {
            if (geometryGeneration != null) return geometryGeneration;
            if (grid == null || !gameObject.scene.IsValid()) return null;

            GeneratedGeometryMarker[] candidates = FindObjectsByType<GeneratedGeometryMarker>(
                FindObjectsSortMode.None);
            GeneratedGeometryMarker match = null;
            foreach (GeneratedGeometryMarker candidate in candidates)
            {
                if (candidate.SourceGrid != grid || candidate.gameObject.scene != gameObject.scene) continue;
                if (match != null) return null;
                match = candidate;
            }

            return match;
        }

        private void OnDrawGizmos()
        {
            if (grid == null) return;

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = grid.transform.localToWorldMatrix;
            try
            {
                if (grid.TryGetCell(startRow, startColumn, out _))
                {
                    DrawCell(startRow, startColumn, new Color(0.15f, 0.85f, 0.25f, 0.7f));
                }

                if (grid.TryGetCell(destinationRow, destinationColumn, out _))
                {
                    DrawCell(destinationRow, destinationColumn, new Color(0.95f, 0.15f, 0.15f, 0.7f));
                }

                NavigationPathResult result = LastResult;
                if (result == null || result.Status != NavigationPathStatus.Success) return;

                NavigationPathValidationResult validation = LastPhysicalValidation;
                if (validation != null && validation.Status == NavigationPathValidationStatus.Clear)
                {
                    Gizmos.matrix = Matrix4x4.identity;
                    Gizmos.color = new Color(0.2f, 0.9f, 0.25f, 1f);
                    for (int index = 0; index < validation.Segments.Count; index++)
                    {
                        Gizmos.DrawLine(validation.Segments[index].Start, validation.Segments[index].End);
                    }
                }
                else if (validation != null && validation.Status == NavigationPathValidationStatus.Blocked)
                {
                    Gizmos.matrix = Matrix4x4.identity;
                    for (int index = 0; index < validation.Segments.Count; index++)
                    {
                        NavigationPathSegmentValidation segment = validation.Segments[index];
                        Gizmos.color = segment.IsClear ? new Color(0.2f, 0.9f, 0.25f, 1f) :
                            new Color(1f, 0.15f, 0.12f, 1f);
                        Gizmos.DrawLine(segment.Start, segment.End);
                    }

                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(validation.FirstProblemPosition, Mathf.Max(0.1f, capsuleRadius * 0.4f));
                }

                Gizmos.matrix = grid.transform.localToWorldMatrix;

                for (int index = 0; index < result.Cells.Count; index++)
                {
                    Vector2Int cell = result.Cells[index];
                    if ((cell.y == startRow && cell.x == startColumn) ||
                        (cell.y == destinationRow && cell.x == destinationColumn)) continue;
                    DrawCell(cell.y, cell.x, new Color(1f, 0.82f, 0.1f, 0.68f));
                }
                NavigationPathSmoothingResult smoothing = LastSmoothingResult;
                if (smoothing != null)
                {
                    Gizmos.matrix = Matrix4x4.identity;
                    DrawWorldPath(result, new Color(1f, 0.68f, 0.12f, 1f), grid.transform.up * 0.08f);
                    if (smoothing.IsUsable)
                    {
                        DrawWorldPath(smoothing.SmoothedPath, new Color(0.12f, 0.85f, 1f, 1f),
                            grid.transform.up * 0.16f);
                    }
                    else if (smoothing.PhysicalValidation != null &&
                             smoothing.PhysicalValidation.Status == NavigationPathValidationStatus.Blocked)
                    {
                        NavigationPathValidationResult smoothingValidation = smoothing.PhysicalValidation;
                        for (int index = 0; index < smoothingValidation.Segments.Count; index++)
                        {
                            NavigationPathSegmentValidation segment = smoothingValidation.Segments[index];
                            Gizmos.color = segment.IsClear ? new Color(0.2f, 0.9f, 0.25f, 1f) : Color.red;
                            Gizmos.DrawLine(segment.Start, segment.End);
                        }

                        Gizmos.color = Color.red;
                        Gizmos.DrawSphere(smoothingValidation.FirstProblemPosition,
                            Mathf.Max(0.1f, capsuleRadius * 0.4f));
                    }
                }
            }
            finally
            {
                Gizmos.matrix = previousMatrix;
                Gizmos.color = previousColor;
            }
        }

        private void DrawCell(int row, int column, Color color)
        {
            Gizmos.color = color;
            float size = grid.CellSize * 0.78f;
            Vector3 center = new Vector3((column + 0.5f) * grid.CellSize, 0.08f,
                (row + 0.5f) * grid.CellSize);
            Gizmos.DrawCube(center, new Vector3(size, 0.04f, size));
        }

        private static void DrawWorldPath(NavigationPathResult path, Color color, Vector3 offset)
        {
            Gizmos.color = color;
            for (int index = 0; index + 1 < path.Waypoints.Count; index++)
            {
                Gizmos.DrawLine(path.Waypoints[index] + offset, path.Waypoints[index + 1] + offset);
            }
        }

    }
}
