using UnityEngine;

public sealed class PredatorAttackState : State
{
    private readonly PredatorFSM agent;
    private FSMAgent target;

    public PredatorAttackState(PredatorFSM agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter()
    {
        target = agent.CurrentTarget;
        agent.Movement.Stop();
    }

    public override void Update()
    {
        if (target == null || target.IsDead ||
            SafeZoneVolume.IsPositionSafe(target.transform.position))
        {
            agent.ClearTargetAndReturnToPatrol();
            return;
        }

        if (!agent.Perception.CanSeeHunter)
        {
            StateMachine.ChangeState(PredatorStateId.Search);
            return;
        }

        Vector3 offset = Vector3.ProjectOnPlane(
            target.transform.position - agent.transform.position, Vector3.up);
        agent.Movement.FaceDirection(offset);
        if (offset.magnitude > agent.CaptureRadius)
        {
            StateMachine.ChangeState(PredatorStateId.Chase);
            return;
        }

        if (agent.Combat.TryCapture(target, agent.CaptureRadius, offset.magnitude))
            agent.ClearTargetAndReturnToPatrol();
    }

    public override void Exit()
    {
        agent.Movement.Stop();
        target = null;
    }
}
