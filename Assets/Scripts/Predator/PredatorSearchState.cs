using UnityEngine;

public sealed class PredatorSearchState : State
{
    private const int MaximumSearchAttempts = 8;
    private readonly PredatorFSM agent;
    private Vector3 searchCenter;
    private float searchAngle;
    private float searchRadius = 2f;
    private bool reachedSearchPosition;
    private int searchAttempts;
    private bool navigationRequestIssued;
    private int navigationRequestTickCount;
    private Vector3 requestedNavigationDestination;

    public PredatorSearchState(PredatorFSM agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter()
    {
        searchCenter = agent.LastKnownPosition;
        searchAngle = 0f;
        reachedSearchPosition = false;
        searchAttempts = 0;
        navigationRequestIssued = false;
        RequestCurrentNavigationDestination();
    }

    public override void Update()
    {
        if (!agent.IsTargetAvailable(agent.CurrentTarget))
        {
            agent.ClearTargetAndReturnToPatrol();
            return;
        }

        if (agent.Perception.CanSeeTarget && agent.Perception.Target == agent.CurrentTarget)
        {
            StateMachine.ChangeState(PredatorStateId.Pursuit);
            return;
        }

        if (agent.HasLastKnownPosition && Vector3.ProjectOnPlane(
            agent.LastKnownPosition - searchCenter, Vector3.up).sqrMagnitude > 0.25f)
        {
            searchCenter = agent.LastKnownPosition;
            searchAngle = 0f;
            reachedSearchPosition = false;
            searchAttempts = 0;
            CancelNavigationIfOwned();
            RequestCurrentNavigationDestination();
        }

        if (agent.HasNavigationController)
        {
            UpdateNavigationSearch();
            return;
        }

        UpdateLegacySearch();
    }

    public override void Exit()
    {
        CancelNavigationIfOwned();
        if (!agent.HasNavigationController) agent.Movement.Stop();
    }

    private void UpdateNavigationSearch()
    {
        PredatorNavigationMovementController controller = agent.NavigationController;
        if (controller == null) return;
        if (controller.TickCount <= navigationRequestTickCount) return;

        GraphNavigationAgent.NavigationStatus status = controller.Status;
        if (status == GraphNavigationAgent.NavigationStatus.Arrived ||
            status == GraphNavigationAgent.NavigationStatus.NoPath ||
            status == GraphNavigationAgent.NavigationStatus.MissingProvider)
        {
            CancelNavigationIfOwned();
            AdvanceSearchDestination();
            return;
        }

        RequestCurrentNavigationDestination();
    }

    private void UpdateLegacySearch()
    {
        if (!reachedSearchPosition)
        {
            bool movingToLastKnown = agent.Movement.MoveTowards(
                searchCenter, true, agent.WaypointCheckDistance);
            if (!movingToLastKnown && agent.Movement.WasBlocked)
            {
                searchCenter = agent.transform.position;
                reachedSearchPosition = true;
                return;
            }

            if (!movingToLastKnown) reachedSearchPosition = true;
            return;
        }

        Vector3 searchPoint = searchCenter + new Vector3(
            Mathf.Cos(searchAngle) * searchRadius, 0f,
            Mathf.Sin(searchAngle) * searchRadius);
        bool moving = agent.Movement.MoveTowards(searchPoint, true, agent.WaypointCheckDistance * 0.5f);
        if (moving) return;

        searchAngle += Mathf.PI * 0.25f;
        searchAttempts++;
        if (searchAttempts >= MaximumSearchAttempts)
            agent.ClearTargetAndReturnToPatrol();
    }

    private void AdvanceSearchDestination()
    {
        if (!reachedSearchPosition)
        {
            reachedSearchPosition = true;
            RequestCurrentNavigationDestination();
            return;
        }

        searchAngle += Mathf.PI * 0.25f;
        searchAttempts++;
        if (searchAttempts >= MaximumSearchAttempts)
        {
            agent.ClearTargetAndReturnToPatrol();
            return;
        }

        RequestCurrentNavigationDestination();
    }

    private void RequestCurrentNavigationDestination()
    {
        if (!agent.HasNavigationController) return;

        Vector3 destination = reachedSearchPosition
            ? searchCenter + new Vector3(
                Mathf.Cos(searchAngle) * searchRadius, 0f,
                Mathf.Sin(searchAngle) * searchRadius)
            : searchCenter;
        if (navigationRequestIssued && requestedNavigationDestination == destination) return;

        agent.RequestNavigationDestination(destination);
        requestedNavigationDestination = destination;
        navigationRequestIssued = true;
        navigationRequestTickCount = agent.NavigationController != null
            ? agent.NavigationController.TickCount
            : 0;
    }

    private void CancelNavigationIfOwned()
    {
        if (!navigationRequestIssued) return;
        agent.CancelNavigation();
        navigationRequestIssued = false;
    }
}
