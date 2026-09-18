using UnityEngine;

[RequireComponent(typeof(BoidSensor))]
public sealed class InterestSpacingBehaviour : SteeringBehaviour
{
    [SerializeField, Min(0.1f)] private float distributionRadius = 5f;
    [SerializeField, Min(0.1f)] private float activationDistance = 2.5f;
    [SerializeField, Min(0.1f)] private float deactivationDistance = 2.8f;
    [SerializeField, Min(0.1f)] private float strength = 6f;

    private BoidSensor sensor;
    private bool isDistributing;
    private float circulationSign = 1f;

    public override bool IsApplicable
    {
        get
        {
            if (sensor == null || sensor.CurrentInterest == null ||
                !sensor.CurrentInterest.IsAvailable)
            {
                isDistributing = false;
                return false;
            }

            Vector3 offset = transform.position - sensor.CurrentInterest.Position;
            offset = Vector3.ProjectOnPlane(offset, Vector3.up);
            if (offset.sqrMagnitude > distributionRadius * distributionRadius)
            {
                isDistributing = false;
                return false;
            }

            BoidAgent closestNeighbor = FindClosestNeighbor();
            if (closestNeighbor == null)
            {
                isDistributing = false;
                return false;
            }

            Vector3 neighborOffset = transform.position - closestNeighbor.transform.position;
            neighborOffset = Vector3.ProjectOnPlane(neighborOffset, Vector3.up);
            float distanceThreshold = isDistributing
                ? deactivationDistance
                : activationDistance;

            if (neighborOffset.sqrMagnitude > distanceThreshold * distanceThreshold)
            {
                isDistributing = false;
                return false;
            }

            if (!isDistributing)
            {
                circulationSign = CalculateCirculationSign(closestNeighbor, offset);
                isDistributing = true;
            }

            return true;
        }
    }

    private void Awake()
    {
        sensor = GetComponent<BoidSensor>();
    }

    public override Vector3 CalculateSteering()
    {
        BoidAgent closestNeighbor = FindClosestNeighbor();
        if (closestNeighbor == null || sensor.CurrentInterest == null ||
            !sensor.CurrentInterest.IsAvailable)
        {
            return Vector3.zero;
        }

        Vector3 radialDirection =
            transform.position - sensor.CurrentInterest.Position;
        radialDirection = Vector3.ProjectOnPlane(radialDirection, Vector3.up);

        if (radialDirection.sqrMagnitude < 0.0001f)
        {
            radialDirection = transform.forward;
        }

        Vector3 tangent = Vector3.Cross(Vector3.up, radialDirection.normalized);
        Vector3 awayFromNeighbor = transform.position - closestNeighbor.transform.position;
        awayFromNeighbor = Vector3.ProjectOnPlane(awayFromNeighbor, Vector3.up);

        float distanceSquared = Mathf.Max(awayFromNeighbor.sqrMagnitude, 0.01f);
        return tangent * circulationSign * (strength / distanceSquared);
    }

    private float CalculateCirculationSign(
        BoidAgent closestNeighbor,
        Vector3 radialDirection)
    {
        if (radialDirection.sqrMagnitude < 0.0001f)
        {
            radialDirection = transform.forward;
        }

        Vector3 tangent = Vector3.Cross(Vector3.up, radialDirection.normalized);
        Vector3 awayFromNeighbor = transform.position - closestNeighbor.transform.position;
        awayFromNeighbor = Vector3.ProjectOnPlane(awayFromNeighbor, Vector3.up);
        float tangentialSide = Vector3.Dot(awayFromNeighbor, tangent);

        if (Mathf.Abs(tangentialSide) < 0.001f)
        {
            tangentialSide = GetInstanceID() < closestNeighbor.GetInstanceID() ? -1f : 1f;
        }

        return Mathf.Sign(tangentialSide);
    }

    private BoidAgent FindClosestNeighbor()
    {
        BoidAgent closestNeighbor = null;
        float closestDistanceSquared = float.PositiveInfinity;

        foreach (BoidAgent neighbor in sensor.Neighbors)
        {
            if (neighbor == null)
            {
                continue;
            }

            Vector3 offset = neighbor.transform.position - transform.position;
            offset = Vector3.ProjectOnPlane(offset, Vector3.up);
            float distanceSquared = offset.sqrMagnitude;

            if (distanceSquared < closestDistanceSquared)
            {
                closestNeighbor = neighbor;
                closestDistanceSquared = distanceSquared;
            }
        }

        return closestNeighbor;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        distributionRadius = Mathf.Max(0.1f, distributionRadius);
        activationDistance = Mathf.Clamp(activationDistance, 0.1f, distributionRadius);
        deactivationDistance = Mathf.Clamp(
            deactivationDistance,
            activationDistance,
            distributionRadius);
        strength = Mathf.Max(0.1f, strength);
    }
}
