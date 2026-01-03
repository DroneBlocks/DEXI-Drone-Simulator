using UnityEngine;
using System;
using System.Threading.Tasks;

public class ROSCameraPublisher : MonoBehaviour
{
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
    private float publishRate = 10f;

    [SerializeField]
    [Tooltip("JPEG compression quality (1-100, lower = smaller file)")]
    private int jpegQuality = 75;

    [SerializeField]
    [Tooltip("Show FPS counter")]
    private bool showFPS = true;

    [SerializeField]
    [Tooltip("Camera frame ID")]
    private string frameId = "camera";

    [Header("ROS Topics")]
    [SerializeField]
    [Tooltip("Topic for compressed image")]
    private string imageTopic = "/cam0/image_raw/compressed";

    [SerializeField]
    [Tooltip("Topic for camera info")]
    private string cameraInfoTopic = "/cam0/camera_info";

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
    [Tooltip("Horizontal field of view in degrees (from Pi Camera v3 calibration at 800x600)")]
    private float horizontalFOV = 38f;  // Pi Camera v3 calibrated: fx=1161 at 800x600 → 38° horizontal

    [SerializeField]
    [Tooltip("Show GUI button for manual publishing")]
    private bool showPublishButton = true;

    [SerializeField]
    [Tooltip("Automatically publish images at the target publish rate")]
    private bool autoPublish = true;

    private RenderTexture renderTexture;
    private Texture2D texture2D;

    private float publishInterval;
    private float lastPublishTime;

    // FPS tracking
    private float[] frameDeltaTimeArray = new float[30];
    private int frameDeltaTimeIndex = 0;
    private float currentFPS;

    // Resolved topic names (with namespace applied)
    private string resolvedImageTopic;
    private string resolvedCameraInfoTopic;
    private bool hasAdvertised = false;

    [Serializable]
    private class CompressedImageMessageData
    {
        public Header header;
        public string format;
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

        // Apply namespace to topics
        resolvedImageTopic = ROSBridgeManager.Instance.ApplyNamespace(imageTopic);
        resolvedCameraInfoTopic = ROSBridgeManager.Instance.ApplyNamespace(cameraInfoTopic);

        // Subscribe to connection events
        ROSBridgeManager.Instance.OnConnected += OnROSConnected;
        ROSBridgeManager.Instance.OnDisconnected += OnROSDisconnected;

        // If already connected, advertise topics
        if (ROSBridgeManager.Instance.IsConnected)
        {
            AdvertiseTopics();
        }

        Debug.Log($"Camera calibration parameters:");
        Debug.Log($"Focal Length: {focalLength}");
        Debug.Log($"Principal Point X: {principalPointX}");
        Debug.Log($"Principal Point Y: {principalPointY}");
        Debug.Log($"Image Width: {imageWidth}");
        Debug.Log($"Image Height: {imageHeight}");
        Debug.Log($"Horizontal FOV: {horizontalFOV}");
        Debug.Log($"Image Topic: {resolvedImageTopic}");
        Debug.Log($"Camera Info Topic: {resolvedCameraInfoTopic}");
    }

    private void OnROSConnected()
    {
        Debug.Log("ROSCameraPublisher: ROS Bridge connected, advertising topics");
        AdvertiseTopics();
    }

    private void OnROSDisconnected()
    {
        Debug.Log("ROSCameraPublisher: ROS Bridge disconnected");
        hasAdvertised = false;
    }

    private void AdvertiseTopics()
    {
        if (hasAdvertised) return;

        ROSBridgeManager.Instance.Advertise(resolvedImageTopic, "sensor_msgs/CompressedImage");
        ROSBridgeManager.Instance.Advertise(resolvedCameraInfoTopic, "sensor_msgs/CameraInfo");
        hasAdvertised = true;
    }

    private async Task PublishCameraInfo()
    {
        if (!ROSBridgeManager.Instance.IsConnected) return;

        try
        {
            var cameraInfoData = new CameraInfoMessageData
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
                d = new double[] { 0, 0, 0, 0, 0 },
                k = new double[] {
                    focalLength, 0, principalPointX,
                    0, focalLength, principalPointY,
                    0, 0, 1
                },
                r = new double[] {
                    1, 0, 0,
                    0, 1, 0,
                    0, 0, 1
                },
                p = new double[] {
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
            };

            await ROSBridgeManager.Instance.Publish(
                resolvedCameraInfoTopic,
                "sensor_msgs/CameraInfo",
                cameraInfoData
            );
        }
        catch (Exception e)
        {
            Debug.LogError($"Error publishing camera info: {e.Message}");
        }
    }

    public async void PublishCameraImage()
    {
        if (!ROSBridgeManager.Instance.IsConnected)
        {
            Debug.LogWarning("Not connected to ROS bridge!");
            return;
        }

        try
        {
            // Save camera's viewport rect (may be modified by PIP)
            Rect originalRect = sourceCamera.rect;

            // Reset to full viewport for capture
            sourceCamera.rect = new Rect(0, 0, 1, 1);

            // Capture the camera image
            sourceCamera.targetTexture = renderTexture;
            sourceCamera.Render();
            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
            texture2D.Apply();
            sourceCamera.targetTexture = null;
            RenderTexture.active = null;

            // Restore original viewport rect
            sourceCamera.rect = originalRect;

            // Encode as JPEG (much smaller than raw RGB)
            byte[] jpegData = texture2D.EncodeToJPG(jpegQuality);

            var imageData = new CompressedImageMessageData
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
                format = "jpeg",
                data = jpegData
            };

            await ROSBridgeManager.Instance.Publish(
                resolvedImageTopic,
                "sensor_msgs/CompressedImage",
                imageData
            );

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
        if (autoPublish && ROSBridgeManager.Instance.IsConnected && Time.time - lastPublishTime >= publishInterval)
        {
            PublishCameraImage();
            lastPublishTime = Time.time;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from connection events
        if (ROSBridgeManager.Instance != null)
        {
            ROSBridgeManager.Instance.OnConnected -= OnROSConnected;
            ROSBridgeManager.Instance.OnDisconnected -= OnROSDisconnected;
        }

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

        // FPS display in bottom left corner
        if (showFPS)
        {
            int h = Screen.height;
            string fpsText = $"FPS: {currentFPS:F1}";

            // Create a style for the FPS text
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 16;
            style.normal.textColor = Color.white;
            style.fontStyle = FontStyle.Bold;

            // Add a dark background for better visibility
            Rect bgRect = new Rect(10, h - 40, 110, 30);
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture);
            
            // Draw the FPS text
            GUI.color = Color.white;
            GUI.Label(bgRect, fpsText, style);
        }
    }
} 