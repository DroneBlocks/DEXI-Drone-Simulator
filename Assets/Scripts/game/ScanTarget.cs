using UnityEngine;

/// <summary>
/// Component placed on each scannable target (AprilTag or YOLO image).
/// Tracks whether this target is the "real" one in its group.
/// Fake targets are blanked out (hidden). Each target has an expected LED color for validation.
/// </summary>
public class ScanTarget : MonoBehaviour
{
    public enum TargetType { AprilTag, YoloImage }

    [Header("Target Identity")]
    [Tooltip("Which group this target belongs to (e.g. 'apriltags' or 'yolo_vehicles')")]
    public string groupName = "default";

    [Tooltip("Display name for this target")]
    public string targetName = "Target";

    [Tooltip("AprilTag or YOLO image")]
    public TargetType targetType = TargetType.AprilTag;

    [Header("LED Validation")]
    [Tooltip("The expected LED color when this target is correctly identified")]
    public Color expectedLEDColor = Color.white;

    [Header("State (set by GameManager)")]
    [SerializeField] private bool isReal;
    [SerializeField] private bool isScanned;

    public bool IsReal => isReal;
    public bool IsScanned => isScanned;

    private Renderer targetRenderer;
    private Material originalMaterial;
    private Material blankMaterial;

    void Start()
    {
        targetRenderer = GetComponent<Renderer>();
        if (targetRenderer != null)
            originalMaterial = targetRenderer.material;

        if (GameManager.Instance != null)
            GameManager.Instance.RegisterTarget(this);
    }

    /// <summary>
    /// Set whether this target is the real one in its group.
    /// Fake targets get blanked out.
    /// </summary>
    public void SetReal(bool real)
    {
        isReal = real;

        if (targetRenderer != null)
        {
            if (real)
            {
                // Show the actual image/tag
                targetRenderer.material = originalMaterial;
            }
            else
            {
                // Blank it out — flat gray
                if (blankMaterial == null)
                {
                    blankMaterial = new Material(originalMaterial);
                    blankMaterial.mainTexture = null;
                    blankMaterial.color = new Color(0.3f, 0.3f, 0.3f);
                }
                targetRenderer.material = blankMaterial;
            }
        }
    }

    /// <summary>
    /// Mark this target as having been scanned by the drone.
    /// </summary>
    public void MarkScanned()
    {
        isScanned = true;
    }

    /// <summary>
    /// Reset to initial state for a new round.
    /// </summary>
    public void ResetState()
    {
        isScanned = false;
        isReal = false;

        // Restore original appearance before next randomization
        if (targetRenderer != null && originalMaterial != null)
            targetRenderer.material = originalMaterial;
    }

    void OnDrawGizmos()
    {
        if (isReal)
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.01f, new Vector3(0.18f, 0.01f, 0.18f));
        }

        #if UNITY_EDITOR
        string colorName = "";
        if (expectedLEDColor == Color.red) colorName = " (RED)";
        else if (expectedLEDColor == Color.green) colorName = " (GREEN)";
        else if (expectedLEDColor == Color.blue) colorName = " (BLUE)";

        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.05f,
            $"{targetName}{colorName}\n{(isReal ? "REAL" : "blank")}");
        #endif
    }

    void OnDestroy()
    {
        if (blankMaterial != null)
            Destroy(blankMaterial);
    }
}
