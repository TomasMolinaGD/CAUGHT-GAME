using System.Collections.Generic;
using CaughtGame.Navigation;
using NUnit.Framework;
using UnityEngine;

public sealed class NavigationGridPathfinderTests
{
    private GameObject gridObject;
    private NavigationGrid grid;

    [SetUp]
    public void SetUp()
    {
        gridObject = new GameObject("Navigation Grid Pathfinder Test");
        grid = gridObject.AddComponent<NavigationGrid>();
        Assert.That(grid.TryApplyGridSize(5, 5, 4f, true, out _), Is.True);
    }

    [TearDown]
    public void TearDown()
    {
        if (gridObject != null)
        {
            Object.DestroyImmediate(gridObject);
        }
    }

    [Test]
    public void FindPath_ReturnsDirectOrthogonalRouteAndExpectedCost()
    {
        NavigationPathResult result = Find(2, 0, 2, 4, NavigationAgentType.Hunter);

        Assert.That(result.Status, Is.EqualTo(NavigationPathStatus.Success));
        Assert.That(result.Cells.Count, Is.EqualTo(5));
        Assert.That(result.StepCount, Is.EqualTo(4));
        Assert.That(result.TotalCost, Is.EqualTo(16f).Within(0.0001f));
        Assert.That(result.VisitedCellCount, Is.GreaterThan(0));
        Assert.That(result.Waypoints.Count, Is.EqualTo(result.Cells.Count));
        Assert.That(result.Cells[0], Is.EqualTo(new Vector2Int(0, 2)));
        Assert.That(result.Cells[result.Cells.Count - 1], Is.EqualTo(new Vector2Int(4, 2)));
    }

    [Test]
    public void FindPath_RoutesAroundBlockedCellsUsingOnlyOrthogonalSteps()
    {
        Assert.That(grid.SetWalkable(2, 2, false), Is.True);

        NavigationPathResult result = Find(2, 0, 2, 4, NavigationAgentType.Hunter);

        Assert.That(result.Status, Is.EqualTo(NavigationPathStatus.Success));
        Assert.That(result.TotalCost, Is.GreaterThan(16f));
        CollectionAssert.DoesNotContain(result.Cells, new Vector2Int(2, 2));
        AssertOrthogonalAndWalkable(result.Cells);
    }

    [Test]
    public void FindPath_ReportsInvalidStartAndDestinationCoordinates()
    {
        Assert.That(Find(-1, 0, 2, 2, NavigationAgentType.Hunter).Status,
            Is.EqualTo(NavigationPathStatus.InvalidStart));
        Assert.That(Find(2, 2, 5, 2, NavigationAgentType.Hunter).Status,
            Is.EqualTo(NavigationPathStatus.InvalidDestination));
    }

    [Test]
    public void FindPath_ReportsNoPathWhenWalkableRegionsAreDisconnected()
    {
        for (int row = 0; row < grid.Rows; row++)
        {
            for (int column = 0; column < grid.Columns; column++)
            {
                Assert.That(grid.SetWalkable(row, column, false), Is.True);
            }
        }

        grid.SetWalkable(0, 0, true);
        grid.SetWalkable(4, 4, true);

        NavigationPathResult result = Find(0, 0, 4, 4, NavigationAgentType.Hunter);

        Assert.That(result.Status, Is.EqualTo(NavigationPathStatus.NoPath));
        Assert.That(result.Cells, Is.Empty);
    }

