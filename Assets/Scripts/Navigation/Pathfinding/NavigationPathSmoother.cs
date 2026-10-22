using System;
using System.Collections.Generic;
using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>
    /// Optionally removes unnecessary waypoints from a successful A* route. Every shortcut must
    /// pass both grid supercover and the existing physical capsule validator.
    /// </summary>
    public static class NavigationPathSmoother
    {
        private const float PositionTolerance = 0.0001f;
        private const float CornerTolerance = 0.00001f;

        public static NavigationPathSmoothingResult Smooth(NavigationGrid grid,
            GeneratedGeometryMarker generation, NavigationPathResult originalPath,
            NavigationAgentType agentType, float capsuleRadius, float capsuleHeight,
            float additionalClearance = 0f, int layerMask = Physics.AllLayers)
        {
            if (originalPath == null || originalPath.Status != NavigationPathStatus.Success ||
                originalPath.Cells.Count == 0 || originalPath.Cells.Count != originalPath.Waypoints.Count)
            {
                return Failure(NavigationPathSmoothingStatus.InvalidPath, originalPath,
                    NavigationPathValidationStatus.InvalidPath, "A successful A* path with matching cells and waypoints is required.");
            }

            if (!IsValidConfiguration(grid, agentType, capsuleRadius, capsuleHeight,
                    additionalClearance, layerMask))
            {
                return Failure(NavigationPathSmoothingStatus.InvalidConfiguration, originalPath,
                    NavigationPathValidationStatus.InvalidConfiguration,
                    "Grid, agent type, capsule dimensions, clearance margin, or collision mask is invalid.");
            }

            float requiredRadius = capsuleRadius + additionalClearance;
            float requiredHeight = capsuleHeight + additionalClearance * 2f;
            NavigationPathValidationResult baseline = NavigationPathValidator.Validate(grid,
                generation, originalPath, requiredRadius, requiredHeight, layerMask);

            if (baseline.Status == NavigationPathValidationStatus.MissingGeometry)
            {
                return NavigationPathSmoothingResult.Failure(
                    NavigationPathSmoothingStatus.MissingGeometry, originalPath, baseline);
            }

            if (baseline.Status == NavigationPathValidationStatus.InvalidConfiguration)
            {
                return NavigationPathSmoothingResult.Failure(
                    NavigationPathSmoothingStatus.InvalidConfiguration, originalPath, baseline);
            }

            if (baseline.Status == NavigationPathValidationStatus.InvalidPath)
            {
                return NavigationPathSmoothingResult.Failure(
                    NavigationPathSmoothingStatus.InvalidPath, originalPath, baseline);
            }

            if (!IsOriginalRouteLogicallyValid(grid, originalPath, agentType))
            {
                return Failure(NavigationPathSmoothingStatus.InvalidPath, originalPath,
                    NavigationPathValidationStatus.InvalidPath,
                    "The original route contains cells or segments that violate grid traversal rules.");
            }

            var selected = new List<int> { 0 };
            int anchor = 0;
            NavigationPathValidationResult lastValidation = baseline;
            while (anchor < originalPath.Waypoints.Count - 1)
            {
                int chosen = -1;
                for (int candidate = originalPath.Waypoints.Count - 1; candidate > anchor; candidate--)
                {
                    Vector2Int fromCell = originalPath.Cells[anchor];
                    Vector2Int toCell = originalPath.Cells[candidate];
                    if (!IsSegmentLogicallyClear(grid, fromCell, toCell,
                            originalPath.Waypoints[anchor], originalPath.Waypoints[candidate], agentType))
                    {
                        continue;
                    }

                    NavigationPathResult segment = CreateSegmentPath(originalPath, anchor, candidate);
                    NavigationPathValidationResult clearance = NavigationPathValidator.Validate(grid,
                        generation, segment, requiredRadius, requiredHeight, layerMask);
                    lastValidation = clearance;
                    if (clearance.Status == NavigationPathValidationStatus.Clear)
                    {
                        chosen = candidate;
                        break;
                    }

                    if (clearance.Status == NavigationPathValidationStatus.MissingGeometry ||
                        clearance.Status == NavigationPathValidationStatus.InvalidConfiguration)
                    {
                        return NavigationPathSmoothingResult.Failure(
                            clearance.Status == NavigationPathValidationStatus.MissingGeometry
                                ? NavigationPathSmoothingStatus.MissingGeometry
                                : NavigationPathSmoothingStatus.InvalidConfiguration,
                            originalPath, clearance);
                    }
                }

                if (chosen < 0)
                {
                    return NavigationPathSmoothingResult.Failure(
                        NavigationPathSmoothingStatus.NoSafeRoute, originalPath, lastValidation);
                }

                selected.Add(chosen);
                anchor = chosen;
            }

            NavigationPathResult smoothedPath = CreateSelectedPath(originalPath, selected);
            NavigationPathValidationResult finalValidation = NavigationPathValidator.Validate(grid,
                generation, smoothedPath, requiredRadius, requiredHeight, layerMask);
            if (finalValidation.Status != NavigationPathValidationStatus.Clear)
            {
                NavigationPathSmoothingStatus status = finalValidation.Status == NavigationPathValidationStatus.MissingGeometry
                    ? NavigationPathSmoothingStatus.MissingGeometry
                    : finalValidation.Status == NavigationPathValidationStatus.InvalidConfiguration
                        ? NavigationPathSmoothingStatus.InvalidConfiguration
                        : NavigationPathSmoothingStatus.NoSafeRoute;
                return NavigationPathSmoothingResult.Failure(status, originalPath, finalValidation);
            }

            return NavigationPathSmoothingResult.Success(grid, agentType, originalPath,
                smoothedPath, finalValidation);
        }

        private static bool IsValidConfiguration(NavigationGrid grid, NavigationAgentType agentType,
            float radius, float height, float margin, int layerMask)
        {
            return grid != null && grid.Columns > 0 && grid.Rows > 0 && grid.CellSize > 0f &&
                   IsFinite(grid.CellSize) &&
                   (agentType == NavigationAgentType.Hunter || agentType == NavigationAgentType.Wolf) &&
                   IsFinite(radius) && radius > 0f && IsFinite(height) && height >= radius * 2f &&
                   IsFinite(margin) && margin >= 0f && IsFinite(radius + margin) &&
                   IsFinite(height + margin * 2f) && layerMask != 0;
        }

        private static bool IsOriginalRouteLogicallyValid(NavigationGrid grid,
            NavigationPathResult path, NavigationAgentType agentType)
        {
            for (int index = 0; index < path.Cells.Count; index++)
            {
                Vector2Int cell = path.Cells[index];
                if (!IsCellTraversable(grid, cell.x, cell.y, agentType) ||
                    Vector3.Distance(grid.GridToWorldPosition(cell.y, cell.x), path.Waypoints[index]) >
                    Mathf.Max(PositionTolerance, grid.CellSize * PositionTolerance))
                {
                    return false;
                }

                if (index > 0 && !IsSegmentLogicallyClear(grid, path.Cells[index - 1], cell,
                        path.Waypoints[index - 1], path.Waypoints[index], agentType))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSegmentLogicallyClear(NavigationGrid grid, Vector2Int fromCell,
            Vector2Int toCell, Vector3 fromWorld, Vector3 toWorld, NavigationAgentType agentType)
        {
            if (!IsCellTraversable(grid, fromCell.x, fromCell.y, agentType) ||
                !IsCellTraversable(grid, toCell.x, toCell.y, agentType)) return false;

            Vector3 localStart = grid.transform.InverseTransformPoint(fromWorld);
            Vector3 localEnd = grid.transform.InverseTransformPoint(toWorld);
            float x = localStart.x / grid.CellSize - 0.5f;
            float row = localStart.z / grid.CellSize - 0.5f;
            float endX = localEnd.x / grid.CellSize - 0.5f;
            float endRow = localEnd.z / grid.CellSize - 0.5f;
            if (!IsFinite(x) || !IsFinite(row) || !IsFinite(endX) || !IsFinite(endRow) ||
                Mathf.Abs(localStart.y) > PositionTolerance || Mathf.Abs(localEnd.y) > PositionTolerance ||
                Mathf.Abs(x - fromCell.x) > PositionTolerance ||
                Mathf.Abs(row - fromCell.y) > PositionTolerance ||
                Mathf.Abs(endX - toCell.x) > PositionTolerance ||
                Mathf.Abs(endRow - toCell.y) > PositionTolerance)
            {
                return false;
            }

            float deltaX = endX - x;
            float deltaRow = endRow - row;
            int stepX = Math.Sign(deltaX);
            int stepRow = Math.Sign(deltaRow);
            float tDeltaX = stepX == 0 ? float.PositiveInfinity : 1f / Mathf.Abs(deltaX);
            float tDeltaRow = stepRow == 0 ? float.PositiveInfinity : 1f / Mathf.Abs(deltaRow);
            float nextBoundaryX = fromCell.x + stepX * 0.5f;
            float nextBoundaryRow = fromCell.y + stepRow * 0.5f;
            float tMaxX = stepX == 0 ? float.PositiveInfinity : (nextBoundaryX - x) / deltaX;
            float tMaxRow = stepRow == 0 ? float.PositiveInfinity : (nextBoundaryRow - row) / deltaRow;
            int column = fromCell.x;
            int currentRow = fromCell.y;

            while (column != toCell.x || currentRow != toCell.y)
            {
                if (Mathf.Abs(tMaxX - tMaxRow) <= CornerTolerance)
                {
                    if (stepX != 0 && !IsCellTraversable(grid, column + stepX, currentRow, agentType))
                        return false;
                    if (stepRow != 0 && !IsCellTraversable(grid, column, currentRow + stepRow, agentType))
                        return false;
                    column += stepX;
                    currentRow += stepRow;
                    tMaxX += tDeltaX;
                    tMaxRow += tDeltaRow;
                }
                else if (tMaxX < tMaxRow)
                {
                    column += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    currentRow += stepRow;
                    tMaxRow += tDeltaRow;
                }

                if (!IsCellTraversable(grid, column, currentRow, agentType)) return false;
            }

            return true;
        }

        private static bool IsCellTraversable(NavigationGrid grid, int column, int row,
            NavigationAgentType agentType)
        {
            if (!grid.TryGetCell(row, column, out NavigationGridCell cell) || !cell.Walkable) return false;
            return agentType != NavigationAgentType.Wolf || cell.Zone != NavigationGridZone.SafeZone;
        }

        private static NavigationPathResult CreateSegmentPath(NavigationPathResult source,
            int startIndex, int endIndex)
        {
            Vector2Int[] cells = { source.Cells[startIndex], source.Cells[endIndex] };
            Vector3[] points = { source.Waypoints[startIndex], source.Waypoints[endIndex] };
            return NavigationPathResult.CreateSuccess(cells, points,
                Vector3.Distance(points[0], points[1]), 0);
        }

        private static NavigationPathResult CreateSelectedPath(NavigationPathResult source,
            List<int> indices)
        {
            var cells = new Vector2Int[indices.Count];
            var points = new Vector3[indices.Count];
            float distance = 0f;
            for (int index = 0; index < indices.Count; index++)
            {
                int sourceIndex = indices[index];
                cells[index] = source.Cells[sourceIndex];
                points[index] = source.Waypoints[sourceIndex];
                if (index > 0) distance += Vector3.Distance(points[index - 1], points[index]);
            }

            return NavigationPathResult.CreateSuccess(cells, points, distance, source.VisitedCellCount);
        }

        private static NavigationPathSmoothingResult Failure(NavigationPathSmoothingStatus status,
            NavigationPathResult originalPath, NavigationPathValidationStatus validationStatus, string message)
        {
            return NavigationPathSmoothingResult.Failure(status, originalPath,
                new NavigationPathValidationResult(validationStatus, message));
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
