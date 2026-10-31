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
    [SerializeField] private bool chase;
    [SerializeField, Min(0f)] private float stoppingDistance = 0.25f;

    private bool hasDestination;
    private bool providerConfigured;
    private Vector3 destination;

    public GraphNavigationAgent.NavigationStatus Status =>
        navigationAgent != null ? navigationAgent.Status : GraphNavigationAgent.NavigationStatus.MissingProvider;
    public bool HasDestination => hasDestination;

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
        chase = pursue;
        stoppingDistance = Mathf.Max(0f, arrivalDistance);
    }

    public void ConfigureGraphProvider(MonoBehaviour provider)
    {
        graphProvider = provider;
        providerConfigured = false;
        ResolveDependencies();
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
        navigationAgent.InvalidatePath();
    }

    public void CancelDestination()
    {
        hasDestination = false;
        navigationAgent?.InvalidatePath();
        movement?.Stop();
    }

    public void TickNavigation()
    {
        ResolveDependencies();
        if (navigationAgent == null || movement == null || !hasDestination)
        {
            movement?.Stop();
            return;
        }

        bool moving;
        bool accepted = navigationAgent.TryMoveTowards(destination, 0f, stoppingDistance, out moving);
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
                movement.MoveTowards(navigationAgent.MovementTarget, chase, stoppingDistance);
            else
                movement.Stop();
        }
    }

    private void Update()
    {
        TickNavigation();
    }

    private void ResolveDependencies()
    {
        if (navigationAgent == null) navigationAgent = GetComponent<GraphNavigationAgent>();
        if (movement == null) movement = GetComponent<PredatorMovement>();
        if (!providerConfigured && navigationAgent != null)
        {
            navigationAgent.ConfigureGraphComponent(graphProvider);
            providerConfigured = true;
        }
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
