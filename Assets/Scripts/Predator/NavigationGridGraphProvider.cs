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

        public bool IsPositionInSafeZone(Vector3 position)
        {
            return navigationGrid != null && IsValidInput(position) &&
                navigationGrid.TryWorldToGrid(position, out int row, out int column) &&
                navigationGrid.TryGetCell(row, column, out NavigationGridCell cell) &&
                cell.Zone == NavigationGridZone.SafeZone;
        }

        internal bool HasNavigationGrid => navigationGrid != null;

        internal bool IsWolfMovementSegmentTraversable(Vector3 start, Vector3 movement, float radius)
        {
            return IsWolfMovementSegmentTraversable(start, movement, radius, false);
        }

        internal bool IsWolfRecoverySegmentTraversable(Vector3 start, Vector3 movement, float radius)
        {
            return IsWolfMovementSegmentTraversable(start, movement, radius, true);
        }

        internal bool IsWolfFootprintInSafeZone(Vector3 position, float radius)
        {
            if (navigationGrid == null || !IsValidInput(position) ||
                float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f)
            {
                return false;
            }

            return DoesFootprintOverlapSafeZone(position, Mathf.Max(0f, radius));
        }

        internal bool TryBuildSafeZoneRecoveryPath(Vector3 start, float agentRadius,
            float agentHeight, List<Vector3> path)
        {
            if (path == null) return false;
            path.Clear();
            if (navigationGrid == null || !IsValidInput(start) ||
                !IsValidCapsule(agentRadius, agentHeight) || !IsPositionInSafeZone(start) ||
                !navigationGrid.TryWorldToGrid(start, out int startRow, out int startColumn))
            {
                return false;
            }

            Vector2Int startCoordinate = new Vector2Int(startRow, startColumn);
            var frontier = new Queue<Vector2Int>();
            var visited = new HashSet<Vector2Int> { startCoordinate };
            var parent = new Dictionary<Vector2Int, Vector2Int>();
            frontier.Enqueue(startCoordinate);

            while (frontier.Count > 0 && visited.Count <= navigationGrid.CellCount)
            {
                Vector2Int current = frontier.Dequeue();
                for (int i = 0; i < CardinalDirections.Length; i++)
                {
                    Vector2Int next = current + CardinalDirections[i];
                    if (visited.Contains(next) ||
                        !navigationGrid.TryGetCell(next.x, next.y, out NavigationGridCell cell) ||
                        !cell.Walkable)
                    {
                        continue;
                    }

                    Vector3 segmentStart = current == startCoordinate
                        ? start
                        : navigationGrid.GridToWorldPosition(current.x, current.y);
                    Vector3 segmentEnd = navigationGrid.GridToWorldPosition(next.x, next.y);
                    if (!HasPhysicalCapsuleCorridor(segmentStart, segmentEnd,
                        agentRadius, agentHeight))
                    {
                        continue;
                    }

                    visited.Add(next);
                    parent[next] = current;
                    if (cell.Zone != NavigationGridZone.SafeZone &&
                        !IsWolfFootprintInSafeZone(segmentEnd, agentRadius))
                    {
                        if (!IsWolfMovementSegmentTraversable(segmentEnd, Vector3.zero,
                            agentRadius)) continue;
                        BuildRecoveryPath(startCoordinate, next, parent, path);
                        return path.Count > 0;
                    }

                    frontier.Enqueue(next);
                }
            }

            path.Clear();
            return false;
        }

        private bool IsWolfMovementSegmentTraversable(Vector3 start, Vector3 movement,
            float radius, bool allowSafeZones)
        {
            if (navigationGrid == null || !IsValidInput(start) || !IsValidInput(movement) ||
                float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f)
            {
                return false;
            }

            Vector3 horizontalMovement = Vector3.ProjectOnPlane(movement, Vector3.up);
            float distance = horizontalMovement.magnitude;
            float spacing = Mathf.Max(0.05f, Mathf.Min(navigationGrid.CellSize * 0.5f,
                Mathf.Max(0.05f, radius * 0.5f)));
            int samples = Mathf.Max(1, Mathf.CeilToInt(distance / spacing));
            float footprintRadius = Mathf.Max(0f, radius);

            for (int i = 0; i <= samples; i++)
            {
                Vector3 sample = start + horizontalMovement * (i / (float)samples);
                if (!IsWolfFootprintTraversable(sample, footprintRadius, allowSafeZones))
                    return false;
            }

            return true;
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

        private bool IsWalkableWolfPosition(Vector3 position, bool allowSafeZones)
        {
            return navigationGrid.TryWorldToGrid(position, out int row, out int column) &&
                navigationGrid.TryGetCell(row, column, out NavigationGridCell cell) &&
                cell.Walkable && (allowSafeZones || cell.Zone != NavigationGridZone.SafeZone);
        }

        private bool IsWolfFootprintTraversable(Vector3 position, float radius,
            bool allowSafeZones)
        {
            if (radius <= 0f) return IsWalkableWolfPosition(position, allowSafeZones);

            return VisitOverlappedFootprintCells(position, radius, (row, column) =>
                navigationGrid.TryGetCell(row, column, out NavigationGridCell cell) &&
                cell.Walkable && (allowSafeZones || cell.Zone != NavigationGridZone.SafeZone));
        }

        private bool DoesFootprintOverlapSafeZone(Vector3 position, float radius)
        {
            if (radius <= 0f) return IsPositionInSafeZone(position);

            bool overlapsSafeZone = false;
            VisitOverlappedFootprintCells(position, radius, (row, column) =>
            {
                if (navigationGrid.TryGetCell(row, column, out NavigationGridCell cell) &&
                    cell.Zone == NavigationGridZone.SafeZone)
                {
                    overlapsSafeZone = true;
                }
                return true;
            });
            return overlapsSafeZone;
        }

        private bool VisitOverlappedFootprintCells(Vector3 position, float radius,
            System.Func<int, int, bool> visitor)
        {
            Vector3 local = navigationGrid.transform.InverseTransformPoint(position);
            Vector3 scale = navigationGrid.transform.lossyScale;
            float localRadiusX = radius / Mathf.Max(0.0001f, Mathf.Abs(scale.x));
            float localRadiusZ = radius / Mathf.Max(0.0001f, Mathf.Abs(scale.z));
            float cellSize = navigationGrid.CellSize;
            int minColumn = Mathf.FloorToInt((local.x - localRadiusX) / cellSize);
            int maxColumn = Mathf.FloorToInt((local.x + localRadiusX) / cellSize);
            int minRow = Mathf.FloorToInt((local.z - localRadiusZ) / cellSize);
            int maxRow = Mathf.FloorToInt((local.z + localRadiusZ) / cellSize);
            float radiusSquared = radius * radius;

            for (int row = minRow; row <= maxRow; row++)
            for (int column = minColumn; column <= maxColumn; column++)
            {
                float closestX = Mathf.Clamp(local.x, column * cellSize,
                    (column + 1) * cellSize);
                float closestZ = Mathf.Clamp(local.z, row * cellSize,
                    (row + 1) * cellSize);
                Vector3 closestWorld = navigationGrid.transform.TransformPoint(
                    new Vector3(closestX, local.y, closestZ));
                float distanceSquared = Vector3.ProjectOnPlane(
                    closestWorld - position, Vector3.up).sqrMagnitude;
                if (distanceSquared >= radiusSquared - 0.000001f) continue;
                if (!visitor(row, column)) return false;
            }

            return true;
        }

        private bool HasPhysicalCapsuleCorridor(Vector3 start, Vector3 destination,
            float radius, float height)
        {
            Vector3 offset = Vector3.ProjectOnPlane(destination - start, Vector3.up);
            float distance = offset.magnitude;
            Vector3 direction = distance > 0f ? offset / distance : Vector3.zero;
            Vector3 bottom = start + Vector3.up * radius;
            Vector3 top = start + Vector3.up * (height - radius);
            if (Physics.CheckCapsule(bottom, top, radius, obstacleLayers,
                QueryTriggerInteraction.Ignore)) return false;
            return distance <= 0f || !Physics.CapsuleCast(bottom, top, radius, direction,
                distance, obstacleLayers, QueryTriggerInteraction.Ignore);
        }

        private void BuildRecoveryPath(Vector2Int start, Vector2Int exit,
            Dictionary<Vector2Int, Vector2Int> parent, List<Vector3> path)
        {
            var reversed = new List<Vector3>();
            Vector2Int current = exit;
            while (current != start)
            {
                reversed.Add(navigationGrid.GridToWorldPosition(current.x, current.y));
                current = parent[current];
            }
            for (int i = reversed.Count - 1; i >= 0; i--) path.Add(reversed[i]);
        }

        private static readonly Vector2Int[] CardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

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
