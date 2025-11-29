using UnityEngine;

/// <summary>
/// Singleton manager for PX4 vehicle state information.
/// Provides centralized access to arming status and other vehicle state data.
/// </summary>
public class PX4StateManager : MonoBehaviour
{
    private static PX4StateManager _instance;

    public static PX4StateManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // Try to find existing instance
                _instance = FindFirstObjectByType<PX4StateManager>();

                // Create new instance if none exists
                if (_instance == null)
                {
                    GameObject go = new GameObject("PX4StateManager");
                    _instance = go.AddComponent<PX4StateManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    // Arming state constants (from px4_msgs/msg/VehicleStatus)
    public const byte ARMING_STATE_DISARMED = 1;
    public const byte ARMING_STATE_ARMED = 2;

    private byte _armingState = ARMING_STATE_DISARMED;
    private byte _latestArmingReason = 0;
    private byte _latestDisarmingReason = 0;

    /// <summary>
    /// Current arming state of the vehicle
    /// </summary>
    public byte ArmingState
    {
        get => _armingState;
        set
        {
            if (_armingState != value)
            {
                byte previousState = _armingState;
                _armingState = value;
                OnArmingStateChanged(previousState, value);
            }
        }
    }

    /// <summary>
    /// Returns true if the vehicle is armed
    /// </summary>
    public bool IsArmed => _armingState == ARMING_STATE_ARMED;

    /// <summary>
    /// Latest reason for arming
    /// </summary>
    public byte LatestArmingReason
    {
        get => _latestArmingReason;
        set => _latestArmingReason = value;
    }

    /// <summary>
    /// Latest reason for disarming
    /// </summary>
    public byte LatestDisarmingReason
    {
        get => _latestDisarmingReason;
        set => _latestDisarmingReason = value;
    }

    private void Awake()
    {
        // Ensure only one instance exists
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnArmingStateChanged(byte previousState, byte newState)
    {
        string previousStateStr = GetArmingStateString(previousState);
        string newStateStr = GetArmingStateString(newState);

        Debug.Log($"PX4 Arming State Changed: {previousStateStr} -> {newStateStr}");
    }

    private string GetArmingStateString(byte state)
    {
        switch (state)
        {
            case ARMING_STATE_DISARMED:
                return "DISARMED";
            case ARMING_STATE_ARMED:
                return "ARMED";
            default:
                return $"UNKNOWN ({state})";
        }
    }

    /// <summary>
    /// Reset state to disarmed (useful when disconnected from PX4)
    /// </summary>
    public void ResetState()
    {
        ArmingState = ARMING_STATE_DISARMED;
        _latestArmingReason = 0;
        _latestDisarmingReason = 0;
        Debug.Log("PX4 state reset to DISARMED");
    }
}
