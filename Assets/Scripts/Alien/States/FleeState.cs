using CaughtGame.Navigation;
using UnityEngine;

public sealed class FleeState : State
{
    private readonly FSMAgent agent;
    private ulong ownedRequestId;
    private bool ownsRequest;
    private bool failureReported;
    private bool isWaitingInSafeZone;
    private float safeZoneWaitRemaining;

    public FleeState(FSMAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter()
    {
        agent.NavigationIntent.CancelAll();
        ownsRequest = false;
        failureReported = false;
        ResetSafeZoneWait();
        agent.SetCurrentTarget(null);
        StopMovement();

        TryRequestSafeZoneNavigation();
    }

    private void TryRequestSafeZoneNavigation()
    {
        NavigationGrid grid = agent.NavigationGrid;
        if (!agent.GridNavigationEnabled || agent.NavigationController == null || grid == null)
        {
            Fail("navigation is unavailable; Hunter remains stopped in Flee");
            return;
        }

        if (!SafeZonePathSelector.TrySelect(grid, agent.transform.position,
                out SafeZonePathSelection selection))
        {
            Fail("no reachable Safe Zone was found; Hunter remains stopped in Flee");
            return;
        }

        Vector3 destination = grid.GridToWorldPosition(
            selection.DestinationCell.y, selection.DestinationCell.x);
        ownedRequestId = agent.RequestNavigation(destination, agent.WaypointCheckDistance);
        ownsRequest = true;
    }

    public override void Update()
    {
        if (isWaitingInSafeZone)
        {
            UpdateSafeZoneWait(Time.deltaTime);
            return;
        }

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
                break;
            case HunterNavigationControllerStatus.InProgress:
                agent.Animation.SetMoving(true);
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
                Fail($"navigation failed with {failure}; Hunter remains stopped in Flee");
                break;
        }
    }

    public override void Exit()
    {
        ResetSafeZoneWait();
        CancelOwnedRequest();
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
            TryRequestSafeZoneNavigation();
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
