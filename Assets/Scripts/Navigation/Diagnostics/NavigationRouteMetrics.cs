using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>Immutable planned route metrics and optional observed movement duration.</summary>
    public sealed class NavigationRouteMetrics
    {
        public string Identifier { get; }
        public NavigationAgentType AgentType { get; }
        /// <summary>Grid cell using x=column and y=row, matching NavigationPathResult.Cells.</summary>
        public Vector2Int StartCell { get; }
        /// <summary>Grid cell using x=column and y=row, matching NavigationPathResult.Cells.</summary>
        public Vector2Int DestinationCell { get; }
        public NavigationPathStatus PathStatus { get; }
        public int StepCount { get; }
        public float PlannedDistance { get; }
        public float ConfiguredSpeed { get; }
        public float TheoreticalTime { get; }
        public bool HasValidSpeed { get; }
        public bool HasActualTime { get; }
        public float ActualMovementTime { get; }
        public NavigationPathValidationStatus PhysicalValidationStatus { get; }

        private NavigationRouteMetrics(string identifier, NavigationAgentType agentType,
            Vector2Int startCell, Vector2Int destinationCell, NavigationPathStatus pathStatus,
            int stepCount, float plannedDistance, float configuredSpeed, float theoreticalTime,
            bool hasValidSpeed, bool hasActualTime, float actualMovementTime,
            NavigationPathValidationStatus physicalValidationStatus)
        {
            Identifier = identifier ?? string.Empty;
            AgentType = agentType;
            StartCell = startCell;
            DestinationCell = destinationCell;
            PathStatus = pathStatus;
            StepCount = stepCount;
            PlannedDistance = plannedDistance;
            ConfiguredSpeed = configuredSpeed;
            TheoreticalTime = theoreticalTime;
            HasValidSpeed = hasValidSpeed;
            HasActualTime = hasActualTime;
            ActualMovementTime = actualMovementTime;
            PhysicalValidationStatus = physicalValidationStatus;
        }

        public static NavigationRouteMetrics FromPath(string identifier, NavigationAgentType agentType,
            NavigationPathResult path, float configuredSpeed,
            NavigationPathValidationStatus physicalValidationStatus)
        {
            bool hasPath = path != null && path.Status == NavigationPathStatus.Success &&
                           path.Waypoints.Count > 0 && path.Cells.Count > 0;
            bool validSpeed = !float.IsNaN(configuredSpeed) && !float.IsInfinity(configuredSpeed) &&
                              configuredSpeed > 0f;
            float distance = hasPath ? path.TotalCost : 0f;
            Vector2Int start = hasPath ? path.Cells[0] : new Vector2Int(-1, -1);
            Vector2Int destination = hasPath ? path.Cells[path.Cells.Count - 1] : new Vector2Int(-1, -1);
            return new NavigationRouteMetrics(identifier, agentType, start, destination,
                path == null ? NavigationPathStatus.InvalidGrid : path.Status,
                hasPath ? path.StepCount : 0, distance, configuredSpeed,
                hasPath && validSpeed ? distance / configuredSpeed : -1f,
                validSpeed, false, -1f, physicalValidationStatus);
        }

        public NavigationRouteMetrics WithActualMovementTime(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f) return this;
            return new NavigationRouteMetrics(Identifier, AgentType, StartCell, DestinationCell,
                PathStatus, StepCount, PlannedDistance, ConfiguredSpeed, TheoreticalTime,
                HasValidSpeed, true, seconds, PhysicalValidationStatus);
        }

        public NavigationRouteMetrics WithoutActualMovementTime()
        {
            if (!HasActualTime) return this;
            return new NavigationRouteMetrics(Identifier, AgentType, StartCell, DestinationCell,
                PathStatus, StepCount, PlannedDistance, ConfiguredSpeed, TheoreticalTime,
                HasValidSpeed, false, -1f, PhysicalValidationStatus);
        }
    }
}
