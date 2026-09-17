using UnityEngine;

public class AttackState : State
{
    private readonly FSMAgent agent;
    private BoidLife target;
    private float attackTimer;
    private bool attackStarted;
    private bool attackLanded;
    private bool attackAnimationObserved;

    public AttackState(FSMAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter()
    {
        target = agent.CurrentTarget;
        if (target == null || !target.IsAlive || !agent.Perception.IsLivingBoidDetected(target))
        {
            target = agent.Perception.ClosestLivingBoid;
        }
        agent.SetCurrentTarget(target);
        attackTimer = 0f;
        attackStarted = false;
        attackLanded = false;
        attackAnimationObserved = false;
        agent.SetAttackAnimation(false);
    }

    public override void Update()
    {
        if (attackLanded)
        {
            attackTimer += Time.deltaTime;
            if (agent.IsAttackAnimationFinished(attackTimer, ref attackAnimationObserved))
            {
                if (target != null && target.IsAlive && agent.Perception.IsLivingBoidDetected(target))
                {
                    StateMachine.ChangeState(PoliceState.Pursuit);
                }
                else
                {
                    agent.SetCurrentTarget(null);
                    StateMachine.ChangeState(PoliceState.Patrol);
                }
            }
            return;
        }

        if (target == null || !target.IsAlive || !agent.Perception.IsLivingBoidDetected(target))
        {
            // Losing the target before contact does not consume or reset TBA.
            agent.SetCurrentTarget(null);
            StateMachine.ChangeState(PoliceState.Patrol);
            return;
        }

        Vector3 offset = Vector3.ProjectOnPlane(
            target.transform.position - agent.transform.position,
            Vector3.up);
        float distance = offset.magnitude;

        if (!attackStarted && distance > agent.MeleeRadius * 0.55f)
        {
            agent.SetAttackAnimation(false);
            agent.SetMovingAnimation(
                agent.Movement.MoveTowards(
                    target.transform.position,
                    agent.MeleeRadius * 0.45f,
                    agent.PursuitSpeedMultiplier));
            return;
        }

        if (!attackStarted)
        {
            attackStarted = true;
            attackTimer = 0f;
            agent.Movement.Stop();
            agent.SetMovingAnimation(false);
            agent.SetAttackAnimation(true);
        }

        attackTimer += Time.deltaTime;
        agent.Movement.FaceDirection(offset);
        if (attackTimer < agent.AttackWindup)
        {
            return;
        }

        offset = Vector3.ProjectOnPlane(
            target.transform.position - agent.transform.position,
            Vector3.up);
        distance = offset.magnitude;
        if (distance > agent.MeleeRadius)
        {
            attackStarted = false;
            attackTimer = 0f;
            agent.SetAttackAnimation(false);
            return;
        }

        if (agent.PerformAttack(target, true))
        {
            attackLanded = true;
            if (!target.IsAlive)
            {
                // Keep playing the complete attack, but never preserve a dead
                // boid as the hunter's pursuit target.
                agent.SetCurrentTarget(null);
            }
        }
    }

    public override void Exit()
    {
        agent.Movement.Stop();
        agent.SetMovingAnimation(false);
        agent.SetAttackAnimation(false);
        target = null;
        attackStarted = false;
        attackLanded = false;
        attackAnimationObserved = false;
    }
}
