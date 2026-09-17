using UnityEngine;

/// <summary>
/// Keeps the hunter committed to the same living boid while TBA recharges.
/// Attack remains a short, single-action state as required by the FSM spec.
/// </summary>
public sealed class PursuitState : State
{
    private readonly FSMAgent agent;
    private BoidLife target;
    private bool isMoving;

    public PursuitState(FSMAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter()
    {
        target = agent.CurrentTarget;
        if (target == null || !target.IsAlive || !agent.Perception.IsLivingBoidDetected(target))
        {
            target = agent.Perception.ClosestLivingBoid;
            agent.SetCurrentTarget(target);
        }

        agent.SetAttackAnimation(false);
        isMoving = false;
    }

    public override void Update()
    {
        if (target == null || !target.IsAlive || !agent.Perception.IsLivingBoidDetected(target))
        {
            agent.SetCurrentTarget(null);
            StateMachine.ChangeState(PoliceState.Patrol);
            return;
        }

        if (agent.IsAttackReady)
        {
            StateMachine.ChangeState(PoliceState.Attack);
            return;
        }

        Vector3 offset = Vector3.ProjectOnPlane(
            target.transform.position - agent.transform.position,
            Vector3.up);
        float startMovingDistance = agent.MeleeRadius * 0.75f;
        if (!isMoving && offset.magnitude <= startMovingDistance)
        {
            agent.Movement.Stop();
            agent.SetMovingAnimation(false);
            return;
        }

        isMoving = agent.Movement.MoveTowards(
            target.transform.position,
            agent.MeleeRadius * 0.45f,
            agent.PursuitSpeedMultiplier);
        agent.SetMovingAnimation(isMoving);
    }

    public override void Exit()
    {
        agent.Movement.Stop();
        agent.SetMovingAnimation(false);
        target = null;
        isMoving = false;
    }
}
