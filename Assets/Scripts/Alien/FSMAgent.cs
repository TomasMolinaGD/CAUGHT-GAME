using UnityEngine;
using System.Collections.Generic;

public class FSMAgent : MonoBehaviour
{
    public List<Transform> waypoints = new List<Transform>();
    public float speed = 10f;
    [SerializeField] private float waypointCheckDistance = 0.1f;
    private int currentNode;
    private int direction = 1;
    [SerializeField] private PatrolData dataPatrol;

    private StateMachine _stateMachine;

    private void Awake()
    {
        _stateMachine = new FSMStateMachine();
        IdleState idleState = new IdleState();
        PatrolState patrolState = new PatrolState(this, dattaPatrol);
        _stateMachine.ChangeState(IdleState):
        _stateMachine.ChangeState(patrolState):
    }

    private void Update()
    {
        PatrolLoop();
        _stateMachine.Updadate();
    }

    /*private void PatrolLoop()
    {
        var nextWayPoint = waypoints[currentNode];
        if(Vector3.Distance(nextWayPoint.position, transform.position) <= waypointCheckDistance)
        {
            currentNode = currentNode + 1 < waypoints.Count ? currentNode + 1 : 0;
        }
        var dir = nextWayPoint.position - transform.position;
        transform.position += dir.normalized * speed * Time.deltaTime;
    }
    
    private void PatrolPingPoing()
    {
        
        var nextWayPoint = waypoints[currentNode];
        if(Vector3.Distance(nextWayPoint.position, transform.position) <= waypointCheckDistance)
        {
            currentNode += direction;
            if (currentNode >= waypoints.Count)
            {
                currentNode = waypoints.Count - 1;
                direction = - 1;
            }
            else if (currentNode < 0)
            {
                currentNode = 1;
                direction = 1;
            }
        }
        var dir = nextWayPoint.position - transform.position;
        transform.position += dir.normalized * speed * Time.deltaTime;
   
    }
    */

}