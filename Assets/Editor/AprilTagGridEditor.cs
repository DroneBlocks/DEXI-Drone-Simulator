using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor tool to generate AprilTag grid in edit mode.
/// Creates materials as assets (not runtime) so they're included in builds.
/// Right-click AprilTagGridGenerator → "Generate Grid (Editor)" or use the menu.
/// </summary>
public class AprilTagGridEditor
{
    private const string MaterialFolder = "Assets/Materials/AprilTags";
    private const string TextureFolder = "Assets/Resources/AprilTags";

    [MenuItem("DEXI/Generate AprilTag Grid In Scene")]
    public static void GenerateFromMenu()
    {
        var generator = Object.FindFirstObjectByType<AprilTagGridGenerator>();
        if (generator == null)
        {
            EditorUtility.DisplayDialog("Error", "No AprilTagGridGenerator found in scene.", "OK");
            return;
        }
        GenerateGrid(generator);
    }

    [MenuItem("CONTEXT/AprilTagGridGenerator/Generate Grid (Editor - Build Safe)")]
    public static void GenerateFromContext(MenuCommand command)
    {
        var generator = command.context as AprilTagGridGenerator;
        if (generator != null)
            GenerateGrid(generator);
    }

    public static void GenerateGrid(AprilTagGridGenerator gen)
    {
        // Ensure material folder exists
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            AssetDatabase.CreateFolder("Assets/Materials", "AprilTags");
        }

        // Find or create base unlit material
        Material baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/URPUnlit.mat");
        if (baseMat == null)
        {
            // Try to find any URP Unlit material
            string[] guids = AssetDatabase.FindAssets("t:Material URPUnlit");
            if (guids.Length > 0)
                baseMat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        if (baseMat == null)
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null)
            {
                EditorUtility.DisplayDialog("Error", "Cannot find URP Unlit shader.", "OK");
                return;
            }
            baseMat = new Material(unlitShader);
        }

        // Clear existing generated tags
        Transform existing = gen.transform.parent != null
            ? gen.transform.parent.Find("GeneratedAprilTags")
            : null;
        if (existing == null)
        {
            // Search at root
            GameObject existingObj = GameObject.Find("GeneratedAprilTags");
            if (existingObj != null)
                existing = existingObj.transform;
        }
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        // Create container
        GameObject container = new GameObject("GeneratedAprilTags");
        Undo.RegisterCreatedObjectUndo(container, "Generate AprilTag Grid");

        // Calculate grid offset for centering
        Vector3 gridOffset = Vector3.zero;
        if (gen.centerGrid)
        {
            gridOffset = new Vector3(
                -(gen.gridCountX - 1) * gen.spacingX / 2f,
                0,
                -(gen.gridCountZ - 1) * gen.spacingZ / 2f
            );
        }

        Vector3 basePosition = gen.transform.position;
        int tagId = gen.startingTagId;
        int created = 0;

        for (int z = 0; z < gen.gridCountZ; z++)
        {
            for (int x = 0; x < gen.gridCountX; x++)
            {
                Vector3 position = basePosition + new Vector3(
                    x * gen.spacingX + gridOffset.x,
                    gen.heightOffset,
                    z * gen.spacingZ + gridOffset.z
                );

                // Create tag quad
                GameObject tagObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tagObj.name = $"AprilTag_{tagId}";
                tagObj.transform.SetParent(container.transform);
                tagObj.transform.position = position;
                tagObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Face up
                if (gen.tagSize > 0)
                    tagObj.transform.localScale = new Vector3(gen.tagSize, gen.tagSize, gen.tagSize);

                // Get or create material asset for this tag
                Material tagMat = GetOrCreateTagMaterial(tagId, baseMat, gen.textureResourcePrefix, gen.textureNameDigits);
                if (tagMat != null)
                    tagObj.GetComponent<Renderer>().sharedMaterial = tagMat;

                // Add AprilTagInfo
                AprilTagInfo tagInfo = tagObj.AddComponent<AprilTagInfo>();
                tagInfo.tagId = tagId;
                tagInfo.gridPosition = new Vector2Int(x, z);

                // Remove collider (not needed for visual tags)
                Object.DestroyImmediate(tagObj.GetComponent<Collider>());

                tagId++;
                created++;
            }
        }

        // Hide the source object
        Renderer sourceRenderer = gen.GetComponent<Renderer>();
        if (sourceRenderer != null)
            sourceRenderer.enabled = false;

        // Mark scene dirty so it saves
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"AprilTagGridEditor: Created {created} tags (IDs {gen.startingTagId} to {tagId - 1}) as scene objects with saved materials.");
        EditorUtility.DisplayDialog("Done",
            $"Created {created} AprilTags as scene objects.\n\n" +
            $"Materials saved to {MaterialFolder}/\n" +
            "These are build-safe — no runtime shader loading needed.",
            "OK");
    }

    static Material GetOrCreateTagMaterial(int tagId, Material baseMat, string prefix, int digits)
    {
        string matPath = $"{MaterialFolder}/apriltag_{tagId:D5}.mat";

        // Check if material already exists
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (existing != null)
            return existing;

        // Load texture
        string textureName = prefix + tagId.ToString().PadLeft(digits, '0');
        Texture2D texture = Resources.Load<Texture2D>(textureName);
        if (texture == null)
        {
            // Try direct path
            string texPath = $"{TextureFolder}/apriltag_{tagId:D5}.png";
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        }

        if (texture == null)
        {
            Debug.LogWarning($"Cannot find texture for tag {tagId}");
            return null;
        }

        // Set point filtering for crisp edges
        string texAssetPath = AssetDatabase.GetAssetPath(texture);
        if (!string.IsNullOrEmpty(texAssetPath))
        {
            TextureImporter importer = AssetImporter.GetAtPath(texAssetPath) as TextureImporter;
            if (importer != null && importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }
        }

        // Create new material asset
        Material mat = new Material(baseMat);
        mat.mainTexture = texture;
        mat.name = $"apriltag_{tagId:D5}";

        AssetDatabase.CreateAsset(mat, matPath);
        return mat;
    }
}
