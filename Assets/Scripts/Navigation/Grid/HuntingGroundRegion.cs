using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>
    /// Queries the walkable HuntingGround cells of a NavigationGrid.
    /// Sampling scans the grid only when explicitly requested, so it stays current
    /// if grid cell data changes and does not allocate a cache per frame.
    /// </summary>
    public sealed class HuntingGroundRegion : MonoBehaviour
    {
        [SerializeField] private NavigationGrid navigationGrid;

        /// <summary>The grid that defines this region, for placement integrations.</summary>
        public NavigationGrid SourceGrid => navigationGrid;

        public bool ContainsWorldPosition(Vector3 worldPosition)
        {
            if (navigationGrid == null ||
                !navigationGrid.TryWorldToGrid(worldPosition, out int row, out int column) ||
                !navigationGrid.TryGetCell(row, column, out NavigationGridCell cell))
            {
                return false;
            }

            return IsValidHuntingCell(cell);
        }

        /// <summary>Returns whether the configured grid cell belongs to this region.</summary>
        public bool IsValidHuntingGroundCell(int row, int column)
        {
            return navigationGrid != null &&
                   navigationGrid.TryGetCell(row, column, out NavigationGridCell cell) &&
                   IsValidHuntingCell(cell);
        }

        /// <summary>
        /// Selects a uniformly random valid cell and returns its world-space center.
        /// This is intended for explicit placement/respawn requests, not per-frame use.
        /// </summary>
        public bool TryGetRandomWorldPosition(out Vector3 worldPosition)
        {
            worldPosition = default;
            if (navigationGrid == null)
            {
                return false;
            }

            int selectedRow = -1;
            int selectedColumn = -1;
            int validCellCount = 0;

            for (int row = 0; row < navigationGrid.Rows; row++)
            {
                for (int column = 0; column < navigationGrid.Columns; column++)
                {
                    if (!navigationGrid.TryGetCell(row, column, out NavigationGridCell cell) ||
                        !IsValidHuntingCell(cell))
                    {
                        continue;
                    }

                    validCellCount++;
                    if (Random.Range(0, validCellCount) == 0)
                    {
                        selectedRow = row;
                        selectedColumn = column;
                    }
                }
            }

            if (selectedRow < 0)
            {
                return false;
            }

            worldPosition = navigationGrid.GridToWorldPosition(selectedRow, selectedColumn);
            return true;
        }

        private static bool IsValidHuntingCell(NavigationGridCell cell) =>
            cell.Walkable && cell.Zone == NavigationGridZone.HuntingGround;
    }
}
