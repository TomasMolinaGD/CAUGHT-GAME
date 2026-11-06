using System.Collections.Generic;
using UnityEngine;

namespace CaughtGame.Navigation
{
    /// <summary>
    /// Performs an explicit physical-clearance check against one marked graybox generation.
    /// Call from an explicit diagnostic action, not from per-frame movement or Scene View drawing.
    /// </summary>
    public static class NavigationPathValidator
    {
        public static NavigationPathValidationResult Validate(NavigationGrid grid,
            GeneratedGeometryMarker generation, NavigationPathResult path,
            float capsuleRadius, float capsuleHeight, int layerMask = Physics.AllLayers)
        {
            if (!IsFinite(capsuleRadius) || !IsFinite(capsuleHeight) || capsuleRadius <= 0f ||
                capsuleHeight < capsuleRadius * 2f || layerMask == 0)
            {
                return Invalid(NavigationPathValidationStatus.InvalidConfiguration,
                    "Capsule dimensions and collision mask must describe a usable physical query.");
            }

            if (grid == null || generation == null || generation.SourceGrid != grid ||
                !generation.isActiveAndEnabled || generation.gameObject.scene != grid.gameObject.scene ||
                !grid.gameObject.scene.IsValid())
            {
                return Invalid(NavigationPathValidationStatus.MissingGeometry,
                    "No active graybox generation for this grid is available in the same scene.");
            }

            if (!grid.gameObject.scene.GetPhysicsScene().Equals(Physics.defaultPhysicsScene))
            {
                return Invalid(NavigationPathValidationStatus.InvalidConfiguration,
                    "Static Physics queries cannot validate a non-default scene physics world.");
            }

            if (!TryCollectWallColliders(generation, out HashSet<Collider> wallColliders))
            {
                return Invalid(NavigationPathValidationStatus.MissingGeometry,
                    "The marked generation is missing its Ground or owned solid wall colliders.");
            }

            if (path == null || path.Status != NavigationPathStatus.Success ||
                path.Waypoints.Count == 0 || path.Cells.Count != path.Waypoints.Count)
            {
                return Invalid(NavigationPathValidationStatus.InvalidPath,
                    "A successful path with matching cell and waypoint data is required.");
            }

            for (int index = 0; index < path.Waypoints.Count; index++)
            {
                if (!IsFinite(path.Waypoints[index]))
                {
                    return Invalid(NavigationPathValidationStatus.InvalidPath,
                        $"Waypoint {index} is not a finite world position.");
                }
            }

            // Validation is explicit and infrequent; synchronize editor/runtime Transform edits once
            // before querying, rather than risking stale collider poses or syncing per segment.
            Physics.SyncTransforms();

            var segments = new NavigationPathSegmentValidation[path.Waypoints.Count - 1];
            int firstProblematicSegment = -1;
            Collider firstBlockingCollider = null;
            Vector3 firstProblemPosition = default;
            Vector3 firstPoint = path.Waypoints[0];
            if (TryGetOverlap(firstPoint, capsuleRadius, capsuleHeight, layerMask,
                    wallColliders, out Collider startOverlap))
            {
                firstProblematicSegment = segments.Length == 0 ? 0 : 0;
                firstBlockingCollider = startOverlap;
                firstProblemPosition = firstPoint;
            }

            for (int index = 0; index < segments.Length; index++)
            {
                Vector3 start = path.Waypoints[index];
                Vector3 end = path.Waypoints[index + 1];
                bool clear = true;
                Collider blocker = null;
                Vector3 problemPosition = end;

                if (TryGetSweepHit(start, end, capsuleRadius, capsuleHeight, layerMask,
                        wallColliders, out RaycastHit hit))
                {
                    clear = false;
                    blocker = hit.collider;
                    problemPosition = hit.point;
                }

                // A sweep does not report a capsule already overlapping at its endpoint.
                if (TryGetOverlap(end, capsuleRadius, capsuleHeight, layerMask,
                        wallColliders, out Collider endOverlap))
                {
                    clear = false;
                    if (blocker == null)
                    {
                        blocker = endOverlap;
                        problemPosition = end;
                    }
                }

                segments[index] = new NavigationPathSegmentValidation(start, end, clear,
                    blocker, problemPosition);
                if (!clear && firstProblematicSegment < 0)
                {
                    firstProblematicSegment = index;
                    firstBlockingCollider = blocker;
                    firstProblemPosition = problemPosition;
                }
            }

            if (firstProblematicSegment >= 0)
            {
                string obstacle = firstBlockingCollider == null ? "an unknown obstacle" :
                    $"'{firstBlockingCollider.name}'";
                return new NavigationPathValidationResult(NavigationPathValidationStatus.Blocked,
                    $"Capsule clearance is blocked by {obstacle}.", firstProblematicSegment,
                    firstBlockingCollider, firstProblemPosition, segments);
            }

            return new NavigationPathValidationResult(NavigationPathValidationStatus.Clear,
                "All route points and swept segments are clear for this capsule size.",
                segments: segments);
        }

