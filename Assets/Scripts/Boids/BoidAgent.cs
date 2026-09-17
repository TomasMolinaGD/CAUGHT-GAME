using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class BoidAgent : MonoBehaviour
{
    [Header("Movement")]
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
    public float MaxSpeed => maxSpeed;

    private Rigidbody body;
    private CapsuleCollider capsule;
    private SteeringBehaviour[] steeringBehaviours;
    private PlayAreaContainmentBehaviour containment;
    private float movementPauseRemaining;
    private readonly Collider[] overlapBuffer = new Collider[16];

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        steeringBehaviours = GetComponents<SteeringBehaviour>();
        containment = GetComponent<PlayAreaContainmentBehaviour>();
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

    private void MoveWithEnvironmentCollision(Vector3 desiredDisplacement)
    {
        Vector3 startPosition = body.position;
        Vector3 resolvedPosition = startPosition;
        Vector3 remaining = desiredDisplacement;

        for (int iteration = 0; iteration < maximumSlideIterations; iteration++)
        {
            float distance = remaining.magnitude;
            if (distance < 0.0001f)
            {
                break;
            }

            Vector3 direction = remaining / distance;
            GetWorldCapsule(resolvedPosition, out Vector3 top, out Vector3 bottom, out float radius);

            if (!Physics.CapsuleCast(
                    top,
                    bottom,
                    radius,
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
            resolvedPosition += surfaceNormal * collisionSkin;
            remaining = Vector3.ProjectOnPlane(remaining - travelled, surfaceNormal);
        }

        ResolveEnvironmentOverlaps(ref resolvedPosition);

        if (containment != null)
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

        Vector3 actualDisplacement = Vector3.ProjectOnPlane(resolvedPosition - startPosition, Vector3.up);
        ActualVelocity = actualDisplacement / Time.fixedDeltaTime;
        body.MovePosition(resolvedPosition);
    }

    private void ResolveEnvironmentOverlaps(ref Vector3 position)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            GetWorldCapsule(position, out Vector3 top, out Vector3 bottom, out float radius);
            int overlapCount = Physics.OverlapCapsuleNonAlloc(
                top,
                bottom,
                radius,
                overlapBuffer,
                environmentLayers,
                QueryTriggerInteraction.Ignore);

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
    }

    private void GetWorldCapsule(Vector3 position, out Vector3 top, out Vector3 bottom, out float radius)
    {
        Vector3 scale = transform.lossyScale;
        radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float halfHeight = Mathf.Max(capsule.height * Mathf.Abs(scale.y) * 0.5f, radius);
        Vector3 center = position + body.rotation * Vector3.Scale(capsule.center, scale);
        Vector3 verticalOffset = body.rotation * Vector3.up * (halfHeight - radius);
        top = center + verticalOffset;
        bottom = center - verticalOffset;
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
