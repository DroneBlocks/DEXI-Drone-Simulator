using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool to create DEXIGridScene with a 10x10 AprilTag grid.
/// Access via menu: DEXI > Create DEXI Grid Scene 2
/// </summary>
public class DEXIGridSceneSetup
{
    [MenuItem("DEXI/Create DEXI Grid Scene")]
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
        // 12m x 12m dark floor (covers 10x10 grid with margin)
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0, -0.005f, 0);
        ground.transform.localScale = new Vector3(14f, 0.01f, 14f);
        Material groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        groundMat.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        ground.GetComponent<Renderer>().material = groundMat;

        // --- AprilTag Grid (10x10) ---
        GameObject tagSource = GameObject.CreatePrimitive(PrimitiveType.Quad);
        tagSource.name = "AprilTagSource";
        tagSource.transform.position = Vector3.zero;
        tagSource.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Face up
        tagSource.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);

        AprilTagGridGenerator tagGen = tagSource.AddComponent<AprilTagGridGenerator>();
        tagGen.gridCountX = 10;
        tagGen.gridCountZ = 10;
        tagGen.spacingX = 1.0f;
        tagGen.spacingZ = 1.0f;
        tagGen.startingTagId = 0;
        tagGen.centerGrid = true;  // Centered — drone starts at center
        tagGen.heightOffset = 0.001f;
        tagGen.tagSize = 0.15f;   // 6-inch tags

        // --- Helipad (off-center for YOLO landing detection) ---
        GameObject helipad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        helipad.name = "Helipad";
        helipad.transform.position = new Vector3(3f, 0.002f, 3f); // Off-center
        helipad.transform.localScale = new Vector3(0.6f, 0.001f, 0.6f);

        // Try to load helipad material
        Material helipadMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/HelipadMaterial.mat");
        if (helipadMat != null)
        {
            helipad.GetComponent<Renderer>().material = helipadMat;
        }
        else
        {
            // Fallback: orange pad
            Material fallbackMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            fallbackMat.color = new Color(1f, 0.5f, 0f, 1f);
            helipad.GetComponent<Renderer>().material = fallbackMat;
        }

        // --- DEXI Drone ---
        GameObject dexiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DEXI.prefab");
        GameObject drone = null;
        if (dexiPrefab != null)
        {
            drone = (GameObject)PrefabUtility.InstantiatePrefab(dexiPrefab);
            drone.name = "DEXI";
            drone.transform.position = new Vector3(0f, 0.5f, 0f); // Center of grid
        }
        else
        {
            Debug.LogWarning("DEXI prefab not found at Assets/Prefabs/DEXI.prefab");
        }

        // --- Downward Camera ---
        GameObject downCamObj = new GameObject("DownwardCamera");
        Camera downCam = downCamObj.AddComponent<Camera>();
        downCam.fieldOfView = 48.8f; // Pi Camera v2 vertical FOV
        downCam.nearClipPlane = 0.1f;
        downCam.farClipPlane = 10f;
        downCam.depth = -1;

        DownwardCamera downCamScript = downCamObj.AddComponent<DownwardCamera>();
        if (drone != null)
        {
            SerializedObject so = new SerializedObject(downCamScript);
            so.FindProperty("target").objectReferenceValue = drone.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ROS camera publisher on downward camera
        ROSCameraPublisher camPub = downCamObj.AddComponent<ROSCameraPublisher>();
        SerializedObject camPubSO = new SerializedObject(camPub);
        camPubSO.FindProperty("sourceCamera").objectReferenceValue = downCam;
        camPubSO.FindProperty("imageWidth").intValue = 640;
        camPubSO.FindProperty("imageHeight").intValue = 480;
        camPubSO.FindProperty("horizontalFOV").floatValue = 62f;
        camPubSO.FindProperty("publishRate").floatValue = 15f;
        camPubSO.ApplyModifiedPropertiesWithoutUndo();

        // PIP camera on downward camera
        downCamObj.AddComponent<PictureInPictureCamera>();

        // --- Main Camera ---
        GameObject mainCamObj = new GameObject("Main Camera");
        mainCamObj.tag = "MainCamera";
        Camera mainCam = mainCamObj.AddComponent<Camera>();
        mainCam.fieldOfView = 60f;
        mainCam.nearClipPlane = 0.1f;
        mainCam.farClipPlane = 100f;
        mainCamObj.transform.position = new Vector3(0f, 5f, -6f);
        mainCamObj.transform.rotation = Quaternion.Euler(30f, 0f, 0f);

        DroneCamera droneCam = mainCamObj.AddComponent<DroneCamera>();
        if (drone != null)
        {
            SerializedObject droneCamSO = new SerializedObject(droneCam);
            var targetProp = droneCamSO.FindProperty("target");
            if (targetProp != null)
            {
                targetProp.objectReferenceValue = drone.transform;
                droneCamSO.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        // Set orbit speed to match AVR2025
        SerializedObject droneCamSO2 = new SerializedObject(droneCam);
        var orbitProp = droneCamSO2.FindProperty("orbitSpeed");
        if (orbitProp != null)
        {
            orbitProp.floatValue = 3000f;
            droneCamSO2.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- Runtime Materials (WebGL-safe shader references) ---
        GameObject matObj = new GameObject("RuntimeMaterials");
        RuntimeMaterials rtMats = matObj.AddComponent<RuntimeMaterials>();
        Material unlitMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/URPUnlit.mat");
        Material litMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/URPLit.mat");
        if (unlitMat != null && litMat != null)
        {
            SerializedObject matSO = new SerializedObject(rtMats);
            matSO.FindProperty("unlitBase").objectReferenceValue = unlitMat;
            matSO.FindProperty("litBase").objectReferenceValue = litMat;
            matSO.ApplyModifiedPropertiesWithoutUndo();
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

        // --- Scene Switcher ---
        GameObject switcherObj = new GameObject("SceneSwitcher");
        switcherObj.AddComponent<SceneSwitcher>();

        // --- Event System ---
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // --- Grid World (visual reference lines) ---
        GameObject gridWorldObj = new GameObject("GridWorld");
        GridWorldGenerator gridWorld = gridWorldObj.AddComponent<GridWorldGenerator>();
        // Scale down to match the tag grid area
        SerializedObject gridSO = new SerializedObject(gridWorld);
        gridSO.FindProperty("gridSize").intValue = 12;
        gridSO.FindProperty("gridSpacing").floatValue = 1f;
        gridSO.FindProperty("majorLineInterval").intValue = 5;
        gridSO.FindProperty("create3DGrid").boolValue = false;
        gridSO.FindProperty("showCoordinateLabels").boolValue = true;
        gridSO.FindProperty("labelInterval").intValue = 5;
        gridSO.FindProperty("createLandingPads").boolValue = false;
        gridSO.FindProperty("createColoredZones").boolValue = false;
        gridSO.ApplyModifiedPropertiesWithoutUndo();

        // Save the scene
        string scenePath = "Assets/Scenes/DEXIGridScene.unity";
        EditorSceneManager.SaveScene(newScene, scenePath);

        // Add to build settings
        AddSceneToBuildSettings(scenePath);

        Debug.Log($"DEXIGridScene created at {scenePath}");
        Debug.Log("10x10 AprilTag grid (100 tags, 6\" size, 1m spacing, centered)");
        Debug.Log("Helipad at (3, 0, 3) for YOLO landing detection");
        Debug.Log("Press Play to see the environment.");

        EditorUtility.DisplayDialog(
            "Scene Created",
            "DEXIGridScene has been created!\n\n" +
            "Grid: 10x10 AprilTags (100 tags, 6\" size)\n" +
            "Spacing: 1m x 1m (9m x 9m area)\n" +
            "Drone: Centered in grid\n" +
            "Helipad: Off-center at (3, 0, 3)\n" +
            "Camera: Pi Cam v2 (640x480, 62° HFOV)\n\n" +
            "Press Play to see the environment.",
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
    }
}
