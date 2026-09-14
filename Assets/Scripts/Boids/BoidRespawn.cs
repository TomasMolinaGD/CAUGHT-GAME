using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoidLife), typeof(Rigidbody))]
public class BoidRespawn : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float respawnDelay = 5f;
    [SerializeField, Min(0f)] private float respawnRadius = 15f;
    [SerializeField] private LayerMask groundLayerMask = 1 << 8;
    [SerializeField, Min(0.1f)] private float raycastHeight = 50f;
    [SerializeField, Min(0f)] private float groundOffset = 0.05f;

    private BoidLife life;
    private Rigidbody body;
    private Vector3 initialPosition;
    private Coroutine respawnRoutine;

    private void Awake()
    {
        life = GetComponent<BoidLife>();
        body = GetComponent<Rigidbody>();
        initialPosition = transform.position;
        life.Collected += HandleCollection;
    }

    private void OnDestroy()
    {
        life.Collected -= HandleCollection;
    }

    private void HandleCollection(BoidLife _)
    {
        if (respawnRoutine == null)
        {
            respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);

        Vector3 respawnPosition = FindRespawnPosition();
        transform.position = respawnPosition;
        body.position = respawnPosition;
        respawnRoutine = null;
        life.Respawn();
    }

    private Vector3 FindRespawnPosition()
    {
        Vector2 randomOffset = Random.insideUnitCircle * respawnRadius;
        Vector3 candidate = initialPosition + new Vector3(randomOffset.x, 0f, randomOffset.y);
        Vector3 rayOrigin = candidate + Vector3.up * raycastHeight;

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            raycastHeight * 2f,
            groundLayerMask,
            QueryTriggerInteraction.Ignore))
        {
            candidate.y = hit.point.y + groundOffset;
        }
        else
        {
            candidate.y = initialPosition.y;
        }

        return candidate;
    }

    private void OnValidate()
    {
        respawnDelay = Mathf.Max(0.1f, respawnDelay);
        respawnRadius = Mathf.Max(0f, respawnRadius);
        raycastHeight = Mathf.Max(0.1f, raycastHeight);
        groundOffset = Mathf.Max(0f, groundOffset);
    }
}
