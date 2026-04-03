using UnityEngine;

/// <summary>
/// Detects when the drone lands on this zone and takes off again.
/// The landing is only confirmed once the drone leaves the zone after touching down.
/// </summary>
public class LandingZone : MonoBehaviour
{
    [Header("Landing Zone")]
    public string zoneName = "Landing Pad";

    [Tooltip("Maximum velocity to count as touched down (m/s)")]
    public float landingSpeedThreshold = 0.3f;

    [Header("Visuals")]
    public Color idleColor = new Color(0.2f, 0.2f, 0.2f);
    public Color droneDetectedColor = new Color(0.9f, 0.9f, 0.1f);  // Yellow — drone in zone
    public Color touchedDownColor = new Color(1f, 0.5f, 0f);         // Orange — touched down, waiting for takeoff
    public Color completedColor = new Color(0.1f, 0.9f, 0.1f);       // Green — landed and took off

    [Header("State")]
    [SerializeField] private bool isCompleted;
    [SerializeField] private bool hasTouchedDown;
    public bool IsLanded => isCompleted;

    private bool droneInZone;
    private Rigidbody droneRb;
    private Renderer padRenderer;

    void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.RegisterLandingZone(this);

        padRenderer = GetComponent<Renderer>();
        SetPadColor(idleColor);

        // Ensure at least one trigger collider exists
        bool hasTrigger = false;
        foreach (var col in GetComponents<Collider>())
        {
            if (col.isTrigger) hasTrigger = true;
        }
        if (!hasTrigger)
        {
            BoxCollider trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0, 50f, 0);
            trigger.size = new Vector3(2f, 100f, 2f);
        }
    }

    void Update()
    {
        if (isCompleted || !droneInZone || droneRb == null) return;

        // Detect touchdown: drone is in the zone and slow enough
        if (!hasTouchedDown && droneRb.linearVelocity.magnitude < landingSpeedThreshold)
        {
            hasTouchedDown = true;
            SetPadColor(touchedDownColor);
            Debug.Log($"LandingZone: Drone touched down on '{zoneName}' — take off to confirm");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        DroneController drone = other.GetComponentInParent<DroneController>();
        if (drone == null) return;

        droneInZone = true;
        droneRb = drone.GetComponent<Rigidbody>();
        if (!isCompleted)
            SetPadColor(droneDetectedColor);
        Debug.Log($"LandingZone: Drone entered '{zoneName}'");
    }

    void OnTriggerExit(Collider other)
    {
        DroneController drone = other.GetComponentInParent<DroneController>();
        if (drone == null) return;

        droneInZone = false;
        droneRb = null;

        // If drone touched down and now left — landing is complete
        if (hasTouchedDown && !isCompleted)
        {
            isCompleted = true;
            SetPadColor(completedColor);
            Debug.Log($"LandingZone: Landing CONFIRMED on '{zoneName}' — drone took off");

            if (GameManager.Instance != null)
                GameManager.Instance.ReportLanding(this);
        }
        else if (!isCompleted)
        {
            // Drone flew through without slowing down
            SetPadColor(idleColor);
        }

        Debug.Log($"LandingZone: Drone left '{zoneName}'");
    }

    public void MarkLanded()
    {
        isCompleted = true;
    }

    public void ResetState()
    {
        isCompleted = false;
        hasTouchedDown = false;
        droneInZone = false;
        SetPadColor(idleColor);
    }

    private void SetPadColor(Color color)
    {
        if (padRenderer != null)
            padRenderer.material.color = color;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = isCompleted ? Color.green : new Color(0.2f, 0.8f, 0.2f, 0.5f);
        Gizmos.DrawWireCube(transform.position, new Vector3(0.3f, 0.02f, 0.3f));

        #if UNITY_EDITOR
        string status = isCompleted ? "COMPLETED" : hasTouchedDown ? "TOUCHED DOWN" : "waiting";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.1f,
            $"{zoneName}\n{status}");
        #endif
    }
}
