using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Validates LED color changes against the answer key.
/// Listens for LED color updates (from ROS or keyboard testing) and checks
/// if the color matches the expectedLEDColor of any unscanned real target.
///
/// In keyboard mode, press 1=RED, 2=GREEN, 3=BLUE to simulate LED submissions.
/// </summary>
public class LEDColorValidator : MonoBehaviour
{
    [Header("Color Matching")]
    [Tooltip("How close RGB values must be to match (0-255 per channel)")]
    public int colorTolerance = 30;

    [Header("Debug")]
    [SerializeField] private Color lastReceivedColor;
    [SerializeField] private string lastMatchResult;

    // The color map: maps expected colors to their names
    private static readonly Dictionary<string, Color> namedColors = new Dictionary<string, Color>
    {
        { "RED", Color.red },
        { "GREEN", Color.green },
        { "BLUE", Color.blue }
    };

    private LEDRingVisualizer ledVisualizer;
    private LEDRingSubscriber ledSubscriber;

    void Start()
    {
        ledVisualizer = FindFirstObjectByType<LEDRingVisualizer>();

        // Subscribe to ROS LED messages
        ledSubscriber = FindFirstObjectByType<LEDRingSubscriber>();
        if (ledSubscriber != null)
        {
            ledSubscriber.OnLEDColorsReceived += OnROSLEDReceived;
            Debug.Log("LEDColorValidator: Listening for ROS LED color changes");
        }
    }

    void OnDestroy()
    {
        if (ledSubscriber != null)
            ledSubscriber.OnLEDColorsReceived -= OnROSLEDReceived;
    }

    /// <summary>
    /// Called when LED colors arrive via ROS.
    /// Extracts the dominant color from the first LED and submits it.
    /// </summary>
    private void OnROSLEDReceived(LEDState[] leds)
    {
        if (leds == null || leds.Length == 0) return;

        // Use the first LED's color as the submitted answer
        LEDState led = leds[0];
        Color color = new Color(led.r / 255f, led.g / 255f, led.b / 255f);
        SubmitColor(color, "ROS");
    }

    void Update()
    {
        // In keyboard mode, allow 1/2/3 to simulate LED color submissions
        KeyboardDroneController kbc = FindFirstObjectByType<KeyboardDroneController>();
        if (kbc != null && kbc.Mode == KeyboardDroneController.ControlMode.Keyboard)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                SubmitColor(Color.red, "keyboard");
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                SubmitColor(Color.green, "keyboard");
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                SubmitColor(Color.blue, "keyboard");
        }
    }

    /// <summary>
    /// Called when an LED color is received (from ROS or keyboard).
    /// Checks if it matches any real target's expected color.
    /// </summary>
    public void SubmitColor(Color color, string source = "ROS")
    {
        lastReceivedColor = color;

        if (GameManager.Instance == null) return;

        // Auto-start game on first submission
        if (GameManager.Instance.State == GameManager.GameState.WaitingToStart)
            GameManager.Instance.StartGame();

        if (GameManager.Instance.State != GameManager.GameState.Running) return;

        // Set the drone's LED ring to this color
        if (ledVisualizer != null)
            ledVisualizer.SetAllLEDs(color);

        // Check against all real, unscanned targets
        string colorName = GetColorName(color);
        bool matched = false;

        var targets = GameManager.Instance.GetAllTargets();
        foreach (var target in targets)
        {
            if (!target.IsReal || target.IsScanned) continue;

            if (ColorsMatch(color, target.expectedLEDColor))
            {
                // Correct match!
                GameManager.Instance.ReportScan(target);
                lastMatchResult = $"CORRECT: {target.groupName} → {target.targetName} ({colorName})";
                Debug.Log($"LEDColorValidator: [{source}] {lastMatchResult}");
                matched = true;
                break;
            }
        }

        if (!matched)
        {
            lastMatchResult = $"WRONG: {colorName} doesn't match any pending target";
            Debug.Log($"LEDColorValidator: [{source}] {lastMatchResult}");
        }
    }

    /// <summary>
    /// Check if two colors match within tolerance.
    /// </summary>
    private bool ColorsMatch(Color a, Color b)
    {
        int dr = Mathf.Abs((int)(a.r * 255) - (int)(b.r * 255));
        int dg = Mathf.Abs((int)(a.g * 255) - (int)(b.g * 255));
        int db = Mathf.Abs((int)(a.b * 255) - (int)(b.b * 255));
        return dr <= colorTolerance && dg <= colorTolerance && db <= colorTolerance;
    }

    private string GetColorName(Color c)
    {
        foreach (var kv in namedColors)
        {
            if (ColorsMatch(c, kv.Value)) return kv.Key;
        }
        return $"({(int)(c.r * 255)},{(int)(c.g * 255)},{(int)(c.b * 255)})";
    }
}
