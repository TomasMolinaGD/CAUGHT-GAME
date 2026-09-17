using UnityEngine;

[RequireComponent(typeof(BoidAgent))]
public sealed class PlayAreaContainmentBehaviour : SteeringBehaviour
{
    [Header("Playable Area")]
    [SerializeField] private Vector2 center = new Vector2(484f, 623f);
    [SerializeField] private Vector2 halfExtents = new Vector2(50f, 34f);
    [SerializeField, Min(0.1f)] private float turningMargin = 8f;
    [SerializeField, Min(0f)] private float hardBoundaryPadding = 1.5f;
    [SerializeField, Min(0.1f)] private float strength = 6f;

    private BoidAgent owner;

    public override bool IsApplicable => GetReturnDirection().sqrMagnitude > 0.0001f;

    private void Awake()
    {
        owner = GetComponent<BoidAgent>();
    }

    public override Vector3 CalculateSteering()
    {
        Vector3 returnDirection = GetReturnDirection();
        if (returnDirection.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        // Aim at the middle instead of merely stepping away from the nearest
        // edge. Cancelling the tangential velocity prevents wall-following.
        Vector3 areaCenter = new Vector3(center.x, transform.position.y, center.y);
        Vector3 inward = Vector3.ProjectOnPlane(areaCenter - transform.position, Vector3.up).normalized;
        Vector3 desiredVelocity = inward * owner.MaxSpeed;
        return (desiredVelocity - owner.Velocity) * strength;
    }

    public Vector3 ClampInsidePlayableArea(Vector3 position, out bool clampedX, out bool clampedZ)
    {
        float usableHalfWidth = Mathf.Max(0.1f, halfExtents.x - hardBoundaryPadding);
        float usableHalfDepth = Mathf.Max(0.1f, halfExtents.y - hardBoundaryPadding);
        float clampedPositionX = Mathf.Clamp(position.x, center.x - usableHalfWidth, center.x + usableHalfWidth);
        float clampedPositionZ = Mathf.Clamp(position.z, center.y - usableHalfDepth, center.y + usableHalfDepth);
        clampedX = !Mathf.Approximately(position.x, clampedPositionX);
        clampedZ = !Mathf.Approximately(position.z, clampedPositionZ);
        position.x = clampedPositionX;
        position.z = clampedPositionZ;
        return position;
    }

    private Vector3 GetReturnDirection()
    {
        float safeHalfWidth = Mathf.Max(0.1f, halfExtents.x - turningMargin);
        float safeHalfDepth = Mathf.Max(0.1f, halfExtents.y - turningMargin);
        Vector3 position = transform.position;

        float safeX = Mathf.Clamp(position.x, center.x - safeHalfWidth, center.x + safeHalfWidth);
        float safeZ = Mathf.Clamp(position.z, center.y - safeHalfDepth, center.y + safeHalfDepth);

        return new Vector3(safeX - position.x, 0f, safeZ - position.z);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        halfExtents.x = Mathf.Max(0.2f, halfExtents.x);
        halfExtents.y = Mathf.Max(0.2f, halfExtents.y);
        turningMargin = Mathf.Clamp(turningMargin, 0.1f, Mathf.Min(halfExtents.x, halfExtents.y) - 0.1f);
        hardBoundaryPadding = Mathf.Clamp(
            hardBoundaryPadding,
            0f,
            Mathf.Min(halfExtents.x, halfExtents.y) - 0.1f);
        strength = Mathf.Max(0.1f, strength);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 areaCenter = new Vector3(center.x, transform.position.y, center.y);
        Vector3 outerSize = new Vector3(halfExtents.x * 2f, 0.2f, halfExtents.y * 2f);
        Vector3 safeSize = new Vector3(
            (halfExtents.x - turningMargin) * 2f,
            0.2f,
            (halfExtents.y - turningMargin) * 2f);

        Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.8f);
        Gizmos.DrawWireCube(areaCenter, outerSize);
        Gizmos.color = new Color(0.1f, 1f, 0.45f, 0.8f);
        Gizmos.DrawWireCube(areaCenter, safeSize);
    }
}
