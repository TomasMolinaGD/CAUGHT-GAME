using System;
using UnityEngine;

namespace CaughtGame.Navigation
{
    public enum RegionQueryStatus
    {
        Clear,
        Blocked,
        InvalidConfiguration
    }

    /// <summary>Exact static circle-versus-cell queries on the horizontal grid plane.</summary>
    public static class HuntingGroundFootprint
    {
        private const float AxisTolerance = 0.000001f;
        private const float MinimumDimension = 0.0001f;
        // World-space cushion for floating-point error at mathematical tangencies.
        private const float GeometryEpsilon = 0.00001f;
        private const int ConstraintIterations = 12;
        private const float NormalProbeDistance = 0.05f;
        private static readonly Vector3[] NormalDirections =
        {
            Vector3.right,
            new Vector3(0.9238795f, 0f, 0.3826834f),
            new Vector3(0.7071068f, 0f, 0.7071068f),
            new Vector3(0.3826834f, 0f, 0.9238795f),
            Vector3.forward,
            new Vector3(-0.3826834f, 0f, 0.9238795f),
            new Vector3(-0.7071068f, 0f, 0.7071068f),
            new Vector3(-0.9238795f, 0f, 0.3826834f),
            Vector3.left,
            new Vector3(-0.9238795f, 0f, -0.3826834f),
            new Vector3(-0.7071068f, 0f, -0.7071068f),
            new Vector3(-0.3826834f, 0f, -0.9238795f),
            Vector3.back,
            new Vector3(0.3826834f, 0f, -0.9238795f),
            new Vector3(0.7071068f, 0f, -0.7071068f),
            new Vector3(0.9238795f, 0f, -0.3826834f)
        };

        public static bool Contains(
            HuntingGroundRegion region,
            Vector3 worldPosition,
            float radius,
            float margin)
        {
            return Query(region, worldPosition, radius, margin) == RegionQueryStatus.Clear;
        }

        public static RegionQueryStatus Query(
            HuntingGroundRegion region,
            Vector3 worldPosition,
            float radius,
            float margin)
        {
            if (region == null || !IsFinite(worldPosition) || !IsFinite(radius) ||
                !IsFinite(margin) || radius < 0f || margin < 0f)
            {
                return RegionQueryStatus.InvalidConfiguration;
            }

            if (!TryBuildFrame(region.SourceGrid, out GridFrame frame))
            {
                return RegionQueryStatus.InvalidConfiguration;
            }

            double effectiveRadius = (double)radius + margin;
            if (!IsFinite(effectiveRadius))
            {
                return RegionQueryStatus.InvalidConfiguration;
            }

            double intersectionRadius = effectiveRadius + GeometryEpsilon;
            double intersectionRadiusSquared = intersectionRadius * intersectionRadius;
            if (!IsFinite(intersectionRadius) || !IsFinite(intersectionRadiusSquared))
            {
                return RegionQueryStatus.InvalidConfiguration;
            }

            double offsetX = (double)worldPosition.x - frame.Origin.x;
            double offsetY = (double)worldPosition.y - frame.Origin.y;
            double offsetZ = (double)worldPosition.z - frame.Origin.z;
            double u = offsetX * frame.AxisX.x + offsetY * frame.AxisX.y + offsetZ * frame.AxisX.z;
            double v = offsetX * frame.AxisZ.x + offsetY * frame.AxisZ.y + offsetZ * frame.AxisZ.z;
            double totalWidth = (double)frame.CellWidth * frame.Columns;
            double totalDepth = (double)frame.CellDepth * frame.Rows;
            if (!IsFinite(u) || !IsFinite(v) || !IsFinite(totalWidth) || !IsFinite(totalDepth) ||
                totalWidth > float.MaxValue || totalDepth > float.MaxValue)
            {
                return RegionQueryStatus.InvalidConfiguration;
            }

            if (u - effectiveRadius <= GeometryEpsilon ||
                u + effectiveRadius >= totalWidth - GeometryEpsilon ||
                v - effectiveRadius <= GeometryEpsilon ||
                v + effectiveRadius >= totalDepth - GeometryEpsilon)
            {
                return RegionQueryStatus.Blocked;
            }

            int minColumn = FirstIntersectingCell(u - intersectionRadius, frame.CellWidth, frame.Columns);
            int maxColumn = LastIntersectingCell(u + intersectionRadius, frame.CellWidth, frame.Columns);
            int minRow = FirstIntersectingCell(v - intersectionRadius, frame.CellDepth, frame.Rows);
            int maxRow = LastIntersectingCell(v + intersectionRadius, frame.CellDepth, frame.Rows);

            for (int row = minRow; row <= maxRow; row++)
            {
                for (int column = minColumn; column <= maxColumn; column++)
                {
                    double minX = column * (double)frame.CellWidth;
                    double maxX = minX + frame.CellWidth;
                    double minZ = row * (double)frame.CellDepth;
                    double maxZ = minZ + frame.CellDepth;
                    double closestX = Math.Min(Math.Max(u, minX), maxX);
                    double closestZ = Math.Min(Math.Max(v, minZ), maxZ);
                    double dx = u - closestX;
                    double dz = v - closestZ;
                    if (dx * dx + dz * dz <= intersectionRadiusSquared &&
                        !region.IsValidHuntingGroundCell(row, column))
                    {
                        return RegionQueryStatus.Blocked;
                    }
                }
            }

            return RegionQueryStatus.Clear;
        }

