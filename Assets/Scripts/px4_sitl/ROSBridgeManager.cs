using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using NativeWebSocket;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Centralized manager for ROS Bridge WebSocket connection
/// Handles a single connection and routes messages to registered subscribers
/// </summary>
public class ROSBridgeManager : MonoBehaviour
{
    // Singleton instance
    private static ROSBridgeManager _instance;
    public static ROSBridgeManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<ROSBridgeManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("ROSBridgeManager");
                    _instance = go.AddComponent<ROSBridgeManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern string GetRosBridgeUrlFromQuery();

    [DllImport("__Internal")]
    private static extern string GetHostname();
#endif

    [Header("ROS2 Connection Settings")]
    [SerializeField]
    [Tooltip("URL for ROS bridge when running in Unity Editor or standalone builds")]
    private string rosbridge_url = "ws://localhost:9090";

    [SerializeField]
    [Tooltip("URL for ROS bridge when running in WebGL builds (fallback if URL param not provided)")]
    private string webgl_rosbridge_url = "ws://localhost:9090";

    [SerializeField]
    [Tooltip("Port for ROS bridge when using hostname-based URL construction")]
    private int rosbridge_port = 9090;

    [SerializeField]
    [Tooltip("Automatically connect to ROS bridge on Start (useful for embedded iframes)")]
    private bool autoConnectOnStart = false;

    [SerializeField]
    [Tooltip("Number of connection retry attempts (0 = no retries)")]
    [Range(0, 10)]
    private int maxRetryAttempts = 3;

    [SerializeField]
    [Tooltip("Delay between retry attempts in seconds")]
    [Range(1f, 10f)]
    private float retryDelaySeconds = 2f;

    [Header("Connection Status")]
    [SerializeField]
    [Tooltip("Current connection status (read-only)")]
    private bool isConnected = false;

    private WebSocket websocket;
    private Dictionary<string, List<IROSSubscriber>> subscribers = new Dictionary<string, List<IROSSubscriber>>();

    // Events for connection status
    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnError;

    public bool IsConnected => isConnected;

    private void Awake()
    {
        // Ensure singleton
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        // Auto-connect if enabled
        if (autoConnectOnStart)
        {
            Debug.Log("Auto-connecting to ROS bridge...");
            await ConnectWithRetry();
        }
    }

    /// <summary>
    /// Connect to ROS Bridge with automatic retry logic
    /// </summary>
    private async Task<bool> ConnectWithRetry()
    {
        int attempts = 0;
        int maxAttempts = maxRetryAttempts + 1; // +1 for initial attempt

        while (attempts < maxAttempts)
        {
            attempts++;

            if (attempts > 1)
            {
                Debug.Log($"Retry attempt {attempts - 1}/{maxRetryAttempts} in {retryDelaySeconds} seconds...");
                await Task.Delay((int)(retryDelaySeconds * 1000));
            }

            bool success = await Connect();
            if (success)
            {
                if (attempts > 1)
                {
                    Debug.Log($"Successfully connected after {attempts} attempt(s)");
                }
                return true;
            }

            if (attempts < maxAttempts)
            {
                Debug.LogWarning($"Connection attempt {attempts} failed");
            }
            else
            {
                Debug.LogError($"Failed to connect after {attempts} attempt(s)");
            }
        }

        return false;
    }

    /// <summary>
    /// Register a subscriber to receive messages for a specific topic
    /// </summary>
    public void RegisterSubscriber(IROSSubscriber subscriber)
    {
        if (subscriber == null)
        {
            Debug.LogError("Cannot register null subscriber");
            return;
        }

        string topic = subscriber.TopicPath;
        if (!subscribers.ContainsKey(topic))
        {
            subscribers[topic] = new List<IROSSubscriber>();
        }

        if (!subscribers[topic].Contains(subscriber))
        {
            subscribers[topic].Add(subscriber);
            Debug.Log($"Registered subscriber for topic: {topic}");

            // If already connected, subscribe to this topic
            if (isConnected)
            {
                SubscribeToTopic(topic, subscriber.MessageType);
            }
        }
    }

    /// <summary>
    /// Unregister a subscriber
    /// </summary>
    public void UnregisterSubscriber(IROSSubscriber subscriber)
    {
        if (subscriber == null) return;

        string topic = subscriber.TopicPath;
        if (subscribers.ContainsKey(topic))
        {
            subscribers[topic].Remove(subscriber);
            if (subscribers[topic].Count == 0)
            {
                subscribers.Remove(topic);
                Debug.Log($"Unregistered last subscriber for topic: {topic}");
            }
        }
    }

