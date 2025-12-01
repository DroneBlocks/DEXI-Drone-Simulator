using UnityEngine;
using System.Collections.Generic;

public class LEDRingVisualizer : MonoBehaviour
{
    [Header("LED Ring Configuration")]
    [SerializeField]
    [Tooltip("Number of LEDs in the ring")]
    private int ledCount = 45;

    [SerializeField]
    [Tooltip("Radius of the LED ring in meters")]
    private float ringRadius = 0.15f;

    [SerializeField]
    [Tooltip("Height offset from the drone center")]
    private float ringHeight = -0.05f;

    [SerializeField]
    [Tooltip("Size of each LED sphere")]
    private float ledSize = 0.01f;

    [SerializeField]
    [Tooltip("Brightness multiplier for the LEDs")]
    [Range(0f, 2f)]
    private float brightnessMultiplier = 1.0f;

    [SerializeField]
    [Tooltip("Make LEDs emit light")]
    private bool enableLights = false;

    [SerializeField]
    [Tooltip("Light intensity when enabled")]
    private float lightIntensity = 1.0f;

    [SerializeField]
    [Tooltip("Light range when enabled")]
    private float lightRange = 0.2f;

    [Header("Material Settings")]
    [SerializeField]
    [Tooltip("Use intensity boost for brighter LEDs")]
    private bool useEmissiveMaterial = true;

    [SerializeField]
    [Tooltip("Color intensity multiplier for brighter appearance")]
    [Range(1f, 5f)]
    private float emissiveIntensity = 2.0f;

    [Header("Default Color")]
    [SerializeField]
    [Tooltip("Default LED color when no ROS messages received")]
    private Color defaultColor = Color.green;

    [SerializeField]
    [Tooltip("Default brightness (0-255)")]
    [Range(0, 255)]
    private int defaultBrightness = 128;

    private List<GameObject> ledObjects = new List<GameObject>();
    private List<Renderer> ledRenderers = new List<Renderer>();
    private List<Light> ledLights = new List<Light>();
    private Material ledMaterial;

    void Start()
    {
        CreateLEDRing();
    }

    private void CreateLEDRing()
    {
        // Clear any existing LEDs
        foreach (var led in ledObjects)
        {
            if (led != null)
                Destroy(led);
        }
        ledObjects.Clear();
        ledRenderers.Clear();
        ledLights.Clear();

        // Create material for LEDs - try URP Lit shader first (supports emission)
        Shader ledShader = Shader.Find("Universal Render Pipeline/Lit");

        if (ledShader == null)
        {
            // Fallback to URP Unlit
            ledShader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (ledShader == null)
        {
            // Last resort - built-in Standard for non-URP projects
            ledShader = Shader.Find("Standard");
        }

        ledMaterial = new Material(ledShader);

        // Make it emissive so it glows without needing external lights
        ledMaterial.EnableKeyword("_EMISSION");
        ledMaterial.SetFloat("_Surface", 0); // 0 = Opaque

        // Create LED objects in a ring
        for (int i = 0; i < ledCount; i++)
        {
            // Calculate position in ring
            float angle = (i / (float)ledCount) * 2f * Mathf.PI;
            Vector3 position = new Vector3(
                ringRadius * Mathf.Cos(angle),
                ringHeight,
                ringRadius * Mathf.Sin(angle)
            );

            // Create LED GameObject
            GameObject led = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            led.name = $"LED_{i}";
            led.transform.SetParent(transform);
            led.transform.localPosition = position;
            led.transform.localScale = Vector3.one * ledSize;

            // Remove the collider to improve performance
            Collider collider = led.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            // Set material
            Renderer renderer = led.GetComponent<Renderer>();
            renderer.material = new Material(ledMaterial); // Create instance to avoid shared material issues
            ledRenderers.Add(renderer);

            // Add light component if enabled
            if (enableLights)
            {
                Light light = led.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = lightRange;
                light.intensity = 0; // Start off
                ledLights.Add(light);
            }

            ledObjects.Add(led);
        }

        // Set default color
        SetAllLEDsToDefault();
    }

    private void SetAllLEDsToDefault()
    {
        byte r = (byte)(defaultColor.r * 255);
        byte g = (byte)(defaultColor.g * 255);
        byte b = (byte)(defaultColor.b * 255);
        byte brightness = (byte)defaultBrightness;

        for (int i = 0; i < ledRenderers.Count; i++)
        {
            UpdateLED(i, r, g, b, brightness);
        }
    }

    public void UpdateLEDs(LEDState[] leds)
    {
        if (leds == null)
            return;

        foreach (var led in leds)
        {
            if (led.index < ledRenderers.Count)
            {
                UpdateLED((int)led.index, led.r, led.g, led.b, led.brightness);
            }
        }
    }

    private void UpdateLED(int index, byte r, byte g, byte b, byte brightness)
    {
        if (index < 0 || index >= ledRenderers.Count)
            return;

        // Calculate color with brightness
        float brightnessScale = (brightness / 255f) * brightnessMultiplier;

        // Use unlit shader with boosted colors for better visibility
        // Apply intensity boost to make colors more vibrant
        float intensityBoost = useEmissiveMaterial ? emissiveIntensity : 1.0f;
        Color color = new Color(
            (r / 255f) * brightnessScale * intensityBoost,
            (g / 255f) * brightnessScale * intensityBoost,
            (b / 255f) * brightnessScale * intensityBoost,
            1f
        );

        // Update renderer color
        Renderer renderer = ledRenderers[index];

        // Set color and emission for Standard shader
        renderer.material.color = color;
        renderer.material.SetColor("_EmissionColor", color);

        // Update light if enabled
        if (enableLights && index < ledLights.Count)
        {
            Light light = ledLights[index];
            light.color = color;
            light.intensity = brightnessScale * lightIntensity;
        }
    }

    // Public method to manually set all LEDs to a color (for testing)
    public void SetAllLEDs(Color color)
    {
        byte r = (byte)(color.r * 255);
        byte g = (byte)(color.g * 255);
        byte b = (byte)(color.b * 255);
        byte brightness = 255;

        for (int i = 0; i < ledRenderers.Count; i++)
        {
            UpdateLED(i, r, g, b, brightness);
        }
    }

    // Public method to test rainbow effect (for testing without ROS)
    public void TestRainbow()
    {
        StartCoroutine(RainbowTest());
    }

    private System.Collections.IEnumerator RainbowTest()
    {
        Color[] colors = new Color[]
        {
            Color.red, new Color(1f, 0.5f, 0f), Color.yellow, Color.green,
            Color.blue, new Color(0.29f, 0f, 0.51f), new Color(0.58f, 0f, 0.83f)
        };

        int offset = 0;
        while (true)
        {
            for (int i = 0; i < ledCount; i++)
            {
                Color color = colors[(i + offset) % colors.Length];
                byte r = (byte)(color.r * 255);
                byte g = (byte)(color.g * 255);
                byte b = (byte)(color.b * 255);
                UpdateLED(i, r, g, b, 255);
            }
            offset++;
            yield return new WaitForSeconds(0.05f);
        }
    }

    private void OnValidate()
    {
        // Recreate LED ring if parameters change in editor
        if (Application.isPlaying && ledObjects.Count > 0)
        {
            CreateLEDRing();
        }
    }

    private void OnDestroy()
    {
        // Clean up materials
        if (ledMaterial != null)
            Destroy(ledMaterial);

        foreach (var renderer in ledRenderers)
        {
            if (renderer != null && renderer.material != null)
                Destroy(renderer.material);
        }
    }
}
