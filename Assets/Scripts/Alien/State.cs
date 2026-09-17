public abstract class State
{
    protected StateMachine StateMachine;
    protected State(StateMachine stateMachine)
    {
        StateMachine = stateMachine;
    }
    public virtual void Enter()
    {}
    public virtual void Update()
    {}
    public virtual void Exit()
    {}
}
