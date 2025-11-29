using UnityEngine;

public class ThrustController : MonoBehaviour
{
    [Header("Motor References")]
    [SerializeField] private Transform motorM1; // Front Right
    [SerializeField] private Transform motorM2; // Back Left
    [SerializeField] private Transform motorM3; // Front Left
    [SerializeField] private Transform motorM4; // Back Right

    [Header("Flight Settings")]
    [SerializeField] private float mass = 0.25f;           // Drone mass in kg
    [SerializeField] private float hoverThrust = 2.45f;    // Base thrust needed to hover
    [SerializeField] private float maxTiltAngle = 15f;     // Maximum tilt angle in degrees
    [SerializeField] private float yawSpeed = 45f;         // Degrees per second

    [Header("Attitude Control")]
    [SerializeField] private float tiltSpeed = 90f;         // Keep tilt speed the same for controlled input
    [SerializeField] private float levelingSpeed = 1440f;   // Extreme speed for instant leveling
    [SerializeField] private float tiltSmoothness = 0.01f;  // Almost no smoothing

    [Header("Altitude Control")]
    [SerializeField] private float minHeight = 0.5f;       // Minimum allowed height
    [SerializeField] private float maxHeight = 10f;        // Maximum allowed height
    [SerializeField] private float heightChangeSpeed = 2f;  // Meters per second
    
    [Header("Altitude PID")]
    [SerializeField] private float heightKp = 2.0f;        // Height proportional gain
    [SerializeField] private float heightKi = 0.5f;        // Height integral gain
    [SerializeField] private float heightKd = 1.0f;        // Height derivative gain

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private Rigidbody rb;
    private bool isInitialized;
    private float targetHeight;
    private float heightIntegral;
    private float lastHeightError;
    private float currentYaw;
    private Vector3 lastPosition;
    private Vector3 targetRotation;
    private Vector3 smoothedRotation;

    private void OnEnable()
    {
        ResetDrone();
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (!rb)
        {
            Debug.LogError("Rigidbody required!");
            enabled = false;
            return;
        }

        // Configure rigidbody
        rb.mass = mass;
        rb.useGravity = true;
        rb.linearDamping = 1.0f;
        rb.angularDamping = 2.0f;
        rb.maxAngularVelocity = 1.0f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        ResetDrone();
    }

    private void ResetDrone()
    {
        // Reset position and rotation
        transform.rotation = Quaternion.identity;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Initialize control variables
        targetHeight = transform.position.y;
        heightIntegral = 0f;
        lastHeightError = 0f;
        currentYaw = 0f;
        lastPosition = transform.position;
        targetRotation = Vector3.zero;
        smoothedRotation = Vector3.zero;
        
        // Enable controls after a short delay
        isInitialized = false;
        CancelInvoke();
        Invoke(nameof(EnableControl), 0.5f);
    }

    private void EnableControl()
    {
        isInitialized = true;
        Debug.Log("Controls enabled - Use W/S for height, A/D for yaw");
    }

    private void Update()
    {
        if (!isInitialized) return;

        // Update target height based on input
        float heightInput = 0f;
        if (Input.GetKey(KeyCode.W)) heightInput = 1f;
        if (Input.GetKey(KeyCode.S)) heightInput = -1f;
        
        targetHeight += heightInput * heightChangeSpeed * Time.deltaTime;
        targetHeight = Mathf.Clamp(targetHeight, minHeight, maxHeight);

        // Get attitude inputs
        float pitchInput = 0f;
        float rollInput = 0f;
        
        // Update pitch (inverted for intuitive control)
        if (Input.GetKey(KeyCode.UpArrow)) pitchInput = 1f;
        if (Input.GetKey(KeyCode.DownArrow)) pitchInput = -1f;
        
        // Update roll
        if (Input.GetKey(KeyCode.LeftArrow)) rollInput = -1f;
        if (Input.GetKey(KeyCode.RightArrow)) rollInput = 1f;

        // Update yaw
        float yawInput = 0f;
        if (Input.GetKey(KeyCode.A)) yawInput = -1f;
        if (Input.GetKey(KeyCode.D)) yawInput = 1f;
        
        // Update target rotations with faster return to zero
        if (pitchInput != 0f)
        {
            targetRotation.x = Mathf.MoveTowards(targetRotation.x, pitchInput * maxTiltAngle, tiltSpeed * Time.deltaTime);
        }
        else
        {
            // Snap back to zero almost instantly when no input
            targetRotation.x = Mathf.MoveTowards(targetRotation.x, 0f, levelingSpeed * Time.deltaTime);
            if (Mathf.Abs(targetRotation.x) < 0.1f) targetRotation.x = 0f;
        }

        if (rollInput != 0f)
        {
            targetRotation.z = Mathf.MoveTowards(targetRotation.z, rollInput * maxTiltAngle, tiltSpeed * Time.deltaTime);
        }
        else
        {
            // Snap back to zero almost instantly when no input
            targetRotation.z = Mathf.MoveTowards(targetRotation.z, 0f, levelingSpeed * Time.deltaTime);
            if (Mathf.Abs(targetRotation.z) < 0.1f) targetRotation.z = 0f;
        }

        currentYaw += yawInput * yawSpeed * Time.deltaTime;

        // Use much faster smoothing when returning to level
        float smoothingFactor = (pitchInput == 0f && rollInput == 0f) ? 0.5f : tiltSmoothness;
        smoothedRotation = Vector3.Lerp(smoothedRotation, targetRotation, Time.deltaTime / smoothingFactor);
        
        // Snap to exactly zero when very close
        if (Mathf.Abs(targetRotation.x) < 0.1f && Mathf.Abs(smoothedRotation.x) < 0.1f) smoothedRotation.x = 0f;
        if (Mathf.Abs(targetRotation.z) < 0.1f && Mathf.Abs(smoothedRotation.z) < 0.1f) smoothedRotation.z = 0f;
    }

