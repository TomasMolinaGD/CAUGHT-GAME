using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>Deterministically selects a valid Hunter spawn without mutating the grid.</summary>
    public static class HunterSpawnSelector
    {
        public static bool TrySelectSpawn(
            NavigationGrid grid,
            float groundHeight,
            out Vector3 worldPosition)
        {
            worldPosition = default;
            if (grid == null || !IsFinite(groundHeight) ||
                grid.Columns <= 0 || grid.Rows <= 0 ||
                !IsFinite(grid.CellSize) || grid.CellSize <= 0f)
            {
                return false;
            }

            for (int row = 0; row < grid.Rows; row++)
            {
                for (int column = 0; column < grid.Columns; column++)
                {
                    if (!grid.TryGetCell(row, column, out NavigationGridCell cell) ||
                        !cell.Walkable || cell.Zone != NavigationGridZone.Spawn)
                    {
                        continue;
                    }

                    Vector3 candidate;
                    try
                    {
                        candidate = grid.GridToWorldPosition(row, column);
                    }
                    catch (System.ArgumentOutOfRangeException)
                    {
                        return false;
                    }

                    candidate.y = groundHeight;
                    if (IsFinite(candidate.x) && IsFinite(candidate.y) && IsFinite(candidate.z))
                    {
                        worldPosition = candidate;
                        return true;
                    }

                    return false;
                }
            }

            return false;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
