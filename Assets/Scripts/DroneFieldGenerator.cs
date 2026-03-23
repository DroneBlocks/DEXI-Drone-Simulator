using UnityEngine;

/// <summary>
/// Generates a drone field cage with floor, walls, and optional ceiling.
/// Dimensions are in feet for ease of configuration, converted to meters internally.
/// </summary>
public class DroneFieldGenerator : MonoBehaviour
{
    private const float FEET_TO_METERS = 0.3048f;

    [Header("Field Dimensions (feet)")]
    [Tooltip("Field width (X axis) in feet")]
    public float widthFeet = 15f;

    [Tooltip("Field height (Y axis) in feet")]
    public float heightFeet = 15f;

    [Tooltip("Field length (Z axis) in feet")]
    public float lengthFeet = 40f;

    [Header("Appearance")]
    [Tooltip("Floor color")]
    public Color floorColor = new Color(0.15f, 0.15f, 0.15f, 1f);

    [Tooltip("Wall color")]
    public Color wallColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    [Tooltip("Wall thickness in meters")]
    public float wallThickness = 0.02f;

    [Header("Options")]
    [Tooltip("Create ceiling")]
    public bool createCeiling = false;

    [Tooltip("Show field dimension labels")]
    public bool showLabels = true;

    [Tooltip("Show center line along the length")]
    public bool showCenterLine = true;

    [Tooltip("Show start and end markers")]
    public bool showEndMarkers = true;

    [Header("Runtime")]
    [Tooltip("Generate field at runtime (disable if using editor-generated field for WebGL builds)")]
    public bool generateAtRuntime = false;

    private GameObject fieldContainer;

    void Awake()
    {
        if (generateAtRuntime)
            GenerateField();
    }

    [ContextMenu("Regenerate Field")]
    public void RegenerateField()
    {
        ClearField();
        GenerateField();
    }

    [ContextMenu("Clear Field")]
    public void ClearField()
    {
        if (fieldContainer != null)
        {
            if (Application.isPlaying)
                Destroy(fieldContainer);
            else
                DestroyImmediate(fieldContainer);
        }
    }

    void GenerateField()
    {
        float width = widthFeet * FEET_TO_METERS;
        float height = heightFeet * FEET_TO_METERS;
        float length = lengthFeet * FEET_TO_METERS;

        fieldContainer = new GameObject("DroneField");
        fieldContainer.transform.SetParent(transform);
        fieldContainer.transform.localPosition = Vector3.zero;

        CreateFloor(width, length);
        CreateWalls(width, height, length);

        if (createCeiling)
            CreateCeiling(width, height, length);

        if (showCenterLine)
            CreateCenterLine(length);

        if (showEndMarkers)
            CreateEndMarkers(width, length);

        if (showLabels)
            CreateLabels(width, height, length);

        Debug.Log($"DroneFieldGenerator: Created {widthFeet}' x {heightFeet}' x {lengthFeet}' field " +
                  $"({width:F1}m x {height:F1}m x {length:F1}m)");
    }

    void CreateFloor(float width, float length)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(fieldContainer.transform);
        floor.transform.localPosition = new Vector3(0, -0.005f, 0);
        floor.transform.localScale = new Vector3(width, 0.01f, length);

