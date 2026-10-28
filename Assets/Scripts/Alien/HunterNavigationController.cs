using UnityEngine;
using CaughtGame.Navigation;

public enum HunterNavigationControllerStatus
{
    Disabled,
    WaitingForRequest,
    InProgress,
    DestinationReached,
    NoPath,
    InvalidGrid,
    InvalidStart,
    InvalidDestination,
    StartNotTraversable,
    DestinationNotTraversable
}

/// <summary>
/// Converts Hunter navigation intent into a cached grid route. It never moves the Hunter.
/// </summary>
public sealed class HunterNavigationController
{
    private readonly Transform hunterTransform;
    private readonly HunterNavigationIntent intent;
    private readonly NavigationGrid grid;
    private NavigationPathResult currentPath;
    private ulong processedRequestId;
    private bool hasProcessedRequest;
    private bool enabled;
    private bool repathRequested;
    private int nextWaypointIndex;
    private ulong routeVersion;
    private Vector3 terminalDestination;
    private bool hasTerminalDestination;

    public HunterNavigationController(Transform hunterTransform, HunterNavigationIntent intent,
        NavigationGrid grid)
    {
        this.hunterTransform = hunterTransform;
        this.intent = intent;
        this.grid = grid;
        Status = HunterNavigationControllerStatus.Disabled;
    }

    public bool Enabled => enabled;
    public HunterNavigationControllerStatus Status { get; private set; }
    public NavigationPathResult CurrentPath
    {
        get
        {
            SynchronizeIntentState();
            return currentPath;
        }
    }
    public int PathCalculationCount { get; private set; }
    public int CurrentWaypointIndex => nextWaypointIndex;
    public ulong CurrentRouteVersion => routeVersion;
    public bool HasTerminalDestination
    {
        get
        {
            SynchronizeIntentState();
            return hasTerminalDestination && currentPath != null;
        }
    }

    public void SetEnabled(bool value)
    {
        if (enabled == value)
        {
            return;
        }

        enabled = value;
        if (!enabled)
        {
            ClearRoute(HunterNavigationControllerStatus.Disabled);
        }
        else
        {
            hasProcessedRequest = false;
            repathRequested = true;
            Status = HunterNavigationControllerStatus.WaitingForRequest;
        }
    }

    public void RequestRepath()
    {
        repathRequested = true;
        if (enabled)
        {
            currentPath = null;
            nextWaypointIndex = 0;
            hasProcessedRequest = false;
            routeVersion++;
            Status = HunterNavigationControllerStatus.WaitingForRequest;
        }
    }

    public bool TryAdvanceWaypoint(ulong expectedRequestId, ulong expectedRouteVersion,
        int expectedWaypointIndex)
    {
        SynchronizeIntentState();
        if (Status != HunterNavigationControllerStatus.InProgress || currentPath == null ||
            intent == null || !intent.TryGetCurrent(out HunterNavigationRequest request) ||
            request.Id != expectedRequestId || routeVersion != expectedRouteVersion ||
            expectedWaypointIndex < 0 || expectedWaypointIndex >= currentPath.Waypoints.Count ||
            nextWaypointIndex != expectedWaypointIndex)
        {
            return false;
        }

        nextWaypointIndex++;
        if (nextWaypointIndex >= currentPath.Waypoints.Count)
        {
            Status = HasReachedDestination(request)
                ? HunterNavigationControllerStatus.DestinationReached
                : HunterNavigationControllerStatus.InProgress;
        }

        return true;
    }

    /// <summary>
    /// Processes a changed intent once. Repeated ticks do not run A* again unless a new
    /// request arrives or RequestRepath is explicitly called.
    /// </summary>
    public void Tick()
    {
        if (!enabled)
        {
            Status = HunterNavigationControllerStatus.Disabled;
            return;
        }

        if (intent == null || !intent.TryGetCurrent(out HunterNavigationRequest request))
        {
            ClearRoute(HunterNavigationControllerStatus.WaitingForRequest);
            return;
        }

        if (!hasProcessedRequest || request.Id != processedRequestId || repathRequested)
        {
            CalculateRoute(request);
        }
        else if (currentPath != null && HasReachedDestination(request))
        {
            Status = HunterNavigationControllerStatus.DestinationReached;
        }
    }

    public bool TryGetNextWaypoint(out Vector3 waypoint)
    {
        SynchronizeIntentState();
        if (Status != HunterNavigationControllerStatus.InProgress || currentPath == null ||
            nextWaypointIndex >= currentPath.Waypoints.Count)
        {
            waypoint = default;
            return false;
        }

        waypoint = currentPath.Waypoints[nextWaypointIndex];
        return true;
    }

