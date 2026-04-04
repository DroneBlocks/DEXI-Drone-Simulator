using UnityEngine;

public class AprilTagGridGenerator : MonoBehaviour
{
    [Header("Grid Settings")]
    [Tooltip("Number of tags along X axis")]
    public int gridCountX = 10;

    [Tooltip("Number of tags along Z axis")]
    public int gridCountZ = 2;

    [Tooltip("Distance between tags along X axis in meters")]
    public float spacingX = 1.0f;

    [Tooltip("Distance between tags along Z axis in meters")]
    public float spacingZ = 0.5f;

    [Tooltip("Starting tag ID (first tag will have this ID)")]
    public int startingTagId = 0;

    [Header("Positioning")]
    [Tooltip("Center the grid around this object's position")]
    public bool centerGrid = true;

    [Tooltip("Height offset from ground (Y position)")]
    public float heightOffset = 0f;

    [Header("Tag Size")]
    [Tooltip("Tag size in meters (overrides source object scale). Set to 0 to use source object's scale.")]
    public float tagSize = 0.15f;

    [Header("Materials")]
    [Tooltip("Array of materials for tags. If empty, uses pre-baked materials from Materials/AprilTags/")]
    public Material[] tagMaterials;

    [Header("Auto-Load Textures (editor only)")]
    [Tooltip("Resource path prefix for texture names (used by editor tools)")]
    public string textureResourcePrefix = "AprilTags/apriltag_";

    [Tooltip("Number of digits for zero-padded texture names (e.g. 5 for apriltag_00000)")]
    public int textureNameDigits = 5;

    private GameObject tagsContainer;
    private GameObject sourceObject;

    [Header("Runtime")]
    [Tooltip("Generate tags at runtime (disable if using editor-generated tags for WebGL builds)")]
    public bool generateAtRuntime = false;

    void Start()
    {
        if (generateAtRuntime)
            GenerateGrid();
    }

    [ContextMenu("Regenerate Grid")]
    public void RegenerateGrid()
    {
        ClearGrid();
        GenerateGrid();
    }

    [ContextMenu("Clear Grid")]
    public void ClearGrid()
    {
        if (tagsContainer != null)
        {
            if (Application.isPlaying)
                Destroy(tagsContainer);
            else
                DestroyImmediate(tagsContainer);
        }
    }

    void GenerateGrid()
    {
        sourceObject = gameObject;

        tagsContainer = new GameObject("GeneratedAprilTags");
        tagsContainer.transform.position = transform.position;
        tagsContainer.transform.rotation = Quaternion.identity;

        // Calculate grid offset for centering
        Vector3 gridOffset = Vector3.zero;
        if (centerGrid)
        {
            gridOffset = new Vector3(
                -(gridCountX - 1) * spacingX / 2f,
                0,
                -(gridCountZ - 1) * spacingZ / 2f
            );
        }

        Vector3 basePosition = transform.position;
        int tagId = startingTagId;

        for (int z = 0; z < gridCountZ; z++)
        {
            for (int x = 0; x < gridCountX; x++)
            {
                Vector3 position = basePosition + new Vector3(
                    x * spacingX + gridOffset.x,
                    heightOffset,
                    z * spacingZ + gridOffset.z
                );

                CreateTag(tagId, position, x, z);
                tagId++;
            }
        }

        // Hide the source object
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
            renderer.enabled = false;

        Debug.Log($"AprilTagGridGenerator: Created {gridCountX * gridCountZ} tags (IDs {startingTagId} to {tagId - 1}), " +
                  $"grid {gridCountX}x{gridCountZ}, spacing {spacingX}m x {spacingZ}m, tag size {tagSize}m");
    }

    void CreateTag(int tagId, Vector3 worldPosition, int gridX, int gridZ)
    {
        GameObject tagObj = Instantiate(sourceObject, worldPosition, sourceObject.transform.rotation);
        tagObj.name = $"AprilTag_{tagId}";
        tagObj.transform.SetParent(tagsContainer.transform);

        // Apply tag size if specified
        if (tagSize > 0)
        {
            tagObj.transform.localScale = new Vector3(tagSize, tagSize, tagSize);
        }

        Renderer renderer = tagObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = true;

            // Try explicit materials array first
            int materialIndex = tagId - startingTagId;
            if (tagMaterials != null && materialIndex < tagMaterials.Length && tagMaterials[materialIndex] != null)
            {
                renderer.material = tagMaterials[materialIndex];
            }
            else
            {
                // Auto-load texture from Resources
                Material mat = CreateMaterialForTag(tagId);
                if (mat != null)
                {
                    renderer.material = mat;
                }
            }
        }

        // Remove the generator script from clones
        AprilTagGridGenerator clonedGenerator = tagObj.GetComponent<AprilTagGridGenerator>();
        if (clonedGenerator != null)
        {
            if (Application.isPlaying)
                Destroy(clonedGenerator);
            else
                DestroyImmediate(clonedGenerator);
        }

        // Add AprilTagInfo component
        AprilTagInfo tagInfo = tagObj.GetComponent<AprilTagInfo>();
        if (tagInfo == null)
        {
            tagInfo = tagObj.AddComponent<AprilTagInfo>();
        }
        tagInfo.tagId = tagId;
        tagInfo.gridPosition = new Vector2Int(gridX, gridZ);
    }

    Material CreateMaterialForTag(int tagId)
    {
        string textureName = textureResourcePrefix + tagId.ToString().PadLeft(textureNameDigits, '0');
        Texture2D texture = Resources.Load<Texture2D>(textureName);

        if (texture == null)
        {
            Debug.LogWarning($"AprilTagGridGenerator: Could not load texture '{textureName}' for tag {tagId}");
            return null;
        }

        // Use Unlit material so tags are clearly visible regardless of lighting
        Material mat = RuntimeMaterials.Instance.CreateUnlit(texture);
        mat.name = $"AprilTag_{tagId}_Mat";

        // Point filtering for crisp tag edges
        texture.filterMode = FilterMode.Point;

        return mat;
    }

    void OnDestroy()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = true;
        }
    }
}

/// <summary>
/// Component to store April tag metadata on each tag object
/// </summary>
public class AprilTagInfo : MonoBehaviour
{
    [Tooltip("The April tag ID")]
    public int tagId;

    [Tooltip("Grid position (x, z) of this tag")]
    public Vector2Int gridPosition;
}
