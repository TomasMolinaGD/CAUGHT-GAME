using UnityEngine;

[RequireComponent(typeof(BoidSensor))]
public sealed class SeparationBehaviour : SteeringBehaviour
{
    [SerializeField, Min(0.1f)] private float strength = 10f;

    private BoidSensor sensor;

    private void Awake()
    {
        sensor = GetComponent<BoidSensor>();
    }

    public override Vector3 CalculateSteering()
    {
        Vector3 separation = Vector3.zero;

        foreach (BoidAgent neighbor in sensor.SeparationNeighbors)
        {
            if (neighbor == null)
            {
                continue;
            }

            Vector3 awayFromNeighbor = transform.position - neighbor.transform.position;
            awayFromNeighbor = Vector3.ProjectOnPlane(awayFromNeighbor, Vector3.up);

            float distanceSquared = awayFromNeighbor.sqrMagnitude;
            if (distanceSquared < 0.0001f)
            {
                float side = GetInstanceID() < neighbor.GetInstanceID() ? -1f : 1f;
                awayFromNeighbor = transform.right * side;
                distanceSquared = 0.01f;
            }

            separation += awayFromNeighbor.normalized / distanceSquared;
        }

        return separation * strength;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        strength = Mathf.Max(0.1f, strength);
    }
}
