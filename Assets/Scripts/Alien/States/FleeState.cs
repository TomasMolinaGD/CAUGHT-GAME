using System.Collections.Generic;
using CaughtGame.Navigation;
using UnityEngine;

public sealed class FleeState : State
{
    private const int MaxFailureReplans = 1;

    private readonly FSMAgent agent;
    private ulong ownedRequestId;
    private bool ownsRequest;
    private bool failureReported;
    private bool isWaitingInSafeZone;
    private float safeZoneWaitRemaining;
    private SafeZonePathSelection activeSelection;
    private PredatorFSM trackedThreat;
    private Vector3 plannedThreatPosition;
    private float replanCooldownRemaining;
    private int failureReplanCount;

    public FleeState(FSMAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter()
    {
        agent.NavigationIntent.CancelAll();
        ownsRequest = false;
        failureReported = false;
        activeSelection = null;
        trackedThreat = null;
        replanCooldownRemaining = 0f;
        failureReplanCount = 0;
        ResetSafeZoneWait();
        agent.SetCurrentTarget(null);
        StopMovement();

        TryRequestSafeZoneNavigation(false, "enter-flee");
    }

    private bool TryRequestSafeZoneNavigation(bool replacingActiveRequest, string trigger = null)
    {
        NavigationGrid grid = agent.NavigationGrid;
        bool trace = trigger != null && agent.LogFleeRouteDecisions;
        var controller = agent.NavigationController;
        var prior = activeSelection;
        ulong? priorId = ownsRequest ? ownedRequestId : (ulong?)null;
        int priorIndex = controller?.CurrentWaypointIndex ?? 0;
        bool awaitingPath = controller?.Status == HunterNavigationControllerStatus.WaitingForRequest;
        ulong? priorVersion = controller?.CurrentRouteVersion;
        bool hasThreat = TryGetCurrentThreat(
            out PredatorFSM threat, out Vector3 threatPosition, out float captureRadius);
        var away = trace && hasThreat ? new List<Vector2Int>() : null;
        SafeZonePathSelection selection = null;

        void Report(string result)
        {
            if (!trace) return;
            string resultingId = agent.NavigationIntent.TryGetCurrent(out var request)
                ? request.Id.ToString() : "none";
            Vector3 hunter = agent.transform.position;
            string alternative = !hasThreat || selection == null ? "n/a" : "none";
            if (selection != null && away != null)
                foreach (Vector2Int cell in away)
                    if (cell != selection.DestinationCell) { alternative = cell.ToString(); break; }

            int effectiveIndex = awaitingPath && prior?.Path.Waypoints.Count > 1 ? 1 : priorIndex;
            string active = DescribeRoute(prior, effectiveIndex, hunter, hasThreat, threatPosition,
                captureRadius);
            string candidate = DescribeRoute(selection, selection?.Path.Waypoints.Count > 1 ? 1 : 0,
                hunter, hasThreat, threatPosition, captureRadius);
            Debug.Log($"[FleeRoute] t={Time.time:F3} event={(replacingActiveRequest ? "replace" : "initial")} " +
                $"trigger={trigger} decision={result} requestBefore={priorId?.ToString() ?? "none"} " +
                $"requestAfter={resultingId} controllerVersionBefore={priorVersion?.ToString() ?? "n/a"} awaitingPathBefore={awaitingPath} " +
                $"hunter={hunter:F3} wolf={(hasThreat ? threat.name + "@" + threatPosition.ToString("F3") : "n/a")} " +
                $"active=[{active}] candidate=[{candidate}] otherAwayCell={alternative}", agent);
        }

        if (!agent.GridNavigationEnabled || agent.NavigationController == null || grid == null)
        {
            Report("unavailable");
            Fail("navigation is unavailable; Hunter remains stopped in Flee");
            return false;
        }

        bool selected = SafeZonePathSelector.TrySelect(grid, agent.transform.position,
            hasThreat ? threatPosition : (Vector3?)null, captureRadius, agent.FleeThreatSafetyMargin,
            out selection, prior?.ZoneId, away, prior?.DestinationCell);
        if (!selected)
        {
            if (replacingActiveRequest && ownsRequest)
            {
                CancelOwnedRequest();
                activeSelection = null;
            }
            Report("no-reachable-candidate");
            Fail("no reachable Safe Zone was found; Hunter remains stopped in Flee");
            return false;
        }

        Vector3 destination = grid.GridToWorldPosition(
            selection.DestinationCell.y, selection.DestinationCell.x);
        bool sameDestination = activeSelection != null && selection.DestinationCell == activeSelection.DestinationCell;
        if (replacingActiveRequest && ownsRequest && activeSelection != null)
        {
            if (sameDestination)
            {
                int index = awaitingPath && activeSelection.Path.Waypoints.Count > 1 ? 1 : priorIndex;
                RouteThreatScore remaining = SafeZonePathSelector.EvaluateThreat(activeSelection.Path,
                    agent.transform.position, threatPosition, captureRadius, agent.FleeThreatSafetyMargin, index);
                RouteThreatScore proposed = SafeZonePathSelector.EvaluateThreat(selection.Path,
                    agent.transform.position, threatPosition, captureRadius, agent.FleeThreatSafetyMargin,
                    selection.Path.Waypoints.Count > 1 ? 1 : 0);
                if (!proposed.SaferThan(remaining))
                {
                    Report("keep/same-destination");
                    RecordThreatObservation(threat, threatPosition, hasThreat);
                    return true;
                }
            }

            if (!agent.ReplaceNavigation(ownedRequestId, destination,
                    agent.WaypointCheckDistance, agent.FleeSpeedMultiplier, out ulong replacementId,
                    selection.Path))
            {
                Report("replace-request-refused");
                Fail("its navigation request could not be replaced; Hunter remains stopped in Flee");
                return false;
            }

            ownedRequestId = replacementId;
        }
        else
        {
            ownedRequestId = agent.RequestNavigation(
                destination, agent.WaypointCheckDistance, agent.FleeSpeedMultiplier, selection.Path);
        }

        ownsRequest = true;
        Report(!replacingActiveRequest ? "selected-least-cost" : sameDestination
            ? "replace-accepted/safer-route-same-cell" : "replace-accepted/least-cost-ranking");
        failureReported = false;
        activeSelection = selection;
        RecordThreatObservation(threat, threatPosition, hasThreat);
        return true;
    }

    public override void Update()
    {
        Tick(Time.deltaTime);
    }

    private void Tick(float deltaTime)
    {
        if (isWaitingInSafeZone)
        {
            UpdateSafeZoneWait(deltaTime);
            return;
        }

        replanCooldownRemaining = Mathf.Max(
            0f, replanCooldownRemaining - Mathf.Max(0f, deltaTime));

        if (!ownsRequest)
        {
            StopMovement();
            return;
        }

        if (!agent.NavigationIntent.TryGetCurrent(out HunterNavigationRequest request) ||
            request.Id != ownedRequestId)
        {
            agent.NavigationIntent.CancelAll();
            ownsRequest = false;
            Fail("its navigation request was replaced; Hunter remains stopped in Flee");
            return;
        }

        HunterNavigationController controller = agent.NavigationController;
        if (controller == null)
        {
            CancelOwnedRequest();
            Fail("navigation became unavailable; Hunter remains stopped in Flee");
            return;
        }

        switch (controller.Status)
        {
            case HunterNavigationControllerStatus.WaitingForRequest:
                TryReplanForThreat();
                break;
            case HunterNavigationControllerStatus.InProgress:
                // A validated route separates a later obstruction from a consecutive failed retry.
                failureReplanCount = 0;
                agent.Animation.SetMoving(true);
                TryReplanForThreat();
                break;
            case HunterNavigationControllerStatus.DestinationReached:
                if (agent.IsInsideNavigationSafeZone(agent.transform.position))
                {
                    BeginSafeZoneWait();
                }
                else
                {
                    CancelOwnedRequest();
                    Fail("navigation reported arrival outside a Safe Zone; Hunter remains stopped in Flee");
                }
                break;
            case HunterNavigationControllerStatus.NoPath:
            case HunterNavigationControllerStatus.InvalidGrid:
            case HunterNavigationControllerStatus.InvalidStart:
            case HunterNavigationControllerStatus.InvalidDestination:
            case HunterNavigationControllerStatus.StartNotTraversable:
            case HunterNavigationControllerStatus.DestinationNotTraversable:
            case HunterNavigationControllerStatus.Disabled:
                HunterNavigationControllerStatus failure = controller.Status;
                CancelOwnedRequest();
                if (failureReplanCount < MaxFailureReplans)
                {
                    failureReplanCount++;
                    if (!TryRequestSafeZoneNavigation(false))
                        Fail($"navigation failed with {failure}; Hunter remains stopped in Flee");
                }
                else
                {
                    Fail($"navigation failed with {failure}; Hunter remains stopped in Flee");
                }
                break;
        }
    }

    public override void Exit()
    {
        ResetSafeZoneWait();
        CancelOwnedRequest();
        activeSelection = null;
        trackedThreat = null;
        StopMovement();
    }

    private void BeginSafeZoneWait()
    {
        CancelOwnedRequest();
        StopMovement();
        isWaitingInSafeZone = true;
        safeZoneWaitRemaining = agent.SafeZoneWaitTime;
    }

    private void UpdateSafeZoneWait(float deltaTime)
    {
        StopMovement();
        if (!agent.IsInsideNavigationSafeZone(agent.transform.position))
        {
            ResetSafeZoneWait();
            activeSelection = null;
            trackedThreat = null;
            replanCooldownRemaining = 0f;
            TryRequestSafeZoneNavigation(false);
            return;
        }

        safeZoneWaitRemaining = Mathf.Max(0f, safeZoneWaitRemaining - Mathf.Max(0f, deltaTime));
        if (safeZoneWaitRemaining > 0f)
        {
            return;
        }

        PoliceState nextState = agent.GridNavigationEnabled && agent.HasReachedRequiredGathers
            ? PoliceState.Exit
            : agent.EngagementReturnState;
        StateMachine.ChangeState(nextState);
    }

    private void ResetSafeZoneWait()
    {
        isWaitingInSafeZone = false;
        safeZoneWaitRemaining = 0f;
    }

    private void CancelOwnedRequest()
    {
        if (!ownsRequest)
        {
            return;
        }

        agent.CancelNavigation(ownedRequestId);
        ownsRequest = false;
    }

    private void TryReplanForThreat()
    {
        if (!ownsRequest || replanCooldownRemaining > 0f ||
            !TryGetCurrentThreat(out PredatorFSM threat, out Vector3 threatPosition,
                out _))
        {
            return;
        }

        Vector3 displacement = threatPosition - plannedThreatPosition;
        displacement.y = 0f;
        if (threat == trackedThreat && displacement.sqrMagnitude < 0.000001f) return;
        float threshold = agent.FleeThreatReplanDistance;
        int index = agent.NavigationController.CurrentWaypointIndex;
        if (agent.NavigationController.Status == HunterNavigationControllerStatus.WaitingForRequest &&
            activeSelection?.Path.Waypoints.Count > 1) index = 1;
        RouteThreatScore currentScore = activeSelection != null
            ? SafeZonePathSelector.EvaluateThreat(
                activeSelection.Path, agent.transform.position, threatPosition,
                threat.CaptureRadius, agent.FleeThreatSafetyMargin,
                index)
            : RouteThreatScore.None;
        RouteThreatScore previous = activeSelection != null ? SafeZonePathSelector.EvaluateThreat(
            activeSelection.Path, agent.transform.position, plannedThreatPosition,
            threat.CaptureRadius, agent.FleeThreatSafetyMargin, index) : RouteThreatScore.None;
        bool becameUnsafe = !previous.CrossesDangerRadius && currentScore.CrossesDangerRadius;
        bool enteredCaptureRadius = !previous.CrossesCaptureRadius && currentScore.CrossesCaptureRadius;
        bool nowApproaching = previous.InitialApproach <= 0.0001f && currentScore.InitialApproach > 0.0001f;
        if (!becameUnsafe && !enteredCaptureRadius && !nowApproaching && threat == trackedThreat &&
            displacement.sqrMagnitude < threshold * threshold)
            return;

        TryRequestSafeZoneNavigation(true, enteredCaptureRadius ? "entered-capture-envelope"
            : becameUnsafe ? "entered-danger-envelope" : nowApproaching ? "approaching-wolf"
            : threat != trackedThreat ? "wolf-changed" : "wolf-moved");
    }

    private string DescribeRoute(SafeZonePathSelection route, int index, Vector3 hunter,
        bool hasThreat, Vector3 wolf, float radius)
    {
        if (route == null) return "none";
        string routeInfo = $"zone={route.ZoneId} cell={route.DestinationCell} destinationCost={route.DestinationCost:F3} " +
            $"pathCost={route.Path.TotalCost:F3} detour={route.UsesDetour}";
        if (index < 0 || index >= route.Path.Waypoints.Count) return routeInfo + " waypoint=n/a";
        Vector3 waypoint = route.Path.Waypoints[index];
        routeInfo += $" wp[{index}]={waypoint:F3}";
        if (!hasThreat) return routeInfo + " distances/approach/risk=n/a";
        RouteThreatScore score = SafeZonePathSelector.EvaluateThreat(route.Path, hunter, wolf, radius,
            agent.FleeThreatSafetyMargin, index);
        float minimum = SafeZonePathSelector.FirstSegmentMinimumDistance(hunter, waypoint, wolf);
        hunter.y = waypoint.y = wolf.y = 0f;
        return routeInfo + $" start={Vector3.Distance(hunter, wolf):F3} min={minimum:F3}" +
            $" end={Vector3.Distance(waypoint, wolf):F3} InitialApproach={score.InitialApproach:F3}" +
            $" totalRisk={score.TotalRisk:F3} captureCross={score.CrossesCaptureRadius} dangerCross={score.CrossesDangerRadius}";
    }

    private void RecordThreatObservation(
        PredatorFSM threat,
        Vector3 threatPosition,
        bool hasThreat)
    {
        trackedThreat = hasThreat ? threat : null;
        plannedThreatPosition = hasThreat ? threatPosition : Vector3.zero;
        replanCooldownRemaining = agent.FleeReplanCooldown;
    }

    private bool TryGetCurrentThreat(
        out PredatorFSM threat,
        out Vector3 threatPosition,
        out float captureRadius)
    {
        threat = agent.Perception != null ? agent.Perception.ClosestWolf : null;
        threatPosition = Vector3.zero;
        captureRadius = 0f;
        if (threat == null || !threat.gameObject.activeInHierarchy) return false;

        threatPosition = threat.transform.position;
        captureRadius = threat.CaptureRadius;
        return true;
    }

    private void StopMovement()
    {
        agent.Movement.Stop();
        agent.Animation.SetMoving(false);
        agent.Animation.SetAttacking(false);
    }

    private void Fail(string reason)
    {
        StopMovement();
        if (failureReported)
        {
            return;
        }

        failureReported = true;
        Debug.LogWarning($"Hunter Flee stopped: {reason}", agent);
    }

}
