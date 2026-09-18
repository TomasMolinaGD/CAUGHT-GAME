using UnityEngine;

[RequireComponent(typeof(BoidAgent), typeof(BoidSensor))]
public sealed class EvadeBehaviour : SteeringBehaviour
{
    [SerializeField, Min(0.1f)] private float strength = 3f;

    private BoidAgent owner;
    private BoidSensor sensor;

    public override bool IsApplicable =>
        sensor != null && sensor.CurrentThreat != null && sensor.CurrentThreat.IsAvailable;

    private void Awake()
    {
        owner = GetComponent<BoidAgent>();
        sensor = GetComponent<BoidSensor>();
    }

    public override Vector3 CalculateSteering()
    {
        BoidThreat threat = sensor.CurrentThreat;
        if (threat == null)
        {
            return Vector3.zero;
        }

        Vector3 awayFromThreat = transform.position - threat.Position;
        awayFromThreat = Vector3.ProjectOnPlane(awayFromThreat, Vector3.up);

        if (awayFromThreat.sqrMagnitude < 0.0001f)
        {
            awayFromThreat = transform.forward;
        }

        Vector3 desiredVelocity = awayFromThreat.normalized * owner.MaxSpeed;
        return (desiredVelocity - owner.Velocity) *
            strength * threat.AvoidanceMultiplier;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        strength = Mathf.Max(0.1f, strength);
    }
}
