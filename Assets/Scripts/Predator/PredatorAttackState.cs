using UnityEngine;

public sealed class PredatorAttackState : State
{
    private readonly PredatorFSM agent;
    private GameObject target;
    private float attackElapsedTime;
    private bool attackStateObserved;

    public PredatorAttackState(PredatorFSM agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter()
    {
        target = agent.CurrentTarget;
        attackElapsedTime = 0f;
        attackStateObserved = false;
        agent.Movement.Stop();
        agent.Animation.SetAttacking(true);
    }

    public override void Update()
    {
        if (!agent.IsTargetAvailable(target))
        {
            agent.ClearTargetAndReturnToPatrol();
            return;
        }

        if (!agent.Perception.CanSeeTarget || agent.Perception.Target != target)
        {
            StateMachine.ChangeState(PredatorStateId.Search);
            return;
        }

        agent.Perception.UpdateTrackedPosition(target);

        Vector3 offset = Vector3.ProjectOnPlane(
            target.transform.position - agent.transform.position, Vector3.up);
        agent.Movement.FaceDirection(offset);
        if (offset.magnitude > agent.CaptureRadius)
        {
            StateMachine.ChangeState(PredatorStateId.Pursuit);
            return;
        }

        attackElapsedTime += Time.deltaTime;
        if (!agent.Animation.IsAttackFinished(attackElapsedTime, ref attackStateObserved))
            return;

        if (agent.Combat.TryCapture(target, agent.CaptureRadius, offset.magnitude,
            agent.Perception.IsExplicitThreatTarget(target)))
            agent.ClearTargetAndReturnToPatrol();
        else
            StateMachine.ChangeState(PredatorStateId.Pursuit);
    }

    public override void Exit()
    {
        agent.Movement.Stop();
        agent.Animation.SetAttacking(false);
        target = null;
        attackElapsedTime = 0f;
        attackStateObserved = false;
    }
}
