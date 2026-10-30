using System.Collections.Generic;
using UnityEngine;

public interface IAStarGraphProvider
{
    bool HasDirectLineOfSight(Vector3 start, Vector3 destination, float agentRadius, float agentHeight);
    bool TryFindPath(Vector3 start, Vector3 destination, bool avoidSafeZones, List<Vector3> path);
}
