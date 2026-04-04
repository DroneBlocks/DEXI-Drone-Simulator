using UnityEngine;

/// <summary>
/// Simple keyboard drone controller for testing game mechanics without PX4/ROS.
/// Press Tab to toggle between Keyboard and ROS mode.
///
/// Controls:
///   W/S        — Throttle up / down
///   A/D        — Yaw left / right
///   Up/Down    — Pitch forward / back
///   Left/Right — Roll left / right
/// </summary>
public class KeyboardDroneController : MonoBehaviour
{
    public enum ControlMode { ROS, Keyboard }

    [Header("Mode")]
    [SerializeField] private ControlMode mode = ControlMode.ROS;
    public ControlMode Mode => mode;

    [Header("Keyboard Control Settings")]
    [Tooltip("Throttle speed (m/s)")]
    public float throttleSpeed = 0.8f;

    [Tooltip("Pitch/roll movement speed (m/s)")]
    public float moveSpeed = 1.0f;

    [Tooltip("Yaw rotation speed (degrees/s)")]
    public float yawSpeed = 90f;

    [Tooltip("Max pitch/roll tilt angle (degrees)")]
    public float maxTilt = 15f;

    [Tooltip("How fast tilt returns to level (degrees/s)")]
    public float tiltReturnSpeed = 5f;

    [Tooltip("Minimum altitude (meters)")]
    public float minAltitude = 0.05f;

    // Components to disable/enable on toggle
    private DroneOdometrySubscriber odometrySub;
    private DroneController droneController;
    private Rigidbody rb;
    private DroneInputs droneInputs;

    // Keyboard mode state
    private float currentYaw;
    private float currentPitch;
    private float currentRoll;

    void Start()
    {
        odometrySub = GetComponent<DroneOdometrySubscriber>();
        droneController = GetComponent<DroneController>();
        rb = GetComponent<Rigidbody>();
        droneInputs = GetComponent<DroneInputs>();
        currentYaw = transform.eulerAngles.y;
    }

    void Update()
    {
        // Tab to toggle
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (mode == ControlMode.ROS)
                SwitchToKeyboard();
            else
                SwitchToROS();
        }

        if (mode == ControlMode.Keyboard)
            HandleKeyboardInput();
    }

    void SwitchToKeyboard()
    {
        mode = ControlMode.Keyboard;
        currentYaw = transform.eulerAngles.y;

        // Disable ROS-driven components
        if (odometrySub != null) odometrySub.enabled = false;
        if (droneController != null) droneController.enabled = false;
        if (droneInputs != null) droneInputs.enabled = false;

        // Make rigidbody kinematic so we control position directly
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log("KeyboardDroneController: Switched to KEYBOARD mode");
    }

    void SwitchToROS()
    {
        mode = ControlMode.ROS;

        // Re-enable ROS-driven components
        if (odometrySub != null) odometrySub.enabled = true;
        if (droneController != null) droneController.enabled = true;
        if (droneInputs != null) droneInputs.enabled = true;

        // Restore rigidbody
        if (rb != null)
            rb.isKinematic = false;

        Debug.Log("KeyboardDroneController: Switched to ROS mode");
    }

    void HandleKeyboardInput()
    {
        float dt = Time.deltaTime;

        // W/S — Throttle (altitude)
        float throttle = 0f;
        if (Input.GetKey(KeyCode.W)) throttle = 1f;
        if (Input.GetKey(KeyCode.S)) throttle = -1f;

        // A/D — Yaw
        if (Input.GetKey(KeyCode.A)) currentYaw -= yawSpeed * dt;
        if (Input.GetKey(KeyCode.D)) currentYaw += yawSpeed * dt;

        // Up/Down arrows — Pitch (forward/back movement + visual tilt)
        float pitchInput = 0f;
        if (Input.GetKey(KeyCode.UpArrow)) pitchInput = 1f;
        if (Input.GetKey(KeyCode.DownArrow)) pitchInput = -1f;

        // Left/Right arrows — Roll (strafe + visual tilt)
        float rollInput = 0f;
        if (Input.GetKey(KeyCode.LeftArrow)) rollInput = 1f;
        if (Input.GetKey(KeyCode.RightArrow)) rollInput = -1f;

        // Tilt toward input, return to level when released
        float targetPitch = pitchInput * maxTilt;
        float targetRoll = rollInput * maxTilt;
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, dt * tiltReturnSpeed);
        currentRoll = Mathf.Lerp(currentRoll, targetRoll, dt * tiltReturnSpeed);

        // Movement from pitch/roll (relative to yaw heading)
        Vector3 forward = Quaternion.Euler(0, currentYaw, 0) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0, currentYaw, 0) * Vector3.right;

        Vector3 move = Vector3.zero;
        move += forward * pitchInput * moveSpeed * dt;
        move -= right * rollInput * moveSpeed * dt;
        move += Vector3.up * throttle * throttleSpeed * dt;

        Vector3 newPos = transform.position + move;
        newPos.y = Mathf.Max(newPos.y, minAltitude);
        transform.position = newPos;

        // Apply rotation with tilt
        transform.rotation = Quaternion.Euler(currentPitch, currentYaw, currentRoll);
    }

    void OnGUI()
    {
        // Mode indicator in top-right
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperRight
        };

        string modeText;
        if (mode == ControlMode.Keyboard)
        {
            style.normal.textColor = Color.yellow;
            modeText = "KEYBOARD MODE [Tab]";
        }
        else
        {
            style.normal.textColor = new Color(0.5f, 1f, 0.5f);
            modeText = "ROS MODE [Tab]";
        }

        GUI.Label(new Rect(Screen.width - 260, 10, 250, 25), modeText, style);

        // Controls hint in keyboard mode
        if (mode == ControlMode.Keyboard)
        {
            GUIStyle smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            smallStyle.normal.textColor = new Color(1, 1, 1, 0.5f);
            float x = Screen.width - 260;
            GUI.Label(new Rect(x, 35, 250, 20), "WS: throttle  AD: yaw  Arrows: pitch/roll", smallStyle);
            GUI.Label(new Rect(x, 50, 250, 20), "1=RED  2=GREEN  3=BLUE (submit LED)", smallStyle);
        }
    }
}
