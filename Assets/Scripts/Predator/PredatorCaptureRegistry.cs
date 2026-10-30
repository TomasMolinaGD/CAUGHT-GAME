using System.Collections.Generic;
using UnityEngine;

public static class PredatorCaptureRegistry
{
    private static readonly HashSet<GameObject> CapturedTargets = new HashSet<GameObject>();

    public static bool IsSupportedTarget(GameObject target)
    {
        return target != null && (GetLifeComponent(target) != null || GetHunterAgent(target) != null);
    }

    public static bool IsCaptured(GameObject target, bool allowExplicitThreat = false)
    {
        if (target == null || !target.activeInHierarchy) return true;
        BoidLife life = GetLifeComponent(target);
        if (life != null) return !life.IsAlive;
        if (CapturedTargets.Contains(target)) return true;
        FSMAgent hunter = GetHunterAgent(target);
        if (hunter != null)
        {
            ICapturableTarget receiver = GetCaptureReceiver(target);
            return receiver != null && !receiver.IsCaptureAvailable;
        }
        return !allowExplicitThreat || GetThreatComponent(target) == null;
    }

    public static bool TryCapture(GameObject target, bool allowExplicitThreat = false)
    {
        if (target == null || IsCaptured(target, allowExplicitThreat)) return false;
        BoidLife life = GetLifeComponent(target);
        if (life != null)
        {
            life.Kill();
            return true;
        }

        if (GetHunterAgent(target) != null)
        {
            ICapturableTarget receiver = GetCaptureReceiver(target);
            if (receiver != null)
            {
                if (!receiver.IsCaptureAvailable || !receiver.TryCapture()) return false;
                CapturedTargets.Add(target);
                return true;
            }
        }

        if (!allowExplicitThreat || GetThreatComponent(target) == null) return false;
        CapturedTargets.Add(target);
        target.SetActive(false);
        return true;
    }

    private static BoidLife GetLifeComponent(GameObject target)
    {
        if (target == null) return null;
        BoidLife life = target.GetComponent<BoidLife>();
        return life != null ? life : target.GetComponentInChildren<BoidLife>();
    }

    private static FSMAgent GetHunterAgent(GameObject target)
    {
        if (target == null) return null;
        FSMAgent hunter = target.GetComponent<FSMAgent>();
        return hunter != null ? hunter : target.GetComponentInChildren<FSMAgent>();
    }

    public static bool HasThreatComponent(GameObject target)
    {
        return GetThreatComponent(target) != null;
    }

    private static BoidThreat GetThreatComponent(GameObject target)
    {
        if (target == null) return null;
        BoidThreat threat = target.GetComponent<BoidThreat>();
        return threat != null ? threat : target.GetComponentInChildren<BoidThreat>();
    }

    private static ICapturableTarget GetCaptureReceiver(GameObject target)
    {
        ICapturableTarget receiver = target.GetComponent<ICapturableTarget>();
        return receiver != null ? receiver : target.GetComponentInChildren<ICapturableTarget>(true);
    }
}

public interface ICapturableTarget
{
    bool IsCaptureAvailable { get; }
    bool TryCapture();
}
