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

    public enum RegionSweepStatus
    {
        Clear,
        Blocked,
        InvalidStart,
        InvalidConfiguration,
        BudgetExceeded
    }

    /// <summary>
    /// Result of a continuous regional sweep. Normal points from the valid space
    /// toward the blocking surface (the direction in which penetration would occur).
    /// Cell coordinates are -1 when the first contact is the outer boundary.
    /// </summary>
    public readonly struct RegionSweepResult
    {
        public RegionSweepStatus Status { get; }
        public float SafeFraction { get; }
        public float? ContactFraction { get; }
        public Vector3 Normal { get; }
        public int Row { get; }
        public int Column { get; }
        public bool HasBlockingCell => Row >= 0 && Column >= 0;

        public RegionSweepResult(
            RegionSweepStatus status,
            float safeFraction,
            float? contactFraction = null,
            Vector3 normal = default,
            int row = -1,
            int column = -1)
        {
            Status = status;
            SafeFraction = safeFraction;
            ContactFraction = contactFraction;
            Normal = normal;
            Row = row;
            Column = column;
        }
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

        /// <summary>Certifies the complete straight-line movement of a horizontal disk.</summary>
        public static RegionSweepResult Sweep(
            HuntingGroundRegion region,
            Vector3 startPosition,
            Vector3 endPosition,
            float radius,
            float margin,
            int candidateBudget = 4096)
        {
            if (region == null || !IsFinite(startPosition) || !IsFinite(endPosition) ||
                !IsFinite(radius) || !IsFinite(margin) || radius < 0f || margin < 0f ||
                candidateBudget <= 0)
            {
                return InvalidSweep();
            }

            if (!TryBuildFrame(region.SourceGrid, out GridFrame frame))
            {
                return InvalidSweep();
            }

            double effectiveRadius = (double)radius + margin;
            double intersectionRadius = effectiveRadius + GeometryEpsilon;
            double radiusSquared = intersectionRadius * intersectionRadius;
            if (!IsFinite(effectiveRadius) || !IsFinite(intersectionRadius) || !IsFinite(radiusSquared))
            {
                return InvalidSweep();
            }

            if (Query(region, startPosition, radius, margin) != RegionQueryStatus.Clear)
            {
                return new RegionSweepResult(RegionSweepStatus.InvalidStart, 0f);
            }

            Vector2 start = Project(frame, startPosition);
            Vector2 end = Project(frame, endPosition);
            Vector2 delta = end - start;
            double length = delta.magnitude;
            if (!IsFinite(length))
            {
                return InvalidSweep();
            }

            if (length <= double.Epsilon)
            {
                return new RegionSweepResult(RegionSweepStatus.Clear, 1f);
            }

            double totalWidth = (double)frame.CellWidth * frame.Columns;
            double totalDepth = (double)frame.CellDepth * frame.Rows;
            if (!IsFinite(totalWidth) || !IsFinite(totalDepth) || totalWidth <= 0d || totalDepth <= 0d)
            {
                return InvalidSweep();
            }

            double minU = Math.Min(start.x, end.x) - intersectionRadius;
            double maxU = Math.Max(start.x, end.x) + intersectionRadius;
            double minV = Math.Min(start.y, end.y) - intersectionRadius;
            double maxV = Math.Max(start.y, end.y) + intersectionRadius;
            int minColumn = FirstIntersectingCell(minU, frame.CellWidth, frame.Columns);
            int maxColumn = LastIntersectingCell(maxU, frame.CellWidth, frame.Columns);
            int minRow = FirstIntersectingCell(minV, frame.CellDepth, frame.Rows);
            int maxRow = LastIntersectingCell(maxV, frame.CellDepth, frame.Rows);
            long candidates = (long)(maxColumn - minColumn + 1) * (maxRow - minRow + 1);
            if (candidates > candidateBudget)
            {
                return new RegionSweepResult(RegionSweepStatus.BudgetExceeded, 0f);
            }

            double bestT = double.PositiveInfinity;
            Vector2 bestNormal = Vector2.zero;
            int bestRow = -1;
            int bestColumn = -1;
            for (int row = minRow; row <= maxRow; row++)
            {
                for (int column = minColumn; column <= maxColumn; column++)
                {
                    if (region.IsValidHuntingGroundCell(row, column))
                    {
                        continue;
                    }

                    double minX = column * (double)frame.CellWidth;
                    double maxX = minX + frame.CellWidth;
                    double minZ = row * (double)frame.CellDepth;
                    double maxZ = minZ + frame.CellDepth;
                    if (TryRoundedRectangleContact(start, delta, minX, maxX, minZ, maxZ,
                            intersectionRadius, out double contact, out Vector2 normal) && contact < bestT)
                    {
                        bestT = contact;
                        bestNormal = normal;
                        bestRow = row;
                        bestColumn = column;
                    }
                }
            }

            if (TryOuterBoundaryContact(start, delta, totalWidth, totalDepth, intersectionRadius,
                    out double outerT, out Vector2 outerNormal) && outerT < bestT)
            {
                bestT = outerT;
                bestNormal = outerNormal;
                bestRow = -1;
                bestColumn = -1;
            }

            RegionQueryStatus finalStatus = Query(region, endPosition, radius, margin);
            if (finalStatus == RegionQueryStatus.InvalidConfiguration)
            {
                return InvalidSweep();
            }

            if (double.IsPositiveInfinity(bestT))
            {
                if (finalStatus != RegionQueryStatus.Clear)
                {
                    bestT = 1d;
                    bestNormal = Vector2.zero;
                }
                else
                {
                    return new RegionSweepResult(RegionSweepStatus.Clear, 1f);
                }
            }

            float contactFraction = Mathf.Clamp01((float)bestT);
            float retreat = Mathf.Min(0.001f, 0.0001f / Mathf.Max((float)length, 0.0001f));
            float safeFraction = Mathf.Clamp01(contactFraction - retreat);
            if (Query(region, Vector3.Lerp(startPosition, endPosition, safeFraction), radius, margin) !=
                RegionQueryStatus.Clear)
            {
                safeFraction = 0f;
            }
            Vector3 worldNormal = frame.AxisX * bestNormal.x + frame.AxisZ * bestNormal.y;
            return new RegionSweepResult(
                RegionSweepStatus.Blocked,
                safeFraction,
                contactFraction,
                worldNormal.sqrMagnitude > 0.0001f ? worldNormal.normalized : Vector3.zero,
                bestRow,
                bestColumn);
        }

        private static RegionSweepResult InvalidSweep() =>
            new RegionSweepResult(RegionSweepStatus.InvalidConfiguration, 0f);

        private static Vector2 Project(GridFrame frame, Vector3 worldPosition)
        {
            Vector3 offset = worldPosition - frame.Origin;
            return new Vector2(
                Vector3.Dot(offset, frame.AxisX),
                Vector3.Dot(offset, frame.AxisZ));
        }

        private static bool TryRoundedRectangleContact(
            Vector2 start,
            Vector2 delta,
            double minX,
            double maxX,
            double minZ,
            double maxZ,
            double radius,
            out double contact,
            out Vector2 normal)
        {
            contact = double.PositiveInfinity;
            normal = Vector2.zero;
            ConsiderBox(start, delta, minX, maxX, minZ - radius, maxZ + radius, ref contact, ref normal);
            ConsiderBox(start, delta, minX - radius, maxX + radius, minZ, maxZ, ref contact, ref normal);
            ConsiderCorner(start, delta, new Vector2((float)minX, (float)minZ), radius, ref contact, ref normal);
            ConsiderCorner(start, delta, new Vector2((float)minX, (float)maxZ), radius, ref contact, ref normal);
            ConsiderCorner(start, delta, new Vector2((float)maxX, (float)minZ), radius, ref contact, ref normal);
            ConsiderCorner(start, delta, new Vector2((float)maxX, (float)maxZ), radius, ref contact, ref normal);
            return !double.IsPositiveInfinity(contact);
        }

        private static void ConsiderCorner(Vector2 start, Vector2 delta, Vector2 corner, double radius,
            ref double contact, ref Vector2 normal)
        {
            if (TrySegmentCircleContact(start, delta, corner, radius,
                    out double cornerContact, out Vector2 cornerNormal) && cornerContact < contact)
            {
                contact = cornerContact;
                normal = cornerNormal;
            }
        }

        private static void ConsiderBox(Vector2 start, Vector2 delta, double minX, double maxX,
            double minZ, double maxZ, ref double contact, ref Vector2 normal)
        {
            if (TrySegmentBoxContact(start, delta, minX, maxX, minZ, maxZ,
                    out double boxContact, out Vector2 boxNormal) && boxContact < contact)
            {
                contact = boxContact;
                normal = boxNormal;
            }
        }

        private static bool TrySegmentBoxContact(Vector2 start, Vector2 delta, double minX, double maxX,
            double minZ, double maxZ, out double contact, out Vector2 normal)
        {
            contact = 0d;
            normal = Vector2.zero;
            double enter = 0d;
            double exit = 1d;
            Vector2 enterNormal = Vector2.zero;
            if (!ClipAxis(start.x, delta.x, minX, maxX, new Vector2(1f, 0f), new Vector2(-1f, 0f), ref enter, ref exit, ref enterNormal) ||
                !ClipAxis(start.y, delta.y, minZ, maxZ, new Vector2(0f, 1f), new Vector2(0f, -1f), ref enter, ref exit, ref enterNormal))
            {
                return false;
            }
            contact = Math.Max(0d, enter);
            normal = enterNormal;
            return contact <= 1d;
        }

        private static bool ClipAxis(double origin, double direction, double min, double max,
            Vector2 minNormal, Vector2 maxNormal, ref double enter, ref double exit, ref Vector2 enterNormal)
        {
            if (Math.Abs(direction) < 1e-12)
            {
                return origin >= min && origin <= max;
            }
            double t0 = (min - origin) / direction;
            double t1 = (max - origin) / direction;
            Vector2 n0 = minNormal;
            Vector2 n1 = maxNormal;
            if (t0 > t1)
            {
                (t0, t1) = (t1, t0);
                (n0, n1) = (n1, n0);
            }
            if (t0 > enter)
            {
                enter = t0;
                enterNormal = n0;
            }
            exit = Math.Min(exit, t1);
            return enter <= exit;
        }

        private static bool TrySegmentCircleContact(Vector2 start, Vector2 delta, Vector2 center,
            double radius, out double contact, out Vector2 normal)
        {
            contact = double.PositiveInfinity;
            normal = Vector2.zero;
            double offsetX = (double)start.x - center.x;
            double offsetZ = (double)start.y - center.y;
            double deltaX = delta.x;
            double deltaZ = delta.y;
            double a = deltaX * deltaX + deltaZ * deltaZ;
            if (a <= 1e-24)
            {
                return false;
            }
            double b = 2d * (offsetX * deltaX + offsetZ * deltaZ);
            double c = offsetX * offsetX + offsetZ * offsetZ - radius * radius;
            double discriminant = b * b - 4d * a * c;
            if (discriminant < 0d)
            {
                return false;
            }
            double root = Math.Sqrt(Math.Max(0d, discriminant));
            double t0 = (-b - root) / (2d * a);
            double t1 = (-b + root) / (2d * a);
            double candidate = t0 >= 0d ? t0 : t1;
            if (candidate < 0d || candidate > 1d)
            {
                return false;
            }
            double pointX = (double)start.x + deltaX * candidate;
            double pointZ = (double)start.y + deltaZ * candidate;
            double normalX = center.x - pointX;
            double normalZ = center.y - pointZ;
            double normalLength = Math.Sqrt(normalX * normalX + normalZ * normalZ);
            normal = normalLength > 0.000001d
                ? new Vector2((float)(normalX / normalLength), (float)(normalZ / normalLength))
                : Vector2.zero;
            contact = candidate;
            return true;
        }

        private static bool TryOuterBoundaryContact(Vector2 start, Vector2 delta, double width, double depth,
            double radius, out double contact, out Vector2 normal)
        {
            contact = double.PositiveInfinity;
            normal = Vector2.zero;
            double minX = radius;
            double maxX = width - radius;
            double minZ = radius;
            double maxZ = depth - radius;
            if (maxX < minX || maxZ < minZ)
            {
                return false;
            }
            if (delta.x < 0d) ConsiderBoundary((minX - start.x) / delta.x, new Vector2(-1f, 0f), ref contact, ref normal);
            if (delta.x > 0d) ConsiderBoundary((maxX - start.x) / delta.x, new Vector2(1f, 0f), ref contact, ref normal);
            if (delta.y < 0d) ConsiderBoundary((minZ - start.y) / delta.y, new Vector2(0f, -1f), ref contact, ref normal);
            if (delta.y > 0d) ConsiderBoundary((maxZ - start.y) / delta.y, new Vector2(0f, 1f), ref contact, ref normal);
            return !double.IsPositiveInfinity(contact);
        }

        private static void ConsiderBoundary(double candidate, Vector2 candidateNormal,
            ref double contact, ref Vector2 normal)
        {
            if (candidate >= 0d && candidate <= 1d && candidate < contact)
            {
                contact = candidate;
                normal = candidateNormal;
            }
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
