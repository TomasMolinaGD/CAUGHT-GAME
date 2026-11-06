using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PredatorAlertBootstrap : MonoBehaviour
{
    [SerializeField] private PredatorAlertSystem alertSystem;
    [SerializeField] private PredatorFSM[] wolves = Array.Empty<PredatorFSM>();

    private readonly HashSet<PredatorFSM> configuredWolves = new HashSet<PredatorFSM>();
    private bool started;

    private void Awake()
    {
        if (alertSystem == null)
            alertSystem = GetComponent<PredatorAlertSystem>();
        if (alertSystem == null)
            alertSystem = gameObject.AddComponent<PredatorAlertSystem>();
    }

    private void Start()
    {
        started = true;
        ConfigureWolves(ResolveMembers());
    }

    private void OnEnable()
    {
        if (started && alertSystem != null)
            ConfigureWolves(ResolveMembers());
    }

    private void OnDisable()
    {
        foreach (PredatorFSM wolf in configuredWolves)
        {
            if (wolf != null)
                wolf.ConfigureAlertSystem(null);
        }
        configuredWolves.Clear();
    }

    public void Configure(PredatorAlertSystem sharedAlertSystem,
        IReadOnlyList<PredatorFSM> members)
    {
        PredatorFSM[] validatedWolves = ValidateMembers(sharedAlertSystem, members);
        alertSystem = sharedAlertSystem;
        wolves = validatedWolves;
        ApplyWolves(validatedWolves);
    }

    private void ConfigureWolves(IReadOnlyList<PredatorFSM> members)
    {
        ApplyWolves(ValidateMembers(alertSystem, members));
    }

    private static PredatorFSM[] ValidateMembers(PredatorAlertSystem sharedAlertSystem,
        IReadOnlyList<PredatorFSM> members)
    {
        if (sharedAlertSystem == null)
            throw new ArgumentNullException(nameof(sharedAlertSystem));
        if (members == null)
            throw new ArgumentNullException(nameof(members));

        var uniqueWolves = new HashSet<PredatorFSM>();
        var validatedWolves = new PredatorFSM[members.Count];
        for (int i = 0; i < members.Count; i++)
        {
            PredatorFSM wolf = members[i];
            if (wolf == null)
                throw new ArgumentException($"Wolf at index {i} is null.", nameof(members));
            if (!uniqueWolves.Add(wolf))
                throw new InvalidOperationException($"Wolf at index {i} is registered more than once.");
            validatedWolves[i] = wolf;
        }

        return validatedWolves;
    }

    private void ApplyWolves(IReadOnlyList<PredatorFSM> members)
    {
        var nextWolves = new HashSet<PredatorFSM>(members);
        foreach (PredatorFSM wolf in nextWolves)
            wolf.ConfigureAlertSystem(alertSystem);

        foreach (PredatorFSM previous in configuredWolves)
        {
            if (previous != null && !nextWolves.Contains(previous))
                previous.ConfigureAlertSystem(null);
        }

        configuredWolves.Clear();
        foreach (PredatorFSM wolf in nextWolves)
            configuredWolves.Add(wolf);
    }

    private IReadOnlyList<PredatorFSM> ResolveMembers()
    {
        if (wolves != null && wolves.Length > 0)
            return wolves;

        var configured = new List<PredatorFSM>();
        GameObject[] roots = gameObject.scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            PredatorFSM[] sceneWolves = roots[rootIndex].GetComponentsInChildren<PredatorFSM>(false);
            for (int wolfIndex = 0; wolfIndex < sceneWolves.Length; wolfIndex++)
            {
                if (sceneWolves[wolfIndex].gameObject.name.StartsWith(
                        "Wolf_Patrol_B3_", StringComparison.Ordinal))
                    configured.Add(sceneWolves[wolfIndex]);
            }
        }

        if (configured.Count != 4)
            throw new InvalidOperationException($"Expected four B3 Wolves, found {configured.Count}.");
        return configured;
    }

}
