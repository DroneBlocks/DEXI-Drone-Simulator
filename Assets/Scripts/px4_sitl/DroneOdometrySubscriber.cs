using UnityEngine;
using System;
using Newtonsoft.Json;

[Serializable]
public class VehicleOdometry
{
    public long timestamp;
    public float[] position = new float[3];
    public float[] q = new float[4];
    public float[] velocity = new float[3];
    public float[] angular_velocity = new float[3];
}

public class DroneOdometrySubscriber : MonoBehaviour, IROSSubscriber
{
    [Header("Drone Settings")]
    [SerializeField] private Transform droneTransform;
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    [Header("Physics Tracking")]
    [SerializeField] private float positionSmoothSpeed = 15f;
    [SerializeField] private float rotationSmoothSpeed = 15f;
    [SerializeField] private float messageTimeoutSeconds = 0.5f;

    [Header("Floor Constraint")]
    [SerializeField] private float minimumHeight = 0.05f;
    [SerializeField] private float landedAltitudeThreshold = 0.15f;
    [SerializeField] private float landedVelocityThreshold = 0.1f;

    [Header("ROS Topic Configuration")]
    [SerializeField] private string baseTopicPath = "/fmu/out/vehicle_odometry";
    [SerializeField] private string messageType = "px4_msgs/msg/VehicleOdometry";

    public Vector3 TargetPosition { get; private set; }
    public Quaternion TargetRotation { get; private set; } = Quaternion.identity;
    public bool HasReceivedData { get; private set; }

    private float timeSinceLastMessage;
    private bool isFirstData = true;

    private string _namespacedTopicPath;
    public string TopicPath => _namespacedTopicPath ??= ROSBridgeManager.Instance.ApplyNamespace(baseTopicPath);
    public string MessageType => messageType;

    private void OnEnable() => ROSBridgeManager.Instance.RegisterSubscriber(this);
    private void OnDisable() => ROSBridgeManager.Instance.UnregisterSubscriber(this);

    public void ApplyPhysics(Rigidbody rb)
    {
        if (!HasReceivedData || rb == null) return;

        timeSinceLastMessage += Time.fixedDeltaTime;
        if (timeSinceLastMessage > messageTimeoutSeconds) return;

        rb.useGravity = false;

        Vector3 positionError = TargetPosition - rb.position;
        rb.linearVelocity = positionError / Time.fixedDeltaTime * Mathf.Clamp01(Time.fixedDeltaTime * positionSmoothSpeed);

        rb.MoveRotation(Quaternion.Slerp(rb.rotation, TargetRotation, Time.fixedDeltaTime * rotationSmoothSpeed));
        rb.angularVelocity = Vector3.zero;
    }

    public void OnMessageReceived(string message)
    {
        try
        {
            var odometry = JsonConvert.DeserializeObject<VehicleOdometry>(message);
            if (odometry == null || !PX4StateManager.Instance.IsArmed) return;

            Vector3 newPosition = new Vector3(
                odometry.position[1],
                -odometry.position[2],
                odometry.position[0]
            ) + positionOffset;

            float velocityMagnitude = new Vector3(odometry.velocity[0], odometry.velocity[1], odometry.velocity[2]).magnitude;
            bool isLanded = newPosition.y < landedAltitudeThreshold && velocityMagnitude < landedVelocityThreshold;
            newPosition.y = isLanded ? minimumHeight : Mathf.Max(newPosition.y, minimumHeight);

            if (float.IsNaN(odometry.q[0]))
            {
                Debug.LogWarning("Received invalid quaternion (NaN)");
                return;
            }

            Quaternion newRotation = new Quaternion(odometry.q[2], -odometry.q[3], odometry.q[1], -odometry.q[0]);
            newRotation.Normalize();
            newRotation *= Quaternion.Euler(rotationOffset);

            if (HasReceivedData && Quaternion.Dot(TargetRotation, newRotation) < 0)
                newRotation = new Quaternion(-newRotation.x, -newRotation.y, -newRotation.z, -newRotation.w);

            if (isFirstData)
                isFirstData = false;

            timeSinceLastMessage = 0f;
            TargetPosition = newPosition;
            TargetRotation = newRotation;
            HasReceivedData = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing odometry message: {e.Message}\nMessage was: {message}");
        }
    }

    public void OnSubscribed() => Debug.Log($"Successfully subscribed to {TopicPath}");

    public void OnDisconnected()
    {
        Debug.Log($"Disconnected from {TopicPath}");
        HasReceivedData = false;
        isFirstData = true;
    }
}