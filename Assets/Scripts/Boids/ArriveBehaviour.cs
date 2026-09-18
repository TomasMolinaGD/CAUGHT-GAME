using UnityEngine;

[RequireComponent(typeof(BoidAgent), typeof(BoidSensor))]
public sealed class ArriveBehaviour : SteeringBehaviour
{
    [SerializeField, Min(0.1f)] private float strength = 3f;
    [SerializeField, Min(0.1f)] private float maximumApproachSpeed = 2.5f;
    [SerializeField, Min(0f)] private float stoppingRadius = 3f;
    [SerializeField, Min(0.1f)] private float slowingRadius = 7f;

    private BoidAgent owner;
    private BoidSensor sensor;

    public override bool IsApplicable =>
        sensor != null && sensor.CurrentInterest != null && sensor.CurrentInterest.IsAvailable;

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

        Vector3 directionToInterest = interest.Position - transform.position;
        directionToInterest = Vector3.ProjectOnPlane(directionToInterest, Vector3.up);
        float distance = directionToInterest.magnitude;

        if (distance <= stoppingRadius || distance < 0.0001f)
        {
            return -owner.Velocity * strength;
        }

        float targetSpeedFactor = Mathf.InverseLerp(
            stoppingRadius,
            slowingRadius,
            distance);
        float approachSpeed = Mathf.Min(maximumApproachSpeed, owner.MaxSpeed);
        Vector3 desiredVelocity =
            directionToInterest.normalized * approachSpeed * targetSpeedFactor;

        return (desiredVelocity - owner.Velocity) * strength;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        strength = Mathf.Max(0.1f, strength);
        maximumApproachSpeed = Mathf.Max(0.1f, maximumApproachSpeed);
        stoppingRadius = Mathf.Max(0f, stoppingRadius);
        slowingRadius = Mathf.Max(stoppingRadius + 0.1f, slowingRadius);
    }
}
