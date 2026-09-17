using UnityEngine;

[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class AlienVisualGrounding : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float groundHeight = 0.05f;

    private Transform leftFoot;
    private Transform rightFoot;
    private Vector3 visualRootBaseLocalPosition;
    private Quaternion visualRootBaseLocalRotation;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (visualRoot == null)
        {
            visualRoot = FindVisualRoot();
        }

        if (visualRoot != null)
        {
            visualRootBaseLocalPosition = visualRoot.localPosition;
            visualRootBaseLocalRotation = visualRoot.localRotation;
        }

        CacheFeet();
    }

    private void LateUpdate()
    {
        if (visualRoot == null || animator == null || !animator.isHuman)
        {
            return;
        }

        if (leftFoot == null || rightFoot == null)
        {
            CacheFeet();
            if (leftFoot == null || rightFoot == null)
            {
                return;
            }
        }

        // Ignore root offsets authored in the animation. Then compensate using
        // the evaluated humanoid feet, keeping the visible model on the floor.
        visualRoot.localPosition = visualRootBaseLocalPosition;
        visualRoot.localRotation = visualRootBaseLocalRotation;

        float lowestFootHeight = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
        float heightCorrection = groundHeight - lowestFootHeight;
        Vector3 groundedLocalPosition = visualRootBaseLocalPosition;
        groundedLocalPosition.y += heightCorrection / Mathf.Max(0.0001f, transform.lossyScale.y);
        visualRoot.localPosition = groundedLocalPosition;
    }

    private void CacheFeet()
    {
        if (animator == null || !animator.isHuman)
        {
            return;
        }

        leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
    }

    private Transform FindVisualRoot()
    {
        SkinnedMeshRenderer renderer = GetComponentInChildren<SkinnedMeshRenderer>();
        if (renderer == null)
        {
            return transform.childCount > 0 ? transform.GetChild(0) : null;
        }

        Transform candidate = renderer.transform;
        while (candidate.parent != null && candidate.parent != transform)
        {
            candidate = candidate.parent;
        }

        return candidate != transform ? candidate : null;
    }
}
