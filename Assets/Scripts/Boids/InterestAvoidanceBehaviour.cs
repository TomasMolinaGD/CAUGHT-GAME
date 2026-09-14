using UnityEngine;

[RequireComponent(typeof(BoidAgent), typeof(BoidSensor))]
public sealed class InterestAvoidanceBehaviour : SteeringBehaviour
{
    [SerializeField, Min(0.1f)] private float avoidanceRadius = 2f;
    [SerializeField, Min(0.1f)] private float strength = 4f;

    private BoidAgent owner;
    private BoidSensor sensor;

    public override bool IsApplicable
    {
        get
        {
            if (sensor == null || sensor.CurrentInterest == null)
            {
                return false;
            }

            Vector3 offset = transform.position - sensor.CurrentInterest.transform.position;
            offset = Vector3.ProjectOnPlane(offset, Vector3.up);
            return offset.sqrMagnitude < avoidanceRadius * avoidanceRadius;
        }
    }

    private void Awake()
    {
        owner = GetComponent<BoidAgent>();
        sensor = GetComponent<BoidSensor>();
    }

    public override Vector3 CalculateSteering()
    {
        BoidInterest interest = sensor.CurrentInterest;
        if (interest == null)
        {
            return Vector3.zero;
        }

        Vector3 awayFromInterest = transform.position - interest.transform.position;
        awayFromInterest = Vector3.ProjectOnPlane(awayFromInterest, Vector3.up);

        if (awayFromInterest.sqrMagnitude < 0.0001f)
        {
            awayFromInterest = transform.forward;
        }

        Vector3 desiredVelocity = awayFromInterest.normalized * owner.MaxSpeed;
        return (desiredVelocity - owner.Velocity) * strength;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        avoidanceRadius = Mathf.Max(0.1f, avoidanceRadius);
        strength = Mathf.Max(0.1f, strength);
    }
}
