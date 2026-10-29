using UnityEngine;

[DisallowMultipleComponent]
public sealed class HunterMovement : MonoBehaviour
{
    public enum PlacementResult
    {
        Placed = 0,
        InvalidPosition = 1
    }

    public enum BoundsPolicy
    {
        LegacyRectangle = 0,
        NavigationGrid = 1
    }

    [Header("Playable Area")]
    [SerializeField] private BoundsPolicy boundsPolicy = BoundsPolicy.LegacyRectangle;
    [SerializeField] private Vector2 areaCenter = new Vector2(484f, 623f);
    [SerializeField] private Vector2 areaHalfExtents = new Vector2(50f, 34f);
    [SerializeField, Min(0f)] private float boundaryPadding = 1.5f;
    [SerializeField] private float groundHeight = 0.05f;

    private float speed = 3f;
    private float turnSpeed = 8f;
    private bool movementCommandIssued;
    private bool movementCommandGateActive;

    public Vector3 Velocity { get; private set; }
    public float GroundHeight => groundHeight;

    private void Awake()
    {
        if (boundsPolicy == BoundsPolicy.LegacyRectangle)
        {
            ClampInsidePlayableArea();
        }
    }

    private void LateUpdate()
    {
        Vector3 groundedPosition = transform.position;
        groundedPosition.y = groundHeight;
        transform.position = groundedPosition;
    }

    public void Configure(float movementSpeed, float rotationSpeed)
    {
        speed = Mathf.Max(0.1f, movementSpeed);
        turnSpeed = Mathf.Max(0.1f, rotationSpeed);
    }

    /// <summary>Starts the single-command window used by FSMAgent for one frame.</summary>
    public void BeginMovementFrame()
    {
        movementCommandIssued = false;
        movementCommandGateActive = true;
    }

    public bool MoveTowards(Vector3 destination, float stoppingDistance, float speedMultiplier = 1f)
    {
        return MoveTowardsInternal(
            destination,
            stoppingDistance,
            speedMultiplier,
            boundsPolicy == BoundsPolicy.LegacyRectangle);
    }

    /// <summary>
    /// Moves toward a navigation target without applying the legacy rectangular clamp.
    /// The grid/controller remains responsible for selecting a valid target.
    /// </summary>
    public bool MoveTowardsNavigation(Vector3 destination, float stoppingDistance, float speedMultiplier = 1f)
    {
        return MoveTowardsInternal(destination, stoppingDistance, speedMultiplier, false);
    }

    private bool MoveTowardsInternal(
        Vector3 destination,
        float stoppingDistance,
        float speedMultiplier,
        bool constrainToLegacyArea)
    {
        if (movementCommandGateActive && movementCommandIssued)
        {
            return false;
        }

        movementCommandIssued = true;
        Vector3 clampedDestination = constrainToLegacyArea
            ? ClampPosition(destination)
            : new Vector3(destination.x, groundHeight, destination.z);
        Vector3 direction = Vector3.ProjectOnPlane(clampedDestination - transform.position, Vector3.up);
        float distance = direction.magnitude;
        if (distance <= Mathf.Max(0f, stoppingDistance))
        {
            Velocity = Vector3.zero;
            return false;
        }

        Vector3 previousPosition = transform.position;
        float effectiveSpeed = speed * Mathf.Max(0.1f, speedMultiplier);
        float travelDistance = Mathf.Min(effectiveSpeed * Time.deltaTime, distance);
        Vector3 nextPosition = constrainToLegacyArea
            ? ClampPosition(previousPosition + direction.normalized * travelDistance)
            : new Vector3(
                previousPosition.x + direction.normalized.x * travelDistance,
                groundHeight,
                previousPosition.z + direction.normalized.z * travelDistance);
        transform.position = nextPosition;
        Velocity = Time.deltaTime > 0f
            ? Vector3.ProjectOnPlane(nextPosition - previousPosition, Vector3.up) / Time.deltaTime
            : Vector3.zero;
        FaceDirection(direction);
        return Velocity.sqrMagnitude > 0.0001f;
    }

    public void FaceDirection(Vector3 direction)
    {
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (horizontalDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(horizontalDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime);
    }

    public void Stop()
    {
        if (movementCommandGateActive && movementCommandIssued)
        {
            return;
        }

        Velocity = Vector3.zero;
    }

    /// <summary>
    /// Stops the current command and places the Hunter at a validated world position.
    /// The command gate remains consumed so stale movement cannot run in the same frame.
    /// </summary>
    public PlacementResult TryStopAndPlace(Vector3 worldPosition)
    {
        if (!IsFinite(worldPosition))
        {
            return PlacementResult.InvalidPosition;
        }

        worldPosition = boundsPolicy == BoundsPolicy.LegacyRectangle
            ? ClampPosition(worldPosition)
            : new Vector3(worldPosition.x, groundHeight, worldPosition.z);
        movementCommandGateActive = true;
        movementCommandIssued = true;
        Velocity = Vector3.zero;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = worldPosition;
        }

        transform.position = worldPosition;
        return PlacementResult.Placed;
    }

    private void ClampInsidePlayableArea()
    {
        transform.position = ClampPosition(transform.position);
    }

    private Vector3 ClampPosition(Vector3 position)
    {
        float usableX = Mathf.Max(1f, areaHalfExtents.x - boundaryPadding);
        float usableZ = Mathf.Max(1f, areaHalfExtents.y - boundaryPadding);
        position.x = Mathf.Clamp(position.x, areaCenter.x - usableX, areaCenter.x + usableX);
        position.z = Mathf.Clamp(position.z, areaCenter.y - usableZ, areaCenter.y + usableZ);
        position.y = groundHeight;
        return position;
    }

    private static bool IsFinite(Vector3 value) =>
        IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    private void OnValidate()
    {
        areaHalfExtents.x = Mathf.Max(1f, areaHalfExtents.x);
        areaHalfExtents.y = Mathf.Max(1f, areaHalfExtents.y);
        boundaryPadding = Mathf.Clamp(
            boundaryPadding,
            0f,
            Mathf.Min(areaHalfExtents.x, areaHalfExtents.y) - 0.1f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.45f, 0f, 0.8f);
        Gizmos.DrawWireCube(
            new Vector3(areaCenter.x, transform.position.y, areaCenter.y),
            new Vector3(areaHalfExtents.x * 2f, 0.2f, areaHalfExtents.y * 2f));
    }
}
