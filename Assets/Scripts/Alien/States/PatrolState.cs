using UnityEngine;

public class PatrolState : State
{
    public patrolState(FSMAgent agent, PatrolData data, StateMachine stateMachine) : base(stateMachine)
    {
        _agent = agent;
        _data = data;
    }
    private FSMAgent _agent;
    public override void Enter()
    {
        Debug.Log ("Entre en Patrol");
    }
    public override void Update()
    {
        Debug.Log ("Estoy en Patrol");
    }

    public override void Exit()
    {
        Debug.Log ("Sali Patrol");
    }

    private void PatrolLoop()
    {
        var nextWayPoint = _data.waypoints[currentNode];
        if(Vector3.Distance(nextWayPoint.position, _data.transform.position) <= _data.waypointCheckDistance)
        {
            currentNode = currentNode + 1 < _data.waypoints.Count ? currentNode + 1 : 0;
        }
        var dir = nextWayPoint.position - transform.position;
        _data.transform.position += dir.normalized * _agent.speed * Time.deltaTime;
    }
}
[System.Serializable]
public class PatrolData
{
    public List<Transform> waypoints;
    public Transform transfom;
    public float waypointCheckDistance;

}
