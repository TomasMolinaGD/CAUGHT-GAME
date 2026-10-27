using UnityEngine;

public sealed class PredatorPatrolState : State
{
    private readonly PredatorFSM agent;
    private int waypointIndex;

    public PredatorPatrolState(PredatorFSM agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter()
    {
        waypointIndex = FindClosestWaypointIndex();
    }

    public override void Update()
    {
        if (agent.Perception.CanSeeHunter && agent.Perception.Hunter != null)
        {
            agent.SetTarget(agent.Perception.Hunter);
            StateMachine.ChangeState(PredatorStateId.Chase);
            return;
        }

        if (agent.Waypoints.Count == 0) return;
        waypointIndex %= agent.Waypoints.Count;
        Transform waypoint = agent.Waypoints[waypointIndex];
        if (waypoint == null)
        {
            waypointIndex = (waypointIndex + 1) % agent.Waypoints.Count;
            return;
        }

        bool moving = agent.Movement.MoveTowards(waypoint.position, false, agent.WaypointCheckDistance);
        if (!moving) waypointIndex = (waypointIndex + 1) % agent.Waypoints.Count;
    }

    public override void Exit() { agent.Movement.Stop(); }

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
