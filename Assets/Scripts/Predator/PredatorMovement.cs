using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(GraphNavigationAgent))]
public sealed class PredatorMovement : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float turnSpeed = 8f;
    private float patrolSpeed = 2f;
    private float chaseSpeed = 2.5f;
    private GraphNavigationAgent graphNavigation;
    public Vector3 Velocity { get; private set; }

    private void Awake() { graphNavigation = GetComponent<GraphNavigationAgent>(); }

    public void Configure(float newPatrolSpeed, float newChaseSpeed, float newTurnSpeed, float maximumAllowedSpeed)
    {
        float speedCap = Mathf.Max(0.01f, maximumAllowedSpeed);
        chaseSpeed = Mathf.Min(Mathf.Max(0.1f, newChaseSpeed), speedCap);
        patrolSpeed = Mathf.Min(Mathf.Max(0.1f, newPatrolSpeed), chaseSpeed * 0.9f);
        turnSpeed = Mathf.Max(0.1f, newTurnSpeed);
        if (graphNavigation == null) graphNavigation = GetComponent<GraphNavigationAgent>();
        graphNavigation?.Configure(true, 0.4f, 1.8f);
    }

    public bool MoveTowards(Vector3 destination, bool chase, float stoppingDistance)
    {
        if (graphNavigation != null)
        {
            Vector3 previousPosition = transform.position;
            bool handledByGraph = graphNavigation.TryMoveTowards(destination,
                chase ? chaseSpeed : patrolSpeed, stoppingDistance, out bool graphMoving);
            if (handledByGraph)
            {
                Velocity = Time.deltaTime > 0f
                    ? Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up) / Time.deltaTime
                    : Vector3.zero;
                return graphMoving;
            }
        }

        Vector3 offset = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up);
        float distance = offset.magnitude;
        float stopDistance = Mathf.Max(0f, stoppingDistance);
        if (distance <= stopDistance)
        {
            Stop();
            return false;
        }

        Vector3 direction = offset.normalized;
        float speed = chase ? chaseSpeed : patrolSpeed;
        float travelDistance = Mathf.Min(speed * Time.deltaTime, distance - stopDistance);
        transform.position += direction * travelDistance;
        Velocity = Time.deltaTime > 0f ? direction * (travelDistance / Time.deltaTime) : Vector3.zero;
        FaceDirection(direction);
        return Velocity.sqrMagnitude > 0.0001f;
    }

    public void FaceDirection(Vector3 direction)
    {
        direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (direction.sqrMagnitude < 0.0001f) return;
        Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation,
            turnSpeed * Time.deltaTime);
    }

    public void Stop() { Velocity = Vector3.zero; }
    public void InvalidatePath() { graphNavigation?.InvalidatePath(); }

    private void OnValidate()
    {
        turnSpeed = Mathf.Max(0.1f, turnSpeed);
    }
}
