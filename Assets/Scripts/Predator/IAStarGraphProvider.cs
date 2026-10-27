using System.Collections.Generic;
using UnityEngine;

/// <summary>Adapter contract implemented by the project's existing A* graph component.</summary>
public interface IAStarGraphProvider
{
    bool HasDirectLineOfSight(Vector3 start, Vector3 destination, float agentRadius, float agentHeight);
    bool TryFindPath(Vector3 start, Vector3 destination, bool avoidSafeZones, List<Vector3> path);
}
