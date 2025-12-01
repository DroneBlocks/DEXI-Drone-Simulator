using UnityEngine;
using System;
using System.Text;
using System.Collections;
using NativeWebSocket;
using System.Threading.Tasks;

public class ROSCameraPublisher : MonoBehaviour
{
    [SerializeField]
    [Tooltip("URL for ROS bridge when running in Unity Editor or standalone builds")]
    private string rosbridge_url = "ws://localhost:9090";
    
    [SerializeField]
    [Tooltip("URL for ROS bridge when running in WebGL builds")]
    private string webgl_rosbridge_url = "ws://localhost:9090";

    [SerializeField]
    [Tooltip("Camera to capture images from")]
    private Camera sourceCamera;

    [SerializeField]
    [Tooltip("Width of the published image")]
    private int imageWidth = 320;

    [SerializeField]
    [Tooltip("Height of the published image")]
    private int imageHeight = 240;

    [SerializeField]
    [Tooltip("Target publish rate in Hz (frames per second)")]
    private float publishRate = 30f;

    [SerializeField]
    [Tooltip("Show FPS counter")]
    private bool showFPS = true;

    [SerializeField]
    [Tooltip("Camera frame ID")]
    private string frameId = "camera";

    // Camera calibration parameters
    [SerializeField]
    [Tooltip("Camera focal length in pixels")]
    private double focalLength = 400.0;  // Will be calculated from FOV

    [SerializeField]
    [Tooltip("Camera principal point x (usually width/2)")]
    private double principalPointX;

    [SerializeField]
    [Tooltip("Camera principal point y (usually height/2)")]
    private double principalPointY;

    // Add field of view parameters for easier calibration
    [SerializeField]
    [Tooltip("Horizontal field of view in degrees")]
    private float horizontalFOV = 43f;  // Calibrated FOV that gives correct measurements

    [SerializeField]
    [Tooltip("Show GUI button for manual publishing")]
    private bool showPublishButton = true;

    [SerializeField]
    [Tooltip("Automatically publish images at the target publish rate")]
    private bool autoPublish = true;

    private WebSocket websocket;
    private bool isConnected = false;
    private RenderTexture renderTexture;
    private Texture2D texture2D;

    private float publishInterval;
    private float lastPublishTime;
    
    // FPS tracking
    private float[] frameDeltaTimeArray = new float[30];
    private int frameDeltaTimeIndex = 0;
    private float currentFPS;

