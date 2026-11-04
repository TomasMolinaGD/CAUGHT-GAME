using System.Collections.Generic;
using CaughtGame.Navigation;
using UnityEngine;

/// <summary>
/// Bridges graph navigation decisions to the Predator's physical movement component.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(GraphNavigationAgent), typeof(PredatorMovement))]
public sealed class PredatorNavigationMovementController : MonoBehaviour
{
    [SerializeField] private GraphNavigationAgent navigationAgent;
    [SerializeField] private PredatorMovement movement;
    [SerializeField] private MonoBehaviour graphProvider;
    [SerializeField] private bool avoidSafeZones;
    [SerializeField, Min(0.1f)] private float agentRadius = 0.4f;
    [SerializeField, Min(0.1f)] private float agentHeight = 1.8f;
    [SerializeField] private bool chase;
    [SerializeField, Min(0f)] private float stoppingDistance = 0.25f;

    private bool hasDestination;
    private bool providerConfigured;
    private bool agentConfigured;
    private Vector3 destination;
    private readonly List<Vector3> safeZoneRecoveryPath = new List<Vector3>();
    private int safeZoneRecoveryIndex;
    private bool recoveryAttemptedForDestination;

    public GraphNavigationAgent.NavigationStatus Status =>
        navigationAgent != null ? navigationAgent.Status : GraphNavigationAgent.NavigationStatus.MissingProvider;
    public bool HasDestination => hasDestination;
    public Vector3 RequestedDestination => destination;
    public Vector3 MovementTarget => navigationAgent != null ? navigationAgent.MovementTarget : transform.position;
    public bool UsesChaseMovement => chase;
    public float StoppingDistance => stoppingDistance;
    public int TickCount { get; private set; }
    public bool LastTickAccepted { get; private set; }
    public bool LastTickCommandedMovement { get; private set; }
    public bool IsRecoveringFromInvalidOrigin { get; private set; }

    private void Awake()
    {
        ResolveDependencies();
    }

    public void Configure(GraphNavigationAgent agent, PredatorMovement predatorMovement,
        bool pursue, float arrivalDistance)
    {
        navigationAgent = agent;
        movement = predatorMovement;
        providerConfigured = false;
        agentConfigured = false;
        ConfigureMovement(pursue, arrivalDistance);
    }

    public void ConfigureMovement(bool pursue, float arrivalDistance)
    {
        chase = pursue;
        stoppingDistance = Mathf.Max(0f, arrivalDistance);
    }

    public void ConfigureGraphProvider(MonoBehaviour provider)
    {
        graphProvider = provider;
        providerConfigured = false;
        ResolveDependencies();
    }

    public bool IsPositionInSafeZone(Vector3 position)
    {
        ResolveDependencies();
        return graphProvider is NavigationGridGraphProvider gridProvider &&
            gridProvider.IsPositionInSafeZone(position);
    }

    internal bool IsWolfMovementSegmentTraversable(Vector3 start, Vector3 movementDelta,
        float radius)
    {
        ResolveDependencies();
        return !(graphProvider is NavigationGridGraphProvider gridProvider) ||
            !gridProvider.HasNavigationGrid ||
            gridProvider.IsWolfMovementSegmentTraversable(start, movementDelta, radius);
    }

    internal bool IsWolfRecoverySegmentTraversable(Vector3 start, Vector3 movementDelta,
        float radius)
    {
        ResolveDependencies();
        return graphProvider is NavigationGridGraphProvider gridProvider &&
            gridProvider.IsWolfRecoverySegmentTraversable(start, movementDelta, radius);
    }

    public void RequestDestination(Vector3 target)
    {
        ResolveDependencies();
        if (navigationAgent == null || !IsFinite(target))
        {
            CancelDestination();
            return;
        }

        destination = target;
        hasDestination = true;
        ResetRecoveryState();
        recoveryAttemptedForDestination = false;
        navigationAgent.InvalidatePath();
    }

    public void CancelDestination()
    {
        hasDestination = false;
        ResetRecoveryState();
        recoveryAttemptedForDestination = false;
        navigationAgent?.InvalidatePath();
        movement?.Stop();
    }

