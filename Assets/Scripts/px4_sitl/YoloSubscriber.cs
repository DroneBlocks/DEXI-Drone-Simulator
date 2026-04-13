using UnityEngine;
using System;
using Newtonsoft.Json;

[Serializable]
public class YoloDetection
{
    public string class_name;
    public double confidence;
    public double[] bbox;
}

[Serializable]
public class YoloDetectionArray
{
    public RosHeader header;
    public YoloDetection[] detections;
    public double timestamp;
}

public class YoloSubscriber : MonoBehaviour, IROSSubscriber
{
    [Header("ROS Topic Configuration")]
    [SerializeField]
    [Tooltip("Base topic path without namespace (namespace is applied automatically from ROSBridgeManager)")]
    private string baseTopicPath = "/yolo_detections";

    [SerializeField]
    private string messageType = "dexi_interfaces/msg/YoloDetectionArray";

    private string _namespacedTopicPath;

    public System.Action<YoloDetectionArray> OnYoloDetectionsReceived;

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
            var yoloArray = JsonConvert.DeserializeObject<YoloDetectionArray>(message);

            if (yoloArray != null && yoloArray.detections.Length > 0)
            {
                OnYoloDetectionsReceived?.Invoke(yoloArray);
            }
            else
            {
                if (yoloArray == null)
                {
                    Debug.LogError("Failed to parse YOLO Detection message");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing YOLO Detection message: {e.Message}\nMessage was: {message}");
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
