using UnityEngine;

[RequireComponent(typeof(BoidAgent))]
public sealed class WanderBehaviour : SteeringBehaviour
{
    [SerializeField, Range(0f, 90f)] private float maximumTurnAngle = 15f;
    [SerializeField, Min(0.1f)] private float directionChangeInterval = 1.5f;

    private BoidAgent owner;
    private Vector3 desiredDirection;
    private Vector3 targetDirection;
    private float nextDirectionChangeTime;

    private void Awake()
    {
        owner = GetComponent<BoidAgent>();
        desiredDirection = GetHorizontalDirection(transform.forward);
        targetDirection = desiredDirection;
    }

    private void OnEnable()
    {
        desiredDirection = GetHorizontalDirection(transform.forward);
        targetDirection = desiredDirection;
        nextDirectionChangeTime = Time.time + directionChangeInterval;
    }

    public override Vector3 CalculateSteering()
    {
        UpdateDirection();
        Vector3 desiredVelocity = desiredDirection * owner.MaxSpeed;
        return desiredVelocity - owner.Velocity;
    }

    private void UpdateDirection()
    {
        if (Time.time >= nextDirectionChangeTime)
        {
            float turnAngle = Random.Range(-maximumTurnAngle, maximumTurnAngle);
            targetDirection = Quaternion.AngleAxis(turnAngle, Vector3.up) * desiredDirection;
            targetDirection.Normalize();
            nextDirectionChangeTime = Time.time + directionChangeInterval;
        }

        float turnSpeed = maximumTurnAngle / directionChangeInterval;
        desiredDirection = Vector3.RotateTowards(
            desiredDirection,
            targetDirection,
            turnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime,
            0f);
        desiredDirection.Normalize();
    }

    private static Vector3 GetHorizontalDirection(Vector3 direction)
    {
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
        return horizontalDirection.sqrMagnitude > 0.001f
            ? horizontalDirection.normalized
            : Vector3.forward;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        directionChangeInterval = Mathf.Max(0.1f, directionChangeInterval);
    }
}