    public void TickNavigation()
    {
        TickCount++;
        LastTickAccepted = false;
        LastTickCommandedMovement = false;
        ResolveDependencies();
        if (!hasDestination)
        {
            return;
        }

        if (navigationAgent == null || movement == null)
        {
            movement?.Stop();
            return;
        }
        if (IsRecoveringFromInvalidOrigin)
        {
            TickSafeZoneRecovery();
            return;
        }

        if (TryStartSafeZoneRecovery())
        {
            navigationAgent.InvalidatePath();
            TickSafeZoneRecovery();
            return;
        }

        bool moving;
        bool accepted = navigationAgent.TryMoveTowards(destination, 0f, stoppingDistance, out moving);
        LastTickAccepted = accepted;
        GraphNavigationAgent.NavigationStatus status = navigationAgent.Status;
        if (status == GraphNavigationAgent.NavigationStatus.Arrived)
        {
            hasDestination = false;
            movement.Stop();
            return;
        }

        if (!accepted || status == GraphNavigationAgent.NavigationStatus.NoPath ||
            status == GraphNavigationAgent.NavigationStatus.MissingProvider)
        {
            movement.Stop();
            return;
        }

        if (status == GraphNavigationAgent.NavigationStatus.FollowingDirect ||
            status == GraphNavigationAgent.NavigationStatus.FollowingPath)
        {
            if (moving)
            {
                float movementStoppingDistance = navigationAgent.IsFinalMovementTarget
                    ? stoppingDistance
                    : 0f;
                movement.MoveTowards(navigationAgent.MovementTarget, chase, movementStoppingDistance);
                LastTickCommandedMovement = true;
            }
            else
                movement.Stop();
        }
    }

    private void Update()
    {
        TickNavigation();
    }

    private bool TryStartSafeZoneRecovery()
    {
        if (recoveryAttemptedForDestination ||
            !(graphProvider is NavigationGridGraphProvider gridProvider) ||
            !gridProvider.IsPositionInSafeZone(transform.position)) return false;

        recoveryAttemptedForDestination = true;
        safeZoneRecoveryPath.Clear();
        if (!gridProvider.TryBuildSafeZoneRecoveryPath(transform.position,
            movement.CollisionRadius, movement.CollisionHeight, safeZoneRecoveryPath)) return false;

        safeZoneRecoveryIndex = 0;
        IsRecoveringFromInvalidOrigin = true;
        return true;
    }

    private void TickSafeZoneRecovery()
    {
        if (!(graphProvider is NavigationGridGraphProvider gridProvider) || movement == null)
        {
            FailSafeZoneRecovery();
            return;
        }

        if (!gridProvider.IsWolfFootprintInSafeZone(
            transform.position, movement.CollisionRadius))
        {
            CompleteSafeZoneRecovery();
            return;
        }

        while (safeZoneRecoveryIndex < safeZoneRecoveryPath.Count &&
            Vector3.ProjectOnPlane(safeZoneRecoveryPath[safeZoneRecoveryIndex] -
                transform.position, Vector3.up).sqrMagnitude <= 0.0025f)
        {
            safeZoneRecoveryIndex++;
        }

        if (safeZoneRecoveryIndex >= safeZoneRecoveryPath.Count)
        {
            FailSafeZoneRecovery();
            return;
        }

        movement.MoveTowardsSafeZoneRecovery(
            safeZoneRecoveryPath[safeZoneRecoveryIndex], 0f);
        LastTickCommandedMovement = true;
        if (movement.WasBlocked)
        {
            FailSafeZoneRecovery();
            return;
        }

        if (!gridProvider.IsWolfFootprintInSafeZone(
            transform.position, movement.CollisionRadius))
            CompleteSafeZoneRecovery();
    }

    private void CompleteSafeZoneRecovery()
    {
        ResetRecoveryState();
        navigationAgent.ResetFailedPath();
        movement.Stop();
    }

    private void FailSafeZoneRecovery()
    {
        ResetRecoveryState();
        movement?.Stop();
    }

    private void ResetRecoveryState()
    {
        IsRecoveringFromInvalidOrigin = false;
        safeZoneRecoveryPath.Clear();
        safeZoneRecoveryIndex = 0;
    }

    private void ResolveDependencies()
    {
        if (navigationAgent == null) navigationAgent = GetComponent<GraphNavigationAgent>();
        if (movement == null) movement = GetComponent<PredatorMovement>();
        movement?.ConfigureNavigationController(this);
        if (!agentConfigured && navigationAgent != null)
        {
            navigationAgent.Configure(avoidSafeZones, agentRadius, agentHeight);
            agentConfigured = true;
        }
        if (!providerConfigured && navigationAgent != null)
        {
            navigationAgent.ConfigureGraphComponent(graphProvider);
            providerConfigured = true;
        }
    }

    private void OnValidate()
    {
        agentRadius = Mathf.Max(0.1f, agentRadius);
        agentHeight = Mathf.Max(agentRadius * 2f, agentHeight);
        stoppingDistance = Mathf.Max(0f, stoppingDistance);
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
