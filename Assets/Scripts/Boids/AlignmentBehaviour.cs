using UnityEngine;

[RequireComponent(typeof(BoidAgent), typeof(BoidSensor))]
public sealed class AlignmentBehaviour : SteeringBehaviour
{
    [SerializeField, Min(0.1f)] private float strength = 3f;
    [SerializeField, Min(0.1f)] private float directionResponse = 4f;

    private BoidAgent owner;
    private BoidSensor sensor;
    private Vector3 smoothedDirection;

    private void Awake()
    {
        owner = GetComponent<BoidAgent>();
        sensor = GetComponent<BoidSensor>();
        smoothedDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
    }

    public override Vector3 CalculateSteering()
    {
        if (sensor.Neighbors.Count == 0)
        {
            return Vector3.zero;
        }

        Vector3 averageVelocity = Vector3.zero;
        int validNeighborCount = 0;

        foreach (BoidAgent neighbor in sensor.Neighbors)
        {
            if (neighbor != null)
            {
                averageVelocity += neighbor.Velocity;
                validNeighborCount++;
            }
        }

        if (validNeighborCount == 0)
        {
            return Vector3.zero;
        }

        averageVelocity = Vector3.ProjectOnPlane(averageVelocity, Vector3.up);

        if (averageVelocity.sqrMagnitude < 0.01f)
        {
            return Vector3.zero;
        }

        Vector3 targetDirection = averageVelocity.normalized;
        float response = 1f - Mathf.Exp(-directionResponse * Time.fixedDeltaTime);
        smoothedDirection = Vector3.Slerp(smoothedDirection, targetDirection, response).normalized;
        Vector3 desiredVelocity = smoothedDirection * owner.MaxSpeed;
        return (desiredVelocity - owner.Velocity) * strength;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        strength = Mathf.Max(0.1f, strength);
        directionResponse = Mathf.Max(0.1f, directionResponse);
    }
}
