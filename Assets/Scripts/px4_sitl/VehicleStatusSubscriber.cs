using UnityEngine;
using System;
using Newtonsoft.Json;

[Serializable]
public class VehicleStatus
{
    public long timestamp;
    public byte arming_state;
    public byte latest_arming_reason;
    public byte latest_disarming_reason;

    // Additional fields from px4_msgs/msg/VehicleStatus can be added here as needed
    // For now, we only need the arming state fields
}

/// <summary>
/// Vehicle Status Subscriber that uses centralized ROSBridgeManager
/// Subscribes to PX4 vehicle status to track arming state
/// </summary>
public class VehicleStatusSubscriber : MonoBehaviour, IROSSubscriber
{
    [Header("ROS Topic Configuration")]
    [SerializeField]
    [Tooltip("Use /fmu/out/vehicle_status_v1 for PX4 v1.16+ or /fmu/out/vehicle_status for older versions")]
    private string topicPath = "/fmu/out/vehicle_status_v1";

    [SerializeField]
    private string messageType = "px4_msgs/msg/VehicleStatus";

    // IROSSubscriber implementation
    public string TopicPath => topicPath;
    public string MessageType => messageType;

    private void OnEnable()
    {
        // Register with the ROSBridgeManager
        ROSBridgeManager.Instance.RegisterSubscriber(this);
    }

    private void OnDisable()
    {
        // Unregister from the ROSBridgeManager
        ROSBridgeManager.Instance.UnregisterSubscriber(this);
    }

    public void OnMessageReceived(string message)
    {
        try
        {
            // Parse the VehicleStatus message
            var status = JsonConvert.DeserializeObject<VehicleStatus>(message);

            if (status != null)
            {
                // Update the PX4StateManager with the new arming state
                PX4StateManager.Instance.ArmingState = status.arming_state;
                PX4StateManager.Instance.LatestArmingReason = status.latest_arming_reason;
                PX4StateManager.Instance.LatestDisarmingReason = status.latest_disarming_reason;
            }
            else
            {
                Debug.LogError("Failed to parse vehicle status message");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing vehicle status message: {e.Message}\nMessage was: {message}");
        }
    }

    public void OnSubscribed()
    {
        Debug.Log($"Successfully subscribed to {topicPath}");
    }

    public void OnDisconnected()
    {
        Debug.Log($"Disconnected from {topicPath}");
        // Reset state to disarmed when disconnected
        PX4StateManager.Instance.ResetState();
    }
}
