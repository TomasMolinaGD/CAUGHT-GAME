using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class BoidAgent : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float maxSpeed = 4f;
    [SerializeField, Min(0.1f)] private float maxAcceleration = 10f;
    [SerializeField, Min(0.1f)] private float rotationSpeed = 8f;

    public Vector3 Velocity { get; private set; }
    public float MaxSpeed => maxSpeed;

    private Rigidbody body;
    private SteeringBehaviour[] steeringBehaviours;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        steeringBehaviours = GetComponents<SteeringBehaviour>();
        Array.Sort(
            steeringBehaviours,
            (first, second) => second.Priority.CompareTo(first.Priority));
    }

    private void OnEnable()
    {
        Velocity = Vector3.zero;
    }

    private void FixedUpdate()
    {
        Vector3 steering = CalculatePrioritySteering();

        Velocity += steering * Time.fixedDeltaTime;
        Velocity = Vector3.ClampMagnitude(Velocity, maxSpeed);
        Velocity = Vector3.ProjectOnPlane(Velocity, Vector3.up);

        body.MovePosition(body.position + Velocity * Time.fixedDeltaTime);
        RotateTowardsVelocity();
    }

    private Vector3 CalculatePrioritySteering()
    {
        int behaviourIndex = 0;

        while (behaviourIndex < steeringBehaviours.Length)
        {
            int currentPriority = steeringBehaviours[behaviourIndex].Priority;
            Vector3 prioritySteering = Vector3.zero;
            bool hasApplicableBehaviour = false;

            while (
                behaviourIndex < steeringBehaviours.Length &&
                steeringBehaviours[behaviourIndex].Priority == currentPriority)
            {
                SteeringBehaviour behaviour = steeringBehaviours[behaviourIndex];
                if (behaviour.enabled && behaviour.IsApplicable)
                {
                    hasApplicableBehaviour = true;
                    prioritySteering += behaviour.CalculateSteering() * behaviour.Weight;
                }

                behaviourIndex++;
            }

            if (hasApplicableBehaviour)
            {
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
    }
}
