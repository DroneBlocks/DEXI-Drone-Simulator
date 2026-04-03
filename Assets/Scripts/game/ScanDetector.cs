using UnityEngine;

/// <summary>
/// Attached to the drone. Raycasts downward to detect ScanTargets.
/// When the drone hovers over a target for the required dwell time, it triggers a scan.
/// </summary>
public class ScanDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("Maximum detection distance (meters)")]
    public float maxDetectionRange = 1.5f;

    [Tooltip("How long the drone must hover over a target to scan it (seconds)")]
    public float scanDwellTime = 1.0f;

    [Tooltip("Maximum speed to allow scanning (m/s). Drone must be relatively still.")]
    public float maxScanSpeed = 0.5f;

    [Header("Debug")]
    [SerializeField] private ScanTarget currentTarget;
    [SerializeField] private float currentDwell;
    [SerializeField] private bool isScanning;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = GetComponentInParent<Rigidbody>();
    }

    void Update()
    {
        ScanTarget detected = RaycastForTarget();

        if (detected != null && detected == currentTarget)
        {
            // Same target — accumulate dwell time if slow enough
            bool slowEnough = rb == null || rb.linearVelocity.magnitude < maxScanSpeed;

            if (slowEnough && !detected.IsScanned)
            {
                currentDwell += Time.deltaTime;
                isScanning = true;

                if (currentDwell >= scanDwellTime)
                {
                    // Auto-start game on first scan
                    if (GameManager.Instance != null && GameManager.Instance.State == GameManager.GameState.WaitingToStart)
                        GameManager.Instance.StartGame();

                    if (GameManager.Instance != null)
                        GameManager.Instance.ReportScan(detected);

                    isScanning = false;
                }
            }
        }
        else
        {
            // New target or no target
            currentTarget = detected;
            currentDwell = 0f;
            isScanning = false;
        }
    }

    private ScanTarget RaycastForTarget()
    {
        // Raycast straight down from drone
        Ray ray = new Ray(transform.position, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDetectionRange))
        {
            ScanTarget target = hit.collider.GetComponent<ScanTarget>();
            if (target != null)
                return target;

            // Check parent (in case collider is on a child)
            target = hit.collider.GetComponentInParent<ScanTarget>();
            return target;
        }

        return null;
    }

    void OnDrawGizmos()
    {
        // Draw detection ray
        Gizmos.color = isScanning ? Color.yellow : Color.cyan;
        Vector3 start = transform.position;
        Vector3 end = start + Vector3.down * maxDetectionRange;
        Gizmos.DrawLine(start, end);

        // Draw scan progress ring
        if (isScanning && currentTarget != null)
        {
            Gizmos.color = Color.yellow;
            float progress = currentDwell / scanDwellTime;
            Gizmos.DrawWireSphere(currentTarget.transform.position, 0.1f * progress);
        }
    }
}
