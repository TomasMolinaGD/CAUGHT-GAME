using UnityEngine;

[RequireComponent(typeof(BoidAgent), typeof(BoidSensor))]
public sealed class AlignmentBehaviour : SteeringBehaviour
{
    [SerializeField, Min(0.1f)] private float strength = 3f;

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

        averageVelocity /= validNeighborCount;
        averageVelocity = Vector3.ProjectOnPlane(averageVelocity, Vector3.up);

        if (averageVelocity.sqrMagnitude < 0.01f)
        {
            return Vector3.zero;
        }

        return (averageVelocity - owner.Velocity) * strength;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        strength = Mathf.Max(0.1f, strength);
    }
}
