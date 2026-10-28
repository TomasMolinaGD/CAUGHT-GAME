using System.Collections.Generic;
using UnityEngine;
using CaughtGame.Navigation;

/// <summary>
/// Owns the Hunter's final navigation request after the gather requirement is met.
/// It observes navigation only; HunterMovement remains the sole physical mover.
/// </summary>
public sealed class ExitState : State
{
    private readonly FSMAgent agent;
    private readonly List<Vector3> exitCandidates = new List<Vector3>();
    private ulong ownedRequestId;
    private int candidateIndex;
    private bool ownsRequest;

    public bool HasReachedExit { get; private set; }

    public ExitState(FSMAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter()
    {
        CancelOwnedRequest();
        HasReachedExit = false;
        candidateIndex = 0;
        exitCandidates.Clear();
        CollectExitCandidates();
        TryRequestNextDestination();
    }

    public override void Update()
    {
        HunterNavigationController controller = agent.NavigationController;
        if (controller == null || !ownsRequest ||
            !agent.NavigationIntent.TryGetCurrent(out HunterNavigationRequest request) ||
            request.Id != ownedRequestId)
        {
            agent.Animation.SetMoving(false);
            ownsRequest = false;
            return;
        }

        switch (controller.Status)
        {
            case HunterNavigationControllerStatus.InProgress:
                agent.Animation.SetMoving(true);
                break;
            case HunterNavigationControllerStatus.DestinationReached:
                agent.Animation.SetMoving(false);
                HasReachedExit = true;
                CancelOwnedRequest();
                break;
            case HunterNavigationControllerStatus.NoPath:
            case HunterNavigationControllerStatus.InvalidGrid:
            case HunterNavigationControllerStatus.InvalidStart:
            case HunterNavigationControllerStatus.InvalidDestination:
            case HunterNavigationControllerStatus.StartNotTraversable:
            case HunterNavigationControllerStatus.DestinationNotTraversable:
                agent.Animation.SetMoving(false);
                HandleFailedDestination();
                break;
        }
    }

    public override void Exit()
    {
        agent.Animation.SetMoving(false);
        CancelOwnedRequest();
    }

    private void CollectExitCandidates()
    {
        NavigationGrid grid = agent.NavigationGrid;
        if (grid == null)
        {
            return;
        }

        for (int row = 0; row < grid.Rows; row++)
        {
            for (int column = 0; column < grid.Columns; column++)
            {
                if (!grid.TryGetCell(row, column, out NavigationGridCell cell) ||
                    !cell.Walkable || cell.Zone != NavigationGridZone.Exit)
                {
                    continue;
                }

                exitCandidates.Add(grid.GridToWorldPosition(row, column));
            }
        }
    }

    private void TryRequestNextDestination()
    {
        if (candidateIndex >= exitCandidates.Count)
        {
            CancelOwnedRequest();
            agent.Animation.SetMoving(false);
            return;
        }

        CancelOwnedRequest();
        ownedRequestId = agent.RequestNavigation(
            exitCandidates[candidateIndex], agent.WaypointCheckDistance);
        ownsRequest = true;
    }

    private void HandleFailedDestination()
    {
        candidateIndex++;
        TryRequestNextDestination();
    }

    private void CancelOwnedRequest()
    {
        if (ownsRequest)
        {
            agent.CancelNavigation(ownedRequestId);
            ownsRequest = false;
        }
    }
}
