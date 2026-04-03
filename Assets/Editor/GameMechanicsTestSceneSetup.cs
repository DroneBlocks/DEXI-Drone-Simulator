using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool to create the Game Mechanics MVP test scene.
/// Flat ground with 3 AprilTags, 3 YOLO image holders, and 1 landing zone.
/// Access via menu: DEXI > Create Game Mechanics Test Scene
/// </summary>
public class GameMechanicsTestSceneSetup
{
    // 6 inches in meters
    private const float TAG_SIZE = 0.1524f;

    // Spacing between targets in a row (1.5m forces navigation between targets)
    private const float TARGET_SPACING = 1.5f;

    [MenuItem("DEXI/Create Game Mechanics Test Scene")]
    public static void CreateScene()
    {
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- Lighting ---
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.957f, 0.839f, 1f);
        light.intensity = 1f;
        light.shadows = LightShadows.Soft;
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // --- Ground Plane ---
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0, -0.005f, 0);
        ground.transform.localScale = new Vector3(8f, 0.01f, 12f);
        // Dark gray floor — material will be set at runtime by RuntimeMaterials

        // --- AprilTag Zone (3 tags in a row along X, at Z = -2) ---
        float aprilTagZ = -2.0f;
        CreateSectionLabel("AprilTags", new Vector3(0, 0.01f, aprilTagZ - 0.4f));

        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * TARGET_SPACING; // -0.5, 0, 0.5
            int tagId = i; // Use tag IDs 0, 1, 2
            CreateAprilTag(tagId, new Vector3(x, 0.001f, aprilTagZ), i);
        }

        // --- YOLO Image Zones ---
        float yoloZ = 0.0f;

        // Vehicle images (bridge scan targets in the game design)
        // LED colors: Car = Red, Truck = Green, Motorcycle = Blue
        string[] vehicleNames = { "Car", "Truck", "Motorcycle" };
        string[] vehicleResources = { "yolo_car", "yolo_truck", "yolo_motorcycle" };
        Color[] vehicleColors = { Color.red, Color.green, Color.blue };

        CreateSectionLabel("Vehicles (Bridge)", new Vector3(0, 0.01f, yoloZ - 0.4f));
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * TARGET_SPACING;
            CreateYoloImageHolder(vehicleNames[i], vehicleResources[i], "Vehicles (Bridge)",
                vehicleColors[i], new Vector3(x, 0.001f, yoloZ), i);
        }

        // Animal images (cabin scan targets in the game design)
        // LED colors: Cat = Red, Dog = Green, Bird = Blue
        float animalZ = 2.0f;
        string[] animalNames = { "Cat", "Dog", "Bird" };
        string[] animalResources = { "yolo_cat", "yolo_dog", "yolo_bird" };
        Color[] animalColors = { Color.red, Color.green, Color.blue };

        CreateSectionLabel("Animals (Cabin)", new Vector3(0, 0.01f, animalZ - 0.4f));
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * TARGET_SPACING;
            CreateYoloImageHolder(animalNames[i], animalResources[i], "Animals (Cabin)",
                animalColors[i], new Vector3(x, 0.001f, animalZ), i);
        }

        // --- Fly-Through Gates (between animals and landing zone) ---
        float gateZ = 3.0f;
        CreateSectionLabel("Gates", new Vector3(0, 0.01f, gateZ - 0.4f));

        // Gate 1 — offset left, facing Z direction (drone flies through along Z axis)
        GameObject gate1 = new GameObject("Gate_1");
        gate1.transform.position = new Vector3(-0.75f, 0f, gateZ);
        gate1.transform.rotation = Quaternion.identity; // Opening faces +Z/-Z
        FlyThroughGate ftg1 = gate1.AddComponent<FlyThroughGate>();
        ftg1.gateName = "Gate 1";

        // Gate 2 — offset right
        GameObject gate2 = new GameObject("Gate_2");
        gate2.transform.position = new Vector3(0.75f, 0f, gateZ);
        gate2.transform.rotation = Quaternion.identity;
        FlyThroughGate ftg2 = gate2.AddComponent<FlyThroughGate>();
        ftg2.gateName = "Gate 2";

        // --- Landing Zone (at Z = 4.5) ---
        float landingZ = 4.5f;
        CreateSectionLabel("Landing Zone", new Vector3(0, 0.01f, landingZ - 0.4f));
        CreateLandingZone(new Vector3(0, 0.001f, landingZ));

        // --- DEXI Drone ---
        GameObject dexiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DEXI.prefab");
        GameObject drone;
        if (dexiPrefab != null)
        {
            drone = (GameObject)PrefabUtility.InstantiatePrefab(dexiPrefab);
            drone.name = "DEXI";
            drone.transform.position = new Vector3(0f, 0.5f, -4.0f);

            // Add keyboard controller for testing without PX4/ROS
            if (drone.GetComponent<KeyboardDroneController>() == null)
                drone.AddComponent<KeyboardDroneController>();
        }
        else
        {
            Debug.LogWarning("DEXI prefab not found at Assets/Prefabs/DEXI.prefab — add drone manually");
            drone = new GameObject("DEXI_Placeholder");
            drone.transform.position = new Vector3(0f, 0.5f, -4.0f);
            drone.AddComponent<KeyboardDroneController>();
        }

        // --- Main Camera ---
        GameObject mainCamObj = new GameObject("Main Camera");
        mainCamObj.tag = "MainCamera";
        Camera mainCam = mainCamObj.AddComponent<Camera>();
        mainCam.fieldOfView = 60f;
        mainCam.nearClipPlane = 0.1f;
        mainCam.farClipPlane = 100f;
        mainCamObj.transform.position = new Vector3(0f, 3f, -4f);
        mainCamObj.transform.rotation = Quaternion.Euler(35f, 0f, 0f);

        // Add DroneCamera follow script
        DroneCamera droneCam = mainCamObj.AddComponent<DroneCamera>();
        SerializedObject droneCamSO = new SerializedObject(droneCam);
        var targetProp = droneCamSO.FindProperty("target");
        if (targetProp != null)
        {
            targetProp.objectReferenceValue = drone.transform;
            droneCamSO.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- Downward Camera ---
        GameObject downCamObj = new GameObject("DownwardCamera");
        Camera downCam = downCamObj.AddComponent<Camera>();
        downCam.fieldOfView = 48.8f;
        downCam.nearClipPlane = 0.1f;
        downCam.farClipPlane = 10f;
        downCam.depth = -1;

        DownwardCamera downCamScript = downCamObj.AddComponent<DownwardCamera>();
        SerializedObject downCamSO = new SerializedObject(downCamScript);
        downCamSO.FindProperty("target").objectReferenceValue = drone.transform;
        downCamSO.ApplyModifiedPropertiesWithoutUndo();

        // ROS camera publisher
        ROSCameraPublisher camPub = downCamObj.AddComponent<ROSCameraPublisher>();
        SerializedObject camPubSO = new SerializedObject(camPub);
        camPubSO.FindProperty("sourceCamera").objectReferenceValue = downCam;
        camPubSO.FindProperty("imageWidth").intValue = 640;
        camPubSO.FindProperty("imageHeight").intValue = 480;
        camPubSO.FindProperty("horizontalFOV").floatValue = 62f;
        camPubSO.FindProperty("publishRate").floatValue = 15f;
        camPubSO.ApplyModifiedPropertiesWithoutUndo();

        // --- Managers ---
        // ROS Bridge
        GameObject rosObj = new GameObject("ROSBridgeManager");
        ROSBridgeManager rosBridge = rosObj.AddComponent<ROSBridgeManager>();
        SerializedObject rosSO = new SerializedObject(rosBridge);
        rosSO.FindProperty("autoConnectOnStart").boolValue = true;
        rosSO.FindProperty("maxRetryAttempts").intValue = 10;
        rosSO.FindProperty("retryDelaySeconds").floatValue = 5f;
        rosSO.ApplyModifiedPropertiesWithoutUndo();

        // PX4 State Manager
        GameObject px4Obj = new GameObject("PX4StateManager");
        px4Obj.AddComponent<PX4StateManager>();

        // Game Manager
        GameObject gmObj = new GameObject("GameManager");
        gmObj.AddComponent<GameManager>();
        gmObj.AddComponent<LEDColorValidator>();

        // Runtime Materials
        GameObject rtMatObj = new GameObject("RuntimeMaterials");
        rtMatObj.AddComponent<RuntimeMaterials>();

        // PiP Camera
        GameObject pipObj = new GameObject("PictureInPicture");
        pipObj.AddComponent<PictureInPictureCamera>();

        // Scene Switcher
        GameObject switcherObj = new GameObject("SceneSwitcher");
        switcherObj.AddComponent<SceneSwitcher>();

        // Event System
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // --- Save Scene ---
        string scenePath = "Assets/Scenes/GameMechanicsTest.unity";
        EditorSceneManager.SaveScene(newScene, scenePath);
        AddSceneToBuildSettings(scenePath);

        Debug.Log($"GameMechanicsTest scene created at {scenePath}");

        EditorUtility.DisplayDialog(
            "Game Mechanics Test Scene Created",
            "Scene layout:\n\n" +
            "3x AprilTags (6\"x6\") at Z=-1.0\n" +
            "  - Tags 0, 1, 2 — one randomized as REAL\n\n" +
            "3x Vehicle Images (6\"x6\") at Z=0.5\n" +
            "  - Car, Truck, Motorcycle — one randomized as REAL\n\n" +
            "3x Animal Images (6\"x6\") at Z=1.25\n" +
            "  - Cat, Dog, Bird — one randomized as REAL\n\n" +
            "1x Landing Zone at Z=2.5\n\n" +
            "DEXI drone at Z=-2.5\n\n" +
            "Press Play, then fly over targets to scan.\n" +
            "Game auto-starts on first scan.\n" +
            "[R] to reset, [Space] to start manually.",
            "OK"
        );
    }

    static void CreateAprilTag(int tagId, Vector3 position, int indexInGroup)
    {
        // Create a quad facing up
        GameObject tag = GameObject.CreatePrimitive(PrimitiveType.Quad);
        tag.name = $"AprilTag_{tagId}";
        tag.transform.position = position;
        tag.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Face up
        tag.transform.localScale = new Vector3(TAG_SIZE, TAG_SIZE, TAG_SIZE);

        // Load the AprilTag material
        string matPath = $"Assets/Materials/AprilTags/apriltag_{tagId:D5}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat != null)
        {
            tag.GetComponent<Renderer>().sharedMaterial = mat;
        }
        else
        {
            Debug.LogWarning($"AprilTag material not found at {matPath}");
        }

        // Add ScanTarget component
        ScanTarget scanTarget = tag.AddComponent<ScanTarget>();
        scanTarget.groupName = "apriltags";
        scanTarget.targetName = $"AprilTag #{tagId}";
        scanTarget.targetType = ScanTarget.TargetType.AprilTag;

        // LED color mapping: Tag 0 = Red, Tag 1 = Green, Tag 2 = Blue
        Color[] tagColors = { Color.red, Color.green, Color.blue };
        scanTarget.expectedLEDColor = tagColors[tagId];

        // Add AprilTagInfo for compatibility with existing detection
        AprilTagInfo tagInfo = tag.AddComponent<AprilTagInfo>();
        tagInfo.tagId = tagId;
        tagInfo.gridPosition = new Vector2Int(indexInGroup, 0);
    }

    static void CreateYoloImageHolder(string name, string resourceName, string groupName,
        Color expectedLEDColor, Vector3 position, int indexInGroup)
    {
        // Create a quad facing up
        GameObject holder = GameObject.CreatePrimitive(PrimitiveType.Quad);
        holder.name = $"YOLO_{name}";
        holder.transform.position = position;
        holder.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Face up
        holder.transform.localScale = new Vector3(TAG_SIZE, TAG_SIZE, TAG_SIZE);

        // Load texture from Textures/YoloTargets/
        string texAssetPath = $"Assets/Textures/YoloTargets/{resourceName}.png";
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texAssetPath);

        if (tex != null)
        {
            // Ensure proper import settings
            TextureImporter importer = AssetImporter.GetAtPath(texAssetPath) as TextureImporter;
            if (importer != null)
            {
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texAssetPath);
            }
        }
        else
        {
            Debug.LogWarning($"YOLO texture not found at {texAssetPath}");
        }

        // Create material
        EnsureDirectoryExists("Assets/Materials/YoloTargets");
        string matPath = $"Assets/Materials/YoloTargets/{resourceName}.mat";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.name = $"{resourceName}_Mat";
            if (tex != null) mat.mainTexture = tex;
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else if (tex != null)
        {
            mat.mainTexture = tex;
        }

        holder.GetComponent<Renderer>().sharedMaterial = mat;

        // Add ScanTarget component
        ScanTarget scanTarget = holder.AddComponent<ScanTarget>();
        scanTarget.groupName = groupName;
        scanTarget.targetName = name;
        scanTarget.targetType = ScanTarget.TargetType.YoloImage;
        scanTarget.expectedLEDColor = expectedLEDColor;
    }

    static void CreateLandingZone(Vector3 position)
    {
        // Flat pad on the ground
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "LandingPad";
        pad.transform.position = position;
        pad.transform.localScale = new Vector3(0.3f, 0.005f, 0.3f);

        // Green-ish material
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.2f, 0.8f, 0.2f, 1f);
        mat.name = "LandingPad_Mat";

        string matPath = "Assets/Materials/LandingPad_Mat.mat";
        AssetDatabase.CreateAsset(mat, matPath);
        pad.GetComponent<Renderer>().sharedMaterial = mat;

        // Add a larger trigger collider for detection
        // The existing box collider is the visual pad; add a separate trigger above it
        BoxCollider trigger = pad.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0, 30f, 0); // 0.15m above the pad (scaled: 30 * 0.005 = 0.15m)
        trigger.size = new Vector3(1f, 60f, 1f); // tall enough to catch the drone descending (60 * 0.005 = 0.3m)

        // Add LandingZone component
        LandingZone lz = pad.AddComponent<LandingZone>();
        lz.zoneName = "Landing Pad";
    }

    static void CreateSectionLabel(string text, Vector3 position)
    {
        GameObject labelObj = new GameObject($"Label_{text}");
        labelObj.transform.position = position;

        TextMesh textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.fontSize = 32;
        textMesh.characterSize = 0.04f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;

        // Face up
        labelObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

static void EnsureDirectoryExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var scene in scenes)
        {
            if (scene.path == scenePath) return;
        }
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
