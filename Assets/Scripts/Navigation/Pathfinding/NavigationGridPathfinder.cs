using System;
using System.Collections.Generic;
using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>
    /// Finds minimum-cost orthogonal paths through a NavigationGrid without changing its data.
    /// </summary>
    public static class NavigationGridPathfinder
    {
        private static readonly int[] RowOffsets = { -1, 0, 1, 0 };
        private static readonly int[] ColumnOffsets = { 0, 1, 0, -1 };

        public static NavigationPathResult FindPath(NavigationGrid grid,
            int startRow, int startColumn, int destinationRow, int destinationColumn,
            NavigationAgentType agentType)
        {
            if (!HasValidData(grid))
            {
                return NavigationPathResult.CreateFailure(NavigationPathStatus.InvalidGrid);
            }

            if (!grid.IsValidCoordinate(startRow, startColumn))
            {
                return NavigationPathResult.CreateFailure(NavigationPathStatus.InvalidStart);
            }

            if (!grid.IsValidCoordinate(destinationRow, destinationColumn))
            {
                return NavigationPathResult.CreateFailure(NavigationPathStatus.InvalidDestination);
            }

            if (!Enum.IsDefined(typeof(NavigationAgentType), agentType))
            {
                return NavigationPathResult.CreateFailure(NavigationPathStatus.InvalidAgentType);
            }

            if (!grid.TryGetCell(startRow, startColumn, out NavigationGridCell startCell) ||
                !grid.TryGetCell(destinationRow, destinationColumn, out NavigationGridCell destinationCell))
            {
                return NavigationPathResult.CreateFailure(NavigationPathStatus.InvalidGrid);
            }

            if (!CanTraverse(startCell, agentType))
            {
                return NavigationPathResult.CreateFailure(NavigationPathStatus.StartNotTraversable);
            }

            if (!CanTraverse(destinationCell, agentType))
            {
                return NavigationPathResult.CreateFailure(NavigationPathStatus.DestinationNotTraversable);
            }

            int columns = grid.Columns;
            int rows = grid.Rows;
            int cellCount = rows * columns;
            int startIndex = startRow * columns + startColumn;
            int destinationIndex = destinationRow * columns + destinationColumn;
            var costs = new float[cellCount];
            var previous = new int[cellCount];
            var closed = new bool[cellCount];
            for (int index = 0; index < cellCount; index++)
            {
                costs[index] = float.PositiveInfinity;
                previous[index] = -1;
            }

            Vector3 destinationPosition = grid.GridToWorldPosition(destinationRow, destinationColumn);
            var open = new MinHeap(cellCount);
            costs[startIndex] = 0f;
            open.Push(new OpenEntry(startIndex, 0f,
                Heuristic(grid.GridToWorldPosition(startRow, startColumn), destinationPosition)));

            int visitedCellCount = 0;
            while (open.Count > 0)
            {
                OpenEntry entry = open.Pop();
                int currentIndex = entry.CellIndex;
                if (closed[currentIndex] || entry.Cost > costs[currentIndex])
                {
                    continue;
                }

                closed[currentIndex] = true;
                visitedCellCount++;
                if (currentIndex == destinationIndex)
                {
                    return BuildSuccess(grid, previous, costs[destinationIndex],
                        startIndex, destinationIndex, visitedCellCount);
                }

                int currentRow = currentIndex / columns;
                int currentColumn = currentIndex % columns;
                Vector3 currentPosition = grid.GridToWorldPosition(currentRow, currentColumn);
                for (int direction = 0; direction < RowOffsets.Length; direction++)
                {
                    int neighborRow = currentRow + RowOffsets[direction];
                    int neighborColumn = currentColumn + ColumnOffsets[direction];
                    if (!grid.TryGetCell(neighborRow, neighborColumn, out NavigationGridCell neighborCell) ||
                        !CanTraverse(neighborCell, agentType))
                    {
                        continue;
                    }

                    int neighborIndex = neighborRow * columns + neighborColumn;
                    if (closed[neighborIndex])
                    {
                        continue;
                    }

                    Vector3 neighborPosition = grid.GridToWorldPosition(neighborRow, neighborColumn);
                    float candidateCost = costs[currentIndex] + Vector3.Distance(currentPosition, neighborPosition);
                    if (candidateCost >= costs[neighborIndex])
                    {
                        continue;
                    }

                    costs[neighborIndex] = candidateCost;
                    previous[neighborIndex] = currentIndex;
                    open.Push(new OpenEntry(neighborIndex, candidateCost,
                        Heuristic(neighborPosition, destinationPosition)));
                }
            }

            return NavigationPathResult.CreateFailure(NavigationPathStatus.NoPath, visitedCellCount);
        }

        private static bool HasValidData(NavigationGrid grid)
        {
            return grid != null && grid.Columns > 0 && grid.Rows > 0 && grid.CellSize > 0f &&
                   !float.IsNaN(grid.CellSize) && !float.IsInfinity(grid.CellSize) &&
                   (long)grid.Columns * grid.Rows == grid.CellCount;
        }

        private static bool CanTraverse(NavigationGridCell cell, NavigationAgentType agentType)
        {
            return cell.Walkable &&
                   (agentType != NavigationAgentType.Wolf || cell.Zone != NavigationGridZone.SafeZone);
        }

        // Edge costs are Euclidean distances in world space. By the triangle inequality,
        // straight-line world distance never exceeds the cost of any remaining grid route.
        private static float Heuristic(Vector3 position, Vector3 destination)
        {
            return Vector3.Distance(position, destination);
        }

        private static NavigationPathResult BuildSuccess(NavigationGrid grid, int[] previous,
            float totalCost, int startIndex, int destinationIndex, int visitedCellCount)
        {
            var reversedIndices = new List<int>();
            int index = destinationIndex;
            while (index >= 0)
            {
                reversedIndices.Add(index);
                if (index == startIndex)
                {
                    break;
                }

                index = previous[index];
            }

            if (reversedIndices[reversedIndices.Count - 1] != startIndex)
            {
                return NavigationPathResult.CreateFailure(NavigationPathStatus.NoPath, visitedCellCount);
            }

            reversedIndices.Reverse();
            var cells = new Vector2Int[reversedIndices.Count];
            var waypoints = new Vector3[reversedIndices.Count];
            for (int pathIndex = 0; pathIndex < reversedIndices.Count; pathIndex++)
            {
                int cellIndex = reversedIndices[pathIndex];
                int row = cellIndex / grid.Columns;
                int column = cellIndex % grid.Columns;
                cells[pathIndex] = new Vector2Int(column, row);
                waypoints[pathIndex] = grid.GridToWorldPosition(row, column);
            }

            return NavigationPathResult.CreateSuccess(cells, waypoints, totalCost, visitedCellCount);
        }

        private readonly struct OpenEntry
        {
            public readonly int CellIndex;
            public readonly float Cost;
            public readonly float EstimatedTotalCost;

            public OpenEntry(int cellIndex, float cost, float heuristic)
            {
                CellIndex = cellIndex;
                Cost = cost;
                EstimatedTotalCost = cost + heuristic;
            }
        }

        private sealed class MinHeap
        {
            private readonly List<OpenEntry> entries;

            public int Count => entries.Count;

            public MinHeap(int capacity)
            {
                entries = new List<OpenEntry>(capacity);
            }

            public void Push(OpenEntry entry)
            {
                int index = entries.Count;
                entries.Add(entry);
                while (index > 0)
                {
                    int parent = (index - 1) / 2;
                    if (Compare(entries[parent], entry) <= 0) break;
                    entries[index] = entries[parent];
                    index = parent;
                }

                entries[index] = entry;
            }

            public OpenEntry Pop()
            {
                OpenEntry result = entries[0];
                int lastIndex = entries.Count - 1;
                OpenEntry last = entries[lastIndex];
                entries.RemoveAt(lastIndex);
                if (lastIndex == 0) return result;

                int index = 0;
                while (true)
                {
                    int left = index * 2 + 1;
                    if (left >= entries.Count) break;
                    int right = left + 1;
                    int child = right < entries.Count && Compare(entries[right], entries[left]) < 0
                        ? right
                        : left;
                    if (Compare(last, entries[child]) <= 0) break;
                    entries[index] = entries[child];
                    index = child;
                }

                entries[index] = last;
                return result;
            }

            private static int Compare(OpenEntry left, OpenEntry right)
            {
                int comparison = left.EstimatedTotalCost.CompareTo(right.EstimatedTotalCost);
                if (comparison != 0) return comparison;

                // Stable tie-breaking keeps equally good routes reproducible.
                comparison = left.CellIndex.CompareTo(right.CellIndex);
                return comparison != 0 ? comparison : left.Cost.CompareTo(right.Cost);
            }
        }
    }
}