        Material mat = RuntimeMaterials.Instance.CreateLit(floorColor);
        floor.GetComponent<Renderer>().material = mat;
    }

    void CreateWalls(float width, float height, float length)
    {
        GameObject wallsParent = new GameObject("Walls");
        wallsParent.transform.SetParent(fieldContainer.transform);
        wallsParent.transform.localPosition = Vector3.zero;

        // Wall material - semi-transparent
        Material wallMat = RuntimeMaterials.Instance.CreateLitTransparent(wallColor);

        float halfW = width / 2f;
        float halfL = length / 2f;
        float halfH = height / 2f;

        // Left wall (-X)
        CreateWall("Wall_Left", new Vector3(-halfW, halfH, 0),
                   new Vector3(wallThickness, height, length), wallMat, wallsParent.transform);

        // Right wall (+X)
        CreateWall("Wall_Right", new Vector3(halfW, halfH, 0),
                   new Vector3(wallThickness, height, length), wallMat, wallsParent.transform);

        // Back wall (-Z)
        CreateWall("Wall_Back", new Vector3(0, halfH, -halfL),
                   new Vector3(width, height, wallThickness), wallMat, wallsParent.transform);

        // Front wall (+Z)
        CreateWall("Wall_Front", new Vector3(0, halfH, halfL),
                   new Vector3(width, height, wallThickness), wallMat, wallsParent.transform);

        // Wire frame edges for visibility
        CreateWireframeEdges(width, height, length, wallsParent.transform);
    }

    void CreateWall(string name, Vector3 position, Vector3 scale, Material mat, Transform parent)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent);
        wall.transform.localPosition = position;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().material = mat;

        // Make walls non-physical triggers so the drone can't clip through
        wall.GetComponent<Collider>().isTrigger = false;
    }

    void CreateWireframeEdges(float width, float height, float length, Transform parent)
    {
        GameObject edgesParent = new GameObject("Edges");
        edgesParent.transform.SetParent(parent);
        edgesParent.transform.localPosition = Vector3.zero;

        float halfW = width / 2f;
        float halfL = length / 2f;
        Color edgeColor = new Color(1f, 1f, 1f, 0.6f);
        float edgeWidth = 0.015f;

        // Bottom edges
        CreateLineEdge(new Vector3(-halfW, 0, -halfL), new Vector3(halfW, 0, -halfL), edgeColor, edgeWidth, edgesParent.transform);
        CreateLineEdge(new Vector3(-halfW, 0, halfL), new Vector3(halfW, 0, halfL), edgeColor, edgeWidth, edgesParent.transform);
        CreateLineEdge(new Vector3(-halfW, 0, -halfL), new Vector3(-halfW, 0, halfL), edgeColor, edgeWidth, edgesParent.transform);
        CreateLineEdge(new Vector3(halfW, 0, -halfL), new Vector3(halfW, 0, halfL), edgeColor, edgeWidth, edgesParent.transform);

        // Top edges
        CreateLineEdge(new Vector3(-halfW, height, -halfL), new Vector3(halfW, height, -halfL), edgeColor, edgeWidth, edgesParent.transform);
        CreateLineEdge(new Vector3(-halfW, height, halfL), new Vector3(halfW, height, halfL), edgeColor, edgeWidth, edgesParent.transform);
        CreateLineEdge(new Vector3(-halfW, height, -halfL), new Vector3(-halfW, height, halfL), edgeColor, edgeWidth, edgesParent.transform);
        CreateLineEdge(new Vector3(halfW, height, -halfL), new Vector3(halfW, height, halfL), edgeColor, edgeWidth, edgesParent.transform);

        // Vertical edges
        CreateLineEdge(new Vector3(-halfW, 0, -halfL), new Vector3(-halfW, height, -halfL), edgeColor, edgeWidth, edgesParent.transform);
        CreateLineEdge(new Vector3(halfW, 0, -halfL), new Vector3(halfW, height, -halfL), edgeColor, edgeWidth, edgesParent.transform);
        CreateLineEdge(new Vector3(-halfW, 0, halfL), new Vector3(-halfW, height, halfL), edgeColor, edgeWidth, edgesParent.transform);
        CreateLineEdge(new Vector3(halfW, 0, halfL), new Vector3(halfW, height, halfL), edgeColor, edgeWidth, edgesParent.transform);
    }

    void CreateLineEdge(Vector3 start, Vector3 end, Color color, float width, Transform parent)
    {
        GameObject lineObj = new GameObject("Edge");
        lineObj.transform.SetParent(parent);

        // Offset by generator's world position since lines use world space
        Vector3 worldOffset = transform.position;

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        Material lineMat = RuntimeMaterials.Instance.CreateUnlit(color);
        lr.material = lineMat;
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.positionCount = 2;
        lr.SetPosition(0, start + worldOffset);
        lr.SetPosition(1, end + worldOffset);
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
    }

    void CreateCeiling(float width, float height, float length)
    {
        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "Ceiling";
        ceiling.transform.SetParent(fieldContainer.transform);
        ceiling.transform.localPosition = new Vector3(0, height, 0);
        ceiling.transform.localScale = new Vector3(width, 0.01f, length);

        Material mat = RuntimeMaterials.Instance.CreateLitTransparent(new Color(0.2f, 0.2f, 0.2f, 0.3f));
        ceiling.GetComponent<Renderer>().material = mat;
    }

    void CreateCenterLine(float length)
    {
        float halfL = length / 2f;
        Color lineColor = new Color(1f, 1f, 0f, 0.5f); // Yellow

        CreateLineEdge(
            new Vector3(0, 0.005f, -halfL),
            new Vector3(0, 0.005f, halfL),
            lineColor, 0.02f, fieldContainer.transform
        );
    }

    void CreateEndMarkers(float width, float length)
    {
        float halfL = length / 2f;
        GameObject markersParent = new GameObject("EndMarkers");
        markersParent.transform.SetParent(fieldContainer.transform);
        markersParent.transform.localPosition = Vector3.zero;

        // Start marker (green line across width at -Z end)
        CreateLineEdge(
            new Vector3(-width / 2f, 0.005f, -halfL + 0.5f),
            new Vector3(width / 2f, 0.005f, -halfL + 0.5f),
            new Color(0f, 1f, 0f, 0.7f), 0.03f, markersParent.transform
        );

        // End marker (red line across width at +Z end)
        CreateLineEdge(
            new Vector3(-width / 2f, 0.005f, halfL - 0.5f),
            new Vector3(width / 2f, 0.005f, halfL - 0.5f),
            new Color(1f, 0f, 0f, 0.7f), 0.03f, markersParent.transform
        );

        // Start label
        CreateMarkerLabel("START", new Vector3(0, 0.01f, -halfL + 0.5f), Color.green, markersParent.transform);

        // End label
        CreateMarkerLabel("END", new Vector3(0, 0.01f, halfL - 0.5f), Color.red, markersParent.transform);
    }

    void CreateMarkerLabel(string text, Vector3 position, Color color, Transform parent)
    {
        GameObject labelObj = new GameObject($"Label_{text}");
        labelObj.transform.SetParent(parent);
        labelObj.transform.localPosition = position;

        TextMesh textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.fontSize = 40;
        textMesh.characterSize = 0.15f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = color;

        // Face up
        labelObj.transform.localRotation = Quaternion.Euler(90, 0, 0);
    }

    void CreateLabels(float width, float height, float length)
    {
        GameObject labelsParent = new GameObject("DimensionLabels");
        labelsParent.transform.SetParent(fieldContainer.transform);
        labelsParent.transform.localPosition = Vector3.zero;

        float halfW = width / 2f;
        float halfL = length / 2f;

        // Width label along back wall
        CreateMarkerLabel($"{widthFeet}ft ({width:F1}m)",
            new Vector3(0, 0.01f, -halfL - 0.3f), Color.white, labelsParent.transform);

        // Length label along left wall
        GameObject lengthLabel = new GameObject("Label_Length");
        lengthLabel.transform.SetParent(labelsParent.transform);
        lengthLabel.transform.localPosition = new Vector3(-halfW - 0.3f, 0.01f, 0);
        TextMesh tm = lengthLabel.AddComponent<TextMesh>();
        tm.text = $"{lengthFeet}ft ({length:F1}m)";
        tm.fontSize = 40;
        tm.characterSize = 0.15f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;
        lengthLabel.transform.localRotation = Quaternion.Euler(90, 90, 0);
    }
}
