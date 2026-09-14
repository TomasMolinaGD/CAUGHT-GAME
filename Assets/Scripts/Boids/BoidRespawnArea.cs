using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class BoidRespawnArea : MonoBehaviour
{
    [SerializeField] private LayerMask groundLayerMask = 1 << 8;
    [SerializeField, Min(0.1f)] private float raycastHeight = 50f;
    [SerializeField, Min(0f)] private float groundOffset = 0.05f;
    [SerializeField, Min(1)] private int maximumPlacementAttempts = 10;

    private BoxCollider areaCollider;

    private void Awake()
    {
        areaCollider = GetComponent<BoxCollider>();
    }

    public bool TryGetRandomPosition(out Vector3 position)
    {
        EnsureColliderReference();

        for (int attempt = 0; attempt < maximumPlacementAttempts; attempt++)
        {
            Vector3 candidate = CreateRandomCandidate();
            Vector3 rayOrigin = candidate + Vector3.up * raycastHeight;

            if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                raycastHeight * 2f,
                groundLayerMask,
                QueryTriggerInteraction.Ignore))
            {
                position = hit.point + Vector3.up * groundOffset;
                return true;
            }
        }

        position = default;
        return false;
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
        maximumPlacementAttempts = Mathf.Max(1, maximumPlacementAttempts);
        EnsureColliderReference();
        areaCollider.isTrigger = true;
    }

    private void OnDrawGizmosSelected()
    {
        EnsureColliderReference();

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.8f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(areaCollider.center, areaCollider.size);
    }
}
