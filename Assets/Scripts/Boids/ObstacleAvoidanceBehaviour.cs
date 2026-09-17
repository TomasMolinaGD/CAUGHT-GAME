using UnityEngine;

[RequireComponent(typeof(BoidAgent))]
public sealed class ObstacleAvoidanceBehaviour : SteeringBehaviour
{
    private static readonly float[] CandidateAngles =
        { -125f, -90f, -60f, -35f, 35f, 60f, 90f, 125f, 180f };

    [Header("Obstacle Detection")]
    [SerializeField] private LayerMask obstacleLayers = 1 << 12;
    [SerializeField, Min(0.1f)] private float lookAheadDistance = 4f;
    [SerializeField, Min(0.05f)] private float probeRadius = 0.5f;
    [SerializeField, Min(0f)] private float probeHeight = 1f;

    [Header("Avoidance")]
    [SerializeField, Min(0.1f)] private float strength = 7f;
    [SerializeField, Min(0.05f)] private float directionHoldTime = 0.3f;
    [SerializeField, Min(0.1f)] private float stuckDetectionTime = 0.45f;

    private BoidAgent owner;
    private Vector3 avoidanceDirection;
    private float directionHoldRemaining;
    private float stuckDuration;
    private float preferredSide;

    public override bool IsApplicable
    {
        get
        {
            Vector3 forward = GetForwardDirection();
            bool forwardBlocked = Probe(forward, out RaycastHit forwardHit);

            // No obstacle means no avoidance or recovery turn. This also keeps
            // crowding from being mistaken for an environmental collision.
            if (!forwardBlocked)
            {
                directionHoldRemaining = 0f;
                stuckDuration = 0f;
                return false;
            }

            UpdateStuckState();

            if (stuckDuration >= stuckDetectionTime)
            {
                preferredSide = -preferredSide;
                avoidanceDirection = Quaternion.Euler(0f, preferredSide * 135f, 0f) * forward;
                directionHoldRemaining = directionHoldTime;
                stuckDuration = 0f;
                return true;
            }

            if (directionHoldRemaining > 0f)
            {
                if (Probe(avoidanceDirection, out RaycastHit routeHit) &&
                    routeHit.distance < probeRadius * 2.5f)
                {
                    preferredSide = -preferredSide;
                    avoidanceDirection = FindClearestDirection(forward, routeHit.normal);
                    directionHoldRemaining = directionHoldTime;
                    return true;
                }

                directionHoldRemaining -= Time.fixedDeltaTime;
                return true;
            }

            avoidanceDirection = FindClearestDirection(forward, forwardHit.normal);
            directionHoldRemaining = directionHoldTime;
            return true;
        }
    }

    private void Awake()
    {
        owner = GetComponent<BoidAgent>();
        preferredSide = (GetInstanceID() & 1) == 0 ? 1f : -1f;
    }

    public override Vector3 CalculateSteering()
    {
        Vector3 forward = GetForwardDirection();
        Vector3 lateralDirection = Vector3.ProjectOnPlane(avoidanceDirection, forward);
        if (lateralDirection.sqrMagnitude < 0.0001f)
        {
            lateralDirection = avoidanceDirection;
        }

        return lateralDirection.normalized * strength;
    }

    private Vector3 GetForwardDirection()
    {
        Vector3 direction = Vector3.ProjectOnPlane(owner.Velocity, Vector3.up);
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }
        return direction.normalized;
    }

    private Vector3 FindClearestDirection(Vector3 forward, Vector3 obstacleNormal)
    {
        Vector3 bestDirection = -forward;
        float bestScore = -1f;
        Vector3 planarNormal = Vector3.ProjectOnPlane(obstacleNormal, Vector3.up).normalized;

        for (int i = 0; i < CandidateAngles.Length; i++)
        {
            Vector3 candidate = Quaternion.Euler(0f, CandidateAngles[i], 0f) * forward;
            float clearance = Probe(candidate, out RaycastHit hit) ? hit.distance : lookAheadDistance;
            float forwardPreference = Mathf.Clamp01(Vector3.Dot(forward, candidate) * 0.5f + 0.5f);
            float candidateSide = Mathf.Sign(CandidateAngles[i]);
            float individualPreference = candidateSide == preferredSide ? 0.1f : 0f;
            float awayPreference = planarNormal.sqrMagnitude > 0.001f
                ? Mathf.Clamp01(Vector3.Dot(candidate, planarNormal))
                : 0f;
            float score = clearance + forwardPreference * 0.75f +
                individualPreference + awayPreference * 1.25f;

            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = candidate;
            }
        }

        return bestDirection.normalized;
    }

    private void UpdateStuckState()
    {
        bool intendsToMove = owner != null && owner.Velocity.sqrMagnitude > 0.25f;
        bool barelyMoves = owner != null && owner.ActualVelocity.sqrMagnitude < 0.0225f;
        if (intendsToMove && barelyMoves)
        {
            stuckDuration += Time.fixedDeltaTime;
        }
        else
        {
            stuckDuration = Mathf.Max(0f, stuckDuration - Time.fixedDeltaTime * 2f);
        }
    }

    private bool Probe(Vector3 direction, out RaycastHit hit)
    {
        Vector3 origin = transform.position + Vector3.up * probeHeight;
        return Physics.SphereCast(
            origin,
            probeRadius,
            direction,
            out hit,
            lookAheadDistance,
            obstacleLayers,
            QueryTriggerInteraction.Ignore);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        lookAheadDistance = Mathf.Max(0.1f, lookAheadDistance);
        probeRadius = Mathf.Max(0.05f, probeRadius);
        probeHeight = Mathf.Max(0f, probeHeight);
        strength = Mathf.Max(0.1f, strength);
        directionHoldTime = Mathf.Max(0.05f, directionHoldTime);
        stuckDetectionTime = Mathf.Max(0.1f, stuckDetectionTime);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 forward = Application.isPlaying && owner != null
            ? GetForwardDirection()
            : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 origin = transform.position + Vector3.up * probeHeight;

        Gizmos.color = new Color(1f, 0.65f, 0f, 0.9f);
        Gizmos.DrawWireSphere(origin + forward * lookAheadDistance, probeRadius);
        Gizmos.DrawLine(origin, origin + forward * lookAheadDistance);
    }
}
