using UnityEngine;
using System.Collections.Generic;

public class GridWorldGenerator : MonoBehaviour
{
    [Header("Grid Settings")]
    [Tooltip("Total grid size in meters (creates a square grid)")]
    public int gridSize = 100;

    [Tooltip("Distance between grid lines in meters")]
    public float gridSpacing = 1f;

    [Tooltip("Highlight every N grid lines as major lines")]
    public int majorLineInterval = 5;

    [Header("3D Grid Settings")]
    public bool create3DGrid = true;
    public int gridHeight = 100; // Height of the 3D grid in meters
    public int verticalLineInterval = 10; // Place vertical lines every N meters
    public int horizontalPlaneInterval = 10; // Create horizontal grid planes every N meters

    [Header("Grid Appearance")]
    public Color minorGridColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    public Color majorGridColor = new Color(1f, 1f, 1f, 0.8f); // White
    public Color xAxisColor = new Color(1f, 0.3f, 0.3f, 1f); // Red for X axis
    public Color zAxisColor = new Color(0.3f, 0.3f, 1f, 1f); // Blue for Z axis
    public Color yAxisColor = new Color(0.3f, 1f, 0.3f, 1f); // Green for Y axis (vertical)
    public Color verticalLineColor = new Color(0.5f, 0.5f, 0.5f, 0.6f); // Color for vertical lines
    public float lineWidth = 0.02f;
    public float majorLineWidth = 0.04f;

    [Header("Coordinate Labels")]
    public bool showCoordinateLabels = true;
    public int labelInterval = 10; // Show label every N meters
    public float labelHeight = 0.1f;
    public float labelSize = 0.5f;

    [Header("Landing Pads")]
    public bool createLandingPads = true;
    public int landingPadInterval = 10; // Place landing pad every N meters (matches label interval)
    public float landingPadSize = 2f;
    public Color landingPadColor = new Color(1f, 0.5f, 0f, 0.8f); // Orange

    [Header("Colored Zones")]
    public bool createColoredZones = true;
    public Zone[] zones = new Zone[] {
        new Zone { center = new Vector3(30, 0, 30), size = new Vector3(15, 0.05f, 15), color = new Color(0f, 0.5f, 1f, 0.3f) },
        new Zone { center = new Vector3(-30, 0, -30), size = new Vector3(15, 0.05f, 15), color = new Color(1f, 0.5f, 0f, 0.3f) }
    };

    private GameObject gridContainer;

    [System.Serializable]
    public class Zone
    {
        public Vector3 center;
        public Vector3 size;
        public Color color;
    }

    void Start()
    {
        GenerateGridWorld();
    }

    [ContextMenu("Regenerate Grid World")]
    public void RegenerateGridWorld()
    {
        ClearGridWorld();
        GenerateGridWorld();
    }

    void ClearGridWorld()
    {
        if (gridContainer != null)
        {
            DestroyImmediate(gridContainer);
        }
    }

    void GenerateGridWorld()
    {
        gridContainer = new GameObject("GridWorld");
        gridContainer.transform.SetParent(transform);
        gridContainer.transform.localPosition = Vector3.zero;

        CreateGrid();
        if (create3DGrid) Create3DGrid();
        if (showCoordinateLabels) CreateCoordinateLabels();
        if (createLandingPads) CreateLandingPads();
        if (createColoredZones) CreateColoredZones();
    }

    void CreateGrid()
    {
        GameObject gridParent = new GameObject("GridLines");
        gridParent.transform.SetParent(gridContainer.transform);
        gridParent.transform.localPosition = Vector3.zero;

        int halfSize = gridSize / 2;
        float yOffset = 0.01f; // Slightly above ground

        // Create grid lines
        for (int i = -halfSize; i <= halfSize; i++)
        {
            float pos = i * gridSpacing;
            bool isMajorLine = (i % majorLineInterval == 0);
            bool isAxisLine = (i == 0);

            // Lines parallel to X axis (running East-West)
            GameObject lineX = CreateLine(
                new Vector3(-halfSize * gridSpacing, yOffset, pos),
                new Vector3(halfSize * gridSpacing, yOffset, pos),
                isAxisLine ? xAxisColor : (isMajorLine ? majorGridColor : minorGridColor),
                isAxisLine ? majorLineWidth * 1.5f : (isMajorLine ? majorLineWidth : lineWidth),
                gridParent.transform
            );
            lineX.name = $"GridLine_X_{i}";

            // Lines parallel to Z axis (running North-South)
            GameObject lineZ = CreateLine(
                new Vector3(pos, yOffset, -halfSize * gridSpacing),
                new Vector3(pos, yOffset, halfSize * gridSpacing),
                isAxisLine ? zAxisColor : (isMajorLine ? majorGridColor : minorGridColor),
                isAxisLine ? majorLineWidth * 1.5f : (isMajorLine ? majorLineWidth : lineWidth),
                gridParent.transform
            );
            lineZ.name = $"GridLine_Z_{i}";
        }
    }

