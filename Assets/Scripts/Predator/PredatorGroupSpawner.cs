using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PredatorGroupSpawner : MonoBehaviour
{
    [SerializeField] private GameObject predatorPrefab;
    [SerializeField, Min(4)] private int minimumAgents = 4;
    [SerializeField, Min(0.1f)] private float patrolSpeed = 6f;
    [SerializeField, Min(0.1f)] private float pursuitSpeed = 7.6f;
    [SerializeField, Min(0.1f)] private float hunterMaximumSpeed = 8f;
    [SerializeField, Range(0.1f, 0.99f)] private float speedBelowHunterRatio = 0.95f;
    [SerializeField, Min(0.1f)] private float turnSpeed = 8f;
    [SerializeField, Range(0.1f, 1f)] private float captureRadius = 0.9f;
    [SerializeField, Min(0.1f)] private float viewDistance = 18f;
    [SerializeField, Range(1f, 360f)] private float fieldOfView = 120f;
    [SerializeField] private LayerMask alienLayerMask = (1 << 9) | (1 << 10);
    [SerializeField] private LayerMask obstacleLayers = 1 << 12;
    [SerializeField, Min(0.02f)] private float perceptionRefreshInterval = 0.1f;
    [SerializeField, Min(0f)] private float eyeHeight = 1.5f;
    [SerializeField, Min(0.1f)] private float attackAnimationFallback = 1.5f;
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
    [SerializeField] private List<Transform> patrolWaypoints = new List<Transform>();
    [SerializeField] private List<GameObject> alienTargets = new List<GameObject>();
    [SerializeField, Min(0.5f)] private float fallbackSpawnRadius = 4f;

    private readonly List<PredatorFSM> spawned = new List<PredatorFSM>();
    public IReadOnlyList<PredatorFSM> SpawnedAgents => spawned;

    private void Start()
    {
        if (predatorPrefab == null)
        {
            Debug.LogError("PredatorGroupSpawner needs a Predator prefab assigned.", this);
            return;
        }

        for (int i = 0; i < minimumAgents; i++)
        {
            Vector3 position;
            Quaternion rotation;
            if (i < spawnPoints.Count && spawnPoints[i] != null)
            {
                position = spawnPoints[i].position;
                rotation = spawnPoints[i].rotation;
            }
            else
            {
                float angle = i * 2.39996323f;
                position = transform.position + new Vector3(
                    Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * fallbackSpawnRadius;
                rotation = transform.rotation;
            }

            GameObject instance = Instantiate(predatorPrefab, position, rotation, transform);
            PredatorFSM agent = instance.GetComponent<PredatorFSM>();
            if (agent == null) agent = instance.AddComponent<PredatorFSM>();
            agent.Configure(patrolSpeed, pursuitSpeed, hunterMaximumSpeed, turnSpeed,
                captureRadius, speedBelowHunterRatio, viewDistance, fieldOfView, alienLayerMask,
                obstacleLayers, perceptionRefreshInterval, eyeHeight,
                attackAnimationFallback);
            if (alienTargets.Count > 0)
                agent.ConfigureTargets(alienTargets);
            if (instance.GetComponent<PredatorDebugFeedback>() == null)
                instance.AddComponent<PredatorDebugFeedback>();
            agent.ConfigureWaypoints(patrolWaypoints);
            spawned.Add(agent);
        }
    }

    private void OnValidate()
    {
        alienLayerMask |= 1 << 10;
        minimumAgents = Mathf.Max(4, minimumAgents);
        hunterMaximumSpeed = Mathf.Max(8f, hunterMaximumSpeed);
        speedBelowHunterRatio = Mathf.Clamp(speedBelowHunterRatio, 0.95f, 0.99f);
        float maximumPredatorSpeed = hunterMaximumSpeed * speedBelowHunterRatio;
        float minimumPursuitSpeed = Mathf.Min(hunterMaximumSpeed * 0.9f, maximumPredatorSpeed);
        pursuitSpeed = Mathf.Clamp(pursuitSpeed, minimumPursuitSpeed, maximumPredatorSpeed);
        float maximumPatrolSpeed = pursuitSpeed * 0.9f;
        float minimumPatrolSpeed = Mathf.Min(hunterMaximumSpeed * 0.75f, maximumPatrolSpeed);
        patrolSpeed = Mathf.Clamp(patrolSpeed, minimumPatrolSpeed, maximumPatrolSpeed);
        turnSpeed = Mathf.Max(0.1f, turnSpeed);
        captureRadius = Mathf.Clamp(captureRadius, 0.1f, 1f);
        viewDistance = Mathf.Max(0.1f, viewDistance);
        fieldOfView = Mathf.Clamp(fieldOfView, 1f, 360f);
        perceptionRefreshInterval = Mathf.Max(0.02f, perceptionRefreshInterval);
        eyeHeight = Mathf.Max(0f, eyeHeight);
        attackAnimationFallback = Mathf.Max(0.1f, attackAnimationFallback);
        fallbackSpawnRadius = Mathf.Max(0.5f, fallbackSpawnRadius);
    }
}
