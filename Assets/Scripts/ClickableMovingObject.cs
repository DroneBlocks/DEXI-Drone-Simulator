using UnityEngine;

/// <summary>
/// Makes a GameObject move forward slowly when clicked
/// Attach this to any GameObject you want to be clickable and moveable
/// </summary>
public class ClickableMovingObject : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField]
    [Tooltip("Speed of movement in meters per second")]
    private float moveSpeed = 0.5f;

    [SerializeField]
    [Tooltip("Direction to move in local space (default is forward)")]
    private Vector3 moveDirection = Vector3.forward;

    [SerializeField]
    [Tooltip("Normalize the move direction automatically")]
    private bool normalizeMoveDirection = true;

    [Header("Click Behavior")]
    [SerializeField]
    [Tooltip("Toggle movement on/off with each click")]
    private bool toggleOnClick = true;

    [SerializeField]
    [Tooltip("Stop after moving this distance (0 = infinite)")]
    private float maxDistance = 0f;

    [Header("Visual Feedback")]
    [SerializeField]
    [Tooltip("Show debug info when clicked")]
    private bool showDebugInfo = true;

    [SerializeField]
    [Tooltip("Color to highlight when moving (optional)")]
    private Color movingHighlightColor = new Color(0.5f, 1f, 0.5f, 1f);

    [SerializeField]
    [Tooltip("Apply highlight color when moving")]
    private bool useHighlight = false;

    private bool isMoving = false;
    private Vector3 startPosition;
    private float distanceMoved = 0f;
    private Renderer objectRenderer;
    private Color originalColor;
    private Material highlightMaterial;

    void Start()
    {
        startPosition = transform.position;

        // Get renderer for highlighting
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null && useHighlight)
        {
            // Store original color
            originalColor = objectRenderer.material.color;
            // Create instance of material to avoid modifying shared material
            highlightMaterial = new Material(objectRenderer.material);
        }

        // Normalize direction if enabled
        if (normalizeMoveDirection && moveDirection != Vector3.zero)
        {
            moveDirection = moveDirection.normalized;
        }
    }

    void Update()
    {
        // Check for mouse click
        if (Input.GetMouseButtonDown(0))
        {
            // Cast ray from camera through mouse position
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                // Check if we clicked this object
                if (hit.collider.gameObject == gameObject)
                {
                    OnClicked();
                }
            }
        }

        // Move if enabled
        if (isMoving)
        {
            MoveObject();
        }
    }

    void OnClicked()
    {
        if (toggleOnClick)
        {
            // Toggle movement
            isMoving = !isMoving;

            if (isMoving)
            {
                // Reset tracking when starting
                startPosition = transform.position;
                distanceMoved = 0f;

                if (showDebugInfo)
                    Debug.Log($"[{gameObject.name}] Started moving");

                // Apply highlight
                if (useHighlight && objectRenderer != null)
                {
                    highlightMaterial.color = movingHighlightColor;
                    objectRenderer.material = highlightMaterial;
                }
            }
            else
            {
                if (showDebugInfo)
                    Debug.Log($"[{gameObject.name}] Stopped moving");

                // Remove highlight
                if (useHighlight && objectRenderer != null)
                {
                    objectRenderer.material.color = originalColor;
                }
            }
        }
        else
        {
            // Start moving (doesn't toggle off)
            if (!isMoving)
            {
                isMoving = true;
                startPosition = transform.position;
                distanceMoved = 0f;

                if (showDebugInfo)
                    Debug.Log($"[{gameObject.name}] Started moving");

                // Apply highlight
                if (useHighlight && objectRenderer != null)
                {
                    highlightMaterial.color = movingHighlightColor;
                    objectRenderer.material = highlightMaterial;
                }
            }
        }
    }

    void MoveObject()
    {
        // Calculate movement in local space
        Vector3 movement = transform.TransformDirection(moveDirection) * moveSpeed * Time.deltaTime;
        transform.position += movement;

        // Track distance
        distanceMoved += movement.magnitude;

        // Check if we've reached max distance
        if (maxDistance > 0 && distanceMoved >= maxDistance)
        {
            isMoving = false;

            if (showDebugInfo)
                Debug.Log($"[{gameObject.name}] Reached max distance ({maxDistance}m)");

            // Remove highlight
            if (useHighlight && objectRenderer != null)
            {
                objectRenderer.material.color = originalColor;
            }
        }
    }

    // Public methods to control movement from other scripts
    public void StartMoving()
    {
        if (!isMoving)
        {
            isMoving = true;
            startPosition = transform.position;
            distanceMoved = 0f;
        }
    }

    public void StopMoving()
    {
        isMoving = false;

        if (useHighlight && objectRenderer != null)
        {
            objectRenderer.material.color = originalColor;
        }
    }

    public void ToggleMoving()
    {
        if (isMoving)
            StopMoving();
        else
            StartMoving();
    }

    public bool IsMoving => isMoving;

    void OnDestroy()
    {
        // Clean up highlight material
        if (highlightMaterial != null)
        {
            Destroy(highlightMaterial);
        }
    }
}
