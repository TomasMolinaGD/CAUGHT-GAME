using System;

namespace CaughtGame.Navigation
{
    /// <summary>A row-major rectangle of grid cells.</summary>
    [Serializable]
    public struct GridCellRectangle : IEquatable<GridCellRectangle>
    {
        public int Row { get; }
        public int Column { get; }
        public int Rows { get; }
        public int Columns { get; }

        public GridCellRectangle(int row, int column, int rows, int columns)
        {
            if (row < 0 || column < 0 || rows < 1 || columns < 1)
                throw new ArgumentOutOfRangeException(nameof(row), "Rectangle coordinates and dimensions must be positive.");

            Row = row;
            Column = column;
            Rows = rows;
            Columns = columns;
        }

        public bool Equals(GridCellRectangle other) => Row == other.Row && Column == other.Column &&
            Rows == other.Rows && Columns == other.Columns;

        public override bool Equals(object obj) => obj is GridCellRectangle other && Equals(other);
        public override int GetHashCode() => (((Row * 397) ^ Column) * 397 ^ Rows) * 397 ^ Columns;
        public static bool operator ==(GridCellRectangle left, GridCellRectangle right) => left.Equals(right);
        public static bool operator !=(GridCellRectangle left, GridCellRectangle right) => !left.Equals(right);
    }
}
