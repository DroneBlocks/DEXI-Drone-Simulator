using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;

public class MapOverlay : MonoBehaviour
{
    [Header("Google Maps API")]
    [Tooltip("Get your API key from: https://console.cloud.google.com/")]
    public string googleMapsAPIKey = "YOUR_API_KEY_HERE";

    [Header("Map Settings")]
    [Tooltip("Center latitude for the map")]
    public double centerLatitude = 47.397742;  // Default: Zurich (common PX4 SITL location)

    [Tooltip("Center longitude for the map")]
    public double centerLongitude = 8.545594;  // Default: Zurich

    [Tooltip("Map zoom level (1-20, higher = more zoomed in)")]
    [Range(1, 20)]
    public int zoomLevel = 18;

    [Tooltip("Map image size in pixels")]
    public int mapSize = 640;

    [Tooltip("Map type: roadmap, satellite, hybrid, terrain")]
    public MapType mapType = MapType.satellite;

    [Header("Display Mode")]
    [Tooltip("Choose how to display the map")]
    public DisplayMode displayMode = DisplayMode.UIOverlay;

    [Tooltip("Size of ground plane in meters (for GroundPlane mode)")]
    public float groundPlaneSize = 100f;

    [Header("Drone Position")]
    [Tooltip("Drone's current latitude (updates from PX4)")]
    public double droneLatitude = 47.397742;

    [Tooltip("Drone's current longitude (updates from PX4)")]
    public double droneLongitude = 8.545594;

    [Header("UI Settings")]
    [Tooltip("Toggle map visibility with this key")]
    public KeyCode toggleKey = KeyCode.M;

    [Tooltip("Position of map on screen")]
    public MapPosition mapPosition = MapPosition.BottomRight;

    [Tooltip("Size of map overlay (0-1, percentage of screen)")]
    [Range(0.1f, 1f)]
    public float mapScale = 0.3f;

    [Header("Drone Marker")]
    public Color droneMarkerColor = Color.red;
    public float droneMarkerSize = 15f;

    private GameObject mapCanvas;
    private RawImage mapImage;
    private RectTransform droneMarker;
    private GameObject groundPlane;
    private bool isVisible = true;
    private bool isLoading = false;

    public enum DisplayMode
    {
        UIOverlay,
        GroundPlane,
        Both
    }

    public enum MapType
    {
        roadmap,
        satellite,
        hybrid,
        terrain
    }

