using UnityEngine;

[RequireComponent(typeof(BoidAgent), typeof(BoidSensor))]
public class EmergencySeparationBehaviour : SteeringBehaviour
{
    [SerializeField, Min(0.1f)] private float emergencyRadius = 1.1f;
    [SerializeField, Min(0.1f)] private float strength = 4f;

    private BoidSensor sensor;

    public float EmergencyRadius => emergencyRadius;

    public override bool IsApplicable =>
        CalculateRepulsion().sqrMagnitude > 0.0001f;

    private void Awake()
    {
        sensor = GetComponent<BoidSensor>();
    }

    public override Vector3 CalculateSteering()
    {
        return CalculateRepulsion() * strength;
    }

    private Vector3 CalculateRepulsion()
    {
        if (sensor == null)
        {
            return Vector3.zero;
        }

        Vector3 combinedRepulsion = Vector3.zero;
        int contributingNeighbors = 0;

        foreach (BoidAgent neighbor in sensor.SeparationNeighbors)
        {
            if (neighbor == null)
            {
                continue;
            }

            Vector3 away = transform.position - neighbor.transform.position;
            away = Vector3.ProjectOnPlane(away, Vector3.up);
            float distance = away.magnitude;

            if (distance > emergencyRadius)
            {
                continue;
            }

            if (distance < 0.001f)
            {
                float side = GetInstanceID() < neighbor.GetInstanceID() ? -1f : 1f;
                away = transform.right * side;
                distance = 0f;
            }

            float proximity = 1f - Mathf.Clamp01(distance / emergencyRadius);
            combinedRepulsion += away.normalized * proximity;
            contributingNeighbors++;
        }

        return contributingNeighbors > 0
            ? combinedRepulsion / contributingNeighbors
            : Vector3.zero;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        emergencyRadius = Mathf.Max(0.1f, emergencyRadius);
        strength = Mathf.Max(0.1f, strength);
    }

}
