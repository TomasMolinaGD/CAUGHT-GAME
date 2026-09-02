using UnityEngine;

public abstract class State
{
    protected StateMachine StateMachine;
    protected State(StateMachine stateMachine){
        stateMachine = stateMachine
    }
    //virtual: cuando una clase hereda un state y definimos las 3 clases, lo utilizamos para en caso de necesitar, remplazarlas y sobre escribirlas 
    public virtual void Enter()
    {

    }
    public virtual void Update()
    {
        
    }
    public virtual void Exit()
    {

    }
}
