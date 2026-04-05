using UnityEngine;

public class DroneCamera : MonoBehaviour
{
    public Transform target;
    public float followDistance = 0.44f;
    public float height = 0.5f;
    public float smoothSpeed = 5.0f;
    public float tiltAngle = 30f;

    // Orbit settings
    public float orbitSpeed = 2000f;
    public float minVerticalAngle = -30f;
    public float maxVerticalAngle = 80f;

    // Zoom settings
    public float zoomSpeed = 2f;
    public float minZoomDistance = 0.1f;
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
        if (Input.GetMouseButton(1))
        {
            float maxDelta = 5f;
            float mouseX = Mathf.Clamp(Input.GetAxis("Mouse X"), -maxDelta, maxDelta);
            float mouseY = Mathf.Clamp(Input.GetAxis("Mouse Y"), -maxDelta, maxDelta);

            orbitX += mouseX * orbitSpeed * Time.deltaTime;
            orbitY -= mouseY * orbitSpeed * Time.deltaTime;
            orbitY = Mathf.Clamp(orbitY, minVerticalAngle, maxVerticalAngle);
        }

        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput != 0f)
        {
            followDistance -= scrollInput * zoomSpeed;
            followDistance = Mathf.Clamp(followDistance, minZoomDistance, maxZoomDistance);
        }

        Quaternion rotation = Quaternion.Euler(orbitY, orbitX, 0);
        Vector3 desiredPosition = target.position + rotation * new Vector3(0, height, -followDistance);

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        Quaternion desiredRotation = Quaternion.LookRotation(target.position - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, smoothSpeed * Time.deltaTime);
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