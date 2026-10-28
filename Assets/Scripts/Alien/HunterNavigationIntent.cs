using System;
using UnityEngine;

public readonly struct HunterNavigationRequest
{
    public ulong Id { get; }
    public Vector3 Destination { get; }
    public float StoppingDistance { get; }
    public float SpeedMultiplier { get; }

    internal HunterNavigationRequest(ulong id, Vector3 destination, float stoppingDistance, float speedMultiplier)
    {
        Id = id;
        Destination = destination;
        StoppingDistance = stoppingDistance;
        SpeedMultiplier = speedMultiplier;
    }
}

public sealed class HunterNavigationIntent
{
    private ulong nextRequestId;
    private HunterNavigationRequest? activeRequest;

    public bool HasActiveRequest => activeRequest.HasValue;

    public ulong RequestNavigation(Vector3 destination, float stoppingDistance, float speedMultiplier = 1f)
    {
        Validate(destination, stoppingDistance, speedMultiplier);
        ulong id = ++nextRequestId;
        activeRequest = new HunterNavigationRequest(id, destination, stoppingDistance, speedMultiplier);
        return id;
    }

    public bool ReplaceNavigation(
        ulong expectedId,
        Vector3 destination,
        float stoppingDistance,
        float speedMultiplier,
        out ulong newId)
    {
        newId = 0;
        if (!activeRequest.HasValue || activeRequest.Value.Id != expectedId)
        {
            return false;
        }

        Validate(destination, stoppingDistance, speedMultiplier);
        newId = ++nextRequestId;
        activeRequest = new HunterNavigationRequest(newId, destination, stoppingDistance, speedMultiplier);
        return true;
    }

    public bool CancelNavigation(ulong expectedId)
    {
        if (!activeRequest.HasValue || activeRequest.Value.Id != expectedId)
        {
            return false;
        }

        activeRequest = null;
        return true;
    }

    public void CancelAll() => activeRequest = null;

    public bool TryGetCurrent(out HunterNavigationRequest request)
    {
        if (activeRequest.HasValue)
        {
            request = activeRequest.Value;
            return true;
        }

        request = default;
        return false;
    }

    private static void Validate(Vector3 destination, float stoppingDistance, float speedMultiplier)
    {
        if (!IsFinite(destination.x) || !IsFinite(destination.y) || !IsFinite(destination.z))
        {
            throw new ArgumentException("Navigation destination must be finite.", nameof(destination));
        }

        if (float.IsNaN(stoppingDistance) || float.IsInfinity(stoppingDistance) || stoppingDistance < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(stoppingDistance));
        }

        if (float.IsNaN(speedMultiplier) || float.IsInfinity(speedMultiplier) || speedMultiplier <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier));
        }
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
