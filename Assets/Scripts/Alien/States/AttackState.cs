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
        agent.Animation.SetAttacking(false);
    }

    public override void Update()
    {
        if (attackLanded)
        {
            UpdateCompletedAttack();
            return;
        }

        if (ShouldAbortAttack())
        {
            AbortAttack();
            return;
        }

        Vector3 offset = GetTargetOffset();
        float distance = offset.magnitude;

        if (TryMoveIntoAttackRange(distance))
        {
            return;
        }

        if (!attackStarted)
        {
            StartAttack(ShouldUseMeleeAttack(distance));
        }

        UpdateAttackWindup(offset);
    }

    private void UpdateCompletedAttack()
    {
        attackTimer += Time.deltaTime;
        if (!agent.Animation.IsAttackFinished(attackTimer, ref attackAnimationObserved))
        {
            return;
        }

        if (target != null && target.IsAlive && agent.Perception.IsLivingBoidDetected(target))
        {
            StateMachine.ChangeState(PoliceState.Pursuit);
            return;
        }

        agent.SetCurrentTarget(null);
        StateMachine.ChangeState(PoliceState.Patrol);
    }

    private bool ShouldAbortAttack()
    {
        if (target == null || !target.IsAlive)
        {
            return true;
        }

        // Losing the target before committing to an attack does not
        // consume or reset TBA.
        return !attackStarted && !agent.Perception.IsLivingBoidDetected(target);
    }

    private void AbortAttack()
    {
        agent.SetCurrentTarget(null);
        StateMachine.ChangeState(PoliceState.Patrol);
    }

    private bool TryMoveIntoAttackRange(float distance)
    {
        if (attackStarted)
        {
            return false;
        }

        float stoppingDistance;
        if (agent.Combat.RequiresMeleeFollowUp)
        {
            if (distance <= agent.Combat.MeleeAttackRadius * 0.55f)
            {
                return false;
            }

            stoppingDistance = agent.Combat.MeleeAttackRadius * 0.45f;
        }
        else if (distance > agent.Combat.RangedAttackRadius)
        {
            stoppingDistance = agent.Combat.RangedAttackRadius * 0.85f;
        }
        else if (distance <= agent.Combat.MeleeAttackRadius &&
            distance > agent.Combat.MeleeAttackRadius * 0.55f)
        {
            stoppingDistance = agent.Combat.MeleeAttackRadius * 0.45f;
        }
        else
        {
            return false;
        }

        agent.Animation.SetAttacking(false);
        agent.Animation.SetMoving(
            agent.Movement.MoveTowards(
                target.transform.position,
                stoppingDistance,
                agent.PursuitSpeedMultiplier));
        return true;
    }

    private bool ShouldUseMeleeAttack(float distance)
    {
        return agent.Combat.RequiresMeleeFollowUp ||
            distance <= agent.Combat.MeleeAttackRadius;
    }

    private void UpdateAttackWindup(Vector3 offset)
    {
        attackTimer += Time.deltaTime;
        agent.Movement.FaceDirection(offset);
        if (attackTimer < GetSelectedWindup())
        {
            return;
        }

        if (MeleeTargetMovedOutOfRange())
        {
            ResetPendingAttack();
            return;
        }

        ApplyAttack();
    }

    private float GetSelectedWindup()
    {
        return useMeleeAttack
            ? agent.Combat.MeleeAttackWindup
            : agent.Combat.RangedAttackWindup;
    }

    private bool MeleeTargetMovedOutOfRange()
    {
        return useMeleeAttack &&
            GetTargetOffset().magnitude > agent.Combat.MeleeAttackRadius;
    }

    private void ResetPendingAttack()
    {
        attackStarted = false;
        attackTimer = 0f;
        agent.Animation.SetAttacking(false);
    }

    private void ApplyAttack()
    {
        bool attackPerformed = useMeleeAttack
            ? agent.Combat.PerformMeleeAttack(target)
            : agent.Combat.PerformRangedAttack(target);
        if (!attackPerformed)
        {
            return;
        }

        attackLanded = true;
        if (!target.IsAlive)
        {
            // Keep playing the complete attack, but never preserve a dead
            // boid as the hunter's pursuit target.
            agent.SetCurrentTarget(null);
        }
    }

    private Vector3 GetTargetOffset()
    {
        return Vector3.ProjectOnPlane(
            target.transform.position - agent.transform.position,
            Vector3.up);
    }

    public override void Exit()
    {
        agent.Movement.Stop();
        agent.Animation.SetMoving(false);
        agent.Animation.SetAttacking(false);
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
        agent.Animation.SetMoving(false);
        agent.Animation.SetAttacking(true);
    }
}
