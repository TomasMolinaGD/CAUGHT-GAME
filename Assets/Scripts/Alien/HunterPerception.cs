using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public sealed class HunterPerception : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float livingBoidDetectionRadius = 14f;
    [SerializeField, Min(0.1f)] private float deadBoidDetectionRadius = 18f;
    [SerializeField, Min(0.1f)] private float wolfDetectionRadius = 18f;
    [SerializeField] private LayerMask boidLayerMask = 1 << 9;
    [SerializeField] private LayerMask wolfLayerMask = ~0;
    [SerializeField] private LayerMask obstacleLayerMask = 1 << 12;
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.1f;
    [SerializeField, Min(0f)] private float wolfLossGraceDuration = 0.3f;
    [Header("Diagnostics")]
    [SerializeField] private bool showWolfLineOfSightDebug;

    private readonly Collider[] results = new Collider[32];
    private Collider[] wolfResults = new Collider[32];
    private readonly HashSet<BoidLife> livingBoids = new HashSet<BoidLife>();
    private readonly HashSet<BoidLife> deadBoids = new HashSet<BoidLife>();
    private readonly HashSet<ulong> loggedWolfBlockerPairs = new HashSet<ulong>();
    private float refreshTimer;
    private float wolfLossGraceRemaining;
    private int lastLoggedSelectedWolfId;

    public BoidLife ClosestLivingBoid { get; private set; }
    public BoidLife ClosestDeadBoid { get; private set; }
    public PredatorFSM ClosestWolf { get; private set; }
    public int LivingBoidCount => livingBoids.Count;
    public int DeadBoidCount => deadBoids.Count;
    public float LivingBoidDetectionRadius => livingBoidDetectionRadius;
    public float WolfDetectionRadius => wolfDetectionRadius;
    public bool HasRecentWolfThreat => wolfLossGraceRemaining > 0f;
    public float WolfLossGraceRemaining => wolfLossGraceRemaining;

    private void OnEnable()
    {
        refreshTimer = 0f;
        loggedWolfBlockerPairs.Clear();
        lastLoggedSelectedWolfId = 0;
    }

    public void Tick(float deltaTime)
    {
        float elapsed = Mathf.Max(0f, deltaTime);
        if (ClosestWolf == null)
        {
            wolfLossGraceRemaining = Mathf.Max(0f, wolfLossGraceRemaining - elapsed);
        }

        refreshTimer -= elapsed;
        if (refreshTimer > 0f)
        {
            return;
        }

        RefreshDetections();
        refreshTimer = refreshInterval;
    }

    [ContextMenu("Force Perception Refresh")]
    public void ForceRefresh()
    {
        Physics.SyncTransforms();
        RefreshDetections();
        refreshTimer = refreshInterval;
    }

    public void ClearDetections()
    {
        ClosestLivingBoid = null;
        ClosestDeadBoid = null;
        ClosestWolf = null;
        livingBoids.Clear();
        deadBoids.Clear();
        refreshTimer = 0f;
        wolfLossGraceRemaining = 0f;
    }

    private void RefreshDetections()
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

        RefreshWolfDetection();
    }

    private void RefreshWolfDetection()
    {
        ClosestWolf = null;
        float closestDistance = float.PositiveInfinity;
        int count = QueryWolfColliders();

        for (int index = 0; index < count; index++)
        {
            Collider detectedCollider = wolfResults[index];
            PredatorFSM wolf = detectedCollider != null
                ? detectedCollider.GetComponentInParent<PredatorFSM>()
                : null;
            if (wolf == null || !wolf.isActiveAndEnabled ||
                !HasWolfLineOfSight(wolf))
            {
                continue;
            }

            float distanceSquared = Vector3.ProjectOnPlane(
                wolf.transform.position - transform.position,
                Vector3.up).sqrMagnitude;
            if (distanceSquared > wolfDetectionRadius * wolfDetectionRadius ||
                distanceSquared >= closestDistance)
            {
                continue;
            }

            closestDistance = distanceSquared;
            ClosestWolf = wolf;
        }

        if (showWolfLineOfSightDebug)
        {
            ReportSelectedWolf();
        }

        if (ClosestWolf != null)
        {
            wolfLossGraceRemaining = wolfLossGraceDuration;
        }
    }

    private int QueryWolfColliders()
    {
        while (true)
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                wolfDetectionRadius,
                wolfResults,
                wolfLayerMask,
                QueryTriggerInteraction.Collide);
            if (count < wolfResults.Length || wolfResults.Length >= 256)
            {
                return count;
            }

            System.Array.Resize(ref wolfResults, wolfResults.Length * 2);
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
        return HasLineOfSight(boid.transform.position);
    }

    private bool HasLineOfSight(Vector3 targetPosition)
    {
        Vector3 origin = transform.position + Vector3.up;
        Vector3 destination = targetPosition + Vector3.up;
        return !Physics.Linecast(
            origin,
            destination,
            obstacleLayerMask,
            QueryTriggerInteraction.Ignore);
    }

    private bool HasWolfLineOfSight(PredatorFSM wolf)
    {
        Vector3 origin = transform.position + Vector3.up;
        Vector3 destination = wolf.transform.position + Vector3.up;
        bool blocked = Physics.Linecast(
            origin,
            destination,
            out RaycastHit hit,
            obstacleLayerMask,
            QueryTriggerInteraction.Ignore);

        if (showWolfLineOfSightDebug)
        {
            Debug.DrawLine(
                origin,
                destination,
                blocked ? Color.red : Color.green,
                Mathf.Max(refreshInterval, 0.02f),
                false);
            ReportWolfLineOfSightBlocker(wolf, blocked, hit);
        }

        return !blocked;
    }

    private void ReportWolfLineOfSightBlocker(PredatorFSM wolf, bool blocked, RaycastHit hit)
    {
        Collider blockingCollider = blocked ? hit.collider : null;
        if (blockingCollider == null)
        {
            return;
        }

        int blockerId = blockingCollider.GetInstanceID();
        int wolfId = wolf.GetInstanceID();
        ulong pairKey = ((ulong)(uint)wolfId << 32) | (uint)blockerId;
        if (!loggedWolfBlockerPairs.Add(pairKey))
        {
            return;
        }

        Debug.Log(
            $"Hunter wolf LOS to '{wolf.name}' blocked by '{blockingCollider.name}' " +
            $"(layer {blockingCollider.gameObject.layer}) at {hit.point}.",
            blockingCollider);
    }

    private void ReportSelectedWolf()
    {
        int selectedWolfId = ClosestWolf != null ? ClosestWolf.GetInstanceID() : 0;
        if (selectedWolfId == lastLoggedSelectedWolfId)
        {
            return;
        }

        lastLoggedSelectedWolfId = selectedWolfId;
        if (ClosestWolf != null)
        {
            Debug.Log($"Hunter wolf LOS selected '{ClosestWolf.name}' as the current threat.", this);
        }
        else
        {
            Debug.Log("Hunter wolf LOS cleared the current threat.", this);
        }
    }

    private void OnValidate()
    {
        livingBoidDetectionRadius = Mathf.Max(0.1f, livingBoidDetectionRadius);
        deadBoidDetectionRadius = Mathf.Max(0.1f, deadBoidDetectionRadius);
        wolfDetectionRadius = Mathf.Max(0.1f, wolfDetectionRadius);
        refreshInterval = Mathf.Max(0.02f, refreshInterval);
        wolfLossGraceDuration = Mathf.Max(0f, wolfLossGraceDuration);
        if (!showWolfLineOfSightDebug)
        {
            loggedWolfBlockerPairs.Clear();
            lastLoggedSelectedWolfId = 0;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, livingBoidDetectionRadius);
        Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, deadBoidDetectionRadius);
    }
}
