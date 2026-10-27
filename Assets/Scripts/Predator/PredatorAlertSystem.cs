using System;
using UnityEngine;

/// <summary>Broadcasts a newly observed hunter and its last known position to nearby agents.</summary>
public static class PredatorAlertSystem
{
    public static event Action<PredatorFSM, FSMAgent, Vector3> AlertRaised;

    public static void Broadcast(PredatorFSM source, FSMAgent target, Vector3 lastKnownPosition)
    {
        AlertRaised?.Invoke(source, target, lastKnownPosition);
    }
}
