using UnityEngine;

[RequireComponent(typeof(BoidAgent), typeof(BoidSensor))]
public sealed class CohesionBehaviour : SteeringBehaviour
{
    [SerializeField, Min(0.1f)] private float strength = 2f;
    [SerializeField, Min(0f)] private float comfortRadius = 2f;
    [SerializeField, Min(0.1f)] private float slowingRadius = 5f;

    private BoidAgent owner;
    private BoidSensor sensor;

    private void Awake()
    {
        owner = GetComponent<BoidAgent>();
        sensor = GetComponent<BoidSensor>();
    }

    public override Vector3 CalculateSteering()
    {
        if (sensor.Neighbors.Count == 0)
        {
            return Vector3.zero;
        }

        Vector3 center = Vector3.zero;
        int validNeighborCount = 0;

        foreach (BoidAgent neighbor in sensor.Neighbors)
        {
            if (neighbor != null)
            {
                center += neighbor.transform.position;
                validNeighborCount++;
            }
        }

        if (validNeighborCount == 0)
        {
            return Vector3.zero;
        }

        center /= validNeighborCount;

        Vector3 directionToCenter = center - transform.position;
        directionToCenter = Vector3.ProjectOnPlane(directionToCenter, Vector3.up);

        float distanceToCenter = directionToCenter.magnitude;
        if (distanceToCenter <= comfortRadius)
        {
            return Vector3.zero;
        }

        float speedFactor = Mathf.InverseLerp(comfortRadius, slowingRadius, distanceToCenter);
        Vector3 desiredVelocity = directionToCenter.normalized * owner.MaxSpeed * speedFactor;
        return (desiredVelocity - owner.Velocity) * strength;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        strength = Mathf.Max(0.1f, strength);
        comfortRadius = Mathf.Max(0f, comfortRadius);
        slowingRadius = Mathf.Max(comfortRadius + 0.1f, slowingRadius);
    }
}
