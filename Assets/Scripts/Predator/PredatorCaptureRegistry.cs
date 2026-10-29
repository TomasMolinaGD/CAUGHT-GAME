using System.Collections.Generic;
using UnityEngine;

public static class PredatorCaptureRegistry
{
    private static readonly HashSet<GameObject> CapturedTargets = new HashSet<GameObject>();

    public static bool IsCaptured(GameObject target)
    {
        if (target == null || !target.activeInHierarchy) return true;
        BoidLife life = GetLifeComponent(target);
        return life != null ? !life.IsAlive : CapturedTargets.Contains(target);
    }

    public static bool TryCapture(GameObject target)
    {
        if (target == null || IsCaptured(target)) return false;
        BoidLife life = GetLifeComponent(target);
        if (life != null)
        {
            life.Kill();
            return true;
        }

        CapturedTargets.Add(target);
        target.SetActive(false);
        return true;
    }

    private static BoidLife GetLifeComponent(GameObject target)
    {
        BoidLife life = target.GetComponent<BoidLife>();
        return life != null ? life : target.GetComponentInChildren<BoidLife>();
    }
}
