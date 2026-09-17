using UnityEngine;

public class AttackState : State
{
    private readonly FSMAgent agent;
    private BoidLife target;
    private float attackTimer;
    private bool attackStarted;
    private bool attackLanded;
    private bool attackAnimationObserved;
    private bool useMeleeAttack;

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

        if (target == null || !target.IsAlive)
        {
            agent.SetCurrentTarget(null);
            StateMachine.ChangeState(PoliceState.Patrol);
            return;
        }

        if (!attackStarted && !agent.Perception.IsLivingBoidDetected(target))
        {
            // Losing the target before committing to an attack does not
            // consume or reset TBA.
            agent.SetCurrentTarget(null);
            StateMachine.ChangeState(PoliceState.Patrol);
            return;
        }

        Vector3 offset = Vector3.ProjectOnPlane(
            target.transform.position - agent.transform.position,
            Vector3.up);
        float distance = offset.magnitude;

        if (!attackStarted && distance > agent.RangedAttackRadius)
        {
            agent.SetAttackAnimation(false);
            agent.SetMovingAnimation(
                agent.Movement.MoveTowards(
                    target.transform.position,
                    agent.RangedAttackRadius * 0.85f,
                    agent.PursuitSpeedMultiplier));
            return;
        }

        if (!attackStarted && distance <= agent.MeleeRadius &&
            distance > agent.MeleeRadius * 0.55f)
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
            StartAttack(distance <= agent.MeleeRadius);
        }

        attackTimer += Time.deltaTime;
        agent.Movement.FaceDirection(offset);
        float selectedWindup = useMeleeAttack
            ? agent.AttackWindup
            : agent.RangedAttackWindup;
        if (attackTimer < selectedWindup)
        {
            return;
        }

        offset = Vector3.ProjectOnPlane(
            target.transform.position - agent.transform.position,
            Vector3.up);
        distance = offset.magnitude;
        if (useMeleeAttack && distance > agent.MeleeRadius)
        {
            attackStarted = false;
            attackTimer = 0f;
            agent.SetAttackAnimation(false);
            return;
        }

        bool attackPerformed = useMeleeAttack
            ? agent.PerformAttack(target, true)
            : agent.PerformRangedAttack(target);
        if (attackPerformed)
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
        useMeleeAttack = false;
    }

    private void StartAttack(bool meleeAttack)
    {
        useMeleeAttack = meleeAttack;
        attackStarted = true;
        attackTimer = 0f;
        agent.Movement.Stop();
        agent.SetMovingAnimation(false);
        agent.SetAttackAnimation(true);
    }
}
