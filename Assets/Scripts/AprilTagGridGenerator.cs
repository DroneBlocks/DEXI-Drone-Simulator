using UnityEngine;

public class AprilTagGridGenerator : MonoBehaviour
{
    [Header("Grid Settings")]
    [Tooltip("Number of tags along X axis")]
    public int gridCountX = 10;

    [Tooltip("Number of tags along Z axis")]
    public int gridCountZ = 10;

    [Tooltip("Distance between tags in meters")]
    public float spacing = 10f;

    [Tooltip("Starting tag ID (first tag will have this ID)")]
    public int startingTagId = 1;

    [Header("Positioning")]
    [Tooltip("Center the grid around this object's position")]
    public bool centerGrid = true;

    [Tooltip("Height offset from ground (Y position)")]
    public float heightOffset = 0f;

    [Header("Materials (Optional)")]
    [Tooltip("Array of materials for tags. Index 0 = first tag, etc. If empty, uses source object's material.")]
    public Material[] tagMaterials;

    private GameObject tagsContainer;
    private GameObject sourceObject;

    void Start()
    {
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
        // Store reference to source object (this GameObject) and hide it
        sourceObject = gameObject;

        // Create container for generated tags
        tagsContainer = new GameObject("GeneratedAprilTags");
        tagsContainer.transform.position = transform.position;
        tagsContainer.transform.rotation = Quaternion.identity;

        // Calculate grid offset for centering
        Vector3 gridOffset = Vector3.zero;
        if (centerGrid)
        {
            gridOffset = new Vector3(
                -(gridCountX - 1) * spacing / 2f,
                0,
                -(gridCountZ - 1) * spacing / 2f
            );
        }

        Vector3 basePosition = transform.position;
        int tagId = startingTagId;

        for (int z = 0; z < gridCountZ; z++)
        {
            for (int x = 0; x < gridCountX; x++)
            {
                Vector3 position = basePosition + new Vector3(
                    x * spacing + gridOffset.x,
                    heightOffset,
                    z * spacing + gridOffset.z
                );

                CreateTag(tagId, position, x, z);
                tagId++;
            }
        }

        // Hide the source object (the one this script is attached to)
        // Keep it disabled so we can reference it but it's not visible
        GetComponent<Renderer>().enabled = false;

        Debug.Log($"AprilTagGridGenerator: Created {gridCountX * gridCountZ} tags (IDs {startingTagId} to {tagId - 1})");
    }

    void CreateTag(int tagId, Vector3 worldPosition, int gridX, int gridZ)
    {
        // Clone the source object
        GameObject tagObj = Instantiate(sourceObject, worldPosition, sourceObject.transform.rotation);
        tagObj.name = $"AprilTag_{tagId}";
        tagObj.transform.SetParent(tagsContainer.transform);

        // Re-enable renderer on clone (we disabled it on source)
        Renderer renderer = tagObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = true;

            // Apply specific material if available
            int materialIndex = tagId - startingTagId;
            if (tagMaterials != null && materialIndex < tagMaterials.Length && tagMaterials[materialIndex] != null)
            {
                renderer.material = tagMaterials[materialIndex];
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

        // Add AprilTagInfo component to store tag ID
        AprilTagInfo tagInfo = tagObj.GetComponent<AprilTagInfo>();
        if (tagInfo == null)
        {
            tagInfo = tagObj.AddComponent<AprilTagInfo>();
        }
        tagInfo.tagId = tagId;
        tagInfo.gridPosition = new Vector2Int(gridX, gridZ);
    }

    void OnDestroy()
    {
        // Re-enable renderer if object is destroyed while grid exists
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
    [Tooltip("The April tag ID (1-100 for standard grid)")]
    public int tagId;

    [Tooltip("Grid position (x, z) of this tag")]
    public Vector2Int gridPosition;
}
