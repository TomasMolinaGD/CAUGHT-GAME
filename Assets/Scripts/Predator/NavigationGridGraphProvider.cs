using System.Collections.Generic;
using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>
    /// Adapts the shared NavigationGrid pathfinder to the Predator graph contract.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NavigationGridGraphProvider : MonoBehaviour, IAStarGraphProvider
    {
        [SerializeField] private NavigationGrid navigationGrid;
        [SerializeField] private LayerMask obstacleLayers;

        public void Configure(NavigationGrid grid, LayerMask obstacles)
        {
            navigationGrid = grid;
            obstacleLayers = obstacles;
        }

        public bool TryFindPath(Vector3 start, Vector3 destination, bool avoidSafeZones, List<Vector3> path)
        {
            if (path == null)
            {
                return false;
            }

            path.Clear();
            if (!IsValidInput(start) || !IsValidInput(destination) || navigationGrid == null)
            {
                return false;
            }

            if (!navigationGrid.TryWorldToGrid(start, out int startRow, out int startColumn) ||
                !navigationGrid.TryWorldToGrid(destination, out int destinationRow, out int destinationColumn))
            {
                return false;
            }

            NavigationPathResult result = NavigationGridPathfinder.FindPath(
                navigationGrid,
                startRow,
                startColumn,
                destinationRow,
                destinationColumn,
                NavigationAgentType.Wolf);

            if (result == null || result.Status != NavigationPathStatus.Success)
            {
                return false;
            }

            for (int i = 0; i < result.Waypoints.Count; i++)
            {
                path.Add(result.Waypoints[i]);
            }

            return path.Count > 0;
        }

        public bool HasDirectLineOfSight(Vector3 start, Vector3 destination,
            float agentRadius, float agentHeight)
        {
            if (navigationGrid == null || !IsValidInput(start) || !IsValidInput(destination) ||
                !IsValidCapsule(agentRadius, agentHeight))
            {
                return false;
            }

            if (!IsWalkableWolfCorridor(start, destination))
            {
                return false;
            }

            Vector3 offset = destination - start;
            float distance = Vector3.ProjectOnPlane(offset, Vector3.up).magnitude;
            Vector3 direction = distance > 0f
                ? Vector3.ProjectOnPlane(offset, Vector3.up) / distance
                : Vector3.zero;
            Vector3 bottom = start + Vector3.up * agentRadius;
            Vector3 top = start + Vector3.up * (agentHeight - agentRadius);

            if (Physics.CheckCapsule(bottom, top, agentRadius, obstacleLayers,
                QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return distance <= 0f || !Physics.CapsuleCast(
                bottom,
                top,
                agentRadius,
                direction,
                distance,
                obstacleLayers,
                QueryTriggerInteraction.Ignore);
        }

        private bool IsWalkableWolfCorridor(Vector3 start, Vector3 destination)
        {
            float spacing = Mathf.Max(0.05f, navigationGrid.CellSize * 0.5f);
            float distance = Vector3.ProjectOnPlane(destination - start, Vector3.up).magnitude;
            int samples = Mathf.Max(1, Mathf.CeilToInt(distance / spacing));

            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector3 sample = Vector3.Lerp(start, destination, t);
                if (!navigationGrid.TryWorldToGrid(sample, out int row, out int column) ||
                    !navigationGrid.TryGetCell(row, column, out NavigationGridCell cell) ||
                    !cell.Walkable || cell.Zone == NavigationGridZone.SafeZone)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidCapsule(float radius, float height)
        {
            return !float.IsNaN(radius) && !float.IsInfinity(radius) &&
                !float.IsNaN(height) && !float.IsInfinity(height) &&
                radius > 0f && height >= radius * 2f;
        }

        private static bool IsValidInput(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
