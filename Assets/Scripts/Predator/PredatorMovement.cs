using UnityEngine;

[DisallowMultipleComponent]
public sealed class PredatorMovement : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float turnSpeed = 8f;
    [SerializeField, Min(0.05f)] private float collisionRadius = 0.35f;
    [SerializeField, Min(0.2f)] private float collisionHeight = 1.6f;
    [SerializeField, Min(1f)] private float patrolScanSpeed = 60f;
    [SerializeField] private LayerMask obstacleLayers = 1 << 12;
    private float patrolSpeed = 2f;
    private float chaseSpeed = 2.5f;
    private PredatorAnimationController agentAnimation;
    public Vector3 Velocity { get; private set; }
    public bool WasBlocked { get; private set; }

    private void Awake()
    {
        agentAnimation = GetComponent<PredatorAnimationController>();
    }

    public void Configure(float newPatrolSpeed, float newChaseSpeed, float newTurnSpeed,
        float maximumAllowedSpeed, LayerMask newObstacleLayers)
    {
        float speedCap = Mathf.Max(0.01f, maximumAllowedSpeed);
        chaseSpeed = Mathf.Min(Mathf.Max(0.1f, newChaseSpeed), speedCap);
        patrolSpeed = Mathf.Min(Mathf.Max(0.1f, newPatrolSpeed), chaseSpeed * 0.9f);
        turnSpeed = Mathf.Max(0.1f, newTurnSpeed);
        obstacleLayers = newObstacleLayers;
        if (agentAnimation == null) agentAnimation = GetComponent<PredatorAnimationController>();
    }

    public bool MoveTowards(Vector3 destination, bool chase, float stoppingDistance)
    {
        WasBlocked = false;
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
        Vector3 movement = direction * travelDistance;
        if (!CanMove(movement))
        {
            WasBlocked = true;
            Stop();
            return false;
        }
        transform.position += movement;
        Velocity = Time.deltaTime > 0f ? direction * (travelDistance / Time.deltaTime) : Vector3.zero;
        FaceDirection(direction);
        agentAnimation?.SetMoving(Velocity.sqrMagnitude > 0.0001f);
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

    public void ScanInPlace()
    {
        Stop();
        transform.Rotate(Vector3.up, patrolScanSpeed * Time.deltaTime, Space.World);
    }

    public void Stop()
    {
        Velocity = Vector3.zero;
        agentAnimation?.SetMoving(false);
    }

    private bool CanMove(Vector3 movement)
    {
        float distance = movement.magnitude;
        if (distance <= 0f) return true;
        Vector3 direction = movement / distance;
        float sampleSpacing = Mathf.Max(0.1f, collisionRadius * 0.5f);
        float radius = Mathf.Max(0.05f, collisionRadius);
        int sampleCount = Mathf.CeilToInt(distance / sampleSpacing);
        for (int i = 1; i <= sampleCount; i++)
        {
            Vector3 sample = transform.position + movement * (i / (float)sampleCount);
            if (SafeZoneVolume.IsPositionSafe(sample) ||
                SafeZoneVolume.IsPositionSafe(sample + Vector3.right * radius) ||
                SafeZoneVolume.IsPositionSafe(sample - Vector3.right * radius) ||
                SafeZoneVolume.IsPositionSafe(sample + Vector3.forward * radius) ||
                SafeZoneVolume.IsPositionSafe(sample - Vector3.forward * radius)) return false;
        }

        float height = Mathf.Max(radius * 2f, collisionHeight);
        Vector3 bottom = transform.position + Vector3.up * radius;
        Vector3 top = transform.position + Vector3.up * (height - radius);
        return !Physics.CapsuleCast(bottom, top, radius, direction, distance,
            obstacleLayers, QueryTriggerInteraction.Ignore);
    }
    private void OnValidate()
    {
        turnSpeed = Mathf.Max(0.1f, turnSpeed);
        collisionRadius = Mathf.Max(0.05f, collisionRadius);
        collisionHeight = Mathf.Max(collisionRadius * 2f, collisionHeight);
        patrolScanSpeed = Mathf.Max(1f, patrolScanSpeed);
    }
}
