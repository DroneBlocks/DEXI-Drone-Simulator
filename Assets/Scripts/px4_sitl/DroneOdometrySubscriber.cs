using UnityEngine;
using System;
using Newtonsoft.Json;

[Serializable]
public class VehicleOdometry
{
    public long timestamp;
    public float[] position = new float[3];  // x, y, z
    public float[] q = new float[4];  // x, y, z, w (quaternion)
    public float[] velocity = new float[3];  // vx, vy, vz
    public float[] angular_velocity = new float[3];  // vx, vy, vz
}

/// <summary>
/// Drone Odometry Subscriber that uses centralized ROSBridgeManager
/// </summary>
public class DroneOdometrySubscriber : MonoBehaviour, IROSSubscriber
{
    [Header("Drone Settings")]
    [SerializeField]
    private Transform droneTransform;

    [SerializeField]
    private bool useLocalPosition = true;

    [SerializeField]
    private Vector3 positionOffset = Vector3.zero;

    [SerializeField]
    private Vector3 rotationOffset = Vector3.zero;

    [Header("Smoothing Settings")]
    [SerializeField]
    [Tooltip("Enable position and rotation smoothing to reduce jitter")]
    private bool enableSmoothing = true;

    [SerializeField]
    [Range(0.01f, 1f)]
    [Tooltip("Higher values = faster response but more jitter. Lower values = smoother but more lag.")]
    private float positionSmoothingFactor = 0.15f;

    [SerializeField]
    [Range(0.01f, 1f)]
    [Tooltip("Higher values = faster response but more jitter. Lower values = smoother but more lag.")]
    private float rotationSmoothingFactor = 0.1f;

    [Header("ROS Topic Configuration")]
    [SerializeField]
    private string topicPath = "/fmu/out/vehicle_odometry";

    [SerializeField]
    private string messageType = "px4_msgs/msg/VehicleOdometry";

    // Target values received from ROS
    private Vector3 targetPosition;
    private Quaternion targetRotation = Quaternion.identity;
    
    // Current smoothed values
    private Vector3 currentSmoothedPosition;
    private Quaternion currentSmoothedRotation = Quaternion.identity;
    
    private bool isFirstUpdate = true;
    private bool hasReceivedData = false;

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

    private void Update()
    {
        if (!hasReceivedData) return;

        if (enableSmoothing)
        {
            if (isFirstUpdate)
            {
                // Initialize smoothed values on first update
                currentSmoothedPosition = targetPosition;
                currentSmoothedRotation = targetRotation;
                isFirstUpdate = false;
            }
            else
            {
                // Frame-rate independent smoothing
                // Adjust factor to be relative to a reference frame rate (e.g., 60 FPS) to maintain similar "feel" to previous setup
                // or just treat the factor as a speed. 
                // Using the Lerp time adjustment formula: t_adjusted = 1 - Pow(1 - factor, dt * 60)
                
                float posT = 1.0f - Mathf.Pow(1.0f - positionSmoothingFactor, Time.deltaTime * 60.0f);
                float rotT = 1.0f - Mathf.Pow(1.0f - rotationSmoothingFactor, Time.deltaTime * 60.0f);

                currentSmoothedPosition = Vector3.Lerp(currentSmoothedPosition, targetPosition, posT);
                currentSmoothedRotation = Quaternion.Slerp(currentSmoothedRotation, targetRotation, rotT);
            }

            ApplyTransform(currentSmoothedPosition, currentSmoothedRotation);
        }
        else
        {
            // Direct update
            ApplyTransform(targetPosition, targetRotation);
        }
    }

    private void ApplyTransform(Vector3 pos, Quaternion rot)
    {
        if (droneTransform == null) return;

        if (useLocalPosition)
        {
            droneTransform.localPosition = pos;
            droneTransform.localRotation = rot;
        }
        else
        {
            droneTransform.position = pos;
            droneTransform.rotation = rot;
        }
    }

    public void OnMessageReceived(string message)
    {
        try
        {
            // Parse the VehicleOdometry message
            var odometry = JsonConvert.DeserializeObject<VehicleOdometry>(message);

            if (odometry != null)
            {
                // Parse Position
                // PX4: NED (North-East-Down) to Unity: Right-Up-Forward
                Vector3 newPosition = new Vector3(
                    odometry.position[1],  // East -> X (Right)
                    -odometry.position[2], // Down -> -Y (Up)
                    odometry.position[0]   // North -> Z (Forward)
                );

                // Apply position offset
                newPosition += positionOffset;

                // Parse Rotation
                if (!float.IsNaN(odometry.q[0]))
                {
                    // Convert from PX4 NED frame to Unity's coordinate system
                    Quaternion px4Rotation = new Quaternion(
                        odometry.q[2],   // y (East) -> x (Right)
                        -odometry.q[3],  // z (Down) -> -y (Up)
                        odometry.q[1],   // x (North) -> z (Forward)
                        -odometry.q[0]   // w (scalar) - negated for handedness conversion
                    );

                    // Normalize the quaternion to ensure it's valid
                    px4Rotation.Normalize();

                    // Apply coordinate system alignment and offset
                    Quaternion newRotation = px4Rotation * Quaternion.Euler(rotationOffset);

                    // Check for quaternion flip (shortest path continuity)
                    // We check against the LATEST TARGET, not the smoothed value, to ensure continuity in the target stream
                    if (hasReceivedData && Quaternion.Dot(targetRotation, newRotation) < 0)
                    {
                        newRotation = new Quaternion(-newRotation.x, -newRotation.y, -newRotation.z, -newRotation.w);
                    }

                    // Update targets atomically
                    targetPosition = newPosition;
                    targetRotation = newRotation;
                    hasReceivedData = true;
                }
                else
                {
                    Debug.LogWarning("Received invalid quaternion (NaN)");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing odometry message: {e.Message}\nMessage was: {message}");
        }
    }

    public void OnSubscribed()
    {
        Debug.Log($"Successfully subscribed to {topicPath}");
    }

    public void OnDisconnected()
    {
        Debug.Log($"Disconnected from {topicPath}");
        isFirstUpdate = true; // Reset on disconnect
        hasReceivedData = false;
    }
}
