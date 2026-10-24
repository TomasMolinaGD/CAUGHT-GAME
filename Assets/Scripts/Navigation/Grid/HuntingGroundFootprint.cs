using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>
    /// Sampled, allocation-free footprint checks for agents moving on the XZ plane.
    /// All zone membership is delegated to HuntingGroundRegion.
    /// </summary>
    public static class HuntingGroundFootprint
    {
        private const int ConstraintIterations = 12;
        private const float NormalProbeDistance = 0.05f;

        private static readonly Vector3[] FootprintDirections =
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
            if (region == null || !region.ContainsWorldPosition(worldPosition))
            {
                return false;
            }

            float clearance = Mathf.Max(0f, radius) + Mathf.Max(0f, margin);
            if (clearance <= 0.0001f)
            {
                return true;
            }

            for (int i = 0; i < FootprintDirections.Length; i++)
            {
                if (!region.ContainsWorldPosition(worldPosition + FootprintDirections[i] * clearance))
                {
                    return false;
                }
            }

            return true;
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
            for (int i = 0; i < FootprintDirections.Length; i++)
            {
                Vector3 direction = FootprintDirections[i];
                if (!Contains(region, position + direction * NormalProbeDistance, radius, margin))
                {
                    outward += direction;
                }
            }

            return outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.zero;
        }
    }
}