    [Test]
    public void FindPath_AllowsHunterIntoSafeZoneAndPreventsWolfFromEnteringOrCrossingIt()
    {
        for (int row = 0; row < grid.Rows; row++)
        {
            Assert.That(grid.TryGetCell(row, 2, out NavigationGridCell safeCell), Is.True);
            Assert.That(safeCell.Walkable, Is.True);
            Assert.That(grid.SetZone(row, 2, NavigationGridZone.SafeZone, "SafeZone_A"), Is.True);
        }

        NavigationPathResult hunterRoute = Find(2, 0, 2, 4, NavigationAgentType.Hunter);
        NavigationPathResult wolfRoute = Find(2, 0, 2, 4, NavigationAgentType.Wolf);
        NavigationPathResult wolfSafeDestination = Find(2, 0, 2, 2, NavigationAgentType.Wolf);
        NavigationPathResult wolfSafeStart = Find(2, 2, 2, 4, NavigationAgentType.Wolf);

        Assert.That(hunterRoute.Status, Is.EqualTo(NavigationPathStatus.Success));
        CollectionAssert.Contains(hunterRoute.Cells, new Vector2Int(2, 2));
        Assert.That(wolfRoute.Status, Is.EqualTo(NavigationPathStatus.NoPath));
        Assert.That(wolfSafeDestination.Status, Is.EqualTo(NavigationPathStatus.DestinationNotTraversable));
        Assert.That(wolfSafeStart.Status, Is.EqualTo(NavigationPathStatus.StartNotTraversable));
    }

    [Test]
    public void FindPath_CostMatchesWorldSpaceVector3DistanceForEachStep()
    {
        grid.transform.position = new Vector3(17f, 2f, -9f);
        grid.transform.rotation = Quaternion.Euler(0f, 27f, 0f);
        grid.transform.localScale = new Vector3(2f, 1f, 0.5f);

        NavigationPathResult result = Find(1, 1, 3, 3, NavigationAgentType.Hunter);
        float expectedCost = 0f;
        for (int index = 1; index < result.Cells.Count; index++)
        {
            Vector2Int previous = result.Cells[index - 1];
            Vector2Int current = result.Cells[index];
            expectedCost += Vector3.Distance(
                grid.GridToWorldPosition(previous.y, previous.x),
                grid.GridToWorldPosition(current.y, current.x));
        }

        Assert.That(result.Status, Is.EqualTo(NavigationPathStatus.Success));
        Assert.That(result.TotalCost, Is.EqualTo(expectedCost).Within(0.0001f));
    }

    [Test]
    public void FindPath_IsOptimalComparedWithZeroHeuristicDijkstra()
    {
        grid.SetWalkable(1, 2, false);
        grid.SetWalkable(2, 2, false);
        grid.SetWalkable(3, 2, false);
        grid.transform.localScale = new Vector3(1.4f, 1f, 0.7f);

        NavigationPathResult result = Find(2, 0, 2, 4, NavigationAgentType.Hunter);
        float dijkstraCost = CalculateDijkstraCost(2, 0, 2, 4, NavigationAgentType.Hunter);

        Assert.That(result.Status, Is.EqualTo(NavigationPathStatus.Success));
        Assert.That(result.TotalCost, Is.GreaterThan(16f * grid.transform.localScale.x));
        Assert.That(result.TotalCost, Is.EqualTo(dijkstraCost).Within(0.0001f));
    }

    [Test]
    public void FindPath_IsDeterministicForTheSameGridAndEndpoints()
    {
        grid.SetWalkable(1, 1, false);
        grid.SetWalkable(1, 2, false);
        grid.SetWalkable(2, 2, false);

        NavigationPathResult first = Find(0, 0, 4, 4, NavigationAgentType.Hunter);
        NavigationPathResult second = Find(0, 0, 4, 4, NavigationAgentType.Hunter);

        Assert.That(second.Cells, Is.EqualTo(first.Cells));
        Assert.That(second.TotalCost, Is.EqualTo(first.TotalCost));
        Assert.That(second.VisitedCellCount, Is.EqualTo(first.VisitedCellCount));
    }

    [Test]
    public void FindPath_DoesNotModifySerializedGridData()
    {
        grid.SetWalkable(1, 1, false);
        grid.SetZone(2, 2, NavigationGridZone.SafeZone, "SafeZone_B");
        List<CellSnapshot> before = CaptureGrid();

        Find(0, 0, 4, 4, NavigationAgentType.Hunter);
        Find(0, 0, 4, 4, NavigationAgentType.Wolf);

        Assert.That(CaptureGrid(), Is.EqualTo(before));
    }