    public bool TryGetTerminalDestination(ulong expectedRequestId, ulong expectedRouteVersion,
        out Vector3 destination)
    {
        SynchronizeIntentState();
        destination = default;
        if (!hasTerminalDestination || currentPath == null || intent == null ||
            !intent.TryGetCurrent(out HunterNavigationRequest request) ||
            request.Id != expectedRequestId || routeVersion != expectedRouteVersion ||
            Status != HunterNavigationControllerStatus.InProgress)
        {
            return false;
        }

        destination = terminalDestination;
        return true;
    }

    private void CalculateRoute(HunterNavigationRequest request)
    {
        processedRequestId = request.Id;
        hasProcessedRequest = true;
        repathRequested = false;
        currentPath = null;
        nextWaypointIndex = 0;
        hasTerminalDestination = false;
        routeVersion++;
        PathCalculationCount++;

        if (hunterTransform == null || grid == null || !HasValidGridData(grid))
        {
            Status = HunterNavigationControllerStatus.InvalidGrid;
            return;
        }

        if (!grid.TryWorldToGrid(hunterTransform.position, out int startRow, out int startColumn))
        {
            Status = HunterNavigationControllerStatus.InvalidStart;
            return;
        }

        if (!grid.TryWorldToGrid(request.Destination, out int destinationRow, out int destinationColumn))
        {
            Status = HunterNavigationControllerStatus.InvalidDestination;
            return;
        }

        currentPath = NavigationGridPathfinder.FindPath(
            grid, startRow, startColumn, destinationRow, destinationColumn, NavigationAgentType.Hunter);
        Status = MapStatus(currentPath.Status);
        if (Status != HunterNavigationControllerStatus.InProgress)
        {
            currentPath = null;
            return;
        }

        terminalDestination = request.Destination;
        Vector3 finalGridWaypoint = currentPath.Waypoints[currentPath.Waypoints.Count - 1];
        hasTerminalDestination = (finalGridWaypoint - terminalDestination).sqrMagnitude > 0.000001f;

        if (Status == HunterNavigationControllerStatus.InProgress && currentPath.Waypoints.Count > 1)
        {
            nextWaypointIndex = 1;
        }

        if (Status == HunterNavigationControllerStatus.InProgress && HasReachedDestination(request))
        {
            Status = HunterNavigationControllerStatus.DestinationReached;
        }
    }

    private bool HasReachedDestination(HunterNavigationRequest request)
    {
        if (hunterTransform == null)
        {
            return false;
        }

        Vector3 horizontalDelta = hunterTransform.position - request.Destination;
        horizontalDelta.y = 0f;
        return horizontalDelta.sqrMagnitude <= request.StoppingDistance * request.StoppingDistance;
    }

    private void ClearRoute(HunterNavigationControllerStatus status)
    {
        bool hadRoute = currentPath != null || hasProcessedRequest ||
            Status == HunterNavigationControllerStatus.InProgress;
        currentPath = null;
        nextWaypointIndex = 0;
        hasTerminalDestination = false;
        hasProcessedRequest = false;
        repathRequested = false;
        if (hadRoute)
        {
            routeVersion++;
        }
        Status = status;
    }

    private void SynchronizeIntentState()
    {
        if (!enabled || intent == null)
        {
            return;
        }

        if (!intent.TryGetCurrent(out HunterNavigationRequest request))
        {
            ClearRoute(HunterNavigationControllerStatus.WaitingForRequest);
            return;
        }

        if (hasProcessedRequest && request.Id != processedRequestId)
        {
            currentPath = null;
            nextWaypointIndex = 0;
            hasTerminalDestination = false;
            hasProcessedRequest = false;
            repathRequested = true;
            routeVersion++;
            Status = HunterNavigationControllerStatus.WaitingForRequest;
        }
    }

    private static HunterNavigationControllerStatus MapStatus(NavigationPathStatus status)
    {
        switch (status)
        {
            case NavigationPathStatus.Success: return HunterNavigationControllerStatus.InProgress;
            case NavigationPathStatus.NoPath: return HunterNavigationControllerStatus.NoPath;
            case NavigationPathStatus.InvalidGrid: return HunterNavigationControllerStatus.InvalidGrid;
            case NavigationPathStatus.InvalidStart: return HunterNavigationControllerStatus.InvalidStart;
            case NavigationPathStatus.InvalidDestination: return HunterNavigationControllerStatus.InvalidDestination;
            case NavigationPathStatus.StartNotTraversable: return HunterNavigationControllerStatus.StartNotTraversable;
            case NavigationPathStatus.DestinationNotTraversable:
                return HunterNavigationControllerStatus.DestinationNotTraversable;
            default: return HunterNavigationControllerStatus.InvalidGrid;
        }
    }

    private static bool HasValidGridData(NavigationGrid sourceGrid)
    {
        return sourceGrid.Columns > 0 && sourceGrid.Rows > 0 && sourceGrid.CellSize > 0f &&
            !float.IsNaN(sourceGrid.CellSize) && !float.IsInfinity(sourceGrid.CellSize) &&
            (long)sourceGrid.Columns * sourceGrid.Rows == sourceGrid.CellCount;
    }
}
