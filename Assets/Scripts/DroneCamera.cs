using UnityEngine;

public class DroneCamera : MonoBehaviour
{
    public Transform target;
    public float followDistance = 5.0f;
    public float height = 2.0f;
    public float smoothSpeed = 5.0f;
    public float tiltAngle = 30f;
    
    // Orbit settings
    public float orbitSpeed = 100f;
    public float minVerticalAngle = -30f;
    public float maxVerticalAngle = 80f;

    // Zoom settings
    public float zoomSpeed = 2f;
    public float minZoomDistance = 2f;
    public float maxZoomDistance = 50f;
    
    // FPV view settings
    public float fpvForwardOffset = 0.5f;    // Distance in front of drone
    public float fpvHeightOffset = 0.2f;     // Height above drone center
    
    // Bottom view settings
    public float bottomViewHeight = 0.5f;    // Distance below drone
    public float groundViewDistance = 20f;    // How far down to look

    private enum CameraMode
    {
        Follow,
        FPV,
        Bottom
    }
    
    private CameraMode currentMode = CameraMode.Follow;
    private Vector3 lastFollowPosition;
    private Quaternion lastFollowRotation;
    
    // Orbit state
    private float orbitX = 0f;
    private float orbitY = 0f;

    void Start()
    {
        if (target)
        {
            // Set initial position
            Vector3 startPos = target.position;
            startPos.y += height;
            startPos.z -= followDistance;
            transform.position = startPos;
        }

        // Initialize orbit angles
        orbitY = tiltAngle;
    }

    void LateUpdate()
    {
        if (!target) return;

        // Check for camera toggle
        if (Input.GetKeyDown(KeyCode.C))
        {
            // Cycle through modes
            switch (currentMode)
            {
                case CameraMode.Follow:
                    currentMode = CameraMode.FPV;
                    lastFollowPosition = transform.position;
                    lastFollowRotation = transform.rotation;
                    break;
                case CameraMode.FPV:
                    currentMode = CameraMode.Bottom;
                    break;
                case CameraMode.Bottom:
                    currentMode = CameraMode.Follow;
                    break;
            }
        }

        // Update camera based on current mode
        switch (currentMode)
        {
            case CameraMode.Follow:
                UpdateFollowView();
                break;
            case CameraMode.FPV:
                UpdateFPVView();
                break;
            case CameraMode.Bottom:
                UpdateBottomView();
                break;
        }
    }

    void UpdateFollowView()
    {
        // Handle orbit input when right mouse button is held
        if (Input.GetMouseButton(1))
        {
            orbitX += Input.GetAxis("Mouse X") * orbitSpeed * Time.deltaTime;
            orbitY -= Input.GetAxis("Mouse Y") * orbitSpeed * Time.deltaTime;
            orbitY = Mathf.Clamp(orbitY, minVerticalAngle, maxVerticalAngle);
        }

        // Handle zoom with mouse scroll wheel
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput != 0f)
        {
            followDistance -= scrollInput * zoomSpeed;
            followDistance = Mathf.Clamp(followDistance, minZoomDistance, maxZoomDistance);
        }

        // Calculate orbit position
        Quaternion rotation = Quaternion.Euler(orbitY, orbitX, 0);
        Vector3 targetPos = target.position;
        Vector3 offset = rotation * new Vector3(0, height, -followDistance);
        Vector3 desiredPosition = targetPos + offset;

        // Move smoothly to position
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // Look at target
        transform.LookAt(targetPos);
    }

    void UpdateFPVView()
    {
        // Position camera slightly in front and above drone's center
        Vector3 desiredPosition = target.position + 
                                (target.forward * fpvForwardOffset) + 
                                (Vector3.up * fpvHeightOffset);

        // Move smoothly to position
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // Match drone's rotation
        transform.rotation = Quaternion.Lerp(transform.rotation, target.rotation, smoothSpeed * Time.deltaTime);
    }

    void UpdateBottomView()
    {
        // Position camera below drone
        Vector3 desiredPosition = target.position + Vector3.down * bottomViewHeight;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // Look down while maintaining drone's forward direction
        Quaternion desiredRotation = Quaternion.LookRotation(Vector3.down, target.forward);
        transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, smoothSpeed * Time.deltaTime);
    }
} 