using UnityEngine;

public sealed class GatherState : State
{
    private readonly FSMAgent agent;
    private BoidLife target;
    private float collectionTimer;
    private bool isCollecting;

    public GatherState(FSMAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter()
    {
        target = agent.Perception.ClosestDeadBoid;
        agent.SetCurrentTarget(target);
        collectionTimer = 0f;
        isCollecting = false;
        agent.Animation.SetAttacking(false);
    }

    public override void Update()
    {
        if (target == null || !target.CanBeCollected ||
            !agent.Perception.IsDeadBoidDetected(target))
        {
            AbortCollection();
            return;
        }

        Vector3 offset = Vector3.ProjectOnPlane(
            target.transform.position - agent.transform.position,
            Vector3.up);

        if (!isCollecting && offset.magnitude > agent.CollectionDistance)
        {
            agent.Animation.SetMoving(
                agent.Movement.MoveTowards(
                    target.transform.position,
                    agent.CollectionDistance));
            return;
        }

        if (!isCollecting)
        {
            isCollecting = true;
            collectionTimer = 0f;
            agent.Movement.Stop();
            agent.Animation.SetMoving(false);
        }

        agent.Movement.FaceDirection(offset);
        collectionTimer += Time.deltaTime;
        if (collectionTimer < agent.CollectionDuration)
        {
            return;
        }

        if (target.Collect())
        {
            agent.SetCurrentTarget(null);
            StateMachine.ChangeState(PoliceState.Patrol);
            return;
        }

        AbortCollection();
    }

    public override void Exit()
    {
        agent.Movement.Stop();
        agent.Animation.SetMoving(false);
        target = null;
        collectionTimer = 0f;
        isCollecting = false;
    }

    private void AbortCollection()
    {
        agent.SetCurrentTarget(null);
        StateMachine.ChangeState(PoliceState.Patrol);
    }
}
