using System;
using System.Collections.Generic;
using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>Immutable selection result for the least-cost reachable SafeZone destination.</summary>
    public sealed class SafeZonePathSelection
    {
        public string ZoneId { get; }
        public Vector2Int DestinationCell { get; }
        public NavigationPathResult Path { get; }

        internal SafeZonePathSelection(string zoneId, Vector2Int destinationCell,
            NavigationPathResult path)
        {
            ZoneId = zoneId ?? string.Empty;
            DestinationCell = destinationCell;
            Path = path;
        }
    }

    /// <summary>Selects a reachable SafeZone using the actual Hunter A* route cost.</summary>
    public static class SafeZonePathSelector
    {
        public static bool TrySelect(
            NavigationGrid grid,
            Vector3 hunterWorldPosition,
            out SafeZonePathSelection selection)
        {
            selection = null;
            if (grid == null || !grid.TryWorldToGrid(hunterWorldPosition, out int startRow, out int startColumn))
            {
                return false;
            }

            var cellsByZone = new SortedDictionary<string, List<Vector2Int>>(StringComparer.Ordinal);
            for (int row = 0; row < grid.Rows; row++)
            {
                for (int column = 0; column < grid.Columns; column++)
                {
                    if (!grid.TryGetCell(row, column, out NavigationGridCell cell) ||
                        !cell.Walkable || cell.Zone != NavigationGridZone.SafeZone)
                    {
                        continue;
                    }

                    string zoneId = cell.ZoneId;
                    if (!cellsByZone.TryGetValue(zoneId, out List<Vector2Int> zoneCells))
                    {
                        zoneCells = new List<Vector2Int>();
                        cellsByZone.Add(zoneId, zoneCells);
                    }

                    zoneCells.Add(new Vector2Int(column, row));
                }
            }

            NavigationPathResult bestPath = null;
            string bestZoneId = null;
            Vector2Int bestCell = default;
            foreach (KeyValuePair<string, List<Vector2Int>> zone in cellsByZone)
            {
                foreach (Vector2Int destinationCell in zone.Value)
                {
                    NavigationPathResult path = NavigationGridPathfinder.FindPath(
                        grid, startRow, startColumn, destinationCell.y, destinationCell.x,
                        NavigationAgentType.Hunter);
                    if (path.Status != NavigationPathStatus.Success ||
                        !IsBetter(path, zone.Key, destinationCell, bestPath, bestZoneId, bestCell))
                    {
                        continue;
                    }

                    bestPath = path;
                    bestZoneId = zone.Key;
                    bestCell = destinationCell;
                }
            }

            if (bestPath == null)
            {
                return false;
            }

            selection = new SafeZonePathSelection(bestZoneId, bestCell, bestPath);
            return true;
        }

        private static bool IsBetter(
            NavigationPathResult candidatePath,
            string candidateZoneId,
            Vector2Int candidateCell,
            NavigationPathResult bestPath,
            string bestZoneId,
            Vector2Int bestCell)
        {
            if (bestPath == null || candidatePath.TotalCost < bestPath.TotalCost)
            {
                return true;
            }

            if (candidatePath.TotalCost > bestPath.TotalCost)
            {
                return false;
            }

            int zoneComparison = string.CompareOrdinal(candidateZoneId, bestZoneId);
            if (zoneComparison != 0)
            {
                return zoneComparison < 0;
            }

            return candidateCell.y < bestCell.y ||
                   (candidateCell.y == bestCell.y && candidateCell.x < bestCell.x);
        }
    }
}
