using UnityEngine;

public sealed class PredatorChaseState : State
{
    private readonly PredatorFSM agent;
    private GameObject target;

    public PredatorChaseState(PredatorFSM agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter() { target = agent.CurrentTarget; }

    public override void Update()
    {
        if (!agent.IsTargetAvailable(target))
        {
            agent.ClearTargetAndReturnToPatrol();
            return;
        }

        if (agent.Perception.CanSeeTarget)
        {
            if (agent.Perception.CanSeeTarget && agent.Perception.Target != target)
            {
                target = agent.Perception.Target;
                agent.SetTarget(target);
            }

            agent.Perception.UpdateTrackedPosition(target);

            if (agent.CanStartAttack())
            {
                StateMachine.ChangeState(PredatorStateId.Attack);
                return;
            }

            agent.Movement.MoveTowards(target.transform.position, true, agent.CaptureRadius * 0.35f);
            return;
        }

        StateMachine.ChangeState(PredatorStateId.Search);
    }

    public override void Exit() { agent.Movement.Stop(); target = null; }
}
