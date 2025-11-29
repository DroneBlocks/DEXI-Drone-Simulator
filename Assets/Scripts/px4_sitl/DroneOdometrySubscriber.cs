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

    [SerializeField]
    [Tooltip("Enable debug logging to track rotation updates")]
    private bool debugRotation = false;

    [Header("ROS Topic Configuration")]
    [SerializeField]
    private string topicPath = "/fmu/out/vehicle_odometry";

    [SerializeField]
    private string messageType = "px4_msgs/msg/VehicleOdometry";

    private Vector3 smoothedPosition;
    private Quaternion smoothedRotation = Quaternion.identity;
    private bool isFirstUpdate = true;

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
            // Parse the VehicleOdometry message
            var odometry = JsonConvert.DeserializeObject<VehicleOdometry>(message);

            if (odometry != null && droneTransform != null)
            {
                // Update position
                // PX4: NED (North-East-Down) to Unity: Right-Up-Forward
                Vector3 newPosition = new Vector3(
                    odometry.position[1],  // East -> X (Right)
                    -odometry.position[2], // Down -> -Y (Up)
                    odometry.position[0]   // North -> Z (Forward)
                );

                // Apply position offset
                newPosition += positionOffset;

                // Check if quaternion is valid
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

                    // Apply coordinate system alignment
                    Quaternion newRotation = px4Rotation;

                    // Apply rotation offset
                    newRotation *= Quaternion.Euler(rotationOffset);

                    // Apply smoothing if enabled
                    if (enableSmoothing)
                    {
                        if (isFirstUpdate)
                        {
                            // Initialize smoothed values on first update
                            smoothedPosition = newPosition;
                            smoothedRotation = newRotation;
                            isFirstUpdate = false;
                        }
                        else
                        {
                            // Fix quaternion discontinuity - ensure we take the shortest path
                            // If dot product is negative, quaternions represent the same rotation but will slerp the long way
                            if (Quaternion.Dot(smoothedRotation, newRotation) < 0)
                            {
                                newRotation = new Quaternion(-newRotation.x, -newRotation.y, -newRotation.z, -newRotation.w);
                            }

                            // Exponential moving average (low-pass filter)
                            smoothedPosition = Vector3.Lerp(smoothedPosition, newPosition, positionSmoothingFactor);

                            if (debugRotation)
                            {
                                Debug.Log($"Before Slerp - Current: {smoothedRotation.eulerAngles.y:F1}°, Target: {newRotation.eulerAngles.y:F1}°");
                            }

                            smoothedRotation = Quaternion.Slerp(smoothedRotation, newRotation, rotationSmoothingFactor);

                            if (debugRotation)
                            {
                                Debug.Log($"After Slerp - Result: {smoothedRotation.eulerAngles.y:F1}°");
                            }
                        }

                        // Update the drone's transform with smoothed values
                        if (useLocalPosition)
                        {
                            droneTransform.localPosition = smoothedPosition;
                            droneTransform.localRotation = smoothedRotation;
                        }
                        else
                        {
                            droneTransform.position = smoothedPosition;
                            droneTransform.rotation = smoothedRotation;
                        }
                    }
                    else
                    {
                        // Direct update without smoothing
                        if (useLocalPosition)
                        {
                            droneTransform.localPosition = newPosition;
                            droneTransform.localRotation = newRotation;
                        }
                        else
                        {
                            droneTransform.position = newPosition;
                            droneTransform.rotation = newRotation;
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("Received invalid quaternion (NaN)");
                }
            }
            else
            {
                if (odometry == null)
                    Debug.LogError("Failed to parse odometry message");
                if (droneTransform == null)
                    Debug.LogError("Drone transform is not assigned!");
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
    }
}
