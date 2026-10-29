using System;
using UnityEngine;

public static class PredatorAlertSystem
{
    public static event Action<PredatorFSM, GameObject, Vector3> AlertRaised;

    public static void Broadcast(PredatorFSM source, GameObject target, Vector3 lastKnownPosition)
    {
        AlertRaised?.Invoke(source, target, lastKnownPosition);
    }
}
