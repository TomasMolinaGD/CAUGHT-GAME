using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>
    /// Immutable result of a grid path query. Cell coordinates use x=column and y=row.
    /// </summary>
    public sealed class NavigationPathResult
    {
        private static readonly IReadOnlyList<Vector2Int> EmptyCells =
            Array.AsReadOnly(Array.Empty<Vector2Int>());
        private static readonly IReadOnlyList<Vector3> EmptyWaypoints =
            Array.AsReadOnly(Array.Empty<Vector3>());

        private readonly ReadOnlyCollection<Vector2Int> cells;
        private readonly ReadOnlyCollection<Vector3> waypoints;

        public NavigationPathStatus Status { get; }
        public IReadOnlyList<Vector2Int> Cells => cells == null
            ? EmptyCells
            : (IReadOnlyList<Vector2Int>)cells;
        public IReadOnlyList<Vector3> Waypoints => waypoints == null
            ? EmptyWaypoints
            : (IReadOnlyList<Vector3>)waypoints;
        public float TotalCost { get; }
        public int VisitedCellCount { get; }
        public int StepCount => Status == NavigationPathStatus.Success ? Math.Max(0, Cells.Count - 1) : 0;

        private NavigationPathResult(NavigationPathStatus status, Vector2Int[] cells,
            Vector3[] waypoints, float totalCost, int visitedCellCount)
        {
            Status = status;
            this.cells = cells == null ? null : Array.AsReadOnly(cells);
            this.waypoints = waypoints == null ? null : Array.AsReadOnly(waypoints);
            TotalCost = totalCost;
            VisitedCellCount = visitedCellCount;
        }

        internal static NavigationPathResult CreateSuccess(Vector2Int[] cells,
            Vector3[] waypoints, float totalCost, int visitedCellCount)
        {
            return new NavigationPathResult(NavigationPathStatus.Success, cells, waypoints,
                totalCost, visitedCellCount);
        }

        internal static NavigationPathResult CreateFailure(NavigationPathStatus status,
            int visitedCellCount = 0)
        {
            return new NavigationPathResult(status, null, null, 0f, visitedCellCount);
        }
    }
}
