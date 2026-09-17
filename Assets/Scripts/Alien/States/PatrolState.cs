using UnityEngine;
using System.Collections.Generic;

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
        if (!PatrolLoop())
        {
            return;
        }

        if (_agent.Animator != null)
        {
            _agent.Animator.Play("movement");
        }
    }

    public override void Exit()
    {
        Debug.Log("no ma patrol");
    }
    private bool PatrolLoop()
    {
        if (_dataPatrol == null ||
            _dataPatrol.transform == null ||
            _dataPatrol.wayPoints == null ||
            _dataPatrol.wayPoints.Count == 0)
        {
            return false;
        }

        currentNode = Mathf.Clamp(currentNode, 0, _dataPatrol.wayPoints.Count - 1);
        var nextWayPoint = _dataPatrol.wayPoints[currentNode];
        if (nextWayPoint == null)
        {
            currentNode = (currentNode + 1) % _dataPatrol.wayPoints.Count;
            return false;
        }

        if(Vector3.Distance(nextWayPoint.position, _dataPatrol.transform.position) <= _dataPatrol.wayPointCheckDistance)
        {
            currentNode = (currentNode + 1) % _dataPatrol.wayPoints.Count;
            nextWayPoint = _dataPatrol.wayPoints[currentNode];
            if (nextWayPoint == null)
            {
                return false;
            }
        }

        var dir = nextWayPoint.position - _dataPatrol.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            _dataPatrol.transform.rotation = Quaternion.Slerp(
                _dataPatrol.transform.rotation,
                targetRotation,
                _agent.turnSpeed * Time.deltaTime);
        }

        _dataPatrol.transform.position = Vector3.MoveTowards(
            _dataPatrol.transform.position,
            nextWayPoint.position,
            _agent.speed * Time.deltaTime);
        return true;
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
