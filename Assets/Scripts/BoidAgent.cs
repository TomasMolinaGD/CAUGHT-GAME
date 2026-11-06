using System;
using CaughtGame.Navigation;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class BoidAgent : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private BoidAreaPolicy areaPolicy = BoidAreaPolicy.LegacyRectangle;
    [SerializeField, Min(0.1f)] private float maxSpeed = 4f;
    [SerializeField, Min(0.1f)] private float maxAcceleration = 10f;
    [SerializeField, Min(0.1f)] private float rotationSpeed = 8f;
    [SerializeField, Min(0.1f)] private float minimumSafetySpeed = 1.5f;

    [Header("Environment Collision")]
    [SerializeField] private LayerMask environmentLayers = 1 << 12;
    [SerializeField, Min(0.001f)] private float collisionSkin = 0.04f;
    [SerializeField, Range(1, 5)] private int maximumSlideIterations = 3;

    public Vector3 Velocity { get; private set; }
    public Vector3 ActualVelocity { get; private set; }
    public BoidAreaPolicy AreaPolicy => areaPolicy;
    public bool RequiresHuntingGround => areaPolicy == BoidAreaPolicy.RequireHuntingGround;
    public float MaxSpeed => maxSpeed;
    public float HorizontalCapsuleRadius
    {
        get
        {
            if (capsule == null)
            {
                capsule = GetComponent<CapsuleCollider>();
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (body != null && TryGetMovementCapsule(
                    body.position, body.rotation,
                    out BoidCapsuleGeometry.WorldCapsule worldCapsule))
            {
                return worldCapsule.HorizontalFootprintRadius;
            }

            return 0f;
        }
    }

    private Rigidbody body;
    private CapsuleCollider capsule;
    private SteeringBehaviour[] steeringBehaviours;
    private PlayAreaContainmentBehaviour containment;
    private WanderBehaviour wander;
    private float movementPauseRemaining;
    private readonly Collider[] overlapBuffer = new Collider[16];

    private enum PhysicalClearanceResult
    {
        Clear,
        Blocked,
        Uncertain
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        steeringBehaviours = GetComponents<SteeringBehaviour>();
        containment = GetComponent<PlayAreaContainmentBehaviour>();
        wander = GetComponent<WanderBehaviour>();
        Array.Sort(
            steeringBehaviours,
            (first, second) => second.Priority.CompareTo(first.Priority));
    }

    private void OnEnable()
    {
        Velocity = Vector3.zero;
        ActualVelocity = Vector3.zero;
        movementPauseRemaining = 0f;
    }

    private void FixedUpdate()
    {
        if (RequiresHuntingGround &&
            (!TryGetRequiredHuntingGroundRegion(out HuntingGroundRegion requiredRegion) ||
             !requiredRegion.ContainsWorldPosition(body.position)))
        {
            StopMovement();
            return;
        }

        if (RequiresHuntingGround && !TryGetWorldCapsule(body.position, body.rotation, out _))
        {
            StopMovement();
            return;
        }

        if (RequiresHuntingGround &&
            (!TryValidatePhysicalConfiguration(out BoidCapsuleGeometry.WorldCapsule initialCapsule) ||
             !IsEnvironmentClear(initialCapsule)))
        {
            StopMovement();
            return;
        }

        if (movementPauseRemaining > 0f)
        {
            movementPauseRemaining = Mathf.Max(
                0f,
                movementPauseRemaining - Time.fixedDeltaTime);
            Velocity = Vector3.zero;
            ActualVelocity = Vector3.zero;
            return;
        }

        Vector3 steering = CalculatePrioritySteering(out int appliedPriority);

        Velocity += steering * Time.fixedDeltaTime;
        Velocity = Vector3.ClampMagnitude(Velocity, maxSpeed);
        Velocity = Vector3.ProjectOnPlane(Velocity, Vector3.up);

        if (appliedPriority >= 500 && Velocity.sqrMagnitude > 0.0001f &&
            Velocity.magnitude < minimumSafetySpeed)
        {
            Velocity = Velocity.normalized * minimumSafetySpeed;
        }

        MoveWithEnvironmentCollision(Velocity * Time.fixedDeltaTime);
        RotateTowardsVelocity();
    }

    public void PauseMovement(float duration)
    {
        movementPauseRemaining = Mathf.Max(movementPauseRemaining, duration);
        Velocity = Vector3.zero;
        ActualVelocity = Vector3.zero;
    }

    /// <summary>
    /// Resolves the configured required region from persistent semantic configuration.
    /// Physical capsule validity is checked by the movement and respawn consumers, so a
    /// temporarily unavailable collider cannot hide an otherwise valid required region.
    /// </summary>
    public bool TryGetRequiredHuntingGroundRegion(out HuntingGroundRegion region)
    {
        region = null;
        if (!RequiresHuntingGround || containment == null ||
            !containment.TryGetHuntingGroundRegion(out HuntingGroundRegion containmentRegion) ||
            containmentRegion.SourceGrid == null ||
            containmentRegion.SourceGrid.Rows <= 0 || containmentRegion.SourceGrid.Columns <= 0 ||
            (long)containmentRegion.SourceGrid.CellCount !=
                (long)containmentRegion.SourceGrid.Rows * containmentRegion.SourceGrid.Columns ||
            float.IsNaN(containmentRegion.SourceGrid.CellSize) ||
            float.IsInfinity(containmentRegion.SourceGrid.CellSize) ||
            containmentRegion.SourceGrid.CellSize <= 0f ||
            !IsSupportedGridTransform(containmentRegion.SourceGrid.transform))
        {
            return false;
        }

        if (wander != null && wander.enabled && wander.HuntingGroundRegion != containmentRegion)
        {
            return false;
        }

        region = containmentRegion;
        return true;
    }

    private static bool IsSupportedGridTransform(Transform gridTransform)
    {
        const float AxisAlignmentTolerance = 0.000001f;
        Matrix4x4 matrix = gridTransform.localToWorldMatrix;
        Vector3 x = matrix.MultiplyVector(Vector3.right);
        Vector3 y = matrix.MultiplyVector(Vector3.up);
        Vector3 z = matrix.MultiplyVector(Vector3.forward);
        float xMagnitude = x.magnitude;
        float yMagnitude = y.magnitude;
        float zMagnitude = z.magnitude;
        if (xMagnitude <= 0.0001f || yMagnitude <= 0.0001f || zMagnitude <= 0.0001f)
        {
            return false;
        }

        x /= xMagnitude;
        y /= yMagnitude;
        z /= zMagnitude;
        // The grid must remain horizontal; only matrix round-off is tolerated.
        // A dot-product threshold of 0.9999 accepted roughly 0.8 degrees of tilt.
        return y.y >= 1f - AxisAlignmentTolerance &&
               Mathf.Abs(y.x) <= AxisAlignmentTolerance &&
               Mathf.Abs(y.z) <= AxisAlignmentTolerance &&
               Mathf.Abs(x.y) <= AxisAlignmentTolerance &&
               Mathf.Abs(z.y) <= AxisAlignmentTolerance &&
               Mathf.Abs(Vector3.Dot(x, y)) <= 0.0001f &&
               Mathf.Abs(Vector3.Dot(x, z)) <= 0.0001f &&
               Mathf.Abs(Vector3.Dot(y, z)) <= 0.0001f;
    }

    private void MoveWithEnvironmentCollision(Vector3 desiredDisplacement)
    {
        Vector3 startPosition = body.position;
        Vector3 resolvedPosition = startPosition;
        Vector3 remaining = desiredDisplacement;
        Vector3 blockingNormal = Vector3.zero;
        bool collided = false;

        for (int iteration = 0; iteration < maximumSlideIterations; iteration++)
        {
            float distance = remaining.magnitude;
            if (distance < 0.0001f)
            {
                break;
            }

            Vector3 direction = remaining / distance;
            if (!TryGetMovementCapsule(resolvedPosition, body.rotation, out BoidCapsuleGeometry.WorldCapsule worldCapsule))
            {
                StopMovement();
                return;
            }

            if (!Physics.CapsuleCast(
                    worldCapsule.Point0,
                    worldCapsule.Point1,
                    worldCapsule.Radius,
                    direction,
                    out RaycastHit hit,
                    distance + collisionSkin,
                    environmentLayers,
                    QueryTriggerInteraction.Ignore))
            {
                resolvedPosition += remaining;
                remaining = Vector3.zero;
                break;
            }

            collided = true;
            float travelDistance = Mathf.Clamp(hit.distance - collisionSkin, 0f, distance);
            Vector3 travelled = direction * travelDistance;
            resolvedPosition += travelled;

            Vector3 surfaceNormal = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
            if (surfaceNormal.sqrMagnitude < 0.0001f)
            {
                remaining = Vector3.zero;
                break;
            }

            surfaceNormal.Normalize();
            blockingNormal += surfaceNormal;
            resolvedPosition += surfaceNormal * collisionSkin;
            remaining = Vector3.ProjectOnPlane(remaining - travelled, surfaceNormal);
        }

        if (collided && blockingNormal.sqrMagnitude > 0.0001f)
        {
            blockingNormal.Normalize();
            float penetratingSpeed = Vector3.Dot(Velocity, blockingNormal);
            if (penetratingSpeed < 0f)
            {
                Velocity -= blockingNormal * penetratingSpeed;
            }
        }

        if (!ResolveEnvironmentOverlaps(ref resolvedPosition) && RequiresHuntingGround)
        {
            StopMovement();
            return;
        }

        if (containment != null)
        {
            if (!RequiresHuntingGround)
            {
                resolvedPosition = containment.ClampInsidePlayableArea(
                    resolvedPosition,
                    out bool clampedX,
                    out bool clampedZ);
                if (clampedX)
                {
                    Velocity = new Vector3(0f, Velocity.y, Velocity.z);
                }
                if (clampedZ)
                {
                    Velocity = new Vector3(Velocity.x, Velocity.y, 0f);
                }
            }
        }

        if (RequiresHuntingGround)
        {
            if (!TryGetRequiredHuntingGroundRegion(out HuntingGroundRegion requiredRegion) ||
                containment == null)
            {
                StopMovement();
                return;
            }

            RegionSweepResult sweep = HuntingGroundFootprint.Sweep(
                requiredRegion,
                startPosition,
                resolvedPosition,
                HorizontalCapsuleRadius,
                containment.HuntingGroundClearance);
            if (sweep.Status != RegionSweepStatus.Clear)
            {
                if (sweep.Status == RegionSweepStatus.Blocked &&
                    sweep.SafeFraction > 0f)
                {
                    resolvedPosition = Vector3.Lerp(startPosition, resolvedPosition, sweep.SafeFraction);
                }
                else
                {
                    resolvedPosition = startPosition;
                }

                Vector3 regionBlockingNormal = sweep.Normal;
                float outwardSpeed = Vector3.Dot(Velocity, regionBlockingNormal);
                if (outwardSpeed > 0f)
                {
                    Velocity -= regionBlockingNormal * outwardSpeed;
                }
            }

            if (!TryRecertifyPhysicalMovement(startPosition, resolvedPosition, out resolvedPosition))
            {
                StopMovement();
                return;
            }
        }

        Vector3 actualDisplacement = Vector3.ProjectOnPlane(resolvedPosition - startPosition, Vector3.up);
        ActualVelocity = actualDisplacement / Time.fixedDeltaTime;
        body.MovePosition(resolvedPosition);
    }

    private bool ResolveEnvironmentOverlaps(ref Vector3 position)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            if (!TryGetMovementCapsule(position, body.rotation, out BoidCapsuleGeometry.WorldCapsule worldCapsule))
            {
                position = body.position;
                return false;
            }
            int overlapCount = Physics.OverlapCapsuleNonAlloc(
                worldCapsule.Point0,
                worldCapsule.Point1,
                worldCapsule.Radius,
                overlapBuffer,
                environmentLayers,
                QueryTriggerInteraction.Ignore);

            if (overlapCount >= overlapBuffer.Length && RequiresHuntingGround)
            {
                return false;
            }

            bool corrected = false;
            for (int i = 0; i < overlapCount; i++)
            {
                Collider obstacle = overlapBuffer[i];
                if (obstacle == null)
                {
                    continue;
                }

                if (Physics.ComputePenetration(
                    capsule,
                    position,
                    body.rotation,
                    obstacle,
                    obstacle.transform.position,
                    obstacle.transform.rotation,
                    out Vector3 correctionDirection,
                    out float correctionDistance))
                {
                    position += Vector3.ProjectOnPlane(correctionDirection, Vector3.up) *
                        (correctionDistance + collisionSkin);
                    corrected = true;
                }
            }

            if (!corrected)
            {
                break;
            }
        }

        return true;
    }

    private bool TryValidatePhysicalConfiguration(
        out BoidCapsuleGeometry.WorldCapsule capsuleGeometry)
    {
        capsuleGeometry = default;
        if (body == null || capsule == null || !capsule.enabled || capsule.isTrigger ||
            capsule.attachedRigidbody != body || environmentLayers.value == 0 ||
            !body.detectCollisions)
        {
            return false;
        }

        return TryGetMovementCapsule(body.position, body.rotation, out capsuleGeometry);
    }

    private bool IsEnvironmentClear(BoidCapsuleGeometry.WorldCapsule capsuleGeometry)
    {
        return QueryEnvironmentClearance(capsuleGeometry) == PhysicalClearanceResult.Clear;
    }

    private PhysicalClearanceResult QueryEnvironmentClearance(
        BoidCapsuleGeometry.WorldCapsule capsuleGeometry)
    {
        int overlapCount = Physics.OverlapCapsuleNonAlloc(
            capsuleGeometry.Point0,
            capsuleGeometry.Point1,
            capsuleGeometry.Radius,
            overlapBuffer,
            environmentLayers,
            QueryTriggerInteraction.Ignore);

        if (overlapCount >= overlapBuffer.Length)
        {
            return PhysicalClearanceResult.Uncertain;
        }

        for (int i = 0; i < overlapCount; i++)
        {
            Collider obstacle = overlapBuffer[i];
            if (obstacle == null || obstacle == capsule || obstacle.attachedRigidbody == body)
            {
                continue;
            }

            return PhysicalClearanceResult.Blocked;
        }

        return PhysicalClearanceResult.Clear;
    }

    private bool TryRecertifyPhysicalMovement(
        Vector3 startPosition,
        Vector3 candidatePosition,
        out Vector3 approvedPosition)
    {
        approvedPosition = startPosition;
        if (!TryGetMovementCapsule(startPosition, body.rotation, out BoidCapsuleGeometry.WorldCapsule startCapsule) ||
            QueryEnvironmentClearance(startCapsule) != PhysicalClearanceResult.Clear)
        {
            return false;
        }

        Vector3 displacement = candidatePosition - startPosition;
        displacement = Vector3.ProjectOnPlane(displacement, Vector3.up);
        float distance = displacement.magnitude;
        if (distance <= 0.0001f)
        {
            approvedPosition = startPosition;
            return true;
        }

        Vector3 direction = displacement / distance;
        float safeDistance = distance;
        if (Physics.CapsuleCast(
                startCapsule.Point0,
                startCapsule.Point1,
                startCapsule.Radius,
                direction,
                out RaycastHit hit,
                distance + collisionSkin,
                environmentLayers,
                QueryTriggerInteraction.Ignore))
        {
            safeDistance = Mathf.Clamp(hit.distance - collisionSkin, 0f, distance);
        }

        approvedPosition = startPosition + direction * safeDistance;
        if (!TryGetMovementCapsule(approvedPosition, body.rotation, out BoidCapsuleGeometry.WorldCapsule finalCapsule))
        {
            return false;
        }

        PhysicalClearanceResult finalClearance = QueryEnvironmentClearance(finalCapsule);
        if (finalClearance == PhysicalClearanceResult.Uncertain)
        {
            return false;
        }

        if (finalClearance == PhysicalClearanceResult.Clear)
        {
            return true;
        }

        // Retreat along the already-certified segment; never add a second slide here.
        Vector3 clearPosition = startPosition;
        Vector3 blockedPosition = approvedPosition;
        for (int iteration = 0; iteration < 8; iteration++)
        {
            Vector3 midpoint = Vector3.Lerp(clearPosition, blockedPosition, 0.5f);
            if (!TryGetMovementCapsule(midpoint, body.rotation, out BoidCapsuleGeometry.WorldCapsule midpointCapsule))
            {
                return false;
            }

            PhysicalClearanceResult midpointClearance = QueryEnvironmentClearance(midpointCapsule);
            if (midpointClearance == PhysicalClearanceResult.Uncertain)
            {
                return false;
            }

            if (midpointClearance == PhysicalClearanceResult.Clear)
            {
                clearPosition = midpoint;
            }
            else
            {
                blockedPosition = midpoint;
            }
        }

        approvedPosition = clearPosition;
        return true;
    }

    private bool TryGetWorldCapsule(
        Vector3 position,
        Quaternion rotation,
        out BoidCapsuleGeometry.WorldCapsule capsuleGeometry)
    {
        return BoidCapsuleGeometry.TryGetWorldCapsule(body, position, rotation, out capsuleGeometry);
    }

    private bool TryGetMovementCapsule(
        Vector3 position,
        Quaternion rotation,
        out BoidCapsuleGeometry.WorldCapsule capsuleGeometry)
    {
        if (TryGetWorldCapsule(position, rotation, out capsuleGeometry))
        {
            return true;
        }

        if (RequiresHuntingGround)
        {
            capsuleGeometry = default;
            return false;
        }

        Vector3 scale = transform.lossyScale;
        float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float halfHeight = Mathf.Max(capsule.height * Mathf.Abs(scale.y) * 0.5f, radius);
        Vector3 center = position + rotation * Vector3.Scale(capsule.center, scale);
        Vector3 segmentOffset = rotation * Vector3.up * (halfHeight - radius);
        capsuleGeometry = new BoidCapsuleGeometry.WorldCapsule(
            center,
            center + segmentOffset,
            center - segmentOffset,
            radius,
            halfHeight * 2f,
            radius);
        return true;
    }

    private void StopMovement()
    {
        Velocity = Vector3.zero;
        ActualVelocity = Vector3.zero;
    }

    private Vector3 CalculatePrioritySteering(out int appliedPriority)
    {
        appliedPriority = 0;
        int behaviourIndex = 0;

        while (behaviourIndex < steeringBehaviours.Length)
        {
            int currentPriority = steeringBehaviours[behaviourIndex].Priority;
            Vector3 prioritySteering = Vector3.zero;
            Vector3 strongestSteering = Vector3.zero;
            bool hasApplicableBehaviour = false;

            while (
                behaviourIndex < steeringBehaviours.Length &&
                steeringBehaviours[behaviourIndex].Priority == currentPriority)
            {
                SteeringBehaviour behaviour = steeringBehaviours[behaviourIndex];
                if (behaviour.enabled && behaviour.IsApplicable)
                {
                    hasApplicableBehaviour = true;
                    Vector3 weightedSteering = behaviour.CalculateSteering() * behaviour.Weight;
                    prioritySteering += weightedSteering;
                    if (weightedSteering.sqrMagnitude > strongestSteering.sqrMagnitude)
                    {
                        strongestSteering = weightedSteering;
                    }
                }

                behaviourIndex++;
            }

            if (hasApplicableBehaviour)
            {
                appliedPriority = currentPriority;
                if (currentPriority >= 500 &&
                    strongestSteering.sqrMagnitude > 0.0001f &&
                    prioritySteering.sqrMagnitude < strongestSteering.sqrMagnitude * 0.0225f)
                {
                    prioritySteering = strongestSteering;
                }
                return Vector3.ClampMagnitude(prioritySteering, maxAcceleration);
            }
        }

        return Vector3.zero;
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

    private void OnValidate()
    {
        maxSpeed = Mathf.Max(0.1f, maxSpeed);
        maxAcceleration = Mathf.Max(0.1f, maxAcceleration);
        rotationSpeed = Mathf.Max(0.1f, rotationSpeed);
        minimumSafetySpeed = Mathf.Clamp(minimumSafetySpeed, 0.1f, maxSpeed);
        collisionSkin = Mathf.Max(0.001f, collisionSkin);
        maximumSlideIterations = Mathf.Clamp(maximumSlideIterations, 1, 5);
    }
}
