using UnityEngine;

[RequireComponent(typeof(BoidAgent))]
public sealed class WanderBehaviour : SteeringBehaviour
{
    [Header("Group Roaming Area")]
    [SerializeField] private Vector2 areaCenter = new Vector2(484f, 623f);
    [SerializeField] private Vector2 areaHalfExtents = new Vector2(50f, 34f);
    [SerializeField, Min(0.1f)] private float formationRadius = 3f;

    [Header("Direction Changes")]
    [SerializeField, Min(0.1f)] private float directionChangeInterval = 10f;
    [SerializeField, Range(15f, 180f)] private float turnSpeed = 90f;

    private static bool hasSharedTarget;
    private static Vector3 sharedTarget;
    private static float sharedTargetChangeTime;

    private BoidAgent owner;
    private Vector3 desiredDirection;
    private Vector3 formationOffset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSharedTarget()
    {
        hasSharedTarget = false;
        sharedTarget = Vector3.zero;
        sharedTargetChangeTime = 0f;
    }

    private void Awake()
    {
        owner = GetComponent<BoidAgent>();
        desiredDirection = GetHorizontalDirection(transform.forward);
        formationOffset = CalculateFormationOffset();
        EnsureSharedTarget();
    }

    private void OnEnable()
    {
        desiredDirection = GetHorizontalDirection(transform.forward);
        formationOffset = CalculateFormationOffset();
        EnsureSharedTarget();
    }

    public override Vector3 CalculateSteering()
    {
        EnsureSharedTarget();

        Vector3 individualTarget = sharedTarget + formationOffset;
        individualTarget.y = transform.position.y;
        Vector3 targetDirection = GetHorizontalDirection(individualTarget - transform.position);

        desiredDirection = Vector3.RotateTowards(
            desiredDirection,
            targetDirection,
            turnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime,
            0f).normalized;

        Vector3 desiredVelocity = desiredDirection * owner.MaxSpeed;
        return desiredVelocity - owner.Velocity;
    }

    private void EnsureSharedTarget()
    {
        if (hasSharedTarget && Time.time < sharedTargetChangeTime)
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        float minimumTravelDistanceSquared = Mathf.Pow(formationRadius * 4f, 2f);

        for (int attempt = 0; attempt < 8; attempt++)
        {
            sharedTarget = new Vector3(
                Random.Range(areaCenter.x - areaHalfExtents.x, areaCenter.x + areaHalfExtents.x),
                currentPosition.y,
                Random.Range(areaCenter.y - areaHalfExtents.y, areaCenter.y + areaHalfExtents.y));

            Vector3 offset = Vector3.ProjectOnPlane(sharedTarget - currentPosition, Vector3.up);
            if (offset.sqrMagnitude >= minimumTravelDistanceSquared)
            {
                break;
            }
        }

        hasSharedTarget = true;
        sharedTargetChangeTime = Time.time + directionChangeInterval * Random.Range(0.85f, 1.15f);
    }

    private Vector3 CalculateFormationOffset()
    {
        int stableId = Mathf.Abs(GetInstanceID());
        float angle = (stableId * 137.508f) % 360f;
        float radiusBand = 0.55f + (stableId % 5) * 0.1f;
        float radius = formationRadius * radiusBand;
        float radians = angle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * radius;
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
        formationRadius = Mathf.Max(0.1f, formationRadius);
        directionChangeInterval = Mathf.Max(0.1f, directionChangeInterval);
        turnSpeed = Mathf.Clamp(turnSpeed, 15f, 180f);
    }
}
