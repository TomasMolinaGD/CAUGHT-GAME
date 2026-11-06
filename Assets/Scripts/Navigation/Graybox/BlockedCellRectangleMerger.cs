using System;
using System.Collections.Generic;
using CaughtGame.Navigation;

namespace CaughtGame.Navigation
{
    /// <summary>Deterministically partitions all blocked cells into non-overlapping rectangles.</summary>
    public static class BlockedCellRectangleMerger
    {
        public static IReadOnlyList<GridCellRectangle> Merge(NavigationGrid grid)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            var blocked = new bool[grid.Rows, grid.Columns];
            for (int row = 0; row < grid.Rows; row++)
            {
                for (int column = 0; column < grid.Columns; column++)
                {
                    if (!grid.TryGetCell(row, column, out NavigationGridCell cell))
                        throw new InvalidOperationException("NavigationGrid does not contain a complete serialized cell array.");
                    blocked[row, column] = !cell.Walkable;
                }
            }

            var used = new bool[grid.Rows, grid.Columns];
            var rectangles = new List<GridCellRectangle>();
            for (int row = 0; row < grid.Rows; row++)
            {
                for (int column = 0; column < grid.Columns; column++)
                {
                    if (!blocked[row, column] || used[row, column])
                        continue;

                    int width = 0;
                    while (column + width < grid.Columns && blocked[row, column + width] && !used[row, column + width])
                        width++;

                    int height = 1;
                    while (row + height < grid.Rows && RowSpanAvailable(blocked, used, row + height, column, width))
                        height++;

                    rectangles.Add(new GridCellRectangle(row, column, height, width));
                    for (int coveredRow = row; coveredRow < row + height; coveredRow++)
                        for (int coveredColumn = column; coveredColumn < column + width; coveredColumn++)
                            used[coveredRow, coveredColumn] = true;
                }
            }

            return rectangles.AsReadOnly();
        }

        private static bool RowSpanAvailable(bool[,] blocked, bool[,] used, int row, int column, int width)
        {
            for (int offset = 0; offset < width; offset++)
                if (!blocked[row, column + offset] || used[row, column + offset])
                    return false;
            return true;
        }
    }
}
