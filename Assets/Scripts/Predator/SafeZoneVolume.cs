using System.Collections.Generic;
using UnityEngine;

/// <summary>Marks a volume that protects the hunter and is excluded from predator paths.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class SafeZoneVolume : MonoBehaviour
{
    private static readonly HashSet<SafeZoneVolume> ActiveZones = new HashSet<SafeZoneVolume>();
    private Collider zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;
    }

    private void OnEnable() { ActiveZones.Add(this); }
    private void OnDisable() { ActiveZones.Remove(this); }
    private void OnDestroy() { ActiveZones.Remove(this); }

    public bool Contains(Vector3 position)
    {
        if (zoneCollider == null) zoneCollider = GetComponent<Collider>();
        return zoneCollider != null && zoneCollider.enabled &&
            (zoneCollider.ClosestPoint(position) - position).sqrMagnitude < 0.0001f;
    }

    public static bool IsPositionSafe(Vector3 position)
    {
        foreach (SafeZoneVolume zone in ActiveZones)
        {
            if (zone != null && zone.Contains(position)) return true;
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Collider volume = GetComponent<Collider>();
        if (volume == null) return;
        Gizmos.color = new Color(0.15f, 0.8f, 1f, 0.25f);
        Gizmos.DrawCube(volume.bounds.center, volume.bounds.size);
    }
}
