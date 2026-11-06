using UnityEngine;

public sealed class DeadState : State
{
    private readonly FSMAgent agent;
    private float respawnElapsed;
    private bool respawnAttempted;

    public DeadState(FSMAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        this.agent = agent;
    }

    public override void Enter()
    {
        respawnElapsed = 0f;
        respawnAttempted = false;
        agent.EnterDeadState();
    }

    public override void Update()
    {
        UpdateRespawn(Time.deltaTime);
    }

    public override void Exit()
    {
        respawnElapsed = 0f;
        respawnAttempted = false;
    }

    private void UpdateRespawn(float deltaTime)
    {
        if (respawnAttempted) return;

        respawnElapsed += Mathf.Max(0f, deltaTime);
        if (respawnElapsed < agent.RespawnTime) return;

        respawnAttempted = true;
        if (!agent.TryCompleteDeathRespawn())
        {
            Debug.LogWarning(
                "Hunter respawn failed: no valid Spawn cell or placement is available; Hunter remains Dead.",
                agent);
        }
    }
}
