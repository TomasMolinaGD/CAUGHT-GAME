using System.Collections.Generic;
using UnityEngine;

/// <summary>Creates the configured minimum group at scene start.</summary>
[DisallowMultipleComponent]
public sealed class PredatorGroupSpawner : MonoBehaviour
{
    [SerializeField] private GameObject predatorPrefab;
    [SerializeField, Min(4)] private int minimumAgents = 4;
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
    [SerializeField] private List<Transform> patrolWaypoints = new List<Transform>();
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

        if (patrolWaypoints.Count == 0)
        {
            GameObject waypointRoot = GameObject.Find("WayPoints");
            if (waypointRoot != null)
            {
                for (int i = 0; i < waypointRoot.transform.childCount; i++)
                    patrolWaypoints.Add(waypointRoot.transform.GetChild(i));
            }
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
            if (instance.GetComponent<PredatorDebugFeedback>() == null)
                instance.AddComponent<PredatorDebugFeedback>();
            agent.ConfigureWaypoints(patrolWaypoints);
            spawned.Add(agent);
        }
    }

    private void OnValidate()
    {
        minimumAgents = Mathf.Max(4, minimumAgents);
        fallbackSpawnRadius = Mathf.Max(0.5f, fallbackSpawnRadius);
    }
}
