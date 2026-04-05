using UnityEngine;
using System;
using Newtonsoft.Json;

[Serializable]
public class ApriltagTime
{
    public int sec;
    public long nanosec;
}

[Serializable]
public class ApriltagHeader
{
    public ApriltagTime stamp;
    public string frame_id;
}

[Serializable]
public class ApriltagPoint2D
{
    public double x;
    public double y;
}

[Serializable]
public class ApriltagDetection
{
    public string family;
    public int id;
    public int hamming;
    public float goodness;
    public float decision_margin;
    public ApriltagPoint2D centre;
    public ApriltagPoint2D[] corners;
    public double[] homography;
}

[Serializable]
public class ApriltagDetectionArray
{
    public ApriltagHeader header;
    public ApriltagDetection[] detections;
}

public class ApriltagSubscriber : MonoBehaviour, IROSSubscriber
{
    [Header("ROS Topic Configuration")]
    [SerializeField]
    [Tooltip("Base topic path without namespace (namespace is applied automatically from ROSBridgeManager)")]
    private string baseTopicPath = "/apriltag_detections";

    [SerializeField]
    private string messageType = "apriltag_msgs/msg/AprilTagDetectionArray";

    private string _namespacedTopicPath;

    public System.Action<ApriltagDetectionArray> OnApriltagDetectionsReceived;

    public string TopicPath
    {
        get
        {
            if (_namespacedTopicPath == null)
            {
                _namespacedTopicPath = ROSBridgeManager.Instance.ApplyNamespace(baseTopicPath);
            }
            return _namespacedTopicPath;
        }
    }
    public string MessageType => messageType;

    private void OnEnable()
    {
        ROSBridgeManager.Instance.RegisterSubscriber(this);
    }

    private void OnDisable()
    {
        ROSBridgeManager.Instance.UnregisterSubscriber(this);
    }

    public void OnMessageReceived(string message)
    {
        try
        {
            var apriltagArray = JsonConvert.DeserializeObject<ApriltagDetectionArray>(message);

            if (apriltagArray != null && apriltagArray.detections.Length > 0)
            {
                OnApriltagDetectionsReceived?.Invoke(apriltagArray);
            }
            else
            {
                if (apriltagArray == null)
                {
                    Debug.LogError("Failed to parse Apriltag Detection message");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing Apriltag Detection message: {e.Message}\nMessage was: {message}");
        }
    }

    public void OnSubscribed()
    {
        Debug.Log($"Successfully subscribed to {TopicPath}");
    }

    public void OnDisconnected()
    {
        Debug.Log($"Disconnected from {TopicPath}");
    }
}
