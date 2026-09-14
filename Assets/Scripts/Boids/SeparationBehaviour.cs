using UnityEngine;

[RequireComponent(typeof(BoidSensor))]
public sealed class SeparationBehaviour : SteeringBehaviour
{
    [SerializeField, Min(0.1f)] private float strength = 10f;

    private BoidSensor sensor;

    public override bool IsApplicable =>
        sensor != null && sensor.SeparationNeighbors.Count > 0;

    private void Awake()
    {
        sensor = GetComponent<BoidSensor>();
    }

    public override Vector3 CalculateSteering()
    {
        BoidAgent closestNeighbor = null;
        float closestDistanceSquared = float.MaxValue;

        foreach (BoidAgent neighbor in sensor.SeparationNeighbors)
        {
            if (neighbor == null)
            {
                continue;
            }

            Vector3 offset = transform.position - neighbor.transform.position;
            float distanceSquared = Vector3.ProjectOnPlane(offset, Vector3.up).sqrMagnitude;
            if (distanceSquared < closestDistanceSquared)
            {
                closestNeighbor = neighbor;
                closestDistanceSquared = distanceSquared;
            }
        }

        if (closestNeighbor == null)
        {
            return Vector3.zero;
        }

        Vector3 awayFromNeighbor = transform.position - closestNeighbor.transform.position;
        awayFromNeighbor = Vector3.ProjectOnPlane(awayFromNeighbor, Vector3.up);

        if (closestDistanceSquared < 0.0001f)
        {
            float side = GetInstanceID() < closestNeighbor.GetInstanceID() ? -1f : 1f;
            awayFromNeighbor = transform.right * side;
            closestDistanceSquared = 0.01f;
        }

        return awayFromNeighbor.normalized * (strength / closestDistanceSquared);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        strength = Mathf.Max(0.1f, strength);
    }
}