        private static bool TryCollectWallColliders(GeneratedGeometryMarker generation,
            out HashSet<Collider> wallColliders)
        {
            wallColliders = new HashSet<Collider>();
            bool hasGroundCollider = false;
            GeneratedGeometryPartMarker[] parts =
                generation.GetComponentsInChildren<GeneratedGeometryPartMarker>(true);
            if (parts.Length == 0) return false;

            foreach (GeneratedGeometryPartMarker part in parts)
            {
                if (part == null || part.Owner != generation || part.transform.parent != generation.transform)
                {
                    return false;
                }

                // The graybox builder places each generated BoxCollider on the same object as its
                // ownership marker. Child colliders may be hand-authored and are not generator-owned.
                Collider[] colliders = part.GetComponents<Collider>();
                bool isGround = part.gameObject.name == "Ground";
                foreach (Collider collider in colliders)
                {
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (isGround)
                    {
                        hasGroundCollider = true;
                        continue;
                    }

                    if (!collider.isTrigger)
                    {
                        wallColliders.Add(collider);
                    }
                }
            }

            return hasGroundCollider && wallColliders.Count > 0;
        }

        private static bool TryGetOverlap(Vector3 basePosition, float radius, float height,
            int layerMask, HashSet<Collider> ownedWalls, out Collider blockingCollider)
        {
            GetCapsulePoints(basePosition, radius, height, out Vector3 bottom, out Vector3 top);
            Collider[] overlaps = Physics.OverlapCapsule(bottom, top, radius, layerMask,
                QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in overlaps)
            {
                if (ownedWalls.Contains(overlap))
                {
                    blockingCollider = overlap;
                    return true;
                }
            }

            blockingCollider = null;
            return false;
        }

        private static bool TryGetSweepHit(Vector3 startBase, Vector3 endBase, float radius,
            float height, int layerMask, HashSet<Collider> ownedWalls, out RaycastHit firstHit)
        {
            Vector3 displacement = endBase - startBase;
            float distance = displacement.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                firstHit = default;
                return false;
            }

            GetCapsulePoints(startBase, radius, height, out Vector3 bottom, out Vector3 top);
            RaycastHit[] hits = Physics.CapsuleCastAll(bottom, top, radius,
                displacement / distance, distance, layerMask, QueryTriggerInteraction.Ignore);
            float closestDistance = float.PositiveInfinity;
            firstHit = default;
            bool found = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || !ownedWalls.Contains(hit.collider) || hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                firstHit = hit;
                found = true;
            }

            return found;
        }

        private static void GetCapsulePoints(Vector3 basePosition, float radius, float height,
            out Vector3 bottomSphereCenter, out Vector3 topSphereCenter)
        {
            bottomSphereCenter = basePosition + Vector3.up * radius;
            topSphereCenter = basePosition + Vector3.up * (height - radius);
        }

        private static NavigationPathValidationResult Invalid(NavigationPathValidationStatus status,
            string message)
        {
            return new NavigationPathValidationResult(status, message);
        }

        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
