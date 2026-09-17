using UnityEngine;

[RequireComponent(typeof(BoidAgent))]
public sealed class WanderBehaviour : SteeringBehaviour
{
    [Header("Local Roaming Area")]
    [SerializeField] private Vector2 areaCenter = new Vector2(484f, 623f);
    [SerializeField] private Vector2 areaHalfExtents = new Vector2(50f, 34f);
    [SerializeField, Min(1f)] private float minimumTravelDistance = 14f;
    [SerializeField, Min(1f)] private float maximumTravelDistance = 26f;
    [SerializeField, Min(0.1f)] private float arrivalDistance = 2.5f;

    [Header("Direction Changes")]
    [SerializeField, Min(0.1f)] private float directionChangeInterval = 10f;
    [SerializeField, Range(0f, 180f)] private float maximumDirectionChange = 65f;
    [SerializeField, Range(15f, 180f)] private float turnSpeed = 90f;

    private BoidAgent owner;
    private Vector3 desiredDirection;
    private Vector3 targetPosition;
    private float nextTargetChangeTime;
    private bool hasTarget;

    private void Awake()
    {
        owner = GetComponent<BoidAgent>();
        desiredDirection = GetHorizontalDirection(transform.forward);
        SelectLocalTarget();
    }

    private void OnEnable()
    {
        desiredDirection = GetHorizontalDirection(transform.forward);
        hasTarget = false;
    }

    public override Vector3 CalculateSteering()
    {
        if (!hasTarget || Time.time >= nextTargetChangeTime ||
            HorizontalDistanceSquared(transform.position, targetPosition) <= arrivalDistance * arrivalDistance)
        {
            SelectLocalTarget();
        }

        Vector3 targetDirection = GetHorizontalDirection(targetPosition - transform.position);

        desiredDirection = Vector3.RotateTowards(
            desiredDirection,
            targetDirection,
            turnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime,
            0f).normalized;

        Vector3 desiredVelocity = desiredDirection * owner.MaxSpeed;
        return desiredVelocity - owner.Velocity;
    }

    private void SelectLocalTarget()
    {
        Vector3 currentPosition = transform.position;
        Vector3 currentDirection = desiredDirection.sqrMagnitude > 0.001f
            ? desiredDirection
            : GetHorizontalDirection(transform.forward);

        targetPosition = currentPosition;
        float minimumDistanceSquared = minimumTravelDistance * minimumTravelDistance;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            float yaw = Random.Range(-maximumDirectionChange, maximumDirectionChange);
            Vector3 candidateDirection = Quaternion.Euler(0f, yaw, 0f) * currentDirection;
            float travelDistance = Random.Range(minimumTravelDistance, maximumTravelDistance);
            Vector3 candidate = currentPosition + candidateDirection * travelDistance;
            candidate.x = Mathf.Clamp(candidate.x, areaCenter.x - areaHalfExtents.x, areaCenter.x + areaHalfExtents.x);
            candidate.z = Mathf.Clamp(candidate.z, areaCenter.y - areaHalfExtents.y, areaCenter.y + areaHalfExtents.y);
            candidate.y = currentPosition.y;

            targetPosition = candidate;
            if (HorizontalDistanceSquared(currentPosition, candidate) >= minimumDistanceSquared)
            {
                break;
            }
        }

        if (HorizontalDistanceSquared(currentPosition, targetPosition) < minimumDistanceSquared)
        {
            targetPosition = new Vector3(
                Random.Range(areaCenter.x - areaHalfExtents.x, areaCenter.x + areaHalfExtents.x),
                currentPosition.y,
                Random.Range(areaCenter.y - areaHalfExtents.y, areaCenter.y + areaHalfExtents.y));
        }

        hasTarget = true;
        nextTargetChangeTime = Time.time + directionChangeInterval * Random.Range(0.85f, 1.15f);
    }

    private static float HorizontalDistanceSquared(Vector3 first, Vector3 second)
    {
        return Vector3.ProjectOnPlane(first - second, Vector3.up).sqrMagnitude;
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
        areaHalfExtents.x = Mathf.Max(1f, areaHalfExtents.x);
        areaHalfExtents.y = Mathf.Max(1f, areaHalfExtents.y);
        minimumTravelDistance = Mathf.Max(1f, minimumTravelDistance);
        maximumTravelDistance = Mathf.Max(minimumTravelDistance, maximumTravelDistance);
        arrivalDistance = Mathf.Max(0.1f, arrivalDistance);
        directionChangeInterval = Mathf.Max(0.1f, directionChangeInterval);
        maximumDirectionChange = Mathf.Clamp(maximumDirectionChange, 0f, 180f);
        turnSpeed = Mathf.Clamp(turnSpeed, 15f, 180f);
    }
}
