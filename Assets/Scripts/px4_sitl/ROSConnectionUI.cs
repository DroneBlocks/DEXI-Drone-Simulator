using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI Controller for ROS Bridge connection
/// Attach this to a Canvas and wire up the button in the Inspector
/// </summary>
public class ROSConnectionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField]
    [Tooltip("Button to connect/disconnect from ROS")]
    private Button connectButton;

    [SerializeField]
    [Tooltip("Text component on the button (optional)")]
    private TextMeshProUGUI buttonText;

    [SerializeField]
    [Tooltip("Status text to show connection state (optional)")]
    private TextMeshProUGUI statusText;

    [Header("Button Text")]
    [SerializeField]
    private string connectText = "Connect to ROS";

    [SerializeField]
    private string disconnectText = "Disconnect from ROS";

    [SerializeField]
    private string connectingText = "Connecting...";

    [Header("Status Text")]
    [SerializeField]
    private string statusDisconnected = "Status: Disconnected";

    [SerializeField]
    private string statusConnecting = "Status: Connecting...";

    [SerializeField]
    private string statusConnected = "Status: Connected";

    [SerializeField]
    private string statusError = "Status: Connection Error";

    [Header("Status Colors")]
    [SerializeField]
    private Color disconnectedColor = Color.gray;

    [SerializeField]
    private Color connectingColor = Color.yellow;

    [SerializeField]
    private Color connectedColor = Color.green;

    [SerializeField]
    private Color errorColor = Color.red;

    private bool isConnecting = false;

    private void Start()
    {
        // Set up button listener
        if (connectButton != null)
        {
            connectButton.onClick.AddListener(OnConnectButtonClicked);
        }
        else
        {
            Debug.LogError("Connect button not assigned in ROSConnectionUI!");
        }

        // Subscribe to ROS Bridge events
        ROSBridgeManager.Instance.OnConnected += OnROSConnected;
        ROSBridgeManager.Instance.OnDisconnected += OnROSDisconnected;
        ROSBridgeManager.Instance.OnError += OnROSError;

        // Initialize UI
        UpdateUI();
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (ROSBridgeManager.Instance != null)
        {
            ROSBridgeManager.Instance.OnConnected -= OnROSConnected;
            ROSBridgeManager.Instance.OnDisconnected -= OnROSDisconnected;
            ROSBridgeManager.Instance.OnError -= OnROSError;
        }

        // Remove button listener
        if (connectButton != null)
        {
            connectButton.onClick.RemoveListener(OnConnectButtonClicked);
        }
    }

    private async void OnConnectButtonClicked()
    {
        if (isConnecting) return;

        if (ROSBridgeManager.Instance.IsConnected)
        {
            // Disconnect
            UpdateUI(false, false, true); // Show disconnecting state
            await ROSBridgeManager.Instance.Disconnect();
        }
        else
        {
            // Connect
            isConnecting = true;
            UpdateUI(true, false, false);
            bool success = await ROSBridgeManager.Instance.Connect();
            isConnecting = false;

            if (!success)
            {
                UpdateUI(false, false, false); // Show error state
            }
        }
    }

    private void OnROSConnected()
    {
        Debug.Log("UI: ROS Connected");
        isConnecting = false;
        UpdateUI(false, true, false);
    }

    private void OnROSDisconnected()
    {
        Debug.Log("UI: ROS Disconnected");
        isConnecting = false;
        UpdateUI(false, false, false);
    }

    private void OnROSError(string error)
    {
        Debug.LogError($"UI: ROS Error - {error}");
        isConnecting = false;
        UpdateUI(false, false, false);
    }

    private void UpdateUI(bool connecting = false, bool connected = false, bool disconnecting = false)
    {
        // If no parameters provided, check actual state
        if (!connecting && !connected && !disconnecting)
        {
            connected = ROSBridgeManager.Instance.IsConnected;
            connecting = isConnecting;
        }

        // Update button text
        if (buttonText != null)
        {
            if (connecting)
                buttonText.text = connectingText;
            else if (disconnecting)
                buttonText.text = "Disconnecting...";
            else if (connected)
                buttonText.text = disconnectText;
            else
                buttonText.text = connectText;
        }

        // Update button interactable state
        if (connectButton != null)
        {
            connectButton.interactable = !connecting && !disconnecting;
        }

        // Update status text and color
        if (statusText != null)
        {
            if (connecting)
            {
                statusText.text = statusConnecting;
                statusText.color = connectingColor;
            }
            else if (disconnecting)
            {
                statusText.text = "Disconnecting...";
                statusText.color = connectingColor;
            }
            else if (connected)
            {
                statusText.text = statusConnected;
                statusText.color = connectedColor;
            }
            else
            {
                statusText.text = statusDisconnected;
                statusText.color = disconnectedColor;
            }
        }
    }

    // Public method to connect programmatically
    public async void Connect()
    {
        if (!ROSBridgeManager.Instance.IsConnected && !isConnecting)
        {
            OnConnectButtonClicked();
        }
    }

    // Public method to disconnect programmatically
    public async void Disconnect()
    {
        if (ROSBridgeManager.Instance.IsConnected)
        {
            await ROSBridgeManager.Instance.Disconnect();
        }
    }
}
