using UnityEngine;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
public class PatrolState : State
{
    private FSMAgent _agent;
    private PatrolData _dataPatrol;

    private int currentNode = 0;
    //private int direction = 1;

    public PatrolState(FSMAgent agent,PatrolData dataPatrol,StateMachine stateMachine) : base(stateMachine)
    {
        _agent = agent;
        _dataPatrol = dataPatrol;
    }

    public override void Enter()
    {
        Debug.Log("Entré en Patrol");
    }

    public override void Update()
    {
        Debug.Log("Estoy en Patrol");

        PatrolLoop();
        _agent.Animator.Play("movement");
    }

    public override void Exit()
    {
        Debug.Log("no ma patrol");
    }
    private void PatrolLoop()
    {
        var nextWayPoint = _dataPatrol.wayPoints[currentNode];
        if(Vector3.Distance(nextWayPoint.position, _dataPatrol.transform.position) <= _dataPatrol.wayPointCheckDistance)
        {
            currentNode = currentNode + 1 < _dataPatrol.wayPoints.Count ? currentNode + 1 : 0;
        }
        var dir = nextWayPoint.position - _dataPatrol.transform.position;
        _dataPatrol.transform.position += dir.normalized * _agent.speed * Time.deltaTime;
    }
    /* private void PatrolPingPong()
    {
        var nextWayPoint = _data.wayPoints[currentNode];
        if (Vector3.Distance(nextWayPoint.position, _data.transform.position) <= _data.wayPointCheckDistance)
        {
            currentNode += direction;
            if(currentNode >= _data.wayPoints.Count)
            {
                currentNode = _data.wayPoints.Count-1;
                direction = -1;
            }else if (currentNode < 0)
            {
                currentNode = 1;
                direction = 1;
            }
        }
        var dir = nextWayPoint.position - _data.transform.position;
        _data.transform.position += dir.normalized * _agent.speed * Time.deltaTime;
    }*/
}

[System.Serializable]
public class PatrolData
{
    public List<Transform> wayPoints;
    public Transform transform;
    public float wayPointCheckDistance = 0.1f;

}