    public enum MapPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Center
    }

    void Start()
    {
        if (displayMode == DisplayMode.UIOverlay || displayMode == DisplayMode.Both)
        {
            CreateMapUI();
        }

        if (displayMode == DisplayMode.GroundPlane || displayMode == DisplayMode.Both)
        {
            CreateGroundPlane();
        }

        if (!string.IsNullOrEmpty(googleMapsAPIKey) && googleMapsAPIKey != "YOUR_API_KEY_HERE")
        {
            LoadMap();
        }
        else
        {
            Debug.LogWarning("MapOverlay: Please set your Google Maps API key in the Inspector!");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleMap();
        }

        // Update drone marker position based on current lat/lon
        UpdateDroneMarker();
    }

    void CreateMapUI()
    {
        // Create Canvas
        mapCanvas = new GameObject("MapOverlayCanvas");
        Canvas canvas = mapCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = mapCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        mapCanvas.AddComponent<GraphicRaycaster>();

        // Create map container panel
        GameObject mapPanel = new GameObject("MapPanel");
        mapPanel.transform.SetParent(mapCanvas.transform);
        RectTransform mapRect = mapPanel.AddComponent<RectTransform>();

        // Set position based on mapPosition setting
        SetMapPosition(mapRect);

        // Create background
        Image bgImage = mapPanel.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        // Create map image
        GameObject mapImageObj = new GameObject("MapImage");
        mapImageObj.transform.SetParent(mapPanel.transform);
        RectTransform imageRect = mapImageObj.AddComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = new Vector2(5, 5);
        imageRect.offsetMax = new Vector2(-5, -5);

        mapImage = mapImageObj.AddComponent<RawImage>();
        mapImage.color = Color.white;

        // Create drone marker
        GameObject markerObj = new GameObject("DroneMarker");
        markerObj.transform.SetParent(mapImageObj.transform);
        droneMarker = markerObj.AddComponent<RectTransform>();
        droneMarker.anchorMin = new Vector2(0.5f, 0.5f);
        droneMarker.anchorMax = new Vector2(0.5f, 0.5f);
        droneMarker.pivot = new Vector2(0.5f, 0.5f);
        droneMarker.anchoredPosition = Vector2.zero;
        droneMarker.sizeDelta = new Vector2(droneMarkerSize, droneMarkerSize);

        Image markerImage = markerObj.AddComponent<Image>();
        markerImage.color = droneMarkerColor;

        // Create loading text
        CreateLoadingText(mapPanel.transform);

        // Create instructions text
        CreateInstructionsText(mapPanel.transform);
    }

    void SetMapPosition(RectTransform rect)
    {
        float size = mapScale;
        float margin = 20f;

        switch (mapPosition)
        {
            case MapPosition.TopLeft:
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(margin, -margin);
                break;
            case MapPosition.TopRight:
                rect.anchorMin = new Vector2(1, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(1, 1);
                rect.anchoredPosition = new Vector2(-margin, -margin);
                break;
            case MapPosition.BottomLeft:
                rect.anchorMin = new Vector2(0, 0);
                rect.anchorMax = new Vector2(0, 0);
                rect.pivot = new Vector2(0, 0);
                rect.anchoredPosition = new Vector2(margin, margin);
                break;
            case MapPosition.BottomRight:
                rect.anchorMin = new Vector2(1, 0);
                rect.anchorMax = new Vector2(1, 0);
                rect.pivot = new Vector2(1, 0);
                rect.anchoredPosition = new Vector2(-margin, margin);
                break;
            case MapPosition.Center:
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                break;
        }

        rect.sizeDelta = new Vector2(300 * size / 0.3f, 300 * size / 0.3f);
    }

    void CreateLoadingText(Transform parent)
    {
        GameObject textObj = new GameObject("LoadingText");
        textObj.transform.SetParent(parent);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(200, 50);

        Text text = textObj.AddComponent<Text>();
        text.text = "Loading map...";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 16;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
    }

    void CreateInstructionsText(Transform parent)
    {
        GameObject textObj = new GameObject("InstructionsText");
        textObj.transform.SetParent(parent);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0);
        textRect.anchorMax = new Vector2(0.5f, 0);
        textRect.pivot = new Vector2(0.5f, 0);
        textRect.anchoredPosition = new Vector2(0, 5);
        textRect.sizeDelta = new Vector2(250, 30);

        Text text = textObj.AddComponent<Text>();
        text.text = $"Press {toggleKey} to toggle map";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 12;
        text.fontStyle = FontStyle.Italic;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.8f, 0.8f, 0.8f, 1f);
    }

    void CreateGroundPlane()
    {
        groundPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        groundPlane.name = "MapGroundPlane";
        groundPlane.transform.SetParent(transform);
        groundPlane.transform.position = Vector3.zero;

        // Unity plane is 10x10 units by default, scale to match desired size
        float scale = groundPlaneSize / 10f;
        groundPlane.transform.localScale = new Vector3(scale, 1, scale);

        // Create material with unlit shader
        Material mapMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mapMaterial.name = "MapGroundMaterial";
        groundPlane.GetComponent<Renderer>().material = mapMaterial;

        // Remove collider if not needed
        Collider collider = groundPlane.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
    }

    [ContextMenu("Load Map")]
    public void LoadMap()
    {
        if (!isLoading)
        {
            StartCoroutine(LoadMapTexture());
        }
    }

    IEnumerator LoadMapTexture()
    {
        isLoading = true;

        // Build Google Static Maps API URL
        string url = $"https://maps.googleapis.com/maps/api/staticmap?" +
                     $"center={centerLatitude},{centerLongitude}" +
                     $"&zoom={zoomLevel}" +
                     $"&size={mapSize}x{mapSize}" +
                     $"&maptype={mapType}" +
                     $"&key={googleMapsAPIKey}";

        Debug.Log($"MapOverlay: Loading map from URL (API key hidden)");

        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Texture2D mapTexture = DownloadHandlerTexture.GetContent(request);

            // Apply to UI overlay if it exists
            if (mapImage != null)
            {
                mapImage.texture = mapTexture;
            }

            // Apply to ground plane if it exists
            if (groundPlane != null)
            {
                Material groundMat = groundPlane.GetComponent<Renderer>().material;
                groundMat.mainTexture = mapTexture;
            }

            Debug.Log("MapOverlay: Map loaded successfully!");
        }
        else
        {
            Debug.LogError($"MapOverlay: Failed to load map - {request.error}");
            Debug.LogError("Check your API key and ensure 'Maps Static API' is enabled in Google Cloud Console");
        }

        isLoading = false;
    }

    void UpdateDroneMarker()
    {
        // Calculate pixel offset from center based on lat/lon difference
        double latDiff = droneLatitude - centerLatitude;
        double lonDiff = droneLongitude - centerLongitude;

        // Approximate conversion (meters per degree at given latitude)
        double metersPerDegreeLat = 111320;
        double metersPerDegreeLon = 111320 * Mathf.Cos((float)centerLatitude * Mathf.Deg2Rad);

        // Calculate meters offset
        double offsetMetersY = latDiff * metersPerDegreeLat;
        double offsetMetersX = lonDiff * metersPerDegreeLon;

        // Calculate pixels per meter based on zoom level
        // At zoom 20: ~0.15 meters/pixel, zoom 18: ~0.6 meters/pixel, etc.
        double metersPerPixel = 156543.03392 * Mathf.Cos((float)centerLatitude * Mathf.Deg2Rad) / Mathf.Pow(2, zoomLevel);

        float offsetPixelsX = (float)(offsetMetersX / metersPerPixel);
        float offsetPixelsY = (float)(offsetMetersY / metersPerPixel);

        // Update marker position
        if (droneMarker != null)
        {
            droneMarker.anchoredPosition = new Vector2(offsetPixelsX, offsetPixelsY);
        }
    }

    public void SetDronePosition(double latitude, double longitude)
    {
        droneLatitude = latitude;
        droneLongitude = longitude;
    }

    public void RecenterMap(double latitude, double longitude)
    {
        centerLatitude = latitude;
        centerLongitude = longitude;
        LoadMap();
    }

    void ToggleMap()
    {
        isVisible = !isVisible;

        if (mapCanvas != null)
        {
            mapCanvas.SetActive(isVisible);
        }

        if (groundPlane != null)
        {
            groundPlane.SetActive(isVisible);
        }
    }

    public void ShowMap()
    {
        isVisible = true;

        if (mapCanvas != null)
        {
            mapCanvas.SetActive(true);
        }

        if (groundPlane != null)
        {
            groundPlane.SetActive(true);
        }
    }

    public void HideMap()
    {
        isVisible = false;

        if (mapCanvas != null)
        {
            mapCanvas.SetActive(false);
        }

        if (groundPlane != null)
        {
            groundPlane.SetActive(false);
        }
    }
}
