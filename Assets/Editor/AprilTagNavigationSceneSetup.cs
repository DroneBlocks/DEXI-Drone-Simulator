using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool to create the AprilTagNavigationScene with all required objects.
/// Access via menu: DEXI > Create AprilTag Navigation Scene
/// </summary>
public class AprilTagNavigationSceneSetup
{
    [MenuItem("DEXI/Create AprilTag Navigation Scene")]
    public static void CreateScene()
    {
        // Create a new empty scene
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- Lighting ---
        // Directional light
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.957f, 0.839f, 1f);
        light.intensity = 1f;
        light.shadows = LightShadows.Soft;
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // --- Drone Field (cage) ---
        GameObject fieldObj = new GameObject("DroneField");
        DroneFieldGenerator field = fieldObj.AddComponent<DroneFieldGenerator>();
        // Defaults are already 15x15x40

        // --- AprilTag Grid ---
        // Create source tag object (a flat quad)
        GameObject tagSource = GameObject.CreatePrimitive(PrimitiveType.Quad);
        tagSource.name = "AprilTagSource";
        tagSource.transform.position = Vector3.zero;
        // Face up (quad faces +Z by default, rotate to face +Y)
        tagSource.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        // 6-inch tag = 0.15m (will be overridden by tagSize in generator)
        tagSource.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);

        AprilTagGridGenerator tagGen = tagSource.AddComponent<AprilTagGridGenerator>();
        tagGen.gridCountX = 2;   // 2 tags wide (narrow strip)
        tagGen.gridCountZ = 10;  // 10 tags along flight path (Z = length)
        tagGen.spacingX = 0.5f;  // 0.5m between rows (width)
        tagGen.spacingZ = 1.0f;  // 1.0m between tags along flight path
        tagGen.startingTagId = 0;
        tagGen.centerGrid = true;
        tagGen.heightOffset = 0.001f; // Slightly above floor
        tagGen.tagSize = 0.15f;  // 6-inch tags
        // Textures auto-loaded from Resources/AprilTags/

        // --- DEXI Drone ---
        GameObject dexiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DEXI.prefab");
        if (dexiPrefab != null)
        {
            GameObject drone = (GameObject)PrefabUtility.InstantiatePrefab(dexiPrefab);
            drone.name = "DEXI";
            // Start at one end of the field, on the ground
            float fieldHalfLength = 40f * 0.3048f / 2f;
            drone.transform.position = new Vector3(0f, 0.5f, -fieldHalfLength + 1f);

            // --- Downward Camera ---
            // Check if drone already has a downward camera child
            Camera existingDownCam = null;
            foreach (Transform child in drone.GetComponentsInChildren<Transform>())
            {
                DownwardCamera dc = child.GetComponent<DownwardCamera>();
                if (dc != null) existingDownCam = child.GetComponent<Camera>();
            }

            if (existingDownCam == null)
            {
                GameObject downCamObj = new GameObject("DownwardCamera");
                downCamObj.transform.SetParent(null); // Keep at root, it follows via script
                Camera downCam = downCamObj.AddComponent<Camera>();
                downCam.fieldOfView = 48.8f; // Pi Camera v2 vertical FOV
                downCam.nearClipPlane = 0.1f;
                downCam.farClipPlane = 10f;
                downCam.depth = -1; // Render before main camera

                DownwardCamera downCamScript = downCamObj.AddComponent<DownwardCamera>();
                // Set target via serialized field
                SerializedObject so = new SerializedObject(downCamScript);
                so.FindProperty("target").objectReferenceValue = drone.transform;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Add ROS camera publisher
                ROSCameraPublisher camPub = downCamObj.AddComponent<ROSCameraPublisher>();
                SerializedObject camPubSO = new SerializedObject(camPub);
                camPubSO.FindProperty("sourceCamera").objectReferenceValue = downCam;
                camPubSO.FindProperty("imageWidth").intValue = 640;
                camPubSO.FindProperty("imageHeight").intValue = 480;
                camPubSO.FindProperty("horizontalFOV").floatValue = 62f;
                camPubSO.FindProperty("publishRate").floatValue = 15f;
                camPubSO.ApplyModifiedPropertiesWithoutUndo();
            }

            // --- Main Camera (DroneCamera follow cam) ---
            GameObject mainCamObj = new GameObject("Main Camera");
            mainCamObj.tag = "MainCamera";
            Camera mainCam = mainCamObj.AddComponent<Camera>();
            mainCam.fieldOfView = 60f;
            mainCam.nearClipPlane = 0.1f;
            mainCam.farClipPlane = 100f;
            // Position behind and above the drone
            mainCamObj.transform.position = new Vector3(0f, 3f, -fieldHalfLength - 2f);
            mainCamObj.transform.rotation = Quaternion.Euler(20f, 0f, 0f);

            // Add DroneCamera script if it exists
            DroneCamera droneCam = mainCamObj.AddComponent<DroneCamera>();
            if (droneCam != null)
            {
                SerializedObject droneCamSO = new SerializedObject(droneCam);
                var targetProp = droneCamSO.FindProperty("target");
                if (targetProp != null)
                {
                    targetProp.objectReferenceValue = drone.transform;
                    droneCamSO.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }
        else
        {
            Debug.LogWarning("DEXI prefab not found at Assets/Prefabs/DEXI.prefab - add drone manually");

            // Still create a main camera
            GameObject mainCamObj = new GameObject("Main Camera");
            mainCamObj.tag = "MainCamera";
            Camera mainCam = mainCamObj.AddComponent<Camera>();
            mainCam.fieldOfView = 60f;
            mainCamObj.transform.position = new Vector3(0f, 5f, -8f);
            mainCamObj.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
        }

        // --- ROS Bridge Manager ---
        GameObject rosObj = new GameObject("ROSBridgeManager");
        ROSBridgeManager rosBridge = rosObj.AddComponent<ROSBridgeManager>();
        SerializedObject rosSO = new SerializedObject(rosBridge);
        rosSO.FindProperty("autoConnectOnStart").boolValue = true;
        rosSO.FindProperty("maxRetryAttempts").intValue = 10;
        rosSO.FindProperty("retryDelaySeconds").floatValue = 5f;
        rosSO.ApplyModifiedPropertiesWithoutUndo();

        // --- PX4 State Manager ---
        GameObject px4Obj = new GameObject("PX4StateManager");
        px4Obj.AddComponent<PX4StateManager>();

        // --- Picture in Picture (downward camera view) ---
        GameObject pipObj = new GameObject("PictureInPicture");
        pipObj.AddComponent<PictureInPictureCamera>();

        // --- Scene Switcher ---
        GameObject switcherObj = new GameObject("SceneSwitcher");
        switcherObj.AddComponent<SceneSwitcher>();

        // --- Event System (for UI) ---
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // Save the scene
        string scenePath = "Assets/Scenes/AprilTagNavigationScene.unity";
        EditorSceneManager.SaveScene(newScene, scenePath);

        // Add to build settings
        AddSceneToBuildSettings(scenePath);

        Debug.Log($"AprilTagNavigationScene created and saved to {scenePath}");
        Debug.Log("Scene layout: 15'x15'x40' cage with 2x10 AprilTag grid (6\" tags, 1m spacing along Z)");
        Debug.Log("IMPORTANT: Hit Play to see the generated environment (DroneFieldGenerator and AprilTagGridGenerator run at Start)");

        EditorUtility.DisplayDialog(
            "Scene Created",
            "AprilTagNavigationScene has been created!\n\n" +
            "Field: 15' x 15' x 40' cage\n" +
            "Tags: 20 AprilTags (2x10 grid, 6\" size)\n" +
            "Spacing: 0.5m x 1.0m\n" +
            "Camera: Pi Cam v2 (640x480, 62° HFOV)\n\n" +
            "Press Play to see the environment.",
            "OK"
        );
    }

    static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        // Check if already in build settings
        foreach (var scene in scenes)
        {
            if (scene.path == scenePath) return;
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"Added {scenePath} to build settings");
    }
}
