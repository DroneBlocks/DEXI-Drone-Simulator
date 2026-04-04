using UnityEngine;

/// <summary>
/// A gate the drone must fly through. Built from 4 walls forming a rectangular frame
/// with a trigger zone in the opening. The drone must enter one side and exit the other.
/// </summary>
public class FlyThroughGate : MonoBehaviour
{
    [Header("Gate Settings")]
    public string gateName = "Gate";

    [Tooltip("Inner width of the opening (meters)")]
    public float openingWidth = 0.4f;

    [Tooltip("Inner height of the opening (meters)")]
    public float openingHeight = 0.3f;

    [Tooltip("Wall thickness (meters)")]
    public float wallThickness = 0.02f;

    [Tooltip("Frame border size around the opening (meters)")]
    public float frameBorder = 0.1f;

    [Header("Visuals")]
    public Color frameColor = new Color(0.4f, 0.4f, 0.4f);
    public Color triggerIdleColor = new Color(0.3f, 0.3f, 0.8f, 0.3f);
    public Color triggeredColor = new Color(0.1f, 0.9f, 0.1f);

    [Header("State")]
    [SerializeField] private bool isTriggered;
    public bool IsTriggered => isTriggered;

    private GameObject triggerZone;
    private Renderer triggerRenderer;
    private Renderer[] frameRenderers;
    private bool droneInsideTrigger;

    void Start()
    {
        BuildGate();

        if (GameManager.Instance != null)
            GameManager.Instance.RegisterGate(this);
    }

    void BuildGate()
    {
        float totalWidth = openingWidth + frameBorder * 2;
        float totalHeight = openingHeight + frameBorder * 2;
        frameRenderers = new Renderer[4];

        // All positions are centers of each cube piece
        // Bottom: spans full width, sits below the opening
        frameRenderers[0] = CreateWallPiece("Bottom",
            new Vector3(0, frameBorder / 2, 0),
            new Vector3(totalWidth, frameBorder, wallThickness));

        // Top: spans full width, sits above the opening
        frameRenderers[1] = CreateWallPiece("Top",
            new Vector3(0, frameBorder + openingHeight + frameBorder / 2, 0),
            new Vector3(totalWidth, frameBorder, wallThickness));

        // Left: only spans the opening height, between top and bottom
        frameRenderers[2] = CreateWallPiece("Left",
            new Vector3(-(openingWidth / 2 + frameBorder / 2), frameBorder + openingHeight / 2, 0),
            new Vector3(frameBorder, openingHeight, wallThickness));

        // Right: same as left, other side
        frameRenderers[3] = CreateWallPiece("Right",
            new Vector3(openingWidth / 2 + frameBorder / 2, frameBorder + openingHeight / 2, 0),
            new Vector3(frameBorder, openingHeight, wallThickness));

        // Trigger zone in the opening
        triggerZone = new GameObject("TriggerZone");
        triggerZone.transform.SetParent(transform);
        triggerZone.transform.localPosition = new Vector3(0, frameBorder + openingHeight / 2, 0);
        triggerZone.transform.localRotation = Quaternion.identity;

        // Rigidbody needed so trigger events fire even when drone is kinematic
        Rigidbody triggerRb = triggerZone.AddComponent<Rigidbody>();
        triggerRb.isKinematic = true;

        BoxCollider trigger = triggerZone.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(openingWidth * 0.9f, openingHeight * 0.9f, wallThickness * 4);

        // Visual indicator for the trigger zone (semi-transparent quad)
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
        visual.name = "TriggerVisual";
        visual.transform.SetParent(triggerZone.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = new Vector3(openingWidth * 0.9f, openingHeight * 0.9f, 1f);

        // Remove the quad's collider
        Collider visualCol = visual.GetComponent<Collider>();
        if (visualCol != null) Destroy(visualCol);

        triggerRenderer = visual.GetComponent<Renderer>();
        Material mat = RuntimeMaterials.Instance.CreateLitTransparent(triggerIdleColor);
        triggerRenderer.material = mat;

        // Add the trigger detection script to the trigger zone
        GateTriggerDetector detector = triggerZone.AddComponent<GateTriggerDetector>();
        detector.gate = this;
    }

    Renderer CreateWallPiece(string name, Vector3 localPos, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = $"Frame_{name}";
        wall.transform.SetParent(transform);
        wall.transform.localPosition = localPos;
        wall.transform.localRotation = Quaternion.identity;
        wall.transform.localScale = scale;

        Material mat = RuntimeMaterials.Instance.CreateLit(frameColor);
        Renderer rend = wall.GetComponent<Renderer>();
        rend.material = mat;
        return rend;
    }

    /// <summary>
    /// Called by GateTriggerDetector when drone enters the trigger zone.
    /// </summary>
    public void OnDroneEntered()
    {
        if (isTriggered) return;
        droneInsideTrigger = true;
        Debug.Log($"FlyThroughGate: Drone entered '{gateName}'");
    }

    /// <summary>
    /// Called by GateTriggerDetector when drone exits the trigger zone.
    /// Entering and exiting = flew through.
    /// </summary>
    public void OnDroneExited()
    {
        if (isTriggered) return;
        if (!droneInsideTrigger) return;

        droneInsideTrigger = false;
        isTriggered = true;

        SetFrameColor(triggeredColor);
        // Hide the trigger plane once passed
        if (triggerRenderer != null)
            triggerRenderer.enabled = false;

        Debug.Log($"FlyThroughGate: Drone flew through '{gateName}'!");

        if (GameManager.Instance != null)
            GameManager.Instance.ReportGate(this);
    }

    public void ResetState()
    {
        isTriggered = false;
        droneInsideTrigger = false;
        SetFrameColor(frameColor);
        if (triggerRenderer != null)
        {
            triggerRenderer.enabled = true;
            triggerRenderer.material.color = triggerIdleColor;
        }
    }

    private void SetFrameColor(Color color)
    {
        if (frameRenderers == null) return;
        foreach (var rend in frameRenderers)
        {
            if (rend != null)
                rend.material.color = color;
        }
    }

    void OnDrawGizmos()
    {
        // Draw the opening outline
        Gizmos.color = isTriggered ? Color.green : Color.cyan;
        Vector3 center = transform.position + transform.up * (frameBorder + openingHeight / 2);
        Vector3 size = new Vector3(openingWidth, openingHeight, wallThickness * 2);

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = oldMatrix;

        #if UNITY_EDITOR
        string status = isTriggered ? "PASSED" : "waiting";
        UnityEditor.Handles.Label(center + Vector3.up * 0.15f, $"{gateName}\n{status}");
        #endif
    }
}

/// <summary>
/// Helper component on the trigger zone child object.
/// Forwards trigger events to the parent FlyThroughGate.
/// </summary>
public class GateTriggerDetector : MonoBehaviour
{
    [HideInInspector] public FlyThroughGate gate;

    void OnTriggerEnter(Collider other)
    {
        DroneController drone = other.GetComponentInParent<DroneController>();
        if (drone != null && gate != null)
            gate.OnDroneEntered();
    }

    void OnTriggerExit(Collider other)
    {
        DroneController drone = other.GetComponentInParent<DroneController>();
        if (drone != null && gate != null)
            gate.OnDroneExited();
    }
}
