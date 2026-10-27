using UnityEngine;

public sealed class PredatorSearchState : State
{
    private readonly PredatorFSM agent;
    private Vector3 searchCenter;
    private float searchAngle;
    private float searchRadius = 2f;
    private bool reachedSearchPosition;

    public PredatorSearchState(PredatorFSM agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter()
    {
        searchCenter = agent.LastKnownPosition;
        searchAngle = 0f;
        reachedSearchPosition = false;
    }

    public override void Update()
    {
        if (agent.CurrentTarget == null || agent.CurrentTarget.IsDead ||
            SafeZoneVolume.IsPositionSafe(agent.CurrentTarget.transform.position))
        {
            agent.ClearTargetAndReturnToPatrol();
            return;
        }

        if (agent.Perception.CanSeeHunter)
        {
            StateMachine.ChangeState(PredatorStateId.Chase);
            return;
        }

        if (agent.HasLastKnownPosition && Vector3.ProjectOnPlane(
            agent.LastKnownPosition - searchCenter, Vector3.up).sqrMagnitude > 0.25f)
        {
            searchCenter = agent.LastKnownPosition;
            searchAngle = 0f;
            reachedSearchPosition = false;
        }

        if (!reachedSearchPosition)
        {
            bool movingToLastKnown = agent.Movement.MoveTowards(
                searchCenter, true, agent.WaypointCheckDistance);
            if (!movingToLastKnown) reachedSearchPosition = true;
            return;
        }

        Vector3 searchPoint = searchCenter + new Vector3(
            Mathf.Cos(searchAngle) * searchRadius, 0f,
            Mathf.Sin(searchAngle) * searchRadius);
        bool moving = agent.Movement.MoveTowards(searchPoint, true, agent.WaypointCheckDistance * 0.5f);
        if (!moving) searchAngle += Mathf.PI * 0.5f;
    }

    public override void Exit() { agent.Movement.Stop(); }
}
