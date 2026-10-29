using UnityEngine;

/// <summary>Builds the supported world-space capsule from the Rigidbody actually moved.</summary>
public static class BoidCapsuleGeometry
{
    private const float MinimumDimension = 0.0001f;
    private const float OrthogonalityTolerance = 0.0001f;
    private const float UprightTolerance = 0.999999f;

    public struct WorldCapsule
    {
        public Vector3 Center { get; }
        public Vector3 Point0 { get; }
        public Vector3 Point1 { get; }
        public float Radius { get; }
        public float Height { get; }
        public float HorizontalFootprintRadius { get; }

        internal WorldCapsule(
            Vector3 center,
            Vector3 point0,
            Vector3 point1,
            float radius,
            float height,
            float horizontalFootprintRadius)
        {
            Center = center;
            Point0 = point0;
            Point1 = point1;
            Radius = radius;
            Height = height;
            HorizontalFootprintRadius = horizontalFootprintRadius;
        }
    }

    /// <summary>
    /// Returns false for configurations outside the first supported contract: a root,
    /// local-Y upright capsule on the Rigidbody, with finite non-degenerate orthogonal scale.
    /// </summary>
    public static bool TryGetWorldCapsule(
        Rigidbody body,
        Vector3 bodyPosition,
        Quaternion bodyRotation,
        out WorldCapsule capsule)
    {
        return TryGetWorldCapsule(body, bodyPosition, bodyRotation, out capsule, out _);
    }

    /// <summary>Diagnostic overload that reports the first rejected geometry predicate.</summary>
    public static bool TryGetWorldCapsule(
        Rigidbody body,
        Vector3 bodyPosition,
        Quaternion bodyRotation,
        out WorldCapsule capsule,
        out string failureReason)
    {
        capsule = default;
        failureReason = null;
        if (body == null || !IsFinite(bodyPosition) || !IsFinite(bodyRotation))
        {
            failureReason = body == null ? "RigidbodyMissing" : "BodyPoseNonFinite";
            return false;
        }

        float rotationMagnitude = Mathf.Sqrt(
            bodyRotation.x * bodyRotation.x + bodyRotation.y * bodyRotation.y +
            bodyRotation.z * bodyRotation.z + bodyRotation.w * bodyRotation.w);
        if (!IsFinite(rotationMagnitude) || rotationMagnitude <= MinimumDimension)
        {
            failureReason = "RotationInvalid";
            return false;
        }

        bodyRotation = new Quaternion(
            bodyRotation.x / rotationMagnitude,
            bodyRotation.y / rotationMagnitude,
            bodyRotation.z / rotationMagnitude,
            bodyRotation.w / rotationMagnitude);
        if (Vector3.Dot(bodyRotation * Vector3.up, Vector3.up) < UprightTolerance)
        {
            failureReason = "RotationNotUpright";
            return false;
        }

        Transform bodyTransform = body.transform;
        CapsuleCollider collider = bodyTransform.GetComponent<CapsuleCollider>();
        if (collider == null || collider.transform != bodyTransform ||
            (collider.attachedRigidbody != null && collider.attachedRigidbody != body) ||
            collider.direction != 1 ||
            !IsFinite(collider.center) || !IsFinite(collider.radius) ||
            !IsFinite(collider.height) || collider.radius <= 0f || collider.height <= 0f)
        {
            failureReason = collider == null ? "CapsuleColliderMissing" :
                collider.transform != bodyTransform ? "CapsuleColliderNotOnRigidbody" :
                collider.attachedRigidbody != null && collider.attachedRigidbody != body ? "CapsuleRigidbodyMismatch" :
                collider.direction != 1 ? "CapsuleDirectionUnsupported" :
                !IsFinite(collider.center) ? "CapsuleCenterNonFinite" :
                !IsFinite(collider.radius) || collider.radius <= 0f ? "CapsuleRadiusInvalid" :
                "CapsuleHeightInvalid";
            return false;
        }

        Vector3 scale = bodyTransform.lossyScale;
        Vector3 absoluteScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        if (!IsFinite(scale) || absoluteScale.x <= MinimumDimension ||
            absoluteScale.y <= MinimumDimension || absoluteScale.z <= MinimumDimension ||
            !HasOrthogonalWorldScale(bodyTransform))
        {
            failureReason = !IsFinite(scale) ? "WorldScaleNonFinite" :
                absoluteScale.x <= MinimumDimension || absoluteScale.y <= MinimumDimension || absoluteScale.z <= MinimumDimension
                    ? "WorldScaleDegenerate"
                    : "WorldScaleNotOrthogonal";
            return false;
        }

        float radius = collider.radius * Mathf.Max(absoluteScale.x, absoluteScale.z);
        float height = Mathf.Max(collider.height * absoluteScale.y, radius * 2f);
        if (!IsFinite(radius) || !IsFinite(height) || radius <= 0f || height < radius * 2f)
        {
            failureReason = "WorldCapsuleDimensionsInvalid";
            return false;
        }

        Vector3 center = bodyPosition + bodyRotation * Vector3.Scale(collider.center, scale);
        float segmentHalfLength = height * 0.5f - radius;
        Vector3 capsuleAxis = bodyRotation * Vector3.up;
        if (capsuleAxis.sqrMagnitude <= MinimumDimension * MinimumDimension)
        {
            failureReason = "CapsuleAxisDegenerate";
            return false;
        }
        capsuleAxis.Normalize();
        Vector3 segmentOffset = capsuleAxis * segmentHalfLength;
        float horizontalSegmentProjection =
            new Vector2(capsuleAxis.x, capsuleAxis.z).magnitude * segmentHalfLength;
        float horizontalOffset = Vector2.Distance(
            new Vector2(bodyPosition.x, bodyPosition.z),
            new Vector2(center.x, center.z));
        float horizontalFootprintRadius = radius + horizontalOffset + horizontalSegmentProjection;
        if (!IsFinite(center) || !IsFinite(horizontalOffset) || !IsFinite(horizontalFootprintRadius))
        {
            failureReason = "WorldCapsuleDerivedValuesNonFinite";
            return false;
        }

        capsule = new WorldCapsule(
            center,
            center + segmentOffset,
            center - segmentOffset,
            radius,
            height,
            horizontalFootprintRadius);
        return true;
    }

    private static bool HasOrthogonalWorldScale(Transform target)
    {
        Matrix4x4 matrix = target.localToWorldMatrix;
        Vector3 x = matrix.MultiplyVector(Vector3.right);
        Vector3 y = matrix.MultiplyVector(Vector3.up);
        Vector3 z = matrix.MultiplyVector(Vector3.forward);
        float xMagnitude = x.magnitude;
        float yMagnitude = y.magnitude;
        float zMagnitude = z.magnitude;
        if (xMagnitude <= MinimumDimension || yMagnitude <= MinimumDimension || zMagnitude <= MinimumDimension)
        {
            return false;
        }

        x /= xMagnitude;
        y /= yMagnitude;
        z /= zMagnitude;
        return Vector3.Dot(y, Vector3.up) >= UprightTolerance &&
               Mathf.Abs(Vector3.Dot(x, y)) <= OrthogonalityTolerance &&
               Mathf.Abs(Vector3.Dot(x, z)) <= OrthogonalityTolerance &&
               Mathf.Abs(Vector3.Dot(y, z)) <= OrthogonalityTolerance;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

    private static bool IsFinite(Quaternion value) =>
        IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
}
