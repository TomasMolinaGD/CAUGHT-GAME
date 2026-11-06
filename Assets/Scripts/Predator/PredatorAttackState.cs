using UnityEngine;

public sealed class PredatorAttackState : State
{
    private const float ApproachStoppingDistanceRatio = 0.35f;

    private readonly PredatorFSM agent;
    private GameObject target;
    private float attackElapsedTime;
    private bool attackStateObserved;
    private bool attackOutcomeRecorded;
    private bool impactAttempted;
    private bool impactCaptureSucceeded;

    public PredatorAttackState(PredatorFSM agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter()
    {
        target = agent.CurrentTarget;
        attackElapsedTime = 0f;
        attackStateObserved = false;
        attackOutcomeRecorded = false;
        impactAttempted = false;
        impactCaptureSucceeded = false;
        agent.Movement.Stop();
        agent.Animation.SetAttacking(true);
    }

    public override void Update()
    {
        Tick(Time.deltaTime);
    }

    private void Tick(float deltaTime)
    {
        if (!impactAttempted && !agent.IsTargetAvailable(target))
        {
            ReportAttackEndedWithoutCapture("target became unavailable before TryCapture");
            agent.ClearTargetAndReturnToPatrol();
            return;
        }

        if (!impactAttempted &&
            (!agent.Perception.CanSeeTarget || agent.Perception.Target != target))
        {
            ReportAttackEndedWithoutCapture("target perception was lost before TryCapture");
            StateMachine.ChangeState(PredatorStateId.Search);
            return;
        }

        if (!impactAttempted && !agent.Perception.CanTrackTarget(target))
        {
            ReportAttackEndedWithoutCapture(
                "target failed current LOS, range, or Safe Zone validation during windup");
            StateMachine.ChangeState(PredatorStateId.Search);
            return;
        }

        if (!impactAttempted)
            agent.Perception.UpdateTrackedPosition(target);

        Vector3 offset = target != null
            ? Vector3.ProjectOnPlane(
                target.transform.position - agent.transform.position, Vector3.up)
            : Vector3.zero;
        if (offset.sqrMagnitude > 0.0001f)
            agent.Movement.FaceDirection(offset);

        float approachStoppingDistance = agent.CaptureRadius * ApproachStoppingDistanceRatio;
        if (!impactAttempted && offset.magnitude > approachStoppingDistance)
        {
            bool moved = agent.Movement.MoveTowards(
                target.transform.position, true, approachStoppingDistance, deltaTime);
            if (!moved && agent.Movement.WasBlocked)
            {
                ReportAttackEndedWithoutCapture(
                    "windup approach was physically blocked before TryCapture");
                StateMachine.ChangeState(PredatorStateId.Pursuit);
                return;
            }

            offset = Vector3.ProjectOnPlane(
                target.transform.position - agent.transform.position, Vector3.up);
        }
        else if (!impactAttempted)
        {
            agent.Movement.Stop();
        }

        attackElapsedTime += Mathf.Max(0f, deltaTime);
        if (!impactAttempted &&
            agent.Animation.IsAttackImpactReached(attackElapsedTime, ref attackStateObserved))
        {
            agent.Movement.Stop();
            impactAttempted = true;
            attackOutcomeRecorded = true;
            offset = target != null
                ? Vector3.ProjectOnPlane(
                    target.transform.position - agent.transform.position, Vector3.up)
                : Vector3.zero;
            if (agent.IsTargetAvailable(target) && agent.Perception.CanTrackTarget(target))
            {
                impactCaptureSucceeded = agent.Combat.TryCapture(
                    target, agent.CaptureRadius, offset.magnitude,
                    agent.Perception.IsExplicitThreatTarget(target));
            }
            else
            {
                agent.Combat.ReportAttackEndedWithoutCapture(
                    target, agent.CaptureRadius, offset.magnitude,
                    "target failed current LOS, range, or Safe Zone validation at impact");
            }
        }

        if (!agent.Animation.IsAttackFinished(attackElapsedTime, ref attackStateObserved))
            return;

        if (impactCaptureSucceeded)
            agent.ClearTargetAndReturnToPatrol();
        else
            StateMachine.ChangeState(PredatorStateId.Pursuit);
    }

    public override void Exit()
    {
        if (!attackOutcomeRecorded)
            ReportAttackEndedWithoutCapture("Attack state exited before TryCapture");
        agent.Movement.Stop();
        agent.Animation.SetAttacking(false);
        target = null;
        attackElapsedTime = 0f;
        attackStateObserved = false;
        attackOutcomeRecorded = false;
        impactAttempted = false;
        impactCaptureSucceeded = false;
    }

    private void ReportAttackEndedWithoutCapture(string reason)
    {
        if (attackOutcomeRecorded) return;

        attackOutcomeRecorded = true;
        float distance = target != null
            ? Vector3.ProjectOnPlane(
                target.transform.position - agent.transform.position, Vector3.up).magnitude
            : float.PositiveInfinity;
        agent.Combat.ReportAttackEndedWithoutCapture(
            target, agent.CaptureRadius, distance, reason);
    }
}
