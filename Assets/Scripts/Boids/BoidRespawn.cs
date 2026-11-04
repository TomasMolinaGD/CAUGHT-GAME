using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoidLife), typeof(Rigidbody))]
public class BoidRespawn : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float respawnDelay = 5f;
    [SerializeField, Min(0.1f)] private float retryDelay = 2f;
    [SerializeField, Min(0)] private int maximumRespawnRetries = 2;
    [SerializeField] private BoidRespawnArea respawnArea;

    private BoidLife life;
    private Rigidbody body;
    private BoidAgent boidAgent;
    private Vector3 initialPosition;
    private Coroutine respawnRoutine;

    public event Action RespawnPlacementFailed;

    private void Awake()
    {
        life = GetComponent<BoidLife>();
        body = GetComponent<Rigidbody>();
        boidAgent = GetComponent<BoidAgent>();
        initialPosition = transform.position;
        life.Collected += HandleCollection;
    }

    private void OnDestroy()
    {
        if (life != null)
        {
            life.Collected -= HandleCollection;
        }
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

        for (int retry = 0; retry <= maximumRespawnRetries; retry++)
        {
            if (TryRespawnNow())
            {
                respawnRoutine = null;
                yield break;
            }

            if (retry < maximumRespawnRetries)
            {
                yield return new WaitForSeconds(retryDelay);
            }
        }

        respawnRoutine = null;
        RespawnPlacementFailed?.Invoke();
    }

    /// <summary>Performs one bounded placement attempt; false leaves the Boid inactive.</summary>
    public bool TryRespawnNow()
    {
        if (life == null || body == null || life.State != BoidLifeState.Respawning)
        {
            return false;
        }

        Vector3 respawnPosition;
        if (respawnArea != null)
        {
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (!respawnArea.TryGetRandomPosition(capsule, out respawnPosition))
            {
                return false;
            }
        }
        else
        {
            if (boidAgent != null && boidAgent.RequiresHuntingGround)
            {
                return false;
            }

            // Keep the original Parcial 1 behavior when no region-backed area is configured.
            respawnPosition = initialPosition;
        }

        transform.position = respawnPosition;
        body.position = respawnPosition;
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        life.Respawn();
        return true;
    }

    public void SetRespawnArea(BoidRespawnArea area)
    {
        respawnArea = area;
    }

    private void OnValidate()
    {
        respawnDelay = Mathf.Max(0.1f, respawnDelay);
        retryDelay = Mathf.Max(0.1f, retryDelay);
        maximumRespawnRetries = Mathf.Max(0, maximumRespawnRetries);
    }
}
