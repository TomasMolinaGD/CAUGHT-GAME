using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(BoidAgent))]
public sealed class BoidSensor : MonoBehaviour
{
    private const int MaximumDetectedColliders = 64;

    [Header("Detection ranges")]
    [SerializeField, Min(0.1f)] private float perceptionRadius = 10f;
    [SerializeField, Min(0.1f)] private float separationRadius = 2.5f;
    [SerializeField, Min(0.1f)] private float threatDetectionRadius = 12f;
    [SerializeField, Min(0.1f)] private float interestDetectionRadius = 20f;

    [Header("Detection filter")]
    [SerializeField] private LayerMask boidLayerMask = ~0;
    [SerializeField] private LayerMask threatLayerMask = ~0;
    [SerializeField] private LayerMask interestLayerMask = ~0;

    private readonly Collider[] detectedColliders = new Collider[MaximumDetectedColliders];
    private readonly List<BoidAgent> neighbors = new List<BoidAgent>();
    private readonly List<BoidAgent> separationNeighbors = new List<BoidAgent>();
    private BoidAgent owner;
    private BoidThreat currentThreat;
    private BoidInterest currentInterest;

    public IReadOnlyList<BoidAgent> Neighbors => neighbors;
    public IReadOnlyList<BoidAgent> SeparationNeighbors => separationNeighbors;
    public BoidThreat CurrentThreat => currentThreat;
    public BoidInterest CurrentInterest => currentInterest;
    public float PerceptionRadius => perceptionRadius;
    public float SeparationRadius => separationRadius;
    public float ThreatDetectionRadius => threatDetectionRadius;
    public float InterestDetectionRadius => interestDetectionRadius;

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
        currentThreat = null;
        currentInterest = null;

        float queryRadius = Mathf.Max(
            perceptionRadius,
            Mathf.Max(threatDetectionRadius, interestDetectionRadius));
        int detectionMask =
            boidLayerMask.value |
            threatLayerMask.value |
            interestLayerMask.value;

        int detectedCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            queryRadius,
            detectedColliders,
            detectionMask,
            QueryTriggerInteraction.Collide);

        float perceptionRadiusSquared = perceptionRadius * perceptionRadius;
        float separationRadiusSquared = separationRadius * separationRadius;
        float threatRadiusSquared = threatDetectionRadius * threatDetectionRadius;
        float interestRadiusSquared = interestDetectionRadius * interestDetectionRadius;
        float nearestThreatDistanceSquared = float.PositiveInfinity;
        float nearestInterestDistanceSquared = float.PositiveInfinity;

        for (int i = 0; i < detectedCount; i++)
        {
            Collider detectedCollider = detectedColliders[i];
            if (detectedCollider == null)
            {
                continue;
            }

            BoidThreat detectedThreat = detectedCollider.GetComponentInParent<BoidThreat>();
            if (detectedThreat != null && detectedThreat.IsAvailable)
            {
                Vector3 threatOffset = detectedThreat.transform.position - transform.position;
                float threatDistanceSquared = threatOffset.sqrMagnitude;

                if (
                    threatDistanceSquared <= threatRadiusSquared &&
                    threatDistanceSquared < nearestThreatDistanceSquared)
                {
                    currentThreat = detectedThreat;
                    nearestThreatDistanceSquared = threatDistanceSquared;
                }
            }

            BoidInterest detectedInterest = detectedCollider.GetComponentInParent<BoidInterest>();
            if (detectedInterest != null && detectedInterest.IsAvailable)
            {
                Vector3 interestOffset = detectedInterest.transform.position - transform.position;
                float interestDistanceSquared = interestOffset.sqrMagnitude;

                if (
                    interestDistanceSquared <= interestRadiusSquared &&
                    interestDistanceSquared < nearestInterestDistanceSquared)
                {
                    currentInterest = detectedInterest;
                    nearestInterestDistanceSquared = interestDistanceSquared;
                }
            }

            BoidAgent detectedBoid = detectedCollider.GetComponentInParent<BoidAgent>();
            if (
                detectedBoid == null ||
                detectedBoid == owner ||
                !detectedBoid.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 offset = detectedBoid.transform.position - transform.position;
            if (offset.sqrMagnitude > perceptionRadiusSquared || neighbors.Contains(detectedBoid))
            {
                continue;
            }

            neighbors.Add(detectedBoid);

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
        threatDetectionRadius = Mathf.Max(0.1f, threatDetectionRadius);
        interestDetectionRadius = Mathf.Max(0.1f, interestDetectionRadius);
    }

}
