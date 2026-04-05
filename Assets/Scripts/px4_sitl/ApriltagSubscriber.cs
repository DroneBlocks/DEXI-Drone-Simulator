using UnityEngine;
using System;
using Newtonsoft.Json;

// TODO: build out apriltag detection array class

public class ApriltagSubscriber : MonoBehaviour, IROSSubscriber
{
    [Header("ROS Topic Configuration")]
    [SerializeField]
    [Tooltip("Base topic path without namespace (namespace is applied automatically from ROSBridgeManager)")]
    private string baseTopicPath = "/apriltag_detections";

    [SerializeField]
    private string messageType = "apriltag_msgs/msg/AprilTagDetectionArray";

    private string _namespacedTopicPath;

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
            var ledStateArray = JsonConvert.DeserializeObject<LEDStateArray>(message);

            if (ledStateArray != null && ledStateArray.leds != null)
            {

            }
            else
            {
                if (ledStateArray == null)
                {
                    Debug.LogError("Failed to parse LED state message");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing LED state message: {e.Message}\nMessage was: {message}");
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
