using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SimManagementUI : MonoBehaviour
{
    [Header("UI Settings")]
    public KeyCode toggleKey = KeyCode.Space;
    public GameObject sceneButtonPrefab;

    [Header("UI Appearance")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.85f);
    public Color buttonColor = new Color(0.2f, 0.6f, 0.9f, 1f);
    public Color buttonHoverColor = new Color(0.3f, 0.7f, 1f, 1f);
    public Color currentSceneColor = new Color(0.3f, 0.9f, 0.3f, 1f);
    public int fontSize = 18;
    
    [Header("References")]
    public Transform sceneSwitchContainer;
    public Transform droneSwapContainer;

    private PictureInPictureCamera pipCamera;
    private DroneCamera droneCamera;

    private GameObject uiCanvas;
    private bool isVisible = false;
    private List<(Button button, Image image, Text label, DroneType type)> droneButtons = new();

    void Awake()
    {
        uiCanvas = GetComponentInChildren<Canvas>().gameObject;
        uiCanvas.SetActive(false);

        pipCamera = FindFirstObjectByType<PictureInPictureCamera>();
        droneCamera = FindFirstObjectByType<DroneCamera>();
    }

    void Start()
    {
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        string currentSceneName = SceneManager.GetActiveScene().name;

        for (int i = 0; i < sceneCount; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            CreateSceneButton(sceneSwitchContainer, sceneName, i, sceneName == currentSceneName);
        }

        foreach (DroneType type in DroneManager.Instance.drones)
        {
            CreateDroneButton(droneSwapContainer, type);
        }

        DroneManager.Instance.OnDroneSwapped += RefreshDroneButtons;
    }

    void OnDestroy()
    {
        DroneManager.Instance.OnDroneSwapped -= RefreshDroneButtons;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            isVisible = !isVisible;
            ToggleUI(isVisible);
        }
    }

    void ToggleUI(bool toggle)
    {
        uiCanvas.SetActive(toggle);
        pipCamera.SetEnabled(!toggle);
        droneCamera.ShouldUpdateCamera(!toggle);
    }

    void RefreshDroneButtons(DroneType activeDrone)
    {
        foreach (var (button, image, label, type) in droneButtons)
        {
            bool isCurrent = type == activeDrone;
            button.interactable = !isCurrent;

            ColorBlock colors = button.colors;
            colors.normalColor = isCurrent ? currentSceneColor : buttonColor;
            button.colors = colors;
            image.color = isCurrent ? currentSceneColor : buttonColor;

            label.text = isCurrent ? $"{type.name} (Current)" : type.name;
            label.fontStyle = isCurrent ? FontStyle.Bold : FontStyle.Normal;
        }
    }

    void OnDroneButtonClick(DroneType type)
    {
        DroneManager.Instance.SwapToDrone(type.model);
        isVisible = false;
        ToggleUI(isVisible);
    }

    void CreateDroneButton(Transform parent, DroneType type)
    {
        bool isCurrentDrone = DroneManager.Instance.ActiveDrone == type;

        GameObject buttonObj = Instantiate(sceneButtonPrefab, parent);
        Image buttonImage = buttonObj.GetComponent<Image>();
        buttonImage.color = isCurrentDrone ? currentSceneColor : buttonColor;

        Button button = buttonObj.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = isCurrentDrone ? currentSceneColor : buttonColor;
        colors.highlightedColor = buttonHoverColor;
        colors.pressedColor = new Color(0.1f, 0.4f, 0.7f, 1f);
        button.colors = colors;
        button.interactable = !isCurrentDrone;

        button.onClick.AddListener(() => OnDroneButtonClick(type));

        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(buttonObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text buttonText = textObj.AddComponent<Text>();
        buttonText.text = isCurrentDrone ? $"{type.name} (Current)" : type.name;
        buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.fontSize = fontSize;
        buttonText.fontStyle = isCurrentDrone ? FontStyle.Bold : FontStyle.Normal;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.white;

        droneButtons.Add((button, buttonImage, buttonText, type));
    }

    void CreateSceneButton(Transform parent, string sceneName, int sceneIndex, bool isCurrentScene)
    {
        GameObject buttonObj = Instantiate(sceneButtonPrefab, parent);
        Image buttonImage = buttonObj.GetComponent<Image>();
        buttonImage.color = isCurrentScene ? currentSceneColor : buttonColor;

        Button button = buttonObj.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = isCurrentScene ? currentSceneColor : buttonColor;
        colors.highlightedColor = buttonHoverColor;
        colors.pressedColor = new Color(0.1f, 0.4f, 0.7f, 1f);
        button.colors = colors;
        button.interactable = !isCurrentScene;

        int index = sceneIndex;
        button.onClick.AddListener(() => SceneManager.LoadScene(index));

        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(buttonObj.transform, false);

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
    }
}