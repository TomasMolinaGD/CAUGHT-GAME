using System.Collections.Generic;
using UnityEngine;
using CaughtGame.Navigation;

/// <summary>
/// Requests and advances Hunter exploration in the configured Hunting Ground.
/// It owns intent only; HunterMovement remains the sole physical mover.
/// </summary>
public sealed class SearchState : State
{
    private const int MaxFailedRequests = 4;

    private readonly FSMAgent agent;
    private readonly HuntingGroundRegion region;
    private readonly SearchDestinationSelector selector;
    private readonly float minimumDistanceMultiplier;
    private readonly float maximumDistanceMultiplier;
    private readonly int selectionAttempts;
    private readonly HashSet<int> rejectedCandidates = new HashSet<int>();
    private ulong ownedRequestId;
    private bool ownsRequest;
    private int failedRequests;
    private int lastDestinationIndex = -1;

    public SearchState(FSMAgent agent, StateMachine stateMachine, HuntingGroundRegion region,
        int randomSeed = 417, float minimumDistanceMultiplier = 0.75f,
        float maximumDistanceMultiplier = 2f, int selectionAttempts = 24)
        : base(stateMachine)
    {
        this.agent = agent;
        this.region = region;
        selector = new SearchDestinationSelector(randomSeed);
        this.minimumDistanceMultiplier = Mathf.Max(0f, minimumDistanceMultiplier);
        this.maximumDistanceMultiplier = Mathf.Max(0.1f, maximumDistanceMultiplier);
        this.selectionAttempts = Mathf.Max(1, selectionAttempts);
    }

    public override void Enter()
    {
        CancelOwnedRequest();
        agent.Animation.SetAttacking(false);
        ownsRequest = false;
        failedRequests = 0;
        lastDestinationIndex = -1;
        rejectedCandidates.Clear();
        TryRequestNextDestination();
    }

    public override void Update()
    {
        if (TryTransitionToGather() || TryTransitionToAttack())
        {
            return;
        }

        if (region == null || region.SourceGrid == null)
        {
            agent.Animation.SetMoving(false);
            return;
        }

        HunterNavigationController controller = agent.NavigationController;
        if (controller == null || !ownsRequest ||
            !agent.NavigationIntent.TryGetCurrent(out HunterNavigationRequest request) ||
            request.Id != ownedRequestId)
        {
            if (ownsRequest && (!agent.NavigationIntent.TryGetCurrent(out HunterNavigationRequest current) ||
                current.Id != ownedRequestId))
            {
                ownsRequest = false;
            }
            agent.Animation.SetMoving(false);
            return;
        }

        switch (controller.Status)
        {
            case HunterNavigationControllerStatus.InProgress:
                agent.Animation.SetMoving(true);
                break;
            case HunterNavigationControllerStatus.DestinationReached:
                agent.Animation.SetMoving(false);
                failedRequests = 0;
                rejectedCandidates.Clear();
                TryRequestNextDestination();
                break;
            case HunterNavigationControllerStatus.NoPath:
            case HunterNavigationControllerStatus.InvalidGrid:
            case HunterNavigationControllerStatus.InvalidStart:
            case HunterNavigationControllerStatus.InvalidDestination:
            case HunterNavigationControllerStatus.StartNotTraversable:
            case HunterNavigationControllerStatus.DestinationNotTraversable:
                agent.Animation.SetMoving(false);
                HandleFailedRequest();
                break;
        }
    }

    private bool TryTransitionToGather()
    {
        HunterPerception perception = agent.Perception;
        BoidLife target = perception?.ClosestDeadBoid;
        if (target == null || !target.CanBeCollected || !perception.IsDeadBoidDetected(target))
        {
            return false;
        }

        agent.SetCurrentTarget(target);
        return StateMachine.ChangeState(PoliceState.Gather);
    }

    private bool TryTransitionToAttack()
    {
        HunterPerception perception = agent.Perception;
        BoidLife target = perception?.ClosestLivingBoid;
        if (target == null || !target.IsAlive || !perception.IsLivingBoidDetected(target) ||
            !agent.CanStartAttack())
        {
            return false;
        }

        agent.SetCurrentTarget(target);
        return StateMachine.ChangeState(PoliceState.Attack);
    }

    public override void Exit()
    {
        agent.Animation.SetMoving(false);
        if (ownsRequest)
        {
            CancelOwnedRequest();
        }

        ownsRequest = false;
    }

    private void HandleFailedRequest()
    {
        if (failedRequests >= MaxFailedRequests)
        {
            CancelOwnedRequest();
            return;
        }

        failedRequests++;
        if (lastDestinationIndex >= 0)
        {
            rejectedCandidates.Add(lastDestinationIndex);
        }
        TryRequestNextDestination();
    }

    private void TryRequestNextDestination()
    {
        if (region == null || region.SourceGrid == null || failedRequests > MaxFailedRequests ||
            !TryFindNextDestination(out Vector3 destination, out int destinationIndex))
        {
            CancelOwnedRequest();
            return;
        }

        ownedRequestId = agent.RequestNavigation(destination, agent.WaypointCheckDistance);
        ownsRequest = true;
        lastDestinationIndex = destinationIndex;
    }

    private bool TryFindNextDestination(out Vector3 destination, out int destinationIndex)
    {
        NavigationGrid grid = region.SourceGrid;
        float perceptionRadius = agent.Perception != null
            ? agent.Perception.LivingBoidDetectionRadius
            : grid.CellSize;
        float minimumDistance = perceptionRadius * minimumDistanceMultiplier;
        float maximumDistance = Mathf.Max(minimumDistance, perceptionRadius * maximumDistanceMultiplier);
        if (selector.TrySelect(grid, region, agent.transform.position, lastDestinationIndex,
            minimumDistance, maximumDistance, selectionAttempts, rejectedCandidates,
            out destination, out destinationIndex))
        {
            return true;
        }

        return selector.TrySelect(grid, region, agent.transform.position, lastDestinationIndex,
            0f, float.PositiveInfinity, selectionAttempts, rejectedCandidates,
            out destination, out destinationIndex);
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
