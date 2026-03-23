using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool to generate DroneField in edit mode with saved materials.
/// </summary>
public class DroneFieldEditor
{
    private const string MaterialFolder = "Assets/Materials/Field";

    [MenuItem("DEXI/Generate Drone Field In Scene")]
    public static void GenerateFromMenu()
    {
        var generator = Object.FindFirstObjectByType<DroneFieldGenerator>();
        if (generator == null)
        {
            EditorUtility.DisplayDialog("Error", "No DroneFieldGenerator found in scene.", "OK");
            return;
        }
        Generate(generator);
    }

    [MenuItem("CONTEXT/DroneFieldGenerator/Generate Field (Editor - Build Safe)")]
    public static void GenerateFromContext(MenuCommand command)
    {
        var generator = command.context as DroneFieldGenerator;
        if (generator != null)
            Generate(generator);
    }

    static void EnsureMaterialFolder()
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            AssetDatabase.CreateFolder("Assets/Materials", "Field");
        }
    }

    static Material GetUnlitBase()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/URPUnlit.mat");
        if (mat == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s != null) mat = new Material(s);
        }
        return mat;
    }

    static Material GetLitBase()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/URPLit.mat");
        if (mat == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s != null) mat = new Material(s);
        }
        return mat;
    }

    static Material GetOrCreateMaterial(string name, Color color, bool unlit, bool transparent = false)
    {
        EnsureMaterialFolder();
        string path = $"{MaterialFolder}/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        Material baseMat = unlit ? GetUnlitBase() : GetLitBase();
        Material mat = new Material(baseMat);
        mat.color = color;
        mat.name = name;

        if (transparent)
        {
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    public static void Generate(DroneFieldGenerator gen)
    {
        // Let the generator run, then replace materials
        bool wasRuntime = gen.generateAtRuntime;
        gen.generateAtRuntime = true;
        gen.RegenerateField();
        gen.generateAtRuntime = wasRuntime;

        // Find the generated container
        GameObject fieldContainer = null;
        foreach (Transform child in gen.transform)
        {
            if (child.name == "DroneField")
            {
                fieldContainer = child.gameObject;
                break;
            }
        }

        if (fieldContainer == null)
        {
            Debug.LogError("DroneFieldEditor: Could not find generated DroneField");
            return;
        }

        // Replace all LineRenderer materials with saved assets
        int replaced = 0;
        LineRenderer[] lines = fieldContainer.GetComponentsInChildren<LineRenderer>();
        foreach (var lr in lines)
        {
            if (lr.sharedMaterial != null && !AssetDatabase.Contains(lr.sharedMaterial))
            {
                Color color = lr.startColor;
                string colorName = $"field_line_{ColorToName(color)}";
                Material savedMat = GetOrCreateMaterial(colorName, color, true);
                lr.sharedMaterial = savedMat;
                replaced++;
            }
        }

        // Replace all MeshRenderer materials with saved assets
        MeshRenderer[] meshes = fieldContainer.GetComponentsInChildren<MeshRenderer>();
        foreach (var mr in meshes)
        {
            if (mr.sharedMaterial != null && !AssetDatabase.Contains(mr.sharedMaterial))
            {
                Color color = mr.sharedMaterial.color;
                bool isTransparent = color.a < 1f;
                string prefix = isTransparent ? "field_transparent" : "field_lit";
                string colorName = $"{prefix}_{ColorToName(color)}";
                Material savedMat = GetOrCreateMaterial(colorName, color, false, isTransparent);
                mr.sharedMaterial = savedMat;
                replaced++;
            }
        }

        // Mark scene dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"DroneFieldEditor: Replaced {replaced} runtime materials with saved assets.");
        EditorUtility.DisplayDialog("Done",
            $"Drone field generated with {replaced} saved materials.\n" +
            $"Materials saved to {MaterialFolder}/",
            "OK");
    }

    static string ColorToName(Color c)
    {
        int r = Mathf.RoundToInt(c.r * 255);
        int g = Mathf.RoundToInt(c.g * 255);
        int b = Mathf.RoundToInt(c.b * 255);
        int a = Mathf.RoundToInt(c.a * 255);
        return $"{r}_{g}_{b}_{a}";
    }
}
