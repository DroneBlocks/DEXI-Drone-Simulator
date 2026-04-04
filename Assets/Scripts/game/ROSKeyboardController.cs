using UnityEngine;
using UnityEngine.InputSystem;
using Newtonsoft.Json.Linq;

/// <summary>
/// Sends keyboard commands to the drone via ROS, same as the HTML page used to.
/// This is the default controller. Press Tab to switch to free flight.
///
/// Controls:
///   T          — Arm + offboard takeoff
///   L          — Land
///   W/S        — Throttle up / down (via set_velocity_body)
///   A/D        — Yaw left / right
///   Up/Down    — Forward / back
///   Left/Right — Strafe left / right
/// </summary>
public class ROSKeyboardController : MonoBehaviour
{
    [Header("Velocity Settings")]
    public float xySpeed = 0.5f;
    public float zSpeed = 0.3f;
    public float yawRate = 30f;

    [Header("Velocity Loop")]
    public float velocityInterval = 0.1f; // 10Hz

    [Header("ROS Topics/Services")]
    public string offboardTopic = "/dexi/offboard_manager";
    public string offboardMsgType = "dexi_interfaces/msg/OffboardNavCommand";
    public string serviceEndpoint = "/dexi/execute_blockly_command";
    public string serviceType = "dexi_interfaces/srv/ExecuteBlocklyCommand";

    [Header("State")]
    [SerializeField] private bool inFlight;
    [SerializeField] private bool isArming;

    private float velocityTimer;
    private bool advertised;

    void Update()
    {
        var kbc = GetComponent<KeyboardDroneController>();
        if (kbc != null && kbc.Mode == KeyboardDroneController.ControlMode.FreeFlight)
            return;

        if (!ROSBridgeManager.Instance.IsConnected)
            return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (!advertised)
        {
            ROSBridgeManager.Instance.Advertise(
                ROSBridgeManager.Instance.ApplyNamespace(offboardTopic),
                offboardMsgType);
            advertised = true;
        }

        // Auto-detect if drone is already airborne (e.g. page refresh while flying)
        if (!inFlight && !isArming && PX4StateManager.Instance.IsArmed)
        {
            inFlight = true;
            Debug.Log("ROSKeyboard: Drone already armed — resuming velocity control");
        }

        if (kb.tKey.wasPressedThisFrame && !inFlight && !isArming)
        {
            ArmAndTakeoff();
            return;
        }

        if (kb.lKey.wasPressedThisFrame && inFlight)
        {
            Land();
            return;
        }

        // Send velocity commands when in flight OR when any movement key is held
        // This allows immediate control even if inFlight hasn't been set yet
        if (inFlight || AnyMovementKeyHeld(kb))
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

    void ArmAndTakeoff()
    {
        isArming = true;
        Debug.Log("ROSKeyboard: Arming...");

        string ns = ROSBridgeManager.Instance.ApplyNamespace(serviceEndpoint);

        ROSBridgeManager.Instance.CallService(ns, serviceType,
            new { command = "arm", parameter = 0.0, timeout = 10.0,
                  north = 0.0, east = 0.0, down = 0.0, yaw = 0.0,
                  index = 0, r = 0, g = 0, b = 0 },
            (response) =>
            {
                Debug.Log("ROSKeyboard: Armed — taking off...");
                ROSBridgeManager.Instance.CallService(ns, serviceType,
                    new { command = "offboard_takeoff", parameter = 2.0, timeout = 30.0,
                          north = 0.0, east = 0.0, down = 0.0, yaw = 0.0,
                          index = 0, r = 0, g = 0, b = 0 },
                    (response2) =>
                    {
                        inFlight = true;
                        isArming = false;
                        Debug.Log("ROSKeyboard: Airborne — WASD/arrows to fly, L to land");
                    });
            });
    }

    void Land()
    {
        inFlight = false;
        Debug.Log("ROSKeyboard: Landing...");

        PublishVelocity(0, 0, 0, 0);

        string ns = ROSBridgeManager.Instance.ApplyNamespace(serviceEndpoint);
        ROSBridgeManager.Instance.CallService(ns, serviceType,
            new { command = "land", parameter = 0.0, timeout = 30.0,
                  north = 0.0, east = 0.0, down = 0.0, yaw = 0.0,
                  index = 0, r = 0, g = 0, b = 0 },
            (response) =>
            {
                Debug.Log("ROSKeyboard: Landed");
            });
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

        PublishVelocity(vx, vy, vz, yaw);
    }

    void PublishVelocity(float vx, float vy, float vz, float yawRate)
    {
        string topic = ROSBridgeManager.Instance.ApplyNamespace(offboardTopic);

        _ = ROSBridgeManager.Instance.Publish(topic, offboardMsgType, new
        {
            command = "set_velocity_body",
            distance_or_degrees = 0.0,
            north = vx,
            east = vy,
            down = vz,
            yaw = yawRate
        });
    }

    void OnGUI()
    {
        var kbc = GetComponent<KeyboardDroneController>();
        if (kbc != null && kbc.Mode == KeyboardDroneController.ControlMode.FreeFlight)
            return;

        if (!ROSBridgeManager.Instance.IsConnected)
            return;

        GUIStyle small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        small.normal.textColor = new Color(0.5f, 1f, 0.5f, 0.7f);
        float x = Screen.width - 300;

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
    }
}
