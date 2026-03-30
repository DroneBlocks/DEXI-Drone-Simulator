using UnityEngine;

/// <summary>
/// Camera that follows a target and always points straight down.
/// Used for AprilTag detection and PIP view.
/// FOV is configurable - ROSCameraPublisher reads it automatically.
/// </summary>
public class DownwardCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField]
    [Tooltip("The drone to follow")]
    public Transform target;

    [Header("Offset from Drone")]
    [SerializeField]
    [Tooltip("Vertical offset below drone")]
    private float verticalOffset = 0.1f;

    [Header("Smoothing")]
    [SerializeField]
    [Tooltip("How smoothly the camera follows")]
    private float smoothSpeed = 10f;

    [Header("Camera Settings")]
    [SerializeField]
    [Tooltip("Vertical FOV in degrees - ROSCameraPublisher will read this automatically")]
    private float verticalFOV = 48.8f;

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.fieldOfView = verticalFOV;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Position below drone
        Vector3 desiredPosition = target.position + Vector3.down * verticalOffset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // Match drone's full rotation, then point down relative to drone body
        transform.rotation = target.rotation * Quaternion.Euler(90f, 0f, 0f);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
