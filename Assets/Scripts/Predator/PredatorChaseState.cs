using UnityEngine;

public sealed class PredatorChaseState : State
{
    private readonly PredatorFSM agent;
    private FSMAgent target;

    public PredatorChaseState(PredatorFSM agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter() { target = agent.CurrentTarget; }

    public override void Update()
    {
        if (target == null || target.IsDead ||
            SafeZoneVolume.IsPositionSafe(target.transform.position))
        {
            agent.ClearTargetAndReturnToPatrol();
            return;
        }

        if (agent.Perception.CanSeeHunter)
        {
            if (agent.CanStartAttack())
            {
                StateMachine.ChangeState(PredatorStateId.Attack);
                return;
            }

            agent.Movement.MoveTowards(target.transform.position, true, agent.CaptureRadius * 0.8f);
            return;
        }

        StateMachine.ChangeState(PredatorStateId.Search);
    }

    public override void Exit() { agent.Movement.Stop(); target = null; }
}
