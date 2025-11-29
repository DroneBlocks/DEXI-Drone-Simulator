using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

public class SceneSwitcher : MonoBehaviour
{
    [Header("UI Settings")]
    [Tooltip("Press this key to toggle the scene switcher")]
    public KeyCode toggleKey = KeyCode.Space;

    [Header("UI Appearance")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.85f);
    public Color buttonColor = new Color(0.2f, 0.6f, 0.9f, 1f);
    public Color buttonHoverColor = new Color(0.3f, 0.7f, 1f, 1f);
    public Color currentSceneColor = new Color(0.3f, 0.9f, 0.3f, 1f);
    public int fontSize = 18;

    private GameObject uiCanvas;
    private bool isVisible = false;

    void Start()
    {
        CreateSceneSwitcherUI();
        HideUI();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleUI();
        }
    }

    void CreateSceneSwitcherUI()
    {
        // Create Canvas
        uiCanvas = new GameObject("SceneSwitcherUI");
        Canvas canvas = uiCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // Ensure it's on top

        CanvasScaler scaler = uiCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        uiCanvas.AddComponent<GraphicRaycaster>();

        // Create background panel
        GameObject bgPanel = new GameObject("Background");
        bgPanel.transform.SetParent(uiCanvas.transform);
        RectTransform bgRect = bgPanel.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        Image bgImage = bgPanel.AddComponent<Image>();
        bgImage.color = backgroundColor;

        // Create content panel
        GameObject contentPanel = new GameObject("ContentPanel");
        contentPanel.transform.SetParent(uiCanvas.transform);
        RectTransform contentRect = contentPanel.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(600, 400);

        // Add vertical layout
        VerticalLayoutGroup layout = contentPanel.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        // Create title
        CreateTitle(contentPanel.transform);

        // Create scene buttons
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        string currentSceneName = SceneManager.GetActiveScene().name;

        for (int i = 0; i < sceneCount; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

            bool isCurrentScene = (sceneName == currentSceneName);
            CreateSceneButton(contentPanel.transform, sceneName, i, isCurrentScene);
        }

        // Create close instruction
        CreateInstructions(contentPanel.transform);
    }

    void CreateTitle(Transform parent)
    {
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(parent);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(0, 50);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "SCENE SWITCHER";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = fontSize + 8;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
    }

    void CreateSceneButton(Transform parent, string sceneName, int sceneIndex, bool isCurrentScene)
    {
        GameObject buttonObj = new GameObject($"Button_{sceneName}");
        buttonObj.transform.SetParent(parent);
        RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(0, 50);

        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = isCurrentScene ? currentSceneColor : buttonColor;

        Button button = buttonObj.AddComponent<Button>();

        // Set button colors
        ColorBlock colors = button.colors;
        colors.normalColor = isCurrentScene ? currentSceneColor : buttonColor;
        colors.highlightedColor = buttonHoverColor;
        colors.pressedColor = new Color(0.1f, 0.4f, 0.7f, 1f);
        button.colors = colors;

        // Add button click listener
        int index = sceneIndex; // Capture for closure
        button.onClick.AddListener(() => LoadScene(index));

        // Create button text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text buttonText = textObj.AddComponent<Text>();
        buttonText.text = isCurrentScene ? $"{sceneName} (Current)" : sceneName;
        buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.fontSize = fontSize;
        buttonText.fontStyle = isCurrentScene ? FontStyle.Bold : FontStyle.Normal;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.white;

        // Disable button if it's the current scene
        if (isCurrentScene)
        {
            button.interactable = false;
        }
    }

    void CreateInstructions(Transform parent)
    {
        GameObject instructionObj = new GameObject("Instructions");
        instructionObj.transform.SetParent(parent);
        RectTransform instructionRect = instructionObj.AddComponent<RectTransform>();
        instructionRect.sizeDelta = new Vector2(0, 40);

        Text instructionText = instructionObj.AddComponent<Text>();
        instructionText.text = $"Press {toggleKey} to close";
        instructionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        instructionText.fontSize = fontSize - 2;
        instructionText.fontStyle = FontStyle.Italic;
        instructionText.alignment = TextAnchor.MiddleCenter;
        instructionText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
    }

    void LoadScene(int sceneIndex)
    {
        Debug.Log($"Loading scene at index {sceneIndex}");
        SceneManager.LoadScene(sceneIndex);
    }

    void ToggleUI()
    {
        if (isVisible)
        {
            HideUI();
        }
        else
        {
            ShowUI();
        }
    }

    void ShowUI()
    {
        if (uiCanvas != null)
        {
            uiCanvas.SetActive(true);
            isVisible = true;

            // Pause time if desired (optional)
            // Time.timeScale = 0f;
        }
    }

    void HideUI()
    {
        if (uiCanvas != null)
        {
            uiCanvas.SetActive(false);
            isVisible = false;

            // Resume time if paused
            // Time.timeScale = 1f;
        }
    }
}
