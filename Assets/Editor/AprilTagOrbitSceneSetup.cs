using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool to create the AprilTagOrbitScene — a teaching environment where a single
/// AprilTag orbits around the drone. Students write their own ROS node (e.g. tag_yaw_track)
/// that makes the drone yaw to keep the tag centered in its forward-facing camera.
/// Access via menu: DEXI > Create AprilTag Orbit Scene
/// </summary>
public class AprilTagOrbitSceneSetup
{
    [MenuItem("DEXI/Create AprilTag Orbit Scene")]
    public static void CreateScene()
    {
        // Create a new empty scene
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- Lighting ---
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.957f, 0.839f, 1f);
        light.intensity = 1f;
        light.shadows = LightShadows.Soft;
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // --- Drone Field (cage) ---
        // The cage (floor, walls, markers) is generated at Play time. RuntimeMaterials
        // can't be used from editor scripts, so we set generateAtRuntime = true and
        // let DroneFieldGenerator.Awake() build the geometry when the scene runs.
        GameObject fieldObj = new GameObject("DroneField");
        DroneFieldGenerator field = fieldObj.AddComponent<DroneFieldGenerator>();
        field.generateAtRuntime = true;

        // --- Orbiting AprilTag ---
        // Create a quad with an AprilTag material and attach the orbit component.
        GameObject tagObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        tagObj.name = "OrbitingAprilTag";
        tagObj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f); // ~1 ft tag, easy to see

        // Assign a pre-baked AprilTag material (tag 00000) if available
        Material tagMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/AprilTags/apriltag_00000.mat");
        if (tagMat != null)
        {
            tagObj.GetComponent<Renderer>().sharedMaterial = tagMat;
        }
        else
        {
            Debug.LogWarning("AprilTag material not found at Assets/Materials/AprilTags/apriltag_00000.mat");
        }

        // Add the orbit behavior (target will be wired after the drone is instantiated)
        OrbitingAprilTag orbit = tagObj.AddComponent<OrbitingAprilTag>();
        orbit.radius = 2.5f;
        orbit.heightOffset = 0f;
        orbit.orbitSpeed = 0.3f;
        orbit.startAngleDegrees = 0f;    // directly in front of the drone
        orbit.autoStart = false;          // students press 'O' to start the orbit
        orbit.toggleKey = KeyCode.O;

        // --- DEXI Drone ---
        GameObject droneTransformRef = null;
        GameObject dexiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DEXI.prefab");
        if (dexiPrefab != null)
        {
            GameObject drone = (GameObject)PrefabUtility.InstantiatePrefab(dexiPrefab);
            drone.name = "DEXI";
            drone.transform.position = new Vector3(0f, 1.5f, 0f);
            droneTransformRef = drone;

            // --- Forward-facing Camera ---
            // A fresh camera child, pointing along the drone's +Z axis.
            // Offset forward past the drone body so the camera doesn't see its own frame,
            // and raised slightly so the top shell doesn't clip into view.
            GameObject fwdCamObj = new GameObject("ForwardCamera");
            fwdCamObj.transform.SetParent(drone.transform, false);
            fwdCamObj.transform.localPosition = new Vector3(0f, 0.25f, 0.25f);
            fwdCamObj.transform.localRotation = Quaternion.identity;

            Camera fwdCam = fwdCamObj.AddComponent<Camera>();
            fwdCam.fieldOfView = 48.8f;  // Pi Camera v2 vertical FOV
            fwdCam.nearClipPlane = 0.1f;
            fwdCam.farClipPlane = 20f;
            fwdCam.depth = -1;  // Render before main camera

            // Add ROS camera publisher so apriltag_ros can see the feed
            ROSCameraPublisher camPub = fwdCamObj.AddComponent<ROSCameraPublisher>();
            SerializedObject camPubSO = new SerializedObject(camPub);
            camPubSO.FindProperty("sourceCamera").objectReferenceValue = fwdCam;
            camPubSO.FindProperty("imageWidth").intValue = 640;
            camPubSO.FindProperty("imageHeight").intValue = 480;
            camPubSO.FindProperty("horizontalFOV").floatValue = 62f;
            camPubSO.FindProperty("publishRate").floatValue = 15f;
            camPubSO.ApplyModifiedPropertiesWithoutUndo();

            // --- Main Camera (follow cam, pulled back so students see whole scene) ---
            GameObject mainCamObj = new GameObject("Main Camera");
            mainCamObj.tag = "MainCamera";
            Camera mainCam = mainCamObj.AddComponent<Camera>();
            mainCam.fieldOfView = 60f;
            mainCam.nearClipPlane = 0.1f;
            mainCam.farClipPlane = 100f;
            mainCamObj.transform.position = new Vector3(0f, 4f, -5f);
            mainCamObj.transform.rotation = Quaternion.Euler(25f, 0f, 0f);

            DroneCamera droneCam = mainCamObj.AddComponent<DroneCamera>();
            if (droneCam != null)
            {
                // Directly assign the public Transform field — more reliable than
                // SerializedObject plumbing for a public field.
                droneCam.target = drone.transform;
            }
        }
        else
        {
            Debug.LogWarning("DEXI prefab not found at Assets/Prefabs/DEXI.prefab - add drone manually");
            GameObject mainCamObj = new GameObject("Main Camera");
            mainCamObj.tag = "MainCamera";
            Camera mainCam = mainCamObj.AddComponent<Camera>();
            mainCam.fieldOfView = 60f;
            mainCamObj.transform.position = new Vector3(0f, 4f, -5f);
            mainCamObj.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
        }

        // Wire the orbit target to the drone transform
        if (droneTransformRef != null)
        {
            orbit.target = droneTransformRef.transform;
            // Place the tag at its starting orbit position for the editor preview
            Vector3 startOffset = new Vector3(
                Mathf.Sin(orbit.startAngleDegrees * Mathf.Deg2Rad) * orbit.radius,
                orbit.heightOffset,
                Mathf.Cos(orbit.startAngleDegrees * Mathf.Deg2Rad) * orbit.radius
            );
            tagObj.transform.position = droneTransformRef.transform.position + startOffset;
            // Point the quad's +Z away from the drone so the textured face looks at it.
            tagObj.transform.rotation = Quaternion.LookRotation(tagObj.transform.position - droneTransformRef.transform.position);
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

        // --- Picture in Picture (forward-camera view) ---
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
        string scenePath = "Assets/Scenes/AprilTagOrbitScene.unity";
        EditorSceneManager.SaveScene(newScene, scenePath);
        AddSceneToBuildSettings(scenePath);

        Debug.Log($"AprilTagOrbitScene created and saved to {scenePath}");

        EditorUtility.DisplayDialog(
            "Scene Created",
            "AprilTagOrbitScene has been created!\n\n" +
            "Setup:\n" +
            "• DEXI drone with forward-facing camera\n" +
            "• AprilTag (tag 0) starts stationary in front of the drone\n" +
            "• Press 'O' at runtime to toggle the orbit on/off\n" +
            "• Orbit speed 0.3 rad/s at 2.5m radius\n" +
            "• ROS camera feed publishing at 15Hz\n\n" +
            "Hit Play to see the drone field cage (it generates at runtime).\n\n" +
            "Challenge: write a ROS node (e.g. tag_follow) that subscribes to\n" +
            "/apriltag_detections and publishes yaw rate commands to keep\n" +
            "the tag centered in the drone's forward camera.\n\n" +
            "Tune radius, orbit speed, and height on the OrbitingAprilTag component.",
            "OK"
        );
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
        Debug.Log($"Added {scenePath} to build settings");
    }
}