    /// <summary>
    /// Connect to ROS Bridge WebSocket server
    /// </summary>
    public async Task<bool> Connect()
    {
        if (isConnected)
        {
            Debug.LogWarning("Already connected to ROS Bridge");
            return true;
        }

        string url = GetRosBridgeUrl();
        Debug.Log($"Connecting to ROS Bridge at {url}");

        try
        {
            websocket = new WebSocket(url);

            websocket.OnOpen += () =>
            {
                Debug.Log("Connected to ROS Bridge");
                isConnected = true;
                OnConnected?.Invoke();

                // Subscribe to all registered topics
                foreach (var kvp in subscribers)
                {
                    string topic = kvp.Key;
                    if (kvp.Value.Count > 0)
                    {
                        string messageType = kvp.Value[0].MessageType;
                        SubscribeToTopic(topic, messageType);
                    }
                }
            };

            websocket.OnError += (error) =>
            {
                Debug.LogError($"ROS Bridge WebSocket error: {error}");
                OnError?.Invoke(error);
            };

            websocket.OnClose += (e) =>
            {
                Debug.Log("Disconnected from ROS Bridge");
                bool wasConnected = isConnected;
                isConnected = false;

                if (wasConnected)
                {
                    OnDisconnected?.Invoke();
                    NotifySubscribersOfDisconnection();
                }
            };

            websocket.OnMessage += (bytes) =>
            {
                var message = Encoding.UTF8.GetString(bytes);
                HandleWebSocketMessage(message);
            };

            await websocket.Connect();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect to ROS Bridge: {e.Message}");
            OnError?.Invoke(e.Message);
            return false;
        }
    }

    /// <summary>
    /// Disconnect from ROS Bridge
    /// </summary>
    public async Task Disconnect()
    {
        if (websocket != null && isConnected)
        {
            await websocket.Close();
            websocket = null;
            isConnected = false;
            OnDisconnected?.Invoke();
            NotifySubscribersOfDisconnection();
        }
    }

    private string GetRosBridgeUrl()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Priority 1: Check for URL parameter
        try
        {
            string urlFromQuery = GetRosBridgeUrlFromQuery();
            if (!string.IsNullOrEmpty(urlFromQuery))
            {
                Debug.Log($"Using ROS bridge URL from query parameter: {urlFromQuery}");
                return urlFromQuery;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to get URL from query parameter: {e.Message}");
        }

        // Priority 2: Use hostname from current page
        try
        {
            string hostname = GetHostname();
            if (!string.IsNullOrEmpty(hostname))
            {
                string constructedUrl = $"ws://{hostname}:{rosbridge_port}";
                Debug.Log($"Using ROS bridge URL constructed from hostname: {constructedUrl}");
                return constructedUrl;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to get hostname: {e.Message}");
        }

        // Priority 3: Fallback to hardcoded default
        Debug.Log($"Using fallback ROS bridge URL: {webgl_rosbridge_url}");
        return webgl_rosbridge_url;
#else
        return rosbridge_url;
#endif
    }

    private void SubscribeToTopic(string topic, string messageType)
    {
        if (!isConnected || websocket == null) return;

        var subscribeMessage = new
        {
            op = "subscribe",
            topic = topic,
            type = messageType
        };

        string jsonMessage = JsonConvert.SerializeObject(subscribeMessage);
        Debug.Log($"Subscribing to topic: {topic} with type: {messageType}");
        websocket.SendText(jsonMessage);
    }

    private void HandleWebSocketMessage(string message)
    {
        try
        {
            var jsonMessage = JObject.Parse(message);

            // Check if it's a status message
            if (jsonMessage["op"]?.ToString() == "status")
            {
                Debug.Log($"ROS Bridge status: {message}");
                return;
            }

            // Extract topic and message data
            string topic = jsonMessage["topic"]?.ToString();
            if (string.IsNullOrEmpty(topic))
            {
                Debug.LogWarning($"Message missing topic field: {message}");
                return;
            }

            string msgData = jsonMessage["msg"]?.ToString(Formatting.None);
            if (msgData == null)
            {
                Debug.LogWarning($"Message missing 'msg' field for topic {topic}");
                return;
            }

            // Route message to subscribers
            if (subscribers.ContainsKey(topic))
            {
                foreach (var subscriber in subscribers[topic])
                {
                    try
                    {
                        subscriber.OnMessageReceived(msgData);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error in subscriber for topic {topic}: {e.Message}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error handling ROS Bridge message: {e.Message}\nMessage was: {message}");
        }
    }

    private void NotifySubscribersOfDisconnection()
    {
        foreach (var kvp in subscribers)
        {
            foreach (var subscriber in kvp.Value)
            {
                try
                {
                    subscriber.OnDisconnected();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error notifying subscriber of disconnection: {e.Message}");
                }
            }
        }
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private async void OnApplicationQuit()
    {
        await Disconnect();
    }
}
