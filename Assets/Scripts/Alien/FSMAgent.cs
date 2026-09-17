using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class FSMAgent : MonoBehaviour
{
    [SerializeField] private Animator animator;
    public Animator Animator => animator;
    public List<Transform> waypoints = new List<Transform>();
    public float speed = 10f;
    [SerializeField] private float waypointCheckDistance = 0.1f;
    //private int currentNode;
    public int direction = 1;
    [SerializeField] private PatrolData dataPatrol;




    private StateMachine _stateMachine;

    private void Awake()
    {
        _stateMachine = new StateMachine();
        IdleState idleState = new IdleState(_stateMachine);
        PatrolState patrolState = new PatrolState(this, dataPatrol, _stateMachine);
        _stateMachine.RegisterState(PoliceState.idle,idleState);
        _stateMachine.RegisterState(PoliceState.Patrol,patrolState);

        _stateMachine.ChangeState(PoliceState.idle);
        _stateMachine.ChangeState(PoliceState.Patrol);
    }

    private void Update()
    {
        //PatrolLoop();
        _stateMachine.Update();
    }

}