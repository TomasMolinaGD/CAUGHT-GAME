using UnityEngine;

public sealed class PredatorPatrolState : State
{
    private readonly PredatorFSM agent;
    private int waypointIndex;
    private bool navigationRequestIssued;
    private bool navigationRequestBlocked;
    private Vector3 requestedNavigationDestination;

    public PredatorPatrolState(PredatorFSM agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter()
    {
        waypointIndex = FindClosestWaypointIndex();
        navigationRequestIssued = false;
        navigationRequestBlocked = false;
        RequestNavigationForCurrentWaypoint();
    }

    public override void Update()
    {
        if (agent.Perception.CanSeeTarget && agent.Perception.Target != null)
        {
            agent.SetTarget(agent.Perception.Target);
            StateMachine.ChangeState(PredatorStateId.Pursuit);
            return;
        }

        if (agent.Waypoints.Count == 0)
        {
            CancelNavigationIfOwned();
            agent.Movement.ScanInPlace();
            return;
        }
        waypointIndex %= agent.Waypoints.Count;
        Transform waypoint = agent.Waypoints[waypointIndex];
        if (waypoint == null)
        {
            waypointIndex = (waypointIndex + 1) % agent.Waypoints.Count;
            navigationRequestIssued = false;
            navigationRequestBlocked = false;
            RequestNavigationForCurrentWaypoint();
            return;
        }

        if (agent.HasNavigationController)
        {
            UpdateNavigationPatrol(waypoint);
            return;
        }

        bool moving = agent.Movement.MoveTowards(waypoint.position, false, agent.WaypointCheckDistance);
        if (!moving && !agent.Movement.WasBlocked)
            waypointIndex = (waypointIndex + 1) % agent.Waypoints.Count;
    }

    public override void Exit()
    {
        CancelNavigationIfOwned();
        if (!agent.HasNavigationController) agent.Movement.Stop();
    }

    private void UpdateNavigationPatrol(Transform waypoint)
    {
        PredatorNavigationMovementController controller = agent.NavigationController;
        if (controller == null) return;

        GraphNavigationAgent.NavigationStatus status = controller.Status;
        if (status == GraphNavigationAgent.NavigationStatus.Arrived)
        {
            waypointIndex = (waypointIndex + 1) % agent.Waypoints.Count;
            navigationRequestIssued = false;
            navigationRequestBlocked = false;
            RequestNavigationForCurrentWaypoint();
            return;
        }

        if (status == GraphNavigationAgent.NavigationStatus.NoPath ||
            status == GraphNavigationAgent.NavigationStatus.MissingProvider)
        {
            if (!navigationRequestBlocked)
            {
                agent.CancelNavigation();
                navigationRequestBlocked = true;
            }
            return;
        }

        RequestNavigationForCurrentWaypoint(waypoint.position);
    }

    private void RequestNavigationForCurrentWaypoint(Vector3? destinationOverride = null)
    {
        if (!agent.HasNavigationController || navigationRequestBlocked) return;
        if (agent.Waypoints.Count == 0) return;

        Transform waypoint = agent.Waypoints[waypointIndex % agent.Waypoints.Count];
        if (waypoint == null) return;

        Vector3 destination = destinationOverride ?? waypoint.position;
        if (navigationRequestIssued && requestedNavigationDestination == destination) return;
        agent.RequestNavigationDestination(destination);
        requestedNavigationDestination = destination;
        navigationRequestIssued = true;
    }

    private void CancelNavigationIfOwned()
    {
        if (!navigationRequestIssued) return;
        agent.CancelNavigation();
        navigationRequestIssued = false;
    }

    private int FindClosestWaypointIndex()
    {
        if (agent.Waypoints.Count == 0) return 0;
        int closestIndex = 0;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < agent.Waypoints.Count; i++)
        {
            Transform waypoint = agent.Waypoints[i];
            if (waypoint == null) continue;
            float distance = Vector3.ProjectOnPlane(
                waypoint.position - agent.transform.position, Vector3.up).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }
        return closestIndex;
    }
}
