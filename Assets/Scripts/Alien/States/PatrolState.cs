using UnityEngine;

public class PatrolState : State
{
    private readonly FSMAgent agent;
    private int currentNode;

    public PatrolState(FSMAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter()
    {
        currentNode = FindClosestWaypointIndex();
        agent.SetAttackAnimation(false);
    }

    public override void Update()
    {
        if (agent.CanStartAttack())
        {
            StateMachine.ChangeState(PoliceState.Attack);
            return;
        }

        agent.SetMovingAnimation(PatrolLoop());
        agent.InterestSpawner.Tick(Time.deltaTime);
    }

    public override void Exit()
    {
        agent.Movement.Stop();
        agent.SetMovingAnimation(false);
    }

    private bool PatrolLoop()
    {
        if (agent.Waypoints == null || agent.Waypoints.Count == 0)
        {
            agent.Movement.Stop();
            return false;
        }

        currentNode = Mathf.Clamp(currentNode, 0, agent.Waypoints.Count - 1);
        Transform nextWaypoint = agent.Waypoints[currentNode];
        if (nextWaypoint == null)
        {
            currentNode = (currentNode + 1) % agent.Waypoints.Count;
            return false;
        }

        if (HorizontalDistanceSquared(nextWaypoint.position, agent.transform.position) <=
            agent.WaypointCheckDistance * agent.WaypointCheckDistance)
        {
            currentNode = (currentNode + 1) % agent.Waypoints.Count;
            nextWaypoint = agent.Waypoints[currentNode];
            if (nextWaypoint == null)
            {
                return false;
            }
        }

        return agent.Movement.MoveTowards(nextWaypoint.position, agent.WaypointCheckDistance);
    }

    private int FindClosestWaypointIndex()
    {
        if (agent.Waypoints == null || agent.Waypoints.Count == 0)
        {
            return 0;
        }

        int closestIndex = 0;
        float closestDistance = float.PositiveInfinity;
        for (int index = 0; index < agent.Waypoints.Count; index++)
        {
            Transform waypoint = agent.Waypoints[index];
            if (waypoint == null)
            {
                continue;
            }

            float distance = HorizontalDistanceSquared(agent.transform.position, waypoint.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = index;
            }
        }

        return closestIndex;
    }

    private static float HorizontalDistanceSquared(Vector3 first, Vector3 second)
    {
        return Vector3.ProjectOnPlane(first - second, Vector3.up).sqrMagnitude;
    }
}
