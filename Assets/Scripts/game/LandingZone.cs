using UnityEngine;

public class LandingZone : MonoBehaviour
{
    [Header("Landing Zone")]
    public string zoneName = "Landing Pad";

    [Tooltip("Maximum velocity to count as a landing (m/s)")]
    public float landingSpeedThreshold = 0.3f;

    [Header("Visuals")]
    public Color idleColor = new Color(0.2f, 0.2f, 0.2f);
    public Color droneDetectedColor = new Color(0.9f, 0.9f, 0.1f);
    public Color completedColor = new Color(0.1f, 0.9f, 0.1f);

    [Header("State")]
    [SerializeField] private bool isCompleted;
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

        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
        }

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

        if (droneRb.linearVelocity.magnitude < landingSpeedThreshold)
        {
            isCompleted = true;
            SetPadColor(completedColor);
            Debug.Log($"LandingZone: Landing CONFIRMED on '{zoneName}'");

            if (GameManager.Instance != null)
                GameManager.Instance.ReportLanding(this);
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

        if (!isCompleted)
            SetPadColor(idleColor);

        Debug.Log($"LandingZone: Drone left '{zoneName}'");
    }

    public void MarkLanded()
    {
        isCompleted = true;
    }

    public void ResetState()
    {
        isCompleted = false;
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
        string status = isCompleted ? "COMPLETED" : "waiting";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.1f, $"{zoneName}\n{status}");
#endif
    }
}