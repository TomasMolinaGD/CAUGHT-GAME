using CaughtGame.Navigation;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class BoidRespawnArea : MonoBehaviour
{
    [SerializeField] private LayerMask groundLayerMask = 1 << 8;
    [SerializeField] private LayerMask obstacleLayerMask = ~0;
    [SerializeField, Min(0.1f)] private float raycastHeight = 50f;
    [SerializeField, Min(0f)] private float groundOffset = 0.05f;
    [SerializeField, Min(0f)] private float capsuleClearanceMargin = 0.1f;
    [SerializeField, Min(1)] private int maximumPlacementAttempts = 10;
    [SerializeField] private HuntingGroundRegion huntingGroundRegion;

    private const int PhysicsResultCapacity = 64;
    private readonly RaycastHit[] groundHits = new RaycastHit[PhysicsResultCapacity];
    private readonly Collider[] overlapHits = new Collider[PhysicsResultCapacity];
    private BoxCollider areaCollider;

    /// <summary>The optional region used by Parcial 2 placements.</summary>
    public HuntingGroundRegion HuntingGroundRegion => huntingGroundRegion;

    /// <summary>
    /// Preserves the original rectangular placement for agents using LegacyRectangle.
    /// Required agents must use the overload that validates their configured region and capsule.
    /// </summary>
    public bool TryGetRandomPosition(out Vector3 position)
    {
        EnsureColliderReference();
        return TryGetLegacyPosition(out position);
    }

    /// <summary>
    /// Finds a bounded number of random walkable HuntingGround cell centers and accepts only
    /// candidates whose complete capsule footprint, ground support, and physical clearance pass.
    /// </summary>
    public bool TryGetRandomPosition(CapsuleCollider agentCapsule, out Vector3 position)
    {
        position = default;
        EnsureColliderReference();

        BoidAgent agent = agentCapsule != null ? agentCapsule.GetComponent<BoidAgent>() : null;
        bool requiresHuntingGround = agent != null && agent.RequiresHuntingGround;
        if (!requiresHuntingGround)
        {
            return TryGetLegacyPosition(out position);
        }

        if (!agent.TryGetRequiredHuntingGroundRegion(out HuntingGroundRegion requiredRegion) ||
            huntingGroundRegion != requiredRegion)
        {
            return false;
        }

        if (agentCapsule == null || maximumPlacementAttempts <= 0 || agentCapsule.attachedRigidbody == null)
        {
            return false;
        }

        Rigidbody agentBody = agentCapsule.attachedRigidbody;
        if (!BoidCapsuleGeometry.TryGetWorldCapsule(
                agentBody, agentBody.position, agentBody.rotation,
                out BoidCapsuleGeometry.WorldCapsule currentCapsule))
        {
            return false;
        }

        Transform agentRoot = agentBody.transform;
        Vector3 centerOffset = currentCapsule.Center - agentBody.position;
        for (int attempt = 0; attempt < maximumPlacementAttempts; attempt++)
        {
            if (!huntingGroundRegion.TryGetRandomWorldPosition(out Vector3 gridPosition) ||
                !HuntingGroundFootprint.Contains(
                    huntingGroundRegion, gridPosition,
                    currentCapsule.HorizontalFootprintRadius, capsuleClearanceMargin))
            {
                continue;
            }

            if (!TryFindGround(gridPosition, out RaycastHit groundHit))
            {
                continue;
            }

            Vector3 rootPosition = groundHit.point + Vector3.up * (groundOffset + currentCapsule.Height * 0.5f) - centerOffset;
            if (!BoidCapsuleGeometry.TryGetWorldCapsule(
                    agentBody, rootPosition, agentBody.rotation,
                    out BoidCapsuleGeometry.WorldCapsule placedCapsule))
            {
                continue;
            }

            if (!HuntingGroundFootprint.Contains(
                    huntingGroundRegion, placedCapsule.Center,
                    placedCapsule.HorizontalFootprintRadius, capsuleClearanceMargin))
            {
                continue;
            }

            if (!HasPhysicalClearance(
                    agentRoot, groundHit.collider, placedCapsule.Point0, placedCapsule.Point1,
                    placedCapsule.Radius + capsuleClearanceMargin))
            {
                continue;
            }

            position = rootPosition;
            return true;
        }

        return false;
    }

    private bool TryGetLegacyPosition(out Vector3 position)
    {
        for (int attempt = 0; attempt < maximumPlacementAttempts; attempt++)
        {
            Vector3 candidate = CreateRandomCandidate();
            Vector3 rayOrigin = candidate + Vector3.up * raycastHeight;
            if (Physics.Raycast(
                    rayOrigin, Vector3.down, out RaycastHit hit, raycastHeight * 2f,
                    groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                position = hit.point + Vector3.up * groundOffset;
                return true;
            }
        }

        // This fallback is retained only for the original non-region rectangular workflow.
        position = CreateRandomCandidate();
        position.y = transform.position.y + groundOffset;
        return true;
    }

    private bool TryFindGround(Vector3 candidate, out RaycastHit groundHit)
    {
        groundHit = default;
        Vector3 origin = candidate + Vector3.up * raycastHeight;
        int hitCount = Physics.RaycastNonAlloc(
            origin, Vector3.down, groundHits, raycastHeight * 2f,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);

        float closestDistance = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null || Vector3.Dot(hit.normal, Vector3.up) < 0.9f || !IsGroundSurface(hit.collider))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                groundHit = hit;
                found = true;
            }
        }

        // A full result buffer may have truncated nearer hits, so fail closed.
        return found && hitCount < groundHits.Length;
    }

    private bool IsGroundSurface(Collider candidate)
    {
        int layerBit = 1 << candidate.gameObject.layer;
        if ((groundLayerMask.value & layerBit) != 0)
        {
            return true;
        }

        GeneratedGeometryPartMarker partMarker = candidate.GetComponent<GeneratedGeometryPartMarker>();
        return candidate.gameObject.name == "Ground" &&
               partMarker != null &&
               partMarker.Owner != null &&
               partMarker.Owner.SourceGrid == GetSourceGrid();
    }

    private NavigationGrid GetSourceGrid()
    {
        return huntingGroundRegion != null ? huntingGroundRegion.SourceGrid : null;
    }

    private bool HasPhysicalClearance(
        Transform agentRoot, Collider groundCollider, Vector3 point0, Vector3 point1, float radius)
    {
        int count = Physics.OverlapCapsuleNonAlloc(
            point0, point1, radius, overlapHits, obstacleLayerMask, QueryTriggerInteraction.Ignore);

        if (count == overlapHits.Length)
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            Collider hit = overlapHits[i];
            if (hit == null || hit == groundCollider || hit.transform == agentRoot || hit.transform.IsChildOf(agentRoot))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private Vector3 CreateRandomCandidate()
    {
        Vector3 halfSize = areaCollider.size * 0.5f;
        Vector3 localPoint = areaCollider.center + new Vector3(
            Random.Range(-halfSize.x, halfSize.x),
            0f,
            Random.Range(-halfSize.z, halfSize.z));

        return transform.TransformPoint(localPoint);
    }

    private void EnsureColliderReference()
    {
        if (areaCollider == null)
        {
            areaCollider = GetComponent<BoxCollider>();
        }
    }

    private void Reset()
    {
        EnsureColliderReference();
        areaCollider.isTrigger = true;
    }

    private void OnValidate()
    {
        raycastHeight = Mathf.Max(0.1f, raycastHeight);
        groundOffset = Mathf.Max(0f, groundOffset);
        capsuleClearanceMargin = Mathf.Max(0f, capsuleClearanceMargin);
        maximumPlacementAttempts = Mathf.Max(1, maximumPlacementAttempts);
        EnsureColliderReference();
        if (areaCollider != null)
        {
            areaCollider.isTrigger = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        EnsureColliderReference();
        if (areaCollider == null) return;

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.8f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(areaCollider.center, areaCollider.size);
    }
}
