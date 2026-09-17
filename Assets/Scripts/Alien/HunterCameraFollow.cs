using UnityEngine;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class HunterCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 32f, -28f);
    [SerializeField, Min(0f)] private float lookHeight = 1.4f;
    [SerializeField, Min(0.01f)] private float smoothTime = 0.25f;

    private Camera gameplayCamera;
    private Vector3 followVelocity;

    private void Awake()
    {
        gameplayCamera = GetComponent<Camera>();
        gameplayCamera.enabled = true;
        gameplayCamera.targetTexture = null;
        gameplayCamera.targetDisplay = 0;
        gameplayCamera.clearFlags = CameraClearFlags.Skybox;
        gameplayCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Base;

        FindTargetIfNeeded();
        SnapToTarget();
    }

    private void LateUpdate()
    {
        FindTargetIfNeeded();
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            smoothTime);
        transform.LookAt(target.position + Vector3.up * lookHeight, Vector3.up);
    }

    private void FindTargetIfNeeded()
    {
        if (target != null)
        {
            return;
        }

        FSMAgent hunter = FindFirstObjectByType<FSMAgent>();
        if (hunter != null)
        {
            target = hunter.transform;
        }
    }

    private void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        transform.position = target.position + offset;
        transform.LookAt(target.position + Vector3.up * lookHeight, Vector3.up);
    }

    private void OnValidate()
    {
        smoothTime = Mathf.Max(0.01f, smoothTime);
        lookHeight = Mathf.Max(0f, lookHeight);
    }
}