        /// <summary>
        /// Limits one physics-step displacement to the last sampled safe point.
        /// If the starting point is already outside, movement is allowed only when
        /// it approaches the configured recovery target; no corrective teleport occurs.
        /// </summary>
        public static Vector3 ConstrainMovement(
            HuntingGroundRegion region,
            Vector3 startPosition,
            Vector3 proposedPosition,
            float radius,
            float margin,
            bool hasRecoveryTarget,
            Vector3 recoveryTarget,
            out Vector3 outwardNormal)
        {
            outwardNormal = Vector3.zero;
            if (region == null)
            {
                return proposedPosition;
            }

            bool startIsSafe = Contains(region, startPosition, radius, margin);
            bool proposedIsSafe = Contains(region, proposedPosition, radius, margin);
            if (proposedIsSafe)
            {
                return proposedPosition;
            }

            if (!startIsSafe)
            {
                if (hasRecoveryTarget &&
                    (proposedPosition - recoveryTarget).sqrMagnitude <
                    (startPosition - recoveryTarget).sqrMagnitude)
                {
                    return proposedPosition;
                }

                outwardNormal = EstimateOutwardNormal(region, startPosition, radius, margin);
                return startPosition;
            }

            float safeFraction = 0f;
            float unsafeFraction = 1f;
            for (int i = 0; i < ConstraintIterations; i++)
            {
                float middle = (safeFraction + unsafeFraction) * 0.5f;
                Vector3 candidate = Vector3.Lerp(startPosition, proposedPosition, middle);
                if (Contains(region, candidate, radius, margin))
                {
                    safeFraction = middle;
                }
                else
                {
                    unsafeFraction = middle;
                }
            }

            Vector3 constrainedPosition = Vector3.Lerp(startPosition, proposedPosition, safeFraction);
            outwardNormal = EstimateOutwardNormal(region, constrainedPosition, radius, margin);
            if (outwardNormal.sqrMagnitude < 0.0001f)
            {
                outwardNormal = (proposedPosition - constrainedPosition).normalized;
            }
            return constrainedPosition;
        }

        private static Vector3 EstimateOutwardNormal(
            HuntingGroundRegion region,
            Vector3 position,
            float radius,
            float margin)
        {
            Vector3 outward = Vector3.zero;
            for (int i = 0; i < NormalDirections.Length; i++)
            {
                Vector3 direction = NormalDirections[i];
                if (!Contains(region, position + direction * NormalProbeDistance, radius, margin))
                {
                    outward += direction;
                }
            }

            return outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.zero;
        }

        private struct GridFrame
        {
            public int Columns;
            public int Rows;
            public Vector3 Origin;
            public Vector3 AxisX;
            public Vector3 AxisZ;
            public float CellWidth;
            public float CellDepth;
        }

        private static bool TryBuildFrame(NavigationGrid grid, out GridFrame frame)
        {
            frame = default;
            if (grid == null || grid.Columns <= 0 || grid.Rows <= 0 ||
                grid.CellCount != (long)grid.Columns * grid.Rows ||
                !IsFinite(grid.CellSize) || grid.CellSize <= 0f)
            {
                return false;
            }

            Matrix4x4 matrix = grid.transform.localToWorldMatrix;
            Vector3 x = matrix.MultiplyVector(Vector3.right);
            Vector3 y = matrix.MultiplyVector(Vector3.up);
            Vector3 z = matrix.MultiplyVector(Vector3.forward);
            float xMagnitude = x.magnitude;
            float yMagnitude = y.magnitude;
            float zMagnitude = z.magnitude;
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z) ||
                xMagnitude <= MinimumDimension || yMagnitude <= MinimumDimension ||
                zMagnitude <= MinimumDimension)
            {
                return false;
            }

            x /= xMagnitude;
            y /= yMagnitude;
            z /= zMagnitude;
            if (y.y < 1f - AxisTolerance || Mathf.Abs(y.x) > AxisTolerance ||
                Mathf.Abs(y.z) > AxisTolerance || Mathf.Abs(x.y) > AxisTolerance ||
                Mathf.Abs(z.y) > AxisTolerance || Mathf.Abs(Vector3.Dot(x, y)) > AxisTolerance ||
                Mathf.Abs(Vector3.Dot(x, z)) > AxisTolerance ||
                Mathf.Abs(Vector3.Dot(y, z)) > AxisTolerance)
            {
                return false;
            }

            frame = new GridFrame
            {
                Columns = grid.Columns,
                Rows = grid.Rows,
                Origin = grid.transform.TransformPoint(Vector3.zero),
                AxisX = x,
                AxisZ = z,
                CellWidth = xMagnitude * grid.CellSize,
                CellDepth = zMagnitude * grid.CellSize
            };
            return IsFinite(frame.Origin) && IsFinite(frame.CellWidth) && IsFinite(frame.CellDepth);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static int FirstIntersectingCell(double coordinate, float cellDimension, int cellCount)
        {
            double index = Math.Ceiling(coordinate / cellDimension) - 1d;
            if (index <= 0d) return 0;
            if (index >= cellCount - 1d) return cellCount - 1;
            return (int)index;
        }

        private static int LastIntersectingCell(double coordinate, float cellDimension, int cellCount)
        {
            double index = Math.Floor(coordinate / cellDimension);
            if (index <= 0d) return 0;
            if (index >= cellCount - 1d) return cellCount - 1;
            return (int)index;
        }
    }
}