    private void FixedUpdate()
    {
        if (!isInitialized || !rb) return;

        // Calculate vertical velocity
        float verticalVelocity = (transform.position.y - lastPosition.y) / Time.fixedDeltaTime;
        lastPosition = transform.position;

        // PID for height control
        float heightError = targetHeight - transform.position.y;
        heightIntegral += heightError * Time.fixedDeltaTime;
        heightIntegral = Mathf.Clamp(heightIntegral, -20f, 20f); // Anti-windup
        float heightDerivative = (heightError - lastHeightError) / Time.fixedDeltaTime;
        lastHeightError = heightError;

        // Calculate thrust adjustment from PID
        float thrustAdjustment = (heightError * heightKp) + 
                                (heightIntegral * heightKi) + 
                                (heightDerivative * heightKd);

        // Calculate total thrust
        float totalThrust = hoverThrust + thrustAdjustment;
        float thrustPerMotor = Mathf.Max(0, totalThrust / 4f); // Prevent negative thrust

        // Calculate motor thrust adjustments based on attitude
        float pitchAdjust = smoothedRotation.x / maxTiltAngle; // -1 to 1
        float rollAdjust = smoothedRotation.z / maxTiltAngle;  // -1 to 1

        // Apply thrust with attitude adjustments
        float m1Thrust = thrustPerMotor * (1 - pitchAdjust - rollAdjust); // Front Right
        float m2Thrust = thrustPerMotor * (1 + pitchAdjust + rollAdjust); // Back Left
        float m3Thrust = thrustPerMotor * (1 - pitchAdjust + rollAdjust); // Front Left
        float m4Thrust = thrustPerMotor * (1 + pitchAdjust - rollAdjust); // Back Right

        // Clamp all thrusts to valid range
        m1Thrust = Mathf.Max(0, m1Thrust);
        m2Thrust = Mathf.Max(0, m2Thrust);
        m3Thrust = Mathf.Max(0, m3Thrust);
        m4Thrust = Mathf.Max(0, m4Thrust);

        // Apply motor forces
        rb.AddForceAtPosition(motorM1.up * m1Thrust, motorM1.position, ForceMode.Force);
        rb.AddForceAtPosition(motorM2.up * m2Thrust, motorM2.position, ForceMode.Force);
        rb.AddForceAtPosition(motorM3.up * m3Thrust, motorM3.position, ForceMode.Force);
        rb.AddForceAtPosition(motorM4.up * m4Thrust, motorM4.position, ForceMode.Force);

        // Apply rotation
        Quaternion targetRot = Quaternion.Euler(smoothedRotation.x, currentYaw, smoothedRotation.z);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, Time.fixedDeltaTime * 2f));
    }

    private void OnGUI()
    {
        if (!showDebugInfo) return;

        int yPos = 10;
        int leftMargin = 10;
        int width = 400;

        if (!isInitialized)
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(leftMargin, yPos, width, 20), "Initializing...");
            return;
        }

        GUI.color = Color.white;
        GUI.Label(new Rect(leftMargin, yPos, width, 20), "=== FLIGHT DATA ==="); yPos += 25;

        // Height Control
        GUI.Label(new Rect(leftMargin, yPos, width, 20), 
            $"Current Height: {transform.position.y:F2}m"); yPos += 20;
        GUI.Label(new Rect(leftMargin, yPos, width, 20), 
            $"Target Height: {targetHeight:F2}m"); yPos += 20;
        GUI.Label(new Rect(leftMargin, yPos, width, 20), 
            $"Height Error: {(targetHeight - transform.position.y):F2}m"); yPos += 25;

        // Attitude Data
        GUI.Label(new Rect(leftMargin, yPos, width, 20), 
            $"Pitch: {smoothedRotation.x:F1}° (Target: {targetRotation.x:F1}°)"); yPos += 20;
        GUI.Label(new Rect(leftMargin, yPos, width, 20), 
            $"Roll: {smoothedRotation.z:F1}° (Target: {targetRotation.z:F1}°)"); yPos += 20;
        GUI.Label(new Rect(leftMargin, yPos, width, 20), 
            $"Yaw: {currentYaw:F1}°"); yPos += 25;

        // Draw height range bar
        float barWidth = 200;
        float barHeight = 20;
        GUI.Label(new Rect(leftMargin, yPos, width, 20), "Height Range:"); yPos += 20;
        
        // Background bar
        GUI.Box(new Rect(leftMargin, yPos, barWidth, barHeight), "");
        
        // Current height indicator
        float heightPercent = Mathf.InverseLerp(minHeight, maxHeight, transform.position.y);
        GUI.color = Color.green;
        GUI.Box(new Rect(leftMargin, yPos, barWidth * heightPercent, barHeight), "");
        
        // Target height marker
        float targetPercent = Mathf.InverseLerp(minHeight, maxHeight, targetHeight);
        GUI.color = Color.yellow;
        GUI.Box(new Rect(leftMargin + (barWidth * targetPercent) - 2, yPos - 5, 4, barHeight + 10), "");
    }
} 