using CaughtGame.Navigation;
using UnityEngine;

public sealed class FleeState : State
{
    private readonly FSMAgent agent;
    private ulong ownedRequestId;
    private bool ownsRequest;
    private bool failureReported;

    public FleeState(FSMAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter()
    {
        agent.NavigationIntent.CancelAll();
        ownsRequest = false;
        failureReported = false;
        agent.SetCurrentTarget(null);
        StopMovement();

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
                if (IsInsideSafeZone(agent.NavigationGrid, agent.transform.position))
                {
                    CancelOwnedRequest();
                    StopMovement();
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
        CancelOwnedRequest();
        StopMovement();
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

    private static bool IsInsideSafeZone(NavigationGrid grid, Vector3 worldPosition)
    {
        return grid != null &&
            grid.TryWorldToGrid(worldPosition, out int row, out int column) &&
            grid.TryGetCell(row, column, out NavigationGridCell cell) &&
            cell.Walkable && cell.Zone == NavigationGridZone.SafeZone;
    }
}
