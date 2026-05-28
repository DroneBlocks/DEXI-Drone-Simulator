using UnityEngine;
using UnityEngine.InputSystem;
using System.Runtime.InteropServices;

/// <summary>
/// Sends keyboard or controller commands to the drone via ROS.
///
/// Keyboard: T=takeoff, L=land, WASD=alt+yaw, Arrows=move, Space=free flight (editor-only)
/// Controllers: Selected via on-screen menu, read through browser Gamepad API (WebGL).
/// </summary>
public class ROSKeyboardController : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int GetGamepadCount();
    [DllImport("__Internal")]
    private static extern float GetGamepadAxis(int gamepadIndex, int axisIndex);
    [DllImport("__Internal")]
    private static extern int GetGamepadButton(int gamepadIndex, int buttonIndex);
    [DllImport("__Internal")]
    private static extern int GetGamepadAxisCount(int gamepadIndex);
    [DllImport("__Internal")]
    private static extern string GetGamepadName(int gamepadIndex);
#endif

    public enum ControllerPreset { Keyboard, Xbox, Radiomaster }

    [System.Serializable]
    public struct AxisMapping
    {
        public int roll, pitch, throttle, yaw;
        public bool invertPitch, invertRoll, invertYaw;
        public bool throttleCentersAtNegOne;
        public int buttonTakeoff, buttonLand;
    }

    static readonly AxisMapping XboxMapping = new AxisMapping
    {
        roll = 2, pitch = 3, throttle = 1, yaw = 0,
        invertPitch = true, invertRoll = false, invertYaw = false,
        throttleCentersAtNegOne = false,
        buttonTakeoff = 0, buttonLand = 1
    };

    static readonly AxisMapping RadiomasterMapping = new AxisMapping
    {
        roll = 0, pitch = 1, throttle = 3, yaw = 4,
        invertPitch = false, invertRoll = false, invertYaw = false,
        throttleCentersAtNegOne = true,
        buttonTakeoff = 0, buttonLand = 1
    };

    [Header("Velocity Settings")]
    public float xySpeed = 0.5f;
    public float zSpeed = 0.3f;
    public float yawRate = 30f;

    [Header("Controller")]
    public float stickDeadzone = 0.15f;

    [Header("Velocity Loop")]
    public float velocityInterval = 0.1f;

    [Header("ROS Topics/Services")]
    public string offboardTopic = "/dexi/offboard_manager";
    public string offboardMsgType = "dexi_interfaces/msg/OffboardNavCommand";
    public string serviceEndpoint = "/dexi/execute_blockly_command";
    public string serviceType = "dexi_interfaces/srv/ExecuteBlocklyCommand";

    [Header("State")]
    public bool inFlight;
    public bool isArming;
    public bool freeFlightMode;
    public bool resetOdometryOnTakeoff = true;

    [Header("Free Flight")]
    public float freeFlightSpeed = 3f;
    public float freeFlightYawSpeed = 90f;

    [Header("HUD")]
    [Tooltip("Draw the keyboard hint overlay. Disable for minimal teaching scenes.")]
    public bool showHud = false;

    private float velocityTimer;
    private bool advertised;
    private DroneOdometrySubscriber odometry;
    private Rigidbody droneRb;

    private ControllerPreset activePreset = ControllerPreset.Keyboard;
    private AxisMapping activeMapping;
    private bool showSelector = false;
    private int browserGamepadIndex = -1;

    void Start()
    {
        odometry = FindFirstObjectByType<DroneOdometrySubscriber>();
        var drone = FindFirstObjectByType<DroneController>();
        if (drone != null)
            droneRb = drone.GetComponent<Rigidbody>();

        if (odometry != null && resetOdometryOnTakeoff)
        {
            odometry.ResetToSpawn(GameManager.Instance.droneSpawnPosition);
        }
    }

    // --- Browser Gamepad API (WebGL) ---

    int FindBrowserGamepad()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        int count = GetGamepadCount();
        for (int i = 0; i < count; i++)
        {
            string name = GetGamepadName(i);
            if (!string.IsNullOrEmpty(name))
                return i;
        }
