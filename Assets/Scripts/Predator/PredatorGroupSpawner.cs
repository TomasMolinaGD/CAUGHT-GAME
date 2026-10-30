using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class PredatorGroupSpawner : MonoBehaviour
{
    [SerializeField] private GameObject predatorPrefab;
    [SerializeField, Min(4)] private int minimumAgents = 4;
    [SerializeField, Min(0.1f)] private float patrolSpeed = 2.5f;
    [SerializeField, Min(0.1f)] private float pursuitSpeed = 5.4f;
    [SerializeField, Min(0.1f)] private float hunterPatrolSpeed = 3f;
    [SerializeField, Min(0.1f)] private float hunterMaximumSpeed = 6f;
    [SerializeField, Range(0.1f, 0.99f)] private float speedBelowHunterRatio = 0.9f;
    [SerializeField, Min(0.1f)] private float turnSpeed = 8f;
    [SerializeField, Range(0.1f, 1f)] private float captureRadius = 0.9f;
    [SerializeField, Min(0.1f)] private float viewDistance = 18f;
    [SerializeField, Range(1f, 360f)] private float fieldOfView = 120f;
    [FormerlySerializedAs("alienLayerMask")]
    [SerializeField] private LayerMask targetLayers = (1 << 6) | (1 << 7) | (1 << 9);
    [SerializeField] private LayerMask obstacleLayers = 1 << 12;
    [SerializeField, Min(0.02f)] private float perceptionRefreshInterval = 0.1f;
    [SerializeField, Min(0f)] private float eyeHeight = 1.5f;
    [SerializeField, Min(0.1f)] private float attackAnimationFallback = 1.5f;
    [SerializeField, Min(0.1f)] private float alertLifetime = 5f;
    [SerializeField] private AudioClip howlClip;
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
    [SerializeField] private List<Transform> patrolWaypoints = new List<Transform>();
    [Header("Targets")]
    [FormerlySerializedAs("alienTargets")]
    [SerializeField] private List<GameObject> targetObjects = new List<GameObject>();
    [SerializeField, Min(0.5f)] private float fallbackSpawnRadius = 4f;

    private readonly List<PredatorFSM> spawned = new List<PredatorFSM>();
    private PredatorAlertSystem alertSystem;
    public IReadOnlyList<PredatorFSM> SpawnedAgents => spawned;

    private void Start()
    {
        if (predatorPrefab == null)
        {
            Debug.LogError("PredatorGroupSpawner needs a Predator prefab assigned.", this);
            return;
        }

        ConfigureAlertSystem();
        SpawnAgents();
    }

    private void ConfigureAlertSystem()
    {
        alertSystem = GetComponent<PredatorAlertSystem>();
        if (alertSystem == null) alertSystem = gameObject.AddComponent<PredatorAlertSystem>();
        alertSystem.Configure(alertLifetime);
    }

    private void SpawnAgents()
    {
        for (int i = 0; i < minimumAgents; i++)
        {
            if (!TryResolveSpawnPosition(i, out Vector3 position, out Quaternion rotation))
            {
                Debug.LogWarning($"No clear Predator spawn position was found for agent {i}.", this);
                continue;
            }

            SpawnAgent(position, rotation);
        }
    }

    private void SpawnAgent(Vector3 position, Quaternion rotation)
    {
        GameObject instance = Instantiate(predatorPrefab, position, rotation, transform);
        PredatorFSM agent = instance.GetComponent<PredatorFSM>();
        if (agent == null) agent = instance.AddComponent<PredatorFSM>();
        agent.Configure(patrolSpeed, pursuitSpeed, hunterMaximumSpeed, hunterPatrolSpeed, turnSpeed,
            captureRadius, speedBelowHunterRatio, viewDistance, fieldOfView, targetLayers,
            obstacleLayers, perceptionRefreshInterval, eyeHeight,
            attackAnimationFallback, howlClip);
        agent.ConfigureAlertSystem(alertSystem);
        if (targetObjects.Count > 0) agent.ConfigureTargets(targetObjects);
        if (instance.GetComponent<PredatorDebugFeedback>() == null)
            instance.AddComponent<PredatorDebugFeedback>();
        agent.ConfigureWaypoints(patrolWaypoints);
        spawned.Add(agent);
    }

    private void OnValidate()
    {
        minimumAgents = Mathf.Max(4, minimumAgents);
        hunterMaximumSpeed = Mathf.Clamp(hunterMaximumSpeed, 0.1f, 6f);
        hunterPatrolSpeed = Mathf.Clamp(hunterPatrolSpeed, 0.1f, 3f);
        speedBelowHunterRatio = Mathf.Clamp(speedBelowHunterRatio, 0.1f, 0.99f);
        float maximumPredatorSpeed = hunterMaximumSpeed * speedBelowHunterRatio;
        pursuitSpeed = Mathf.Clamp(pursuitSpeed, 0.1f, maximumPredatorSpeed);
        float maximumPatrolSpeed = Mathf.Min(pursuitSpeed * 0.9f, hunterPatrolSpeed * 0.95f);
        patrolSpeed = Mathf.Clamp(patrolSpeed, 0.1f, maximumPatrolSpeed);
        turnSpeed = Mathf.Max(0.1f, turnSpeed);
        captureRadius = Mathf.Clamp(captureRadius, 0.1f, 1f);
        viewDistance = Mathf.Max(0.1f, viewDistance);
        fieldOfView = Mathf.Clamp(fieldOfView, 1f, 360f);
        perceptionRefreshInterval = Mathf.Max(0.02f, perceptionRefreshInterval);
        eyeHeight = Mathf.Max(0f, eyeHeight);
        attackAnimationFallback = Mathf.Max(0.1f, attackAnimationFallback);
        alertLifetime = Mathf.Max(0.1f, alertLifetime);
        fallbackSpawnRadius = Mathf.Max(0.5f, fallbackSpawnRadius);
    }

    private bool TryResolveSpawnPosition(int agentIndex, out Vector3 position, out Quaternion rotation)
    {
        if (agentIndex < spawnPoints.Count && spawnPoints[agentIndex] != null)
        {
            position = spawnPoints[agentIndex].position;
            rotation = spawnPoints[agentIndex].rotation;
        }
        else
        {
            float angle = agentIndex * 2.39996323f;
            position = transform.position + new Vector3(
                Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * fallbackSpawnRadius;
            rotation = transform.rotation;
        }

        if (IsSpawnClear(position)) return true;

        for (int attempt = 0; attempt < 32; attempt++)
        {
            float angle = (agentIndex + attempt) * 2.39996323f;
            float radius = fallbackSpawnRadius + (attempt / 8) * 1.5f;
            Vector3 candidate = transform.position + new Vector3(
                Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            if (!IsSpawnClear(candidate)) continue;
            position = candidate;
            rotation = transform.rotation;
            return true;
        }

        return false;
    }

    private bool IsSpawnClear(Vector3 position)
    {
        if (SafeZoneVolume.IsPositionSafe(position)) return false;
        for (int i = 0; i < spawned.Count; i++)
        {
            PredatorFSM agent = spawned[i];
            if (agent == null) continue;
            Vector3 separation = Vector3.ProjectOnPlane(position - agent.transform.position, Vector3.up);
            if (separation.sqrMagnitude < 0.8f * 0.8f) return false;
        }

        const float radius = 0.35f;
        const float height = 1.6f;
        Vector3 bottom = position + Vector3.up * radius;
        Vector3 top = position + Vector3.up * (height - radius);
        return !Physics.CheckCapsule(bottom, top, radius, obstacleLayers,
            QueryTriggerInteraction.Ignore);
    }
}
