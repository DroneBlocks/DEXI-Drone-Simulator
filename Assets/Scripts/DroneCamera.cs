using UnityEngine;

public class DroneCamera : MonoBehaviour
{
    public Transform target;
    public float followDistance = 3f;
    public float smoothSpeed = 5.0f;

    public float orbitSpeed = 2000f;
    public float minVerticalAngle = -30f;
    public float maxVerticalAngle = 80f;

    public float zoomSpeed = 2f;
    public float minZoomDistance = 0.5f;
    public float maxZoomDistance = 50f;

    public float collisionRadius = 0.2f;
    public float collisionSkinWidth = 0.1f;
    public LayerMask collisionMask = ~0;

    public float fpvForwardOffset = 0.5f;
    public float fpvHeightOffset = 0.2f;
    public float bottomViewHeight = 0.5f;

    private enum CameraMode { Follow, FPV, Bottom }
    private CameraMode currentMode = CameraMode.Follow;

    private float orbitX = 0f;
    private float orbitY = 20f;

    void LateUpdate()
    {
        if (!target) return;

        if (Input.GetKeyDown(KeyCode.C))
        {
            currentMode = (CameraMode)(((int)currentMode + 1) % 3);
        }

        switch (currentMode)
        {
            case CameraMode.Follow: UpdateFollowView(); break;
            case CameraMode.FPV: UpdateFPVView(); break;
            case CameraMode.Bottom: UpdateBottomView(); break;
        }
    }

    void UpdateFollowView()
    {
        if (Input.GetMouseButton(1))
        {
            orbitX += Mathf.Clamp(Input.GetAxis("Mouse X"), -5f, 5f) * orbitSpeed * Time.deltaTime;
            orbitY -= Mathf.Clamp(Input.GetAxis("Mouse Y"), -5f, 5f) * orbitSpeed * Time.deltaTime;
            orbitY = Mathf.Clamp(orbitY, minVerticalAngle, maxVerticalAngle);
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            followDistance = Mathf.Clamp(followDistance - scroll * zoomSpeed, minZoomDistance, maxZoomDistance);
        }

        Quaternion rotation = Quaternion.Euler(orbitY, orbitX, 0);
        Vector3 dir = rotation * Vector3.back;

        float dist = followDistance;
        if (Physics.SphereCast(target.position, collisionRadius, dir, out RaycastHit hit, followDistance, collisionMask))
            dist = Mathf.Max(hit.distance - collisionSkinWidth, minZoomDistance);

        transform.position = target.position + dir * dist;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(target.position - transform.position), smoothSpeed * Time.deltaTime);
    }

    void UpdateFPVView()
    {
        Vector3 desiredPosition = target.position + target.forward * fpvForwardOffset + Vector3.up * fpvHeightOffset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Lerp(transform.rotation, target.rotation, smoothSpeed * Time.deltaTime);
    }

    void UpdateBottomView()
    {
        transform.position = Vector3.Lerp(transform.position,
            target.position + Vector3.down * bottomViewHeight, smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Lerp(transform.rotation,
            Quaternion.LookRotation(Vector3.down, target.forward), smoothSpeed * Time.deltaTime);
    }
}