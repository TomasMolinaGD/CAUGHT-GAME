using UnityEngine;

public sealed class PredatorChaseState : State
{
    private readonly PredatorFSM agent;
    private GameObject target;
    private bool ownsNavigation;
    private bool previousChaseMovement;
    private float previousStoppingDistance;
    private Vector3 requestedDestination;
    private float destinationRefreshElapsed;

    public PredatorChaseState(PredatorFSM agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter()
    {
        target = agent.CurrentTarget;
        ownsNavigation = false;
        destinationRefreshElapsed = 0f;

        PredatorNavigationMovementController controller = agent.NavigationController;
        if (controller == null || target == null) return;

        previousChaseMovement = controller.UsesChaseMovement;
        previousStoppingDistance = controller.StoppingDistance;
        controller.ConfigureMovement(true, agent.CaptureRadius * 0.35f);
        UpdateNavigationDestination(target.transform.position, 0f, true);
    }

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
                if (agent.HasNavigationController)
                    UpdateNavigationDestination(target.transform.position, 0f, true);
            }

            agent.Perception.UpdateTrackedPosition(target);

            if (agent.CanStartAttack())
            {
                StateMachine.ChangeState(PredatorStateId.Attack);
                return;
            }

            if (agent.HasNavigationController)
                UpdateNavigationDestination(target.transform.position, Time.deltaTime, false);
            else
                agent.Movement.MoveTowards(target.transform.position, true,
                    agent.CaptureRadius * 0.35f);
            return;
        }

        StateMachine.ChangeState(PredatorStateId.Search);
    }

    public override void Exit()
    {
        PredatorNavigationMovementController controller = agent.NavigationController;
        if (controller != null && ownsNavigation)
        {
            agent.CancelNavigation();
            controller.ConfigureMovement(previousChaseMovement, previousStoppingDistance);
        }
        else
        {
            agent.Movement.Stop();
        }

        ownsNavigation = false;
        target = null;
    }

    private void UpdateNavigationDestination(Vector3 destination, float deltaTime, bool force)
    {
        if (!agent.HasNavigationController) return;

        if (!force)
        {
            destinationRefreshElapsed += Mathf.Max(0f, deltaTime);
            if (destinationRefreshElapsed < agent.ChaseDestinationRefreshInterval) return;
            destinationRefreshElapsed = 0f;

            Vector3 offset = destination - requestedDestination;
            offset.y = 0f;
            float threshold = agent.ChaseDestinationRefreshDistance;
            if (offset.sqrMagnitude < threshold * threshold) return;
        }

        requestedDestination = destination;
        destinationRefreshElapsed = 0f;
        agent.RequestNavigationDestination(destination);
        ownsNavigation = true;
    }
}
