using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(BoidAgent))]
public sealed class BoidSensor : MonoBehaviour
{
    private const int MaximumDetectedColliders = 64;

    [Header("Detection ranges")]
    [SerializeField, Min(0.1f)] private float perceptionRadius = 6f;
    [SerializeField, Min(0.1f)] private float separationRadius = 1.5f;

    [Header("Detection filter")]
    [SerializeField] private LayerMask boidLayerMask = ~0;

    private readonly Collider[] detectedColliders = new Collider[MaximumDetectedColliders];
    private readonly List<BoidAgent> neighbors = new List<BoidAgent>();
    private readonly List<BoidAgent> separationNeighbors = new List<BoidAgent>();
    private BoidAgent owner;

    public IReadOnlyList<BoidAgent> Neighbors => neighbors;
    public IReadOnlyList<BoidAgent> SeparationNeighbors => separationNeighbors;
    public float PerceptionRadius => perceptionRadius;
    public float SeparationRadius => separationRadius;

    private void Awake()
    {
        owner = GetComponent<BoidAgent>();
    }

    private void FixedUpdate()
    {
        Refresh();
    }

    public void Refresh()
    {
        neighbors.Clear();
        separationNeighbors.Clear();

        int detectedCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            perceptionRadius,
            detectedColliders,
            boidLayerMask,
            QueryTriggerInteraction.Ignore);

        float separationRadiusSquared = separationRadius * separationRadius;

        for (int i = 0; i < detectedCount; i++)
        {
            Collider detectedCollider = detectedColliders[i];
            if (detectedCollider == null)
            {
                continue;
            }

            BoidAgent detectedBoid = detectedCollider.GetComponentInParent<BoidAgent>();
            if (detectedBoid == null || detectedBoid == owner)
            {
                continue;
            }

            if (neighbors.Contains(detectedBoid))
            {
                continue;
            }

            neighbors.Add(detectedBoid);

            Vector3 offset = detectedBoid.transform.position - transform.position;
            if (offset.sqrMagnitude <= separationRadiusSquared)
            {
                separationNeighbors.Add(detectedBoid);
            }
        }
    }

    private void OnValidate()
    {
        perceptionRadius = Mathf.Max(0.2f, perceptionRadius);
        separationRadius = Mathf.Clamp(separationRadius, 0.1f, perceptionRadius - 0.1f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, perceptionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, separationRadius);

        Gizmos.color = Color.cyan;
        for (int i = 0; i < neighbors.Count; i++)
        {
            if (neighbors[i] != null)
            {
                Gizmos.DrawLine(transform.position, neighbors[i].transform.position);
            }
        }
    }
}
