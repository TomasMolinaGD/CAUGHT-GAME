using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class BoidAgent : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float maxSpeed = 3f;
    [SerializeField, Min(0.1f)] private float maxAcceleration = 6f;
    [SerializeField, Min(0.1f)] private float rotationSpeed = 8f;

    [Header("Temporary autonomous test")]
    [SerializeField] private bool enableTestMovement = true;
    [SerializeField, Range(0f, 90f)] private float maximumTurnAngle = 35f;
    [SerializeField, Min(0.1f)] private float directionChangeInterval = 1.5f;

    public Vector3 Velocity { get; private set; }

    private Rigidbody body;
    private SteeringBehaviour[] steeringBehaviours;
    private Vector3 desiredDirection;
    private float nextDirectionChangeTime;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        steeringBehaviours = GetComponents<SteeringBehaviour>();
        desiredDirection = GetHorizontalDirection(transform.forward);
    }

    private void OnEnable()
    {
        Velocity = Vector3.zero;
        desiredDirection = GetHorizontalDirection(transform.forward);
        nextDirectionChangeTime = Time.time + directionChangeInterval;
    }

    private void FixedUpdate()
    {
        Vector3 steering = Vector3.zero;

        if (enableTestMovement)
        {
            UpdateTestDirection();

            Vector3 desiredVelocity = desiredDirection * maxSpeed;
            steering += desiredVelocity - Velocity;
        }

        foreach (SteeringBehaviour behaviour in steeringBehaviours)
        {
            if (behaviour.enabled)
            {
                steering += behaviour.CalculateSteering() * behaviour.Weight;
            }
        }

        steering = Vector3.ClampMagnitude(steering, maxAcceleration);

        Velocity += steering * Time.fixedDeltaTime;
        Velocity = Vector3.ClampMagnitude(Velocity, maxSpeed);
        Velocity = Vector3.ProjectOnPlane(Velocity, Vector3.up);

        body.MovePosition(body.position + Velocity * Time.fixedDeltaTime);
        RotateTowardsVelocity();
    }

    private void UpdateTestDirection()
    {
        if (Time.time < nextDirectionChangeTime)
        {
            return;
        }

        float turnAngle = Random.Range(-maximumTurnAngle, maximumTurnAngle);
        desiredDirection = Quaternion.AngleAxis(turnAngle, Vector3.up) * desiredDirection;
        desiredDirection.Normalize();
        nextDirectionChangeTime = Time.time + directionChangeInterval;
    }

    private void RotateTowardsVelocity()
    {
        if (Velocity.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(Velocity.normalized, Vector3.up);
        Quaternion smoothRotation = Quaternion.Slerp(
            body.rotation,
            targetRotation,
            rotationSpeed * Time.fixedDeltaTime);

        body.MoveRotation(smoothRotation);
    }

    private static Vector3 GetHorizontalDirection(Vector3 direction)
    {
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
        return horizontalDirection.sqrMagnitude > 0.001f
            ? horizontalDirection.normalized
            : Vector3.forward;
    }

    private void OnValidate()
    {
        maxSpeed = Mathf.Max(0.1f, maxSpeed);
        maxAcceleration = Mathf.Max(0.1f, maxAcceleration);
        rotationSpeed = Mathf.Max(0.1f, rotationSpeed);
        directionChangeInterval = Mathf.Max(0.1f, directionChangeInterval);
    }
}
