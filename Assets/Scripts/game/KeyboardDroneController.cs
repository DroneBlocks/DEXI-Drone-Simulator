using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Toggles between ROS mode and free flight mode.
///
/// ROS mode (default): Unity renders drone from odometry. Keyboard control
/// is handled by ROSKeyboardController which sends commands via ROS.
///
/// Free flight mode (Tab): Disables odometry, gives direct keyboard control
/// for testing game mechanics without PX4/ROS.
///
/// Free flight controls:
///   W/S        — Throttle up / down
///   A/D        — Yaw left / right
///   Up/Down    — Pitch forward / back
///   Left/Right — Roll left / right
/// </summary>
public class KeyboardDroneController : MonoBehaviour
{
    public enum ControlMode { ROS, FreeFlight }

    [Header("Mode")]
    [SerializeField] private ControlMode mode = ControlMode.ROS;
    public ControlMode Mode => mode;

    [Header("Free Flight Settings")]
    public float throttleSpeed = 0.8f;
    public float moveSpeed = 1.0f;
    public float yawSpeed = 90f;
    public float maxTilt = 15f;
    public float tiltReturnSpeed = 5f;
    public float minAltitude = 0.05f;

    private DroneOdometrySubscriber odometrySub;
    private DroneController droneController;
    private Rigidbody rb;
    private DroneInputs droneInputs;

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
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.tabKey.wasPressedThisFrame)
        {
            if (mode == ControlMode.ROS)
                EnterFreeFlight();
            else
                EnterROSMode();
        }

        if (mode == ControlMode.FreeFlight)
        {
            HandleFlightInput(kb);
            HandleLEDInput(kb);
        }
    }

    void EnterFreeFlight()
    {
        mode = ControlMode.FreeFlight;
        currentYaw = transform.eulerAngles.y;

        if (odometrySub != null) odometrySub.enabled = false;
        if (droneController != null) droneController.enabled = false;
        if (droneInputs != null) droneInputs.enabled = false;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        PX4StateManager.Instance.ArmingState = PX4StateManager.ARMING_STATE_ARMED;

        if (GameManager.Instance != null && GameManager.Instance.State == GameManager.GameState.WaitingToStart)
            GameManager.Instance.StartGame();

        Debug.Log("FREE FLIGHT mode — WASD/arrows to fly");
    }

    void EnterROSMode()
    {
        mode = ControlMode.ROS;

        if (odometrySub != null) odometrySub.enabled = true;
        if (droneController != null) droneController.enabled = true;
        if (droneInputs != null) droneInputs.enabled = true;

        if (rb != null)
            rb.isKinematic = false;

        Debug.Log("ROS mode — drone controlled via ROS");
    }

    void HandleFlightInput(Keyboard kb)
    {
        float dt = Time.deltaTime;

        float throttle = 0f;
        if (kb.wKey.isPressed) throttle = 1f;
        if (kb.sKey.isPressed) throttle = -1f;

        if (kb.aKey.isPressed) currentYaw -= yawSpeed * dt;
        if (kb.dKey.isPressed) currentYaw += yawSpeed * dt;

        float pitchInput = 0f;
        if (kb.upArrowKey.isPressed) pitchInput = 1f;
        if (kb.downArrowKey.isPressed) pitchInput = -1f;

        float rollInput = 0f;
        if (kb.leftArrowKey.isPressed) rollInput = 1f;
        if (kb.rightArrowKey.isPressed) rollInput = -1f;

        currentPitch = Mathf.Lerp(currentPitch, pitchInput * maxTilt, dt * tiltReturnSpeed);
        currentRoll = Mathf.Lerp(currentRoll, rollInput * maxTilt, dt * tiltReturnSpeed);

        Vector3 forward = Quaternion.Euler(0, currentYaw, 0) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0, currentYaw, 0) * Vector3.right;

        Vector3 move = forward * pitchInput * moveSpeed * dt
                     - right * rollInput * moveSpeed * dt
                     + Vector3.up * throttle * throttleSpeed * dt;

        Vector3 newPos = transform.position + move;
        newPos.y = Mathf.Max(newPos.y, minAltitude);
        transform.position = newPos;
        transform.rotation = Quaternion.Euler(currentPitch, currentYaw, currentRoll);
    }

    void HandleLEDInput(Keyboard kb)
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameManager.GameState.Running)
            return;

        Color? color = null;
        string name = null;

        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) { color = Color.red; name = "RED"; }
        if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) { color = Color.green; name = "GREEN"; }
        if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) { color = Color.blue; name = "BLUE"; }

        if (color == null) return;

        var ledVis = FindFirstObjectByType<LEDRingVisualizer>();
        if (ledVis != null) ledVis.SetAllLEDs(color.Value);

        foreach (var target in GameManager.Instance.GetAllTargets())
        {
            if (!target.IsReal || target.IsScanned) continue;
            if (Mathf.Abs(color.Value.r - target.expectedLEDColor.r) < 0.1f &&
                Mathf.Abs(color.Value.g - target.expectedLEDColor.g) < 0.1f &&
                Mathf.Abs(color.Value.b - target.expectedLEDColor.b) < 0.1f)
            {
                GameManager.Instance.ReportScan(target);
                Debug.Log($"LED {name} matched {target.targetName}");
                return;
            }
        }
        Debug.Log($"LED {name} — no match");
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperRight
        };

        float x = Screen.width - 300;

        if (mode == ControlMode.FreeFlight)
        {
            style.normal.textColor = Color.yellow;
            GUI.Label(new Rect(x, 10, 290, 25), "FREE FLIGHT [Tab → ROS]", style);

            GUIStyle small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            small.normal.textColor = new Color(1, 1, 1, 0.5f);
            GUI.Label(new Rect(x, 30, 290, 20), "WS: throttle  AD: yaw  Arrows: move", small);
            GUI.Label(new Rect(x, 45, 290, 20), "1=RED  2=GREEN  3=BLUE", small);
        }
        else
        {
            style.normal.textColor = new Color(0.5f, 1f, 0.5f);
            GUI.Label(new Rect(x, 10, 290, 25), "ROS MODE [Tab → free flight]", style);
        }
    }
}
