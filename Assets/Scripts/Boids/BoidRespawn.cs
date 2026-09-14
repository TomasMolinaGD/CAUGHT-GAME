using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoidLife), typeof(Rigidbody))]
public class BoidRespawn : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float respawnDelay = 5f;
    [SerializeField] private BoidRespawnArea respawnArea;

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
        if (
            respawnArea != null &&
            respawnArea.TryGetRandomPosition(out Vector3 respawnPosition))
        {
            return respawnPosition;
        }

        return initialPosition;
    }

    private void OnValidate()
    {
        respawnDelay = Mathf.Max(0.1f, respawnDelay);
    }
}
