using UnityEngine;

public class AttackState : State
{
    private FSMAgent _agent;
    private Transform target;

    public float attackDamage = 25f;

    public AttackState(FSMAgent agent,Transform target,StateMachine stateMachine) : base(stateMachine)
    {
        
    }

    public override void Enter()
    {
        Debug.Log("¡Comenzando ataque!");
    }

    public override void Update()
    {
        if (target == null)
            return;

        Vector3 direction = target.position - _agent.transform.position;

        direction.y = 0f;

        if (direction != Vector3.zero)
        {
            _agent.transform.rotation =
                Quaternion.LookRotation(direction);
        }
        _agent.Animator.SetBool("isAttack", true);
        
        Debug.Log("Estoy atacando");
    }

    public override void Exit()
    {
        Debug.Log("Terminando ataque.");
    }
}