    void Create3DGrid()
    {
        GameObject grid3DParent = new GameObject("3DGridLines");
        grid3DParent.transform.SetParent(gridContainer.transform);
        grid3DParent.transform.localPosition = Vector3.zero;

        int halfSize = gridSize / 2;

        // Create vertical lines at intersections
        for (int x = -halfSize; x <= halfSize; x += verticalLineInterval)
        {
            for (int z = -halfSize; z <= halfSize; z += verticalLineInterval)
            {
                bool isXAxis = (x == 0 && z == 0);
                Color lineColor = isXAxis ? yAxisColor : verticalLineColor;
                float width = isXAxis ? majorLineWidth * 1.5f : lineWidth;

                GameObject vertLine = CreateLine(
                    new Vector3(x, 0, z),
                    new Vector3(x, gridHeight, z),
                    lineColor,
                    width,
                    grid3DParent.transform
                );
                vertLine.name = $"VerticalLine_{x}_{z}";
            }
        }

        // Create horizontal grid planes at different heights
        for (int height = horizontalPlaneInterval; height <= gridHeight; height += horizontalPlaneInterval)
        {
            GameObject planeParent = new GameObject($"HorizontalGrid_Y{height}");
            planeParent.transform.SetParent(grid3DParent.transform);
            planeParent.transform.localPosition = Vector3.zero;

            // Create grid lines at this height (same pattern as ground grid)
            for (int i = -halfSize; i <= halfSize; i++)
            {
                float pos = i * gridSpacing;
                bool isMajorLine = (i % majorLineInterval == 0);

                // Only create major lines at height to reduce line count
                if (!isMajorLine) continue;

                // Lines parallel to X axis (running East-West)
                GameObject lineX = CreateLine(
                    new Vector3(-halfSize * gridSpacing, height, pos),
                    new Vector3(halfSize * gridSpacing, height, pos),
                    majorGridColor,
                    lineWidth,
                    planeParent.transform
                );
                lineX.name = $"GridLine_X_{i}_H{height}";

                // Lines parallel to Z axis (running North-South)
                GameObject lineZ = CreateLine(
                    new Vector3(pos, height, -halfSize * gridSpacing),
                    new Vector3(pos, height, halfSize * gridSpacing),
                    majorGridColor,
                    lineWidth,
                    planeParent.transform
                );
                lineZ.name = $"GridLine_Z_{i}_H{height}";
            }
        }
    }

    GameObject CreateLine(Vector3 start, Vector3 end, Color color, float width, Transform parent)
    {
        GameObject lineObj = new GameObject("Line");
        lineObj.transform.SetParent(parent);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();

        // Use unlit shader so colors appear exactly as specified
        Material lineMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lineMat.color = color;
        lr.material = lineMat;

        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.useWorldSpace = false;

        // Disable shadows for better performance
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        return lineObj;
    }

    void CreateCoordinateLabels()
    {
        GameObject labelsParent = new GameObject("CoordinateLabels");
        labelsParent.transform.SetParent(gridContainer.transform);
        labelsParent.transform.localPosition = Vector3.zero;

        int halfSize = gridSize / 2;

        for (int x = -halfSize; x <= halfSize; x += labelInterval)
        {
            for (int z = -halfSize; z <= halfSize; z += labelInterval)
            {
                Vector3 position = new Vector3(x, labelHeight, z);
                CreateLabel($"({x},{z})", position, labelsParent.transform);
            }
        }
    }

    GameObject CreateLabel(string text, Vector3 position, Transform parent)
    {
        GameObject labelObj = new GameObject($"Label_{text}");
        labelObj.transform.SetParent(parent);
        labelObj.transform.position = position;

        TextMesh textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.fontSize = 40; // 2x the original size (was 20)
        textMesh.characterSize = labelSize * 0.1f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;

        // Make label face up
        labelObj.transform.rotation = Quaternion.Euler(90, 0, 0);

        return labelObj;
    }

    void CreateLandingPads()
    {
        GameObject padsParent = new GameObject("LandingPads");
        padsParent.transform.SetParent(gridContainer.transform);
        padsParent.transform.localPosition = Vector3.zero;

        int halfSize = gridSize / 2;

        // Create landing pads at every major grid intersection (matching coordinate labels)
        for (int x = -halfSize; x <= halfSize; x += landingPadInterval)
        {
            for (int z = -halfSize; z <= halfSize; z += landingPadInterval)
            {
                Vector3 pos = new Vector3(x, 0, z);
                GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pad.name = $"LandingPad_{x}_{z}";
                pad.transform.SetParent(padsParent.transform);
                pad.transform.position = pos;
                pad.transform.localScale = new Vector3(landingPadSize, 0.05f, landingPadSize);

                Material padMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                padMat.color = landingPadColor;
                pad.GetComponent<Renderer>().material = padMat;

                // Add collider for landing detection
                pad.GetComponent<Collider>().isTrigger = true;
            }
        }
    }

    void CreateColoredZones()
    {
        GameObject zonesParent = new GameObject("ColoredZones");
        zonesParent.transform.SetParent(gridContainer.transform);
        zonesParent.transform.localPosition = Vector3.zero;

        for (int i = 0; i < zones.Length; i++)
        {
            Zone zone = zones[i];
            GameObject zoneObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zoneObj.name = $"Zone_{i}";
            zoneObj.transform.SetParent(zonesParent.transform);
            zoneObj.transform.position = zone.center;
            zoneObj.transform.localScale = zone.size;

            Material zoneMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            zoneMat.color = zone.color;
            // Make it transparent
            zoneMat.SetFloat("_Surface", 1); // Transparent
            zoneMat.SetFloat("_Blend", 0); // Alpha blending
            zoneObj.GetComponent<Renderer>().material = zoneMat;

            // Remove collider or make it trigger
            DestroyImmediate(zoneObj.GetComponent<Collider>());
        }
    }
}