    private string GetRosBridgeUrl()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return webgl_rosbridge_url;
#else
        return rosbridge_url;
#endif
    }

    [Serializable]
    private class ROSImageMessage
    {
        public string op;
        public string topic;
        public ImageMessageData msg;
        public string type;
    }

    [Serializable]
    private class ImageMessageData
    {
        public Header header;
        public uint height;
        public uint width;
        public string encoding;
        public byte is_bigendian;
        public uint step;
        public byte[] data;
    }

    [Serializable]
    private class Header
    {
        public TimeMsg stamp;
        public string frame_id;
    }

    [Serializable]
    private class TimeMsg
    {
        public int sec;
        public uint nanosec;
    }

    [Serializable]
    private class ROSCameraInfoMessage
    {
        public string op;
        public string topic;
        public CameraInfoMessageData msg;
        public string type;
    }

    [Serializable]
    private class CameraInfoMessageData
    {
        public Header header;
        public uint height;
        public uint width;
        public string distortion_model;
        public double[] d;  // Changed from D to d for ROS1
        public double[] k;  // Changed from K to k for ROS1
        public double[] r;  // Changed from R to r for ROS1
        public double[] p;  // Changed from P to p for ROS1
        public uint binning_x;
        public uint binning_y;
        public ROI roi;
    }

    [Serializable]
    private class ROI
    {
        public uint x_offset;
        public uint y_offset;
        public uint height;
        public uint width;
        public bool do_rectify;
    }

    void Start()
    {
        if (sourceCamera == null)
        {
            sourceCamera = Camera.main;
        }

        // Calculate focal length based on FOV and image width
        float fovRadians = horizontalFOV * Mathf.Deg2Rad;
        focalLength = (imageWidth / 2.0) / Mathf.Tan(fovRadians / 2);

        // Initialize principal points if not set
        if (principalPointX == 0) principalPointX = imageWidth / 2.0;
        if (principalPointY == 0) principalPointY = imageHeight / 2.0;

        // Create render texture and texture2D for image capture
        renderTexture = new RenderTexture(imageWidth, imageHeight, 24);
        texture2D = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);

        // Initialize publish rate tracking
        publishInterval = 1f / publishRate;
        lastPublishTime = -publishInterval; // Ensure first frame publishes immediately
        
        ConnectToROS();

        Debug.Log($"Camera calibration parameters:");
        Debug.Log($"Focal Length: {focalLength}");
        Debug.Log($"Principal Point X: {principalPointX}");
        Debug.Log($"Principal Point Y: {principalPointY}");
        Debug.Log($"Image Width: {imageWidth}");
        Debug.Log($"Image Height: {imageHeight}");
        Debug.Log($"Horizontal FOV: {horizontalFOV}");
    }

    async void ConnectToROS()
    {
        string url = GetRosBridgeUrl();
        Debug.Log($"Attempting to connect to ROSBridge at {url}");
        websocket = new WebSocket(url);

        websocket.OnOpen += () =>
        {
            Debug.Log("Connection open!");
            isConnected = true;

            // Advertise the image publisher
            var advertiseImageMsg = new ROSImageMessage
            {
                op = "advertise",
                topic = "/image_rect",
                type = "sensor_msgs/Image"  // Changed from sensor_msgs/msg/Image
            };
            
            // Advertise the camera info publisher
            var advertiseCameraInfoMsg = new ROSCameraInfoMessage
            {
                op = "advertise",
                topic = "/camera_info",
                type = "sensor_msgs/CameraInfo"  // Changed from sensor_msgs/msg/CameraInfo
            };
            
            websocket.SendText(JsonUtility.ToJson(advertiseImageMsg));
            websocket.SendText(JsonUtility.ToJson(advertiseCameraInfoMsg));
        };

        websocket.OnError += (e) =>
        {
            Debug.LogError($"WebSocket Error! {e}");
        };

        websocket.OnClose += (e) =>
        {
            Debug.Log("Connection closed!");
            isConnected = false;
        };

        websocket.OnMessage += (bytes) =>
        {
            var message = Encoding.UTF8.GetString(bytes);
            Debug.Log($"OnMessage! {message}");
        };

        try
        {
            await websocket.Connect();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect: {e.Message}");
        }
    }

    private async Task PublishCameraInfo()
    {
        if (!isConnected) return;

        try
        {
            var cameraInfoMsg = new ROSCameraInfoMessage
            {
                op = "publish",
                topic = "/camera_info",
                type = "sensor_msgs/CameraInfo",
                msg = new CameraInfoMessageData
                {
                    header = new Header
                    {
                        stamp = new TimeMsg
                        {
                            sec = (int)Time.time,
                            nanosec = (uint)((Time.time % 1) * 1e9)
                        },
                        frame_id = frameId
                    },
                    height = (uint)imageHeight,
                    width = (uint)imageWidth,
                    distortion_model = "plumb_bob",
                    d = new double[] { 0, 0, 0, 0, 0 },  // Changed from D to d
                    k = new double[] {  // Changed from K to k
                        focalLength, 0, principalPointX,
                        0, focalLength, principalPointY,
                        0, 0, 1
                    },
                    r = new double[] {  // Changed from R to r
                        1, 0, 0,
                        0, 1, 0,
                        0, 0, 1
                    },
                    p = new double[] {  // Changed from P to p
                        focalLength, 0, principalPointX, 0,
                        0, focalLength, principalPointY, 0,
                        0, 0, 1, 0
                    },
                    binning_x = 1,
                    binning_y = 1,
                    roi = new ROI
                    {
                        x_offset = 0,
                        y_offset = 0,
                        height = (uint)imageHeight,
                        width = (uint)imageWidth,
                        do_rectify = false
                    }
                }
            };

            string jsonMessage = JsonUtility.ToJson(cameraInfoMsg);
            await websocket.SendText(jsonMessage);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error publishing camera info: {e.Message}");
        }
    }

    public async void PublishCameraImage()
    {
        Debug.Log("Publish Camera Image button clicked!");

        if (!isConnected)
        {
            Debug.LogWarning("Not connected to ROS bridge!");
            return;
        }

        Debug.Log("Publishing image to /image_rect topic...");

        try
        {
            // Capture the camera image
            sourceCamera.targetTexture = renderTexture;
            sourceCamera.Render();
            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
            texture2D.Apply();
            sourceCamera.targetTexture = null;
            RenderTexture.active = null;

            // Get the raw image data
            byte[] originalData = texture2D.GetRawTextureData();
            byte[] flippedData = new byte[originalData.Length];
            
            // Flip the image vertically
            int bytesPerRow = imageWidth * 3; // 3 bytes per pixel (RGB)
            for (int y = 0; y < imageHeight; y++)
            {
                int sourceRow = y * bytesPerRow;
                int targetRow = (imageHeight - 1 - y) * bytesPerRow;
                Array.Copy(originalData, sourceRow, flippedData, targetRow, bytesPerRow);
            }

            var rosMessage = new ROSImageMessage
            {
                op = "publish",
                topic = "/image_rect",
                type = "sensor_msgs/Image",
                msg = new ImageMessageData
                {
                    header = new Header
                    {
                        stamp = new TimeMsg
                        {
                            sec = (int)Time.time,
                            nanosec = (uint)((Time.time % 1) * 1e9)
                        },
                        frame_id = frameId
                    },
                    height = (uint)imageHeight,
                    width = (uint)imageWidth,
                    encoding = "rgb8",
                    is_bigendian = 0,
                    step = (uint)(imageWidth * 3),
                    data = flippedData
                }
            };

            string jsonMessage = JsonUtility.ToJson(rosMessage);
            await websocket.SendText(jsonMessage);

            // Publish camera info along with the image
            await PublishCameraInfo();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error publishing image: {e.Message}");
        }
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif
        
        // Track FPS using moving average
        if (showFPS)
        {
            frameDeltaTimeArray[frameDeltaTimeIndex] = Time.deltaTime;
            frameDeltaTimeIndex = (frameDeltaTimeIndex + 1) % frameDeltaTimeArray.Length;

            float sum = 0f;
            for (int i = 0; i < frameDeltaTimeArray.Length; i++)
            {
                sum += frameDeltaTimeArray[i];
            }
            float averageDeltaTime = sum / frameDeltaTimeArray.Length;
            currentFPS = 1f / averageDeltaTime;
        }

        // Check if it's time to publish based on the target rate (only if auto-publish is enabled)
        if (autoPublish && isConnected && Time.time - lastPublishTime >= publishInterval)
        {
            PublishCameraImage();
            lastPublishTime = Time.time;
        }
    }

    private async void OnApplicationQuit()
    {
        if (websocket != null && isConnected)
        {
            await websocket.Close();
        }
    }

    private void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
        }
    }

    private void OnGUI()
    {
        // Toggle auto-publish button
        if (showPublishButton)
        {
            string buttonText = autoPublish ? "Stop Publishing" : "Start Publishing";
            Color buttonColor = autoPublish ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 1f, 0.3f);

            GUI.backgroundColor = buttonColor;
            if (GUI.Button(new Rect(10, 50, 200, 30), buttonText))
            {
                autoPublish = !autoPublish;
                Debug.Log($"Camera auto-publish {(autoPublish ? "started" : "stopped")}");
            }
            GUI.backgroundColor = Color.white;
        }

        // FPS display in bottom right corner
        if (showFPS)
        {
            int w = Screen.width;
            int h = Screen.height;
            string fpsText = $"FPS: {currentFPS:F1}";
            
            // Create a style for the FPS text
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 16;
            style.normal.textColor = Color.white;
            style.fontStyle = FontStyle.Bold;
            
            // Add a dark background for better visibility
            Rect bgRect = new Rect(w - 120, h - 40, 110, 30);
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture);
            
            // Draw the FPS text
            GUI.color = Color.white;
            GUI.Label(bgRect, fpsText, style);
        }
    }
} 