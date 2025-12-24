using UnityEngine;

public class PictureInPictureCamera : MonoBehaviour
{
    [Header("Camera Reference")]
    [SerializeField]
    [Tooltip("The camera to use for picture-in-picture view (typically downward-facing camera)")]
    private Camera pipCamera;

    [Header("PiP Window Settings")]
    [SerializeField]
    [Tooltip("Width of PiP window as percentage of screen (0-1)")]
    [Range(0.1f, 0.5f)]
    private float windowWidth = 0.25f;

    [SerializeField]
    [Tooltip("Height of PiP window as percentage of screen (0-1)")]
    [Range(0.1f, 0.5f)]
    private float windowHeight = 0.25f;

    [SerializeField]
    [Tooltip("Margin from right edge of screen (in percentage)")]
    [Range(0f, 0.1f)]
    private float marginRight = 0.02f;

    [SerializeField]
    [Tooltip("Margin from bottom edge of screen (in percentage)")]
    [Range(0f, 0.1f)]
    private float marginBottom = 0.02f;

    [SerializeField]
    [Tooltip("Enable/disable the PiP window")]
    private bool enablePiP = true;

    [SerializeField]
    [Tooltip("Key to toggle PiP on/off")]
    private KeyCode toggleKey = KeyCode.P;

    [Header("Border Settings")]
    [SerializeField]
    [Tooltip("Show border around PiP window")]
    private bool showBorder = true;

    [SerializeField]
    [Tooltip("Border color")]
    private Color borderColor = Color.white;

    [SerializeField]
    [Tooltip("Border thickness in pixels")]
    private float borderThickness = 2f;

    private Rect viewportRect;
    private Rect borderRect;

    void Start()
    {
        if (pipCamera == null)
        {
            Debug.LogWarning("[PictureInPictureCamera] No camera assigned. Looking for ROSCameraPublisher...");

            // Try to find the camera from ROSCameraPublisher
            ROSCameraPublisher rosCamPub = FindObjectOfType<ROSCameraPublisher>();
            if (rosCamPub != null)
            {
                pipCamera = rosCamPub.GetComponent<Camera>();
                if (pipCamera == null)
                {
                    // Check if there's a Camera component in the same GameObject or children
                    pipCamera = rosCamPub.GetComponentInChildren<Camera>();
                }
            }

            if (pipCamera == null)
            {
                Debug.LogError("[PictureInPictureCamera] Could not find a camera for PiP view!");
                enabled = false;
                return;
            }
        }

        UpdateViewport();
    }

    void Update()
    {
        // Toggle PiP with key
        if (Input.GetKeyDown(toggleKey))
        {
            enablePiP = !enablePiP;
            UpdateViewport();
        }
    }

    void UpdateViewport()
    {
        if (pipCamera == null) return;

        if (enablePiP)
        {
            // Calculate viewport rect (bottom-right corner)
            float x = 1f - windowWidth - marginRight;
            float y = marginBottom;

            viewportRect = new Rect(x, y, windowWidth, windowHeight);
            pipCamera.rect = viewportRect;

            // Make sure PiP camera renders after main camera
            pipCamera.depth = 10; // Higher depth = renders on top

            // Enable the camera
            pipCamera.enabled = true;

            // Calculate border rect for GUI drawing (in screen coordinates)
            borderRect = new Rect(
                x * Screen.width,
                y * Screen.height,
                windowWidth * Screen.width,
                windowHeight * Screen.height
            );
        }
        else
        {
            // Disable PiP camera when not in use
            pipCamera.enabled = false;
        }
    }

    void OnGUI()
    {
        if (!enablePiP || !showBorder || pipCamera == null) return;

        // Update border rect in case screen size changed
        // Note: GUI coordinates have Y=0 at TOP, viewport has Y=0 at BOTTOM
        // So we need to flip the Y coordinate
        borderRect = new Rect(
            viewportRect.x * Screen.width,
            (1f - viewportRect.y - viewportRect.height) * Screen.height,
            viewportRect.width * Screen.width,
            viewportRect.height * Screen.height
        );

        // Draw border
        DrawBorder(borderRect, borderColor, borderThickness);
    }

    void DrawBorder(Rect rect, Color color, float thickness)
    {
        // Create a 1x1 white texture if we don't have one
        Texture2D texture = Texture2D.whiteTexture;
        GUI.color = color;

        // Top
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), texture);
        // Bottom
        GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), texture);
        // Left
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), texture);
        // Right
        GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), texture);

        GUI.color = Color.white;
    }

    // Public methods to control PiP from other scripts
    public void SetEnabled(bool enabled)
    {
        enablePiP = enabled;
        UpdateViewport();
    }

    public void SetSize(float width, float height)
    {
        windowWidth = Mathf.Clamp(width, 0.1f, 0.5f);
        windowHeight = Mathf.Clamp(height, 0.1f, 0.5f);
        UpdateViewport();
    }

    public void SetPosition(float marginRightPercent, float marginBottomPercent)
    {
        marginRight = Mathf.Clamp(marginRightPercent, 0f, 0.1f);
        marginBottom = Mathf.Clamp(marginBottomPercent, 0f, 0.1f);
        UpdateViewport();
    }
}
