using System.Collections.Generic;
using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>Deterministic, bounded selector for local HuntingGround exploration.</summary>
    public sealed class SearchDestinationSelector
    {
        private static readonly Vector2Int[] LocalOffsets =
        {
            new Vector2Int(0, 0), new Vector2Int(0, -1), new Vector2Int(0, 1),
            new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(-1, -1),
            new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(1, 1)
        };
        private readonly System.Random random;

        public SearchDestinationSelector(int seed)
        {
            random = new System.Random(seed);
        }

        public bool TrySelect(
            NavigationGrid grid,
            HuntingGroundRegion region,
            Vector3 origin,
            int excludedIndex,
            float minimumDistance,
            float maximumDistance,
            int maxAttempts,
            ISet<int> excludedIndices,
            out Vector3 destination,
            out int destinationIndex)
        {
            destination = default;
            destinationIndex = -1;
            if (grid == null || region == null || maxAttempts <= 0 ||
                grid.Rows <= 0 || grid.Columns <= 0 ||
                float.IsNaN(minimumDistance) || float.IsInfinity(minimumDistance) || minimumDistance < 0f ||
                float.IsNaN(maximumDistance) || maximumDistance < minimumDistance)
            {
                return false;
            }

            float minimumSquared = minimumDistance * minimumDistance;
            float maximumSquared = float.IsPositiveInfinity(maximumDistance)
                ? float.PositiveInfinity
                : maximumDistance * maximumDistance;
            float bestScore = float.PositiveInfinity;
            int totalCells = grid.Rows * grid.Columns;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                int index = random.Next(totalCells);
                if (index == excludedIndex || (excludedIndices != null && excludedIndices.Contains(index)))
                {
                    continue;
                }

                int row = index / grid.Columns;
                int column = index % grid.Columns;
                if (!region.IsValidHuntingGroundCell(row, column))
                {
                    continue;
                }

                Vector3 candidate = grid.GridToWorldPosition(row, column);
                Vector3 delta = candidate - origin;
                delta.y = 0f;
                float distanceSquared = delta.sqrMagnitude;
                if (distanceSquared < minimumSquared || distanceSquared > maximumSquared)
                {
                    continue;
                }

                if (distanceSquared < bestScore)
                {
                    bestScore = distanceSquared;
                    destination = candidate;
                    destinationIndex = index;
                }
            }

            // A tiny region can contain a single admissible cell that bounded random
            // probes do not hit. Probe the immediate neighbourhood once before giving
            // up, without introducing a full-grid scan or persistent coverage state.
            if (destinationIndex < 0 && grid.TryWorldToGrid(origin, out int originRow, out int originColumn))
            {
                int localAttempts = Mathf.Min(maxAttempts, LocalOffsets.Length);
                for (int attempt = 0; attempt < localAttempts; attempt++)
                {
                    int row = originRow + LocalOffsets[attempt].x;
                    int column = originColumn + LocalOffsets[attempt].y;
                    if (row < 0 || row >= grid.Rows || column < 0 || column >= grid.Columns ||
                        !region.IsValidHuntingGroundCell(row, column))
                    {
                        continue;
                    }

                    int index = row * grid.Columns + column;
                    if (index == excludedIndex || (excludedIndices != null && excludedIndices.Contains(index)))
                    {
                        continue;
                    }

                    Vector3 candidate = grid.GridToWorldPosition(row, column);
                    Vector3 delta = candidate - origin;
                    delta.y = 0f;
                    float distanceSquared = delta.sqrMagnitude;
                    if (distanceSquared < minimumSquared || distanceSquared > maximumSquared)
                    {
                        continue;
                    }

                    destination = candidate;
                    destinationIndex = index;
                    break;
                }
            }

            return destinationIndex >= 0;
        }
    }
}