#endif
        return -1;
    }

    float ReadAxis(int axisIndex)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (browserGamepadIndex >= 0)
            return GetGamepadAxis(browserGamepadIndex, axisIndex);
#endif
        return 0f;
    }

    bool ReadButton(int buttonIndex)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (browserGamepadIndex >= 0)
            return GetGamepadButton(browserGamepadIndex, buttonIndex) == 1;
#endif
        return false;
    }

    float ApplyDeadzone(float value)
    {
        return Mathf.Abs(value) > stickDeadzone ? value : 0f;
    }

    void ReadControllerAxes(out float throttle, out float yaw, out float pitch, out float roll)
    {
        roll = ApplyDeadzone(ReadAxis(activeMapping.roll));
        pitch = ApplyDeadzone(ReadAxis(activeMapping.pitch));
        yaw = ApplyDeadzone(ReadAxis(activeMapping.yaw));

        if (activeMapping.invertPitch) pitch = -pitch;
        if (activeMapping.invertRoll) roll = -roll;
        if (activeMapping.invertYaw) yaw = -yaw;

        float rawThrottle = ReadAxis(activeMapping.throttle);
        if (activeMapping.throttleCentersAtNegOne)
            throttle = ApplyDeadzone((rawThrottle + 1f) / 2f * 2f - 1f);
        else
            throttle = ApplyDeadzone(rawThrottle);
    }

    bool AnyControllerInput()
    {
        if (activePreset == ControllerPreset.Keyboard) return false;
        if (browserGamepadIndex < 0) return false;
        ReadControllerAxes(out float throttle, out float yaw, out float pitch, out float roll);
        return Mathf.Abs(roll) > stickDeadzone || Mathf.Abs(pitch) > stickDeadzone ||
               Mathf.Abs(yaw) > stickDeadzone || Mathf.Abs(throttle) > stickDeadzone;
    }

    // --- Core ---

    void Update()
    {
        if (GameManager.Instance.State != GameManager.GameState.Running) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        // Tab toggles controller selector
        if (kb.tabKey.wasPressedThisFrame)
            showSelector = !showSelector;

        // Only look for browser gamepad when a controller preset is active
        if (activePreset != ControllerPreset.Keyboard)
            browserGamepadIndex = FindBrowserGamepad();
        else
            browserGamepadIndex = -1;

        // Space toggles free flight (editor-only; bypasses PX4 physics, not allowed in student/AVR builds)
#if UNITY_EDITOR
        if (kb.spaceKey.wasPressedThisFrame)
        {
            freeFlightMode = !freeFlightMode;
            if (odometry != null)
                odometry.FreeFlightOverride = freeFlightMode;
            if (droneRb != null)
                droneRb.isKinematic = freeFlightMode;
        }
#endif

        if (freeFlightMode && droneRb != null)
        {
            HandleFreeFlight(kb);
            return;
        }

        if (!ROSBridgeManager.Instance.IsConnected)
            return;

        if (!advertised)
        {
            ROSBridgeManager.Instance.Advertise(
                ROSBridgeManager.Instance.ApplyNamespace(offboardTopic), offboardMsgType);
            advertised = true;
        }

        if (!inFlight && !isArming && PX4StateManager.Instance.IsArmed)
        {
            inFlight = true;
        }

        if (!PX4StateManager.Instance.IsArmed)
        {
            inFlight = false;
        }

        bool takeoffPressed = kb.tKey.wasPressedThisFrame || ReadButton(activeMapping.buttonTakeoff);
        bool landPressed = kb.lKey.wasPressedThisFrame || ReadButton(activeMapping.buttonLand);

        if (takeoffPressed && !inFlight && !isArming)
        {
            ArmAndTakeoff();
            return;
        }

        if (landPressed && inFlight)
        {
            Land();
            return;
        }

        if (inFlight || AnyMovementKeyHeld(kb) || AnyControllerInput())
        {
            velocityTimer += Time.deltaTime;
            if (velocityTimer >= velocityInterval)
            {
                velocityTimer = 0f;
                SendVelocity(kb);
            }
        }
    }

    bool AnyMovementKeyHeld(Keyboard kb)
    {
        return kb.wKey.isPressed || kb.sKey.isPressed ||
               kb.aKey.isPressed || kb.dKey.isPressed ||
               kb.upArrowKey.isPressed || kb.downArrowKey.isPressed ||
               kb.leftArrowKey.isPressed || kb.rightArrowKey.isPressed;
    }

    void HandleFreeFlight(Keyboard kb)
    {
        float vx = 0, vy = 0, vz = 0, yaw = 0;

        if (kb.upArrowKey.isPressed)    vx += 1;
        if (kb.downArrowKey.isPressed)  vx -= 1;
        if (kb.rightArrowKey.isPressed) vy += 1;
        if (kb.leftArrowKey.isPressed)  vy -= 1;
        if (kb.wKey.isPressed)          vz += 1;
        if (kb.sKey.isPressed)          vz -= 1;
        if (kb.dKey.isPressed)          yaw += 1;
        if (kb.aKey.isPressed)          yaw -= 1;

        if (activePreset != ControllerPreset.Keyboard && browserGamepadIndex >= 0)
        {
            ReadControllerAxes(out float cT, out float cY, out float cP, out float cR);
            vz += cT; yaw += cY; vx += cP; vy += cR;
        }

        Transform t = droneRb.transform;
        t.position += (t.forward * vx + t.right * vy + Vector3.up * vz) * freeFlightSpeed * Time.deltaTime;
        if (Mathf.Abs(yaw) > 0.01f)
            t.Rotate(Vector3.up, yaw * freeFlightYawSpeed * Time.deltaTime);
    }

    void SendVelocity(Keyboard kb)
    {
        float vx = 0, vy = 0, vz = 0, yaw = 0;

        if (kb.upArrowKey.isPressed)    vx += xySpeed;
        if (kb.downArrowKey.isPressed)  vx -= xySpeed;
        if (kb.rightArrowKey.isPressed) vy += xySpeed;
        if (kb.leftArrowKey.isPressed)  vy -= xySpeed;
        if (kb.wKey.isPressed)          vz -= zSpeed;
        if (kb.sKey.isPressed)          vz += zSpeed;
        if (kb.dKey.isPressed)          yaw += yawRate;
        if (kb.aKey.isPressed)          yaw -= yawRate;

        if (activePreset != ControllerPreset.Keyboard && browserGamepadIndex >= 0)
        {
            ReadControllerAxes(out float cT, out float cY, out float cP, out float cR);
            vz -= cT * zSpeed;
            yaw += cY * yawRate;
            vx += cP * xySpeed;
            vy += cR * xySpeed;
        }

        PublishVelocity(vx, vy, vz, yaw);
    }

    void ArmAndTakeoff()
    {
        isArming = true;

        string ns = ROSBridgeManager.Instance.ApplyNamespace(serviceEndpoint);

        ROSBridgeManager.Instance.CallService(ns, serviceType,
            new { command = "arm", parameter = 0.0, timeout = 10.0,
                  north = 0.0, east = 0.0, down = 0.0, yaw = 0.0,
                  index = 0, r = 0, g = 0, b = 0 },
            (response) =>
            {
                ROSBridgeManager.Instance.CallService(ns, serviceType,
                    new { command = "offboard_takeoff", parameter = 2.0, timeout = 30.0,
                          north = 0.0, east = 0.0, down = 0.0, yaw = 0.0,
                          index = 0, r = 0, g = 0, b = 0 },
                    (response2) =>
                    {
                        inFlight = true;
                        isArming = false;
                    });
            });
    }

    void Land()
    {
        inFlight = false;
        PublishVelocity(0, 0, 0, 0);

        string ns = ROSBridgeManager.Instance.ApplyNamespace(serviceEndpoint);
        ROSBridgeManager.Instance.CallService(ns, serviceType,
            new { command = "land", parameter = 0.0, timeout = 30.0,
                  north = 0.0, east = 0.0, down = 0.0, yaw = 0.0,
                  index = 0, r = 0, g = 0, b = 0 },
            null);
    }

    void PublishVelocity(float vx, float vy, float vz, float yawRate)
    {
        string topic = ROSBridgeManager.Instance.ApplyNamespace(offboardTopic);
        _ = ROSBridgeManager.Instance.Publish(topic, offboardMsgType, new
        {
            command = "set_velocity_body",
            distance_or_degrees = 0.0,
            north = vx, east = vy, down = vz, yaw = yawRate
        });
    }

    // --- UI ---

    void SelectPreset(ControllerPreset preset)
    {
        activePreset = preset;
        switch (preset)
        {
            case ControllerPreset.Xbox:
                activeMapping = XboxMapping;
                break;
            case ControllerPreset.Radiomaster:
                activeMapping = RadiomasterMapping;
                break;
            default:
                activeMapping = default;
                break;
        }
        showSelector = false;
    }

    void OnGUI()
    {
        // Controller selector (Tab to toggle)
        if (showSelector)
        {
            DrawSelector();
            return;
        }

        if (!ROSBridgeManager.Instance.IsConnected)
            return;

        GUIStyle small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        small.normal.textColor = new Color(0.5f, 1f, 0.5f, 0.7f);
        float x = Screen.width - 300;

#if UNITY_EDITOR
        if (freeFlightMode)
        {
            GUIStyle freeStyle = new GUIStyle(small) { fontSize = 14 };
            freeStyle.normal.textColor = new Color(1f, 0.6f, 0.2f, 0.9f);
            GUI.Label(new Rect(x, 10, 290, 20), "FREE FLIGHT MODE", freeStyle);
            GUI.Label(new Rect(x, 28, 290, 20), "Space: return to ROS", small);
            return;
        }
#endif

        string presetName = activePreset == ControllerPreset.Keyboard ? "Keyboard" :
                            activePreset == ControllerPreset.Xbox ? "Xbox" : "Radiomaster";

        if (isArming)
        {
            GUI.Label(new Rect(x, 30, 290, 20), "ARMING / TAKING OFF...", small);
        }
        else if (inFlight)
        {
            GUI.Label(new Rect(x, 30, 290, 20), "WS: alt  AD: yaw  Arrows: move  L: land", small);
        }
        else
        {
            GUI.Label(new Rect(x, 30, 290, 20), "T: arm + takeoff", small);
        }

        small.normal.textColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        GUI.Label(new Rect(x, inFlight ? 48 : 48, 290, 20), $"[Tab] Controller: {presetName}", small);
    }

    void DrawSelector()
    {
        float w = 320, h = 200;
        float sx = (Screen.width - w) / 2;
        float sy = (Screen.height - h) / 2;

        // Background
        Texture2D bg = new Texture2D(1, 1);
        bg.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.15f, 0.95f));
        bg.Apply();
        GUI.DrawTexture(new Rect(sx, sy, w, h), bg);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(sx, sy + 10, w, 30), "Select Controller", titleStyle);

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 16 };
        float btnW = 260, btnH = 36;
        float bx = sx + (w - btnW) / 2;
        float by = sy + 50;

        Color savedColor = GUI.backgroundColor;

        GUI.backgroundColor = activePreset == ControllerPreset.Keyboard ? Color.green : Color.white;
        if (GUI.Button(new Rect(bx, by, btnW, btnH), "Keyboard", btnStyle))
            SelectPreset(ControllerPreset.Keyboard);

        by += 42;
        GUI.backgroundColor = activePreset == ControllerPreset.Xbox ? Color.green : Color.white;
        if (GUI.Button(new Rect(bx, by, btnW, btnH), "Xbox / PlayStation", btnStyle))
            SelectPreset(ControllerPreset.Xbox);

        by += 42;
        GUI.backgroundColor = activePreset == ControllerPreset.Radiomaster ? Color.green : Color.white;
        if (GUI.Button(new Rect(bx, by, btnW, btnH), "RadioMaster / ELRS", btnStyle))
            SelectPreset(ControllerPreset.Radiomaster);

        GUI.backgroundColor = savedColor;

        GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11, alignment = TextAnchor.MiddleCenter
        };
        hintStyle.normal.textColor = new Color(1, 1, 1, 0.4f);
        GUI.Label(new Rect(sx, sy + h - 25, w, 20), "[Tab] to close", hintStyle);
    }
}
