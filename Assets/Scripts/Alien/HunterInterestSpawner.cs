using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HunterInterestSpawner : MonoBehaviour
{
    [SerializeField] private GameObject interestPrefab;
    [SerializeField, Min(1)] private int maximumActiveInterests = 5;
    [SerializeField, Min(0.1f)] private float spawnInterval = 8f;
    [SerializeField, Min(0f)] private float initialSpawnDelay = 2f;

    [Header("Spawn Area")]
    [SerializeField] private Vector2 areaCenter = new Vector2(484f, 623f);
    [SerializeField] private Vector2 areaHalfExtents = new Vector2(42f, 26f);
    [SerializeField] private float spawnHeight = 1.05f;
    [SerializeField, Min(0.1f)] private float clearanceRadius = 3f;
    [SerializeField, Min(1)] private int placementAttempts = 16;
    [SerializeField] private LayerMask blockedLayers = (1 << 11) | (1 << 12);

    private readonly List<GameObject> activeInterests = new List<GameObject>();
    private float spawnTimer;
    private Transform runtimeContainer;

    public int ActiveInterestCount
    {
        get
        {
            CleanupDestroyedInterests();
            return activeInterests.Count;
        }
    }

    private void Awake()
    {
        spawnTimer = initialSpawnDelay;
    }

    public void Configure(GameObject prefab)
    {
        interestPrefab = prefab;
    }

    public void Tick(float deltaTime)
    {
        CleanupDestroyedInterests();
        if (interestPrefab == null || activeInterests.Count >= maximumActiveInterests)
        {
            return;
        }

        spawnTimer -= Mathf.Max(0f, deltaTime);
        if (spawnTimer > 0f)
        {
            return;
        }

        if (TryFindSpawnPosition(out Vector3 spawnPosition))
        {
            EnsureRuntimeContainer();
            GameObject interest = Instantiate(
                interestPrefab,
                spawnPosition,
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                runtimeContainer);
            interest.name = $"Hunter Interest {activeInterests.Count + 1:00}";
            activeInterests.Add(interest);
        }

        spawnTimer = spawnInterval;
    }

    private bool TryFindSpawnPosition(out Vector3 spawnPosition)
    {
        for (int attempt = 0; attempt < placementAttempts; attempt++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(areaCenter.x - areaHalfExtents.x, areaCenter.x + areaHalfExtents.x),
                spawnHeight,
                Random.Range(areaCenter.y - areaHalfExtents.y, areaCenter.y + areaHalfExtents.y));

            if (!Physics.CheckSphere(
                candidate,
                clearanceRadius,
                blockedLayers,
                QueryTriggerInteraction.Ignore))
            {
                spawnPosition = candidate;
                return true;
            }
        }

        spawnPosition = default;
        return false;
    }

    private void CleanupDestroyedInterests()
    {
        for (int index = activeInterests.Count - 1; index >= 0; index--)
        {
            if (activeInterests[index] == null)
            {
                activeInterests.RemoveAt(index);
            }
        }
    }

    private void EnsureRuntimeContainer()
    {
        if (runtimeContainer != null)
        {
            return;
        }

        runtimeContainer = new GameObject("Runtime Hunter Interests").transform;
    }

    private void OnValidate()
    {
        maximumActiveInterests = Mathf.Max(1, maximumActiveInterests);
        spawnInterval = Mathf.Max(0.1f, spawnInterval);
        initialSpawnDelay = Mathf.Max(0f, initialSpawnDelay);
        areaHalfExtents.x = Mathf.Max(1f, areaHalfExtents.x);
        areaHalfExtents.y = Mathf.Max(1f, areaHalfExtents.y);
        clearanceRadius = Mathf.Max(0.1f, clearanceRadius);
        placementAttempts = Mathf.Max(1, placementAttempts);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0.75f, 0.75f);
        Gizmos.DrawWireCube(
            new Vector3(areaCenter.x, spawnHeight, areaCenter.y),
            new Vector3(areaHalfExtents.x * 2f, 0.2f, areaHalfExtents.y * 2f));
    }
}
