using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public sealed class HunterPerception : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float livingBoidDetectionRadius = 14f;
    [SerializeField, Min(0.1f)] private float deadBoidDetectionRadius = 18f;
    [SerializeField] private LayerMask boidLayerMask = 1 << 9;
    [SerializeField] private LayerMask obstacleLayerMask = 1 << 12;

    private readonly Collider[] results = new Collider[32];
    private readonly HashSet<BoidLife> livingBoids = new HashSet<BoidLife>();
    private readonly HashSet<BoidLife> deadBoids = new HashSet<BoidLife>();

    public BoidLife ClosestLivingBoid { get; private set; }
    public BoidLife ClosestDeadBoid { get; private set; }
    public int LivingBoidCount => livingBoids.Count;
    public int DeadBoidCount => deadBoids.Count;

    public void RefreshDetections()
    {
        ClosestLivingBoid = null;
        ClosestDeadBoid = null;
        livingBoids.Clear();
        deadBoids.Clear();
        float closestLivingDistance = float.PositiveInfinity;
        float closestDeadDistance = float.PositiveInfinity;
        float maximumRadius = Mathf.Max(livingBoidDetectionRadius, deadBoidDetectionRadius);

        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            maximumRadius,
            results,
            boidLayerMask,
            QueryTriggerInteraction.Collide);

        for (int index = 0; index < count; index++)
        {
            Collider detectedCollider = results[index];
            BoidLife boid = detectedCollider != null
                ? detectedCollider.GetComponentInParent<BoidLife>()
                : null;
            if (boid == null)
            {
                continue;
            }

            if (!HasLineOfSight(boid))
            {
                continue;
            }

            float distanceSquared = Vector3.ProjectOnPlane(
                boid.transform.position - transform.position,
                Vector3.up).sqrMagnitude;

            if (boid.IsAlive &&
                distanceSquared <= livingBoidDetectionRadius * livingBoidDetectionRadius)
            {
                livingBoids.Add(boid);
                if (distanceSquared < closestLivingDistance)
                {
                    closestLivingDistance = distanceSquared;
                    ClosestLivingBoid = boid;
                }
            }
            else if (boid.CanBeCollected &&
                distanceSquared <= deadBoidDetectionRadius * deadBoidDetectionRadius)
            {
                deadBoids.Add(boid);
                if (distanceSquared < closestDeadDistance)
                {
                    closestDeadDistance = distanceSquared;
                    ClosestDeadBoid = boid;
                }
            }
        }
    }

    public bool IsLivingBoidDetected(BoidLife boid)
    {
        return boid != null && boid.IsAlive && livingBoids.Contains(boid);
    }

    public bool IsDeadBoidDetected(BoidLife boid)
    {
        return boid != null && boid.CanBeCollected && deadBoids.Contains(boid);
    }

    private bool HasLineOfSight(BoidLife boid)
    {
        Vector3 origin = transform.position + Vector3.up;
        Vector3 destination = boid.transform.position + Vector3.up;
        return !Physics.Linecast(
            origin,
            destination,
            obstacleLayerMask,
            QueryTriggerInteraction.Ignore);
    }

    private void OnValidate()
    {
        livingBoidDetectionRadius = Mathf.Max(0.1f, livingBoidDetectionRadius);
        deadBoidDetectionRadius = Mathf.Max(0.1f, deadBoidDetectionRadius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, livingBoidDetectionRadius);
        Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, deadBoidDetectionRadius);
    }
}
