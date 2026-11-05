using System;
using System.Collections.Generic;
using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>Immutable selection result for the least-cost reachable SafeZone destination.</summary>
    public sealed class SafeZonePathSelection
    {
        public string ZoneId { get; }
        public Vector2Int DestinationCell { get; }
        public NavigationPathResult Path { get; }
        public float DestinationCost { get; }
        public bool UsesDetour { get; }
        public bool HasThreatAssessment { get; }
        public bool CrossesCaptureRadius { get; }
        public bool CrossesDangerRadius { get; }
        public float ThreatRisk { get; }
        public float MinimumThreatDistance { get; }

        internal SafeZonePathSelection(string zoneId, Vector2Int destinationCell,
            NavigationPathResult path, RouteThreatScore threatScore, NavigationPathResult shortest)
        {
            ZoneId = zoneId ?? string.Empty;
            DestinationCell = destinationCell;
            Path = path;
            DestinationCost = shortest.TotalCost;
            UsesDetour = path != shortest;
            HasThreatAssessment = threatScore.HasThreat;
            CrossesCaptureRadius = threatScore.CrossesCaptureRadius;
            CrossesDangerRadius = threatScore.CrossesDangerRadius;
            ThreatRisk = threatScore.TotalRisk;
            MinimumThreatDistance = threatScore.MinimumDistance;
        }
    }

    public readonly struct RouteThreatScore
    {
        internal RouteThreatScore(bool hasThreat, bool crossesCaptureRadius,
            bool crossesDangerRadius, float totalRisk, float initialApproach,
            float minimumDistance)
        {
            HasThreat = hasThreat;
            CrossesCaptureRadius = crossesCaptureRadius;
            CrossesDangerRadius = crossesDangerRadius;
            TotalRisk = totalRisk;
            InitialApproach = initialApproach;
            MinimumDistance = minimumDistance;
        }

        public bool HasThreat { get; }
        public bool CrossesCaptureRadius { get; }
        public bool CrossesDangerRadius { get; }
        public float TotalRisk { get; }
        public float InitialApproach { get; }
        public float MinimumDistance { get; }

        public bool SaferThan(RouteThreatScore other) => SafeZonePathSelector.CompareThreat(this, other) < 0;

        public static RouteThreatScore None => new RouteThreatScore(
            false, false, false, 0f, 0f, float.PositiveInfinity);
    }

    /// <summary>Selects a reachable SafeZone using the actual Hunter A* route cost.</summary>
    public static class SafeZonePathSelector
    {
        public static bool TrySelect(
            NavigationGrid grid,
            Vector3 hunterWorldPosition,
            out SafeZonePathSelection selection)
        {
            return TrySelectInternal(grid, hunterWorldPosition, false, Vector3.zero,
                0f, 0f, out selection, null);
        }

        public static bool TrySelect(
            NavigationGrid grid,
            Vector3 hunterWorldPosition,
            Vector3 threatWorldPosition,
            float captureRadius,
            float safetyMargin,
            out SafeZonePathSelection selection)
        {
            return TrySelectInternal(grid, hunterWorldPosition, true, threatWorldPosition,
                Mathf.Max(0f, captureRadius), Mathf.Max(0f, safetyMargin), out selection, null);
        }

        // Same selection pass; the optional output contains only reachable candidate cells.
        public static bool TrySelectWithDiagnostics(NavigationGrid grid, Vector3 hunterWorldPosition,
            Vector3 threatWorldPosition, float captureRadius, float safetyMargin,
            out SafeZonePathSelection selection, List<Vector2Int> movingAwayCells)
            => TrySelect(grid, hunterWorldPosition, threatWorldPosition, captureRadius, safetyMargin,
                out selection, null, movingAwayCells);

        public static bool TrySelect(NavigationGrid grid, Vector3 hunterWorldPosition,
            Vector3? threatWorldPosition, float captureRadius, float safetyMargin,
            out SafeZonePathSelection selection, string preferredZoneId, List<Vector2Int> movingAwayCells)
            => TrySelect(grid, hunterWorldPosition, threatWorldPosition, captureRadius, safetyMargin,
                out selection, preferredZoneId, movingAwayCells, null);

        public static bool TrySelect(NavigationGrid grid, Vector3 hunterWorldPosition,
            Vector3? threatWorldPosition, float captureRadius, float safetyMargin,
            out SafeZonePathSelection selection, string preferredZoneId, List<Vector2Int> movingAwayCells,
            Vector2Int? preferredCell)
        {
            movingAwayCells?.Clear();
            return TrySelectInternal(grid, hunterWorldPosition, threatWorldPosition.HasValue, threatWorldPosition.GetValueOrDefault(),
                Mathf.Max(0f, captureRadius), Mathf.Max(0f, safetyMargin), out selection,
                threatWorldPosition.HasValue ? movingAwayCells : null, preferredZoneId, preferredCell);
        }

        private static bool TrySelectInternal(
            NavigationGrid grid,
            Vector3 hunterWorldPosition,
            bool hasThreat,
            Vector3 threatWorldPosition,
            float captureRadius,
            float safetyMargin,
            out SafeZonePathSelection selection, List<Vector2Int> movingAwayCells, string preferredZoneId = null,
            Vector2Int? preferredCell = null)
        {
            selection = null;
            if (grid == null || !grid.TryWorldToGrid(hunterWorldPosition, out int startRow, out int startColumn))
            {
                return false;
            }

            var cellsByZone = new SortedDictionary<string, List<Vector2Int>>(StringComparer.Ordinal);
            for (int row = 0; row < grid.Rows; row++)
            {
                for (int column = 0; column < grid.Columns; column++)
                {
                    if (!grid.TryGetCell(row, column, out NavigationGridCell cell) ||
                        !cell.Walkable || cell.Zone != NavigationGridZone.SafeZone)
                    {
                        continue;
                    }

                    string zoneId = cell.ZoneId;
                    if (!cellsByZone.TryGetValue(zoneId, out List<Vector2Int> zoneCells))
                    {
                        zoneCells = new List<Vector2Int>();
                        cellsByZone.Add(zoneId, zoneCells);
                    }

                    zoneCells.Add(new Vector2Int(column, row));
                }
            }

            NavigationPathResult bestPath = null;
            string bestZoneId = null;
            Vector2Int bestCell = default;
            RouteThreatScore bestThreatScore = RouteThreatScore.None;
            foreach (KeyValuePair<string, List<Vector2Int>> zone in cellsByZone)
            {
                foreach (Vector2Int destinationCell in zone.Value)
                {
                    NavigationPathResult path = NavigationGridPathfinder.FindPath(
                        grid, startRow, startColumn, destinationCell.y, destinationCell.x,
                        NavigationAgentType.Hunter);
                    RouteThreatScore threatScore = hasThreat
                        ? EvaluateThreat(path, hunterWorldPosition, threatWorldPosition,
                            captureRadius, safetyMargin,
                            path.Waypoints.Count > 1 ? 1 : 0)
                        : RouteThreatScore.None;
                    if (movingAwayCells != null && path.Status == NavigationPathStatus.Success)
                    {
                        int first = path.Waypoints.Count > 1 ? 1 : 0;
                        Vector3 step = Flatten(path.Waypoints[first] - hunterWorldPosition);
                        // Distance cannot decrease anywhere on this segment (tangency included).
                        if (step.sqrMagnitude > 0f &&
                            Vector3.Dot(Flatten(hunterWorldPosition - threatWorldPosition), step) >= 0f)
                            movingAwayCells.Add(destinationCell);
                    }
                    if (path.Status != NavigationPathStatus.Success ||
                        !IsBetter(path, threatScore, zone.Key, destinationCell,
                            bestPath, bestThreatScore, bestZoneId, bestCell, preferredZoneId, preferredCell))
                    {
                        continue;
                    }

                    bestPath = path;
                    bestZoneId = zone.Key;
                    bestCell = destinationCell;
                    bestThreatScore = threatScore;
                }
            }

            if (bestPath == null)
            {
                return false;
            }

            // Freeze the geometrically nearest destination before considering a different corridor.
            NavigationPathResult shortest = bestPath;
            if (hasThreat && (bestThreatScore.InitialApproach > 0f || bestThreatScore.CrossesDangerRadius))
            {
                float envelope = captureRadius + safetyMargin;
                float separation = Flatten(hunterWorldPosition - threatWorldPosition).magnitude;
                NavigationPathResult FindDetour(float clearance, bool requireAway)
                {
                    bool AllowEdge(bool first, Vector3 from, Vector3 to)
                    {
                        from = first ? hunterWorldPosition : from;
                        Vector3 radial = Flatten(from - threatWorldPosition);
                        bool leaving = Vector3.Dot(radial, Flatten(to - from)) >= 0f;
                        if (first && requireAway && !leaving) return false;
                        // An origin already inside the envelope may only move outward until clear.
                        return radial.magnitude < clearance ? leaving :
                            FirstSegmentMinimumDistance(from, to, threatWorldPosition) + 0.0001f >= clearance;
                    }
                    return NavigationGridPathfinder.FindPath(grid, startRow, startColumn,
                        bestCell.y, bestCell.x, NavigationAgentType.Hunter, AllowEdge);
                }
                var detour = FindDetour(Mathf.Max(separation, envelope), true);
                if (detour.Status != NavigationPathStatus.Success && separation > envelope)
                    detour = FindDetour(envelope, true);
                if (detour.Status != NavigationPathStatus.Success && bestThreatScore.CrossesDangerRadius)
                    detour = FindDetour(envelope, false);
                if (detour.Status != NavigationPathStatus.Success && bestThreatScore.InitialApproach > 0f)
                    detour = FindDetour(0f, true);
                if (detour.Status == NavigationPathStatus.Success)
                {
                    var score = EvaluateThreat(detour, hunterWorldPosition, threatWorldPosition,
                        captureRadius, safetyMargin, detour.Waypoints.Count > 1 ? 1 : 0);
                    if (score.SaferThan(bestThreatScore))
                    {
                        bestPath = detour;
                        bestThreatScore = score;
                    }
                }
            }
            selection = new SafeZonePathSelection(bestZoneId, bestCell, bestPath, bestThreatScore, shortest);
            return true;
        }

        private static bool IsBetter(
            NavigationPathResult candidatePath,
            RouteThreatScore candidateThreat,
            string candidateZoneId,
            Vector2Int candidateCell,
            NavigationPathResult bestPath,
            RouteThreatScore bestThreat,
            string bestZoneId,
            Vector2Int bestCell,
            string preferredZoneId, Vector2Int? preferredCell)
        {
            if (bestPath == null)
            {
                return true;
            }

            if (candidatePath.TotalCost != bestPath.TotalCost)
            {
                return candidatePath.TotalCost < bestPath.TotalCost;
            }

            // On a replan, a moving threat can justify a different corridor without
            // switching between equally near, still reachable Safe Zones.
            if (preferredZoneId != null && (candidateZoneId == preferredZoneId) != (bestZoneId == preferredZoneId))
                return candidateZoneId == preferredZoneId;
            if (preferredCell.HasValue && candidateZoneId == preferredZoneId && bestZoneId == preferredZoneId)
            {
                bool candidateIsCurrent = candidateCell == preferredCell.Value;
                if (candidateIsCurrent != (bestCell == preferredCell.Value)) return candidateIsCurrent;
            }

            int threatComparison = CompareThreat(candidateThreat, bestThreat);
            if (threatComparison != 0)
            {
                return threatComparison < 0;
            }

            int zoneComparison = string.CompareOrdinal(candidateZoneId, bestZoneId);
            if (zoneComparison != 0)
            {
                return zoneComparison < 0;
            }

            return candidateCell.y < bestCell.y ||
                   (candidateCell.y == bestCell.y && candidateCell.x < bestCell.x);
        }

        internal static int CompareThreat(RouteThreatScore candidate, RouteThreatScore best)
        {
            if (!candidate.HasThreat || !best.HasThreat) return 0;
            if (candidate.CrossesCaptureRadius != best.CrossesCaptureRadius)
                return candidate.CrossesCaptureRadius ? 1 : -1;
            if (candidate.CrossesDangerRadius != best.CrossesDangerRadius)
                return candidate.CrossesDangerRadius ? 1 : -1;
            if (!Mathf.Approximately(candidate.TotalRisk, best.TotalRisk))
                return candidate.TotalRisk < best.TotalRisk ? -1 : 1;
            if (!Mathf.Approximately(candidate.InitialApproach, best.InitialApproach))
                return candidate.InitialApproach < best.InitialApproach ? -1 : 1;
            return 0;
        }

        public static RouteThreatScore EvaluateThreat(
            NavigationPathResult path,
            Vector3 hunterWorldPosition,
            Vector3 threatWorldPosition,
            float captureRadius,
            float safetyMargin,
            int startWaypointIndex = 0)
        {
            if (path == null || path.Status != NavigationPathStatus.Success ||
                path.Waypoints.Count == 0)
                return RouteThreatScore.None;

            float dangerRadius = captureRadius + safetyMargin;
            Vector3 previous = Flatten(hunterWorldPosition);
            Vector3 threat = Flatten(threatWorldPosition);
            float minimumDistance = Vector3.Distance(previous, threat);
            float totalRisk = 0f;
            float initialApproach = 0f;
            bool firstTravelSegment = true;
            bool crossesCaptureRadius = minimumDistance < captureRadius;
            bool crossesDangerRadius = minimumDistance < dangerRadius;

            int firstWaypoint = Mathf.Clamp(startWaypointIndex, 0, path.Waypoints.Count - 1);
            for (int i = firstWaypoint; i < path.Waypoints.Count; i++)
            {
                Vector3 current = Flatten(path.Waypoints[i]);
                float segmentLength = Vector3.Distance(previous, current);
                if (segmentLength <= 0.0001f)
                {
                    previous = current;
                    continue;
                }

                float segmentDistance = DistanceToSegment(threat, previous, current);
                minimumDistance = Mathf.Min(minimumDistance, segmentDistance);
                crossesCaptureRadius |= segmentDistance < captureRadius;
                crossesDangerRadius |= segmentDistance < dangerRadius;
                float dangerPenetration = Mathf.Max(0f, dangerRadius - segmentDistance);
                totalRisk += dangerPenetration * (1f + segmentLength);

                if (firstTravelSegment)
                {
                    float before = Vector3.Distance(previous, threat);
                    initialApproach = Mathf.Max(0f, before - segmentDistance);
                    firstTravelSegment = false;
                }

                previous = current;
            }

            return new RouteThreatScore(true, crossesCaptureRadius, crossesDangerRadius,
                totalRisk, initialApproach, minimumDistance);
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        public static float FirstSegmentMinimumDistance(Vector3 hunter, Vector3 waypoint, Vector3 threat)
        {
            return DistanceToSegment(Flatten(threat), Flatten(hunter), Flatten(waypoint));
        }

        private static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector3 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.0001f) return Vector3.Distance(point, start);
            float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSquared);
            return Vector3.Distance(point, start + segment * t);
        }
    }
}
