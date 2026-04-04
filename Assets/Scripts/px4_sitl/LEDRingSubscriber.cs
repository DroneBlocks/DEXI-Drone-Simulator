using UnityEngine;
using System;
using Newtonsoft.Json;

[Serializable]
public class LEDState
{
    public uint index;
    public byte r;
    public byte g;
    public byte b;
    public byte brightness;
}

[Serializable]
public class LEDStateArray
{
    public LEDState[] leds;
}

/// <summary>
/// LED Ring Subscriber that uses centralized ROSBridgeManager
/// </summary>
public class LEDRingSubscriber : MonoBehaviour, IROSSubscriber
{
    [Header("LED Ring Settings")]
    [SerializeField]
    private LEDRingVisualizer ledRingVisualizer;

    [Header("ROS Topic Configuration")]
    [SerializeField]
    [Tooltip("Base topic path without namespace (namespace is applied automatically from ROSBridgeManager)")]
    private string baseTopicPath = "/dexi/led_state";

    [SerializeField]
    private string messageType = "dexi_interfaces/msg/LEDStateArray";

    // Cached namespaced topic path
    private string _namespacedTopicPath;

    // IROSSubscriber implementation
    public string TopicPath
    {
        get
        {
            // Cache the namespaced topic path on first access
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
        // Register with the ROSBridgeManager
        ROSBridgeManager.Instance.RegisterSubscriber(this);
    }

    private void OnDisable()
    {
        // Unregister from the ROSBridgeManager
        ROSBridgeManager.Instance.UnregisterSubscriber(this);
    }

    /// <summary>
    /// Event fired when LED colors are received.
    /// </summary>
    public System.Action<LEDState[]> OnLEDColorsReceived;

    public void OnMessageReceived(string message)
    {
        try
        {
            // Parse the LEDStateArray message
            var ledStateArray = JsonConvert.DeserializeObject<LEDStateArray>(message);

            if (ledStateArray != null && ledStateArray.leds != null && ledRingVisualizer != null)
            {
                // Update the LED ring visualizer
                ledRingVisualizer.UpdateLEDs(ledStateArray.leds);

                // Notify validator
                OnLEDColorsReceived?.Invoke(ledStateArray.leds);
            }
            else
            {
                if (ledStateArray == null)
                    Debug.LogError("Failed to parse LED state message");
                if (ledRingVisualizer == null)
                    Debug.LogError("LED ring visualizer is not assigned!");
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
