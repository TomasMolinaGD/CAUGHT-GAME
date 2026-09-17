using UnityEngine;

public class IdleState : State
{
    private float timeToChangePatrol = 3f;
    private float timer = 0f;

    public IdleState(StateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        timer = 0f;

        Debug.Log("Entré en Idle");
    }

    public override void Update()
    {
        timer += Time.deltaTime;

        Debug.Log("Estoy en Idle");

        if (timer >= timeToChangePatrol)
        {
            StateMachine.ChangeState(PoliceState.Patrol);

            Debug.Log("Cambio de Idle a Patrol");
        }
    }

    public override void Exit()
    {
        Debug.Log("Salí de Idle");
    }
}