    private NavigationPathResult Find(int startRow, int startColumn, int destinationRow, int destinationColumn,
        NavigationAgentType agentType)
    {
        return NavigationGridPathfinder.FindPath(
            grid, startRow, startColumn, destinationRow, destinationColumn, agentType);
    }

    private void AssertOrthogonalAndWalkable(IReadOnlyList<Vector2Int> cells)
    {
        for (int index = 0; index < cells.Count; index++)
        {
            Vector2Int cell = cells[index];
            Assert.That(grid.TryGetCell(cell.y, cell.x, out NavigationGridCell data), Is.True);
            Assert.That(data.Walkable, Is.True);
            if (index > 0)
            {
                Vector2Int previous = cells[index - 1];
                Assert.That(Mathf.Abs(cell.x - previous.x) + Mathf.Abs(cell.y - previous.y), Is.EqualTo(1));
            }
        }
    }

    private float CalculateDijkstraCost(int startRow, int startColumn, int goalRow, int goalColumn,
        NavigationAgentType agentType)
    {
        int count = grid.Rows * grid.Columns;
        var costs = new float[count];
        var visited = new bool[count];
        for (int index = 0; index < count; index++) costs[index] = float.PositiveInfinity;
        int start = startRow * grid.Columns + startColumn;
        int goal = goalRow * grid.Columns + goalColumn;
        costs[start] = 0f;

        for (int iteration = 0; iteration < count; iteration++)
        {
            int current = -1;
            float currentCost = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                if (!visited[index] && costs[index] < currentCost)
                {
                    current = index;
                    currentCost = costs[index];
                }
            }

            if (current < 0) break;
            if (current == goal) return currentCost;
            visited[current] = true;

            int row = current / grid.Columns;
            int column = current % grid.Columns;
            Relax(row - 1, column, current, currentCost, costs, visited, agentType);
            Relax(row, column + 1, current, currentCost, costs, visited, agentType);
            Relax(row + 1, column, current, currentCost, costs, visited, agentType);
            Relax(row, column - 1, current, currentCost, costs, visited, agentType);
        }

        return float.PositiveInfinity;
    }

    private void Relax(int row, int column, int fromIndex, float fromCost, float[] costs, bool[] visited,
        NavigationAgentType agentType)
    {
        if (!grid.TryGetCell(row, column, out NavigationGridCell cell) || !cell.Walkable ||
            (agentType == NavigationAgentType.Wolf && cell.Zone == NavigationGridZone.SafeZone)) return;

        int index = row * grid.Columns + column;
        if (visited[index]) return;
        int fromRow = fromIndex / grid.Columns;
        int fromColumn = fromIndex % grid.Columns;
        float candidate = fromCost + Vector3.Distance(
            grid.GridToWorldPosition(fromRow, fromColumn), grid.GridToWorldPosition(row, column));
        if (candidate < costs[index]) costs[index] = candidate;
    }

    private List<CellSnapshot> CaptureGrid()
    {
        var cells = new List<CellSnapshot>(grid.CellCount);
        for (int row = 0; row < grid.Rows; row++)
        {
            for (int column = 0; column < grid.Columns; column++)
            {
                Assert.That(grid.TryGetCell(row, column, out NavigationGridCell cell), Is.True);
                cells.Add(new CellSnapshot(cell.Row, cell.Column, cell.Walkable, cell.Zone, cell.ZoneId));
            }
        }

        return cells;
    }

    private sealed class CellSnapshot
    {
        private readonly int row;
        private readonly int column;
        private readonly bool walkable;
        private readonly NavigationGridZone zone;
        private readonly string zoneId;

        public CellSnapshot(int row, int column, bool walkable, NavigationGridZone zone, string zoneId)
        {
            this.row = row;
            this.column = column;
            this.walkable = walkable;
            this.zone = zone;
            this.zoneId = zoneId;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is CellSnapshot other)) return false;
            return row == other.row && column == other.column && walkable == other.walkable &&
                   zone == other.zone && zoneId == other.zoneId;
        }

        public override int GetHashCode() => row ^ column ^ walkable.GetHashCode() ^ (int)zone ^ (zoneId?.GetHashCode() ?? 0);
    }
}
