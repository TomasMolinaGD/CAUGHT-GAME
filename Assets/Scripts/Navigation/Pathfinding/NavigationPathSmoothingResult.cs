namespace CaughtGame.Navigation
{
    /// <summary>Independent output of an optional path-smoothing query.</summary>
    public sealed class NavigationPathSmoothingResult
    {
        public NavigationPathSmoothingStatus Status { get; }
        public NavigationGrid Grid { get; }
        public NavigationAgentType AgentType { get; }
        public NavigationPathResult OriginalPath { get; }
        public NavigationPathResult SmoothedPath { get; }
        public NavigationPathValidationResult PhysicalValidation { get; }
        public bool IsUsable => Status == NavigationPathSmoothingStatus.Success &&
                                SmoothedPath != null && PhysicalValidation != null &&
                                PhysicalValidation.IsClear;
        public int OriginalWaypointCount => OriginalPath == null ? 0 : OriginalPath.Waypoints.Count;
        public int SmoothedWaypointCount => SmoothedPath == null ? 0 : SmoothedPath.Waypoints.Count;
        public float OriginalDistance => OriginalPath == null ? 0f : OriginalPath.TotalCost;
        public float SmoothedDistance => SmoothedPath == null ? 0f : SmoothedPath.TotalCost;

        private NavigationPathSmoothingResult(NavigationPathSmoothingStatus status,
            NavigationPathResult originalPath, NavigationPathResult smoothedPath,
            NavigationPathValidationResult physicalValidation, NavigationGrid grid = null,
            NavigationAgentType agentType = NavigationAgentType.Hunter)
        {
            Status = status;
            Grid = grid;
            AgentType = agentType;
            OriginalPath = originalPath;
            SmoothedPath = smoothedPath;
            PhysicalValidation = physicalValidation;
        }

        internal static NavigationPathSmoothingResult Success(NavigationGrid grid,
            NavigationAgentType agentType, NavigationPathResult originalPath,
            NavigationPathResult smoothedPath, NavigationPathValidationResult validation)
        {
            return new NavigationPathSmoothingResult(NavigationPathSmoothingStatus.Success,
                originalPath, smoothedPath, validation, grid, agentType);
        }

        internal static NavigationPathSmoothingResult Failure(NavigationPathSmoothingStatus status,
            NavigationPathResult originalPath, NavigationPathValidationResult validation)
        {
            return new NavigationPathSmoothingResult(status, originalPath, null, validation);
        }
    }
}
