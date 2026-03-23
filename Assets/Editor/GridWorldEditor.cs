using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool to generate GridWorld in edit mode with saved materials.
/// Creates LineRenderers with material assets instead of runtime Shader.Find.
/// </summary>
public class GridWorldEditor
{
    private const string MaterialFolder = "Assets/Materials/Grid";

    [MenuItem("DEXI/Generate Grid World In Scene")]
    public static void GenerateFromMenu()
    {
        var generator = Object.FindFirstObjectByType<GridWorldGenerator>();
        if (generator == null)
        {
            EditorUtility.DisplayDialog("Error", "No GridWorldGenerator found in scene.", "OK");
            return;
        }
        Generate(generator);
    }

    [MenuItem("CONTEXT/GridWorldGenerator/Generate Grid (Editor - Build Safe)")]
    public static void GenerateFromContext(MenuCommand command)
    {
        var generator = command.context as GridWorldGenerator;
        if (generator != null)
            Generate(generator);
    }

    static Material _unlitMat;
    static Material _litMat;

    static Material GetUnlitBase()
    {
        if (_unlitMat != null) return _unlitMat;
        _unlitMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/URPUnlit.mat");
        if (_unlitMat == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s != null) _unlitMat = new Material(s);
        }
        return _unlitMat;
    }

    static Material GetLitBase()
    {
        if (_litMat != null) return _litMat;
        _litMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/URPLit.mat");
        if (_litMat == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s != null) _litMat = new Material(s);
        }
        return _litMat;
    }

    static void EnsureMaterialFolder()
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            AssetDatabase.CreateFolder("Assets/Materials", "Grid");
        }
    }

    static Material GetOrCreateColorMaterial(string name, Color color, bool unlit)
    {
        EnsureMaterialFolder();
        string path = $"{MaterialFolder}/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        Material baseMat = unlit ? GetUnlitBase() : GetLitBase();
        Material mat = new Material(baseMat);
        mat.color = color;
        mat.name = name;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    public static void Generate(GridWorldGenerator gen)
    {
        // Clear existing
        Transform existing = gen.transform.Find("GridWorld");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);
        GameObject existingRoot = GameObject.Find("GridWorld");
        if (existingRoot != null && existingRoot != gen.gameObject)
        {
            // Find child named GridWorld
            foreach (Transform child in gen.transform)
            {
                if (child.name == "GridWorld")
                {
                    Object.DestroyImmediate(child.gameObject);
                    break;
                }
            }
        }

        // Run the generator's own method but we need materials to be assets
        // Simplest: let it generate at runtime in editor, then replace materials with saved assets

        // Temporarily enable runtime generation
        bool wasRuntime = gen.generateAtRuntime;
        gen.generateAtRuntime = true;

        // Force generation via reflection or direct call
        gen.RegenerateGridWorld();

        gen.generateAtRuntime = wasRuntime;

        // Now find all LineRenderers and replace their materials with saved assets
        GameObject gridWorld = null;
        foreach (Transform child in gen.transform)
        {
            if (child.name == "GridWorld")
            {
                gridWorld = child.gameObject;
                break;
            }
        }

        if (gridWorld == null)
        {
            Debug.LogError("GridWorldEditor: Could not find generated GridWorld");
            return;
        }

        // Replace all line renderer materials with saved assets
        int replaced = 0;
        LineRenderer[] lines = gridWorld.GetComponentsInChildren<LineRenderer>();
        foreach (var lr in lines)
        {
            if (lr.sharedMaterial != null)
            {
                Color color = lr.sharedMaterial.color;
                string colorName = $"grid_{ColorToName(color)}";
                Material savedMat = GetOrCreateColorMaterial(colorName, color, true);
                lr.sharedMaterial = savedMat;
                replaced++;
            }
        }

        // Replace all MeshRenderer materials (landing pads, zones)
        MeshRenderer[] meshes = gridWorld.GetComponentsInChildren<MeshRenderer>();
        foreach (var mr in meshes)
        {
            if (mr.sharedMaterial != null && !AssetDatabase.Contains(mr.sharedMaterial))
            {
                Color color = mr.sharedMaterial.color;
                string colorName = $"grid_lit_{ColorToName(color)}";
                Material savedMat = GetOrCreateColorMaterial(colorName, color, false);
                mr.sharedMaterial = savedMat;
                replaced++;
            }
        }

        // Mark scene dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"GridWorldEditor: Replaced {replaced} runtime materials with saved assets.");
        EditorUtility.DisplayDialog("Done",
            $"Grid world generated with {replaced} saved materials.\n" +
            $"Materials saved to {MaterialFolder}/",
            "OK");
    }

    static string ColorToName(Color c)
    {
        // Create a deterministic name from color
        int r = Mathf.RoundToInt(c.r * 255);
        int g = Mathf.RoundToInt(c.g * 255);
        int b = Mathf.RoundToInt(c.b * 255);
        int a = Mathf.RoundToInt(c.a * 255);
        return $"{r}_{g}_{b}_{a}";
    }
}
