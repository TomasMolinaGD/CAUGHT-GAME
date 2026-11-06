using System;
using System.Collections.Generic;
using UnityEngine;

namespace CaughtGame.Navigation
{
    [DisallowMultipleComponent]
    public sealed class NavigationGrid : MonoBehaviour, ISerializationCallbackReceiver
    {
        private const int MaximumCellCount = 1000000;

        [SerializeField, Min(1)] private int columns = 35;
        [SerializeField, Min(1)] private int rows = 27;
        [SerializeField, Min(0.01f)] private float cellSize = 4f;
        [SerializeField] private List<NavigationGridCell> cells = new List<NavigationGridCell>();

        public int Columns => columns;
        public int Rows => rows;
        public float CellSize => cellSize;
        public int CellCount => cells == null ? 0 : cells.Count;

        private void Reset() => InitializeNewGrid();

        public bool IsValidCoordinate(int row, int column) =>
            row >= 0 && row < rows && column >= 0 && column < columns;

        public bool TryGetCell(int row, int column, out NavigationGridCell cell)
        {
            if (!IsValidCoordinate(row, column) || !HasExpectedCellCount())
            {
                cell = default;
                return false;
            }

            cell = cells[row * columns + column];
            return true;
        }

        public Vector3 GridToWorldPosition(int row, int column)
        {
            if (!TryGetCell(row, column, out _))
            {
                throw new ArgumentOutOfRangeException(nameof(row), "Grid coordinates are outside the configured grid.");
            }

            return transform.TransformPoint(new Vector3((column + 0.5f) * cellSize, 0f, (row + 0.5f) * cellSize));
        }

        public bool TryWorldToGrid(Vector3 worldPosition, out int row, out int column)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            column = Mathf.FloorToInt(local.x / cellSize);
            row = Mathf.FloorToInt(local.z / cellSize);
            return HasExpectedCellCount() && IsValidCoordinate(row, column);
        }

        public bool SetWalkable(int row, int column, bool walkable)
        {
            if (!TryGetIndex(row, column, out int index)) return false;
            cells[index] = cells[index].WithWalkable(walkable);
            return true;
        }

        public bool SetZone(int row, int column, NavigationGridZone zone, string zoneId = null)
        {
            if (!TryGetIndex(row, column, out int index) || !Enum.IsDefined(typeof(NavigationGridZone), zone)) return false;
            cells[index] = cells[index].WithZone(zone, zoneId);
            return true;
        }

        public int CountCellsToDiscard(int newColumns, int newRows)
        {
            if (!IsValidSize(newColumns, newRows, cellSize) || !HasExpectedCellCount()) return 0;
            return (columns * rows) - (Mathf.Min(columns, newColumns) * Mathf.Min(rows, newRows));
        }

        public bool TryApplyGridSize(int newColumns, int newRows, float newCellSize,
            bool allowDiscard, out int discardedCellCount)
        {
            discardedCellCount = 0;
            if (!IsValidSize(newColumns, newRows, newCellSize) || !HasExpectedCellCount()) return false;

            int retainedCount = Mathf.Min(columns, newColumns) * Mathf.Min(rows, newRows);
            discardedCellCount = cells.Count - retainedCount;
            if (discardedCellCount > 0 && !allowDiscard) return false;

            var resized = new List<NavigationGridCell>(newColumns * newRows);
            for (int row = 0; row < newRows; row++)
            {
                for (int column = 0; column < newColumns; column++)
                {
                    if (IsValidCoordinate(row, column)) resized.Add(cells[row * columns + column]);
                    else resized.Add(new NavigationGridCell(row, column));
                }
            }

            columns = newColumns;
            rows = newRows;
            cellSize = newCellSize;
            cells = resized;
            return true;
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            if (cells == null) cells = new List<NavigationGridCell>();
        }

        private bool TryGetIndex(int row, int column, out int index)
        {
            if (!IsValidCoordinate(row, column) || !HasExpectedCellCount())
            {
                index = -1;
                return false;
            }

            index = row * columns + column;
            return true;
        }

        private void InitializeNewGrid()
        {
            if (columns < 1) columns = 35;
            if (rows < 1) rows = 27;
            if (cellSize <= 0f || float.IsNaN(cellSize) || float.IsInfinity(cellSize)) cellSize = 4f;
            if (cells == null) cells = new List<NavigationGridCell>();
            if ((long)columns * rows > MaximumCellCount) return;
            if (cells.Count != 0) return;

            cells.Capacity = columns * rows;
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                    cells.Add(new NavigationGridCell(row, column));
        }

        private bool HasExpectedCellCount() => cells != null && (long)cells.Count == (long)columns * rows;

        private static bool IsValidSize(int width, int height, float size) =>
            width > 0 && height > 0 && size > 0f && !float.IsNaN(size) && !float.IsInfinity(size) &&
            (long)width * height <= MaximumCellCount;
    }
}
