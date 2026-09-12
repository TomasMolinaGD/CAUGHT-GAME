using UnityEngine;

// TEMPORAL: estado incompleto desactivado junto con la FSM del alien.
/*
public class IdleState : State
{

    float _timeToChangePatrol = 3f;
    float _timer = 0f;

public StateIdle(StateMachine stateMachine) : base(stateMachine)
{
    
}

    public override void Enter()
    {
        _timer = 0;
        Debug.Log ("Entre");
    }
    public override void Update()
    {
        _timer += Time.deltaTime;
        if(_timeToChangePatrol <= _timer)
        {

            Debug.Log("Me muevo");
        }
        Debug.Log ("Estoy en Idle");
    }

    public override void Exit()
    {
        Debug.Log ("Sali");
    }
}
*/
