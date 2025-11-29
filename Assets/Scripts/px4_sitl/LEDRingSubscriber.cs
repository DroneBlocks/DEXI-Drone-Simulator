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
    private string topicPath = "/dexi/led_state";

    [SerializeField]
    private string messageType = "dexi_interfaces/msg/LEDStateArray";

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
            // Parse the LEDStateArray message
            var ledStateArray = JsonConvert.DeserializeObject<LEDStateArray>(message);

            if (ledStateArray != null && ledStateArray.leds != null && ledRingVisualizer != null)
            {
                // Update the LED ring visualizer
                ledRingVisualizer.UpdateLEDs(ledStateArray.leds);
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
        Debug.Log($"Successfully subscribed to {topicPath}");
    }

    public void OnDisconnected()
    {
        Debug.Log($"Disconnected from {topicPath}");
    }
}
