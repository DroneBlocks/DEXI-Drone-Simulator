using UnityEngine;

/// <summary>
/// Provides cached runtime material instances.
/// Uses serialized base materials to avoid Shader.Find() which fails in WebGL builds.
/// Attach to a GameObject in the scene or use as a singleton.
/// Base materials should be assigned in the Inspector (drag URP Lit/Unlit materials).
/// If not assigned, falls back to Resources.Load, then Shader.Find as last resort.
/// </summary>
public class RuntimeMaterials : MonoBehaviour
{
    private static RuntimeMaterials _instance;
    public static RuntimeMaterials Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<RuntimeMaterials>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("RuntimeMaterials");
                    _instance = go.AddComponent<RuntimeMaterials>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [Header("Base Materials (assign in Inspector)")]
    [Tooltip("URP Unlit base material — used for tags, grid lines, overlays")]
    [SerializeField] private Material unlitBase;

    [Tooltip("URP Lit base material — used for floors, walls, pads")]
    [SerializeField] private Material litBase;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Pre-warm: load base materials immediately so they're ready for other scripts
        GetUnlitBase();
        GetLitBase();
        Debug.Log($"RuntimeMaterials: Unlit={unlitBase != null}, Lit={litBase != null}");
    }

    /// <summary>
    /// Create a new Unlit material instance. Safe for WebGL builds.
    /// </summary>
    public Material CreateUnlit(Color color)
    {
        Material mat = new Material(GetUnlitBase());
        mat.color = color;
        return mat;
    }

    /// <summary>
    /// Create a new Unlit material with a texture. Safe for WebGL builds.
    /// </summary>
    public Material CreateUnlit(Texture2D texture)
    {
        Material mat = new Material(GetUnlitBase());
        mat.mainTexture = texture;
        return mat;
    }

    /// <summary>
    /// Create a new Lit material instance. Safe for WebGL builds.
    /// </summary>
    public Material CreateLit(Color color)
    {
        Material mat = new Material(GetLitBase());
        mat.color = color;
        return mat;
    }

    /// <summary>
    /// Create a transparent Lit material. Safe for WebGL builds.
    /// </summary>
    public Material CreateLitTransparent(Color color)
    {
        Material mat = new Material(GetLitBase());
        mat.color = color;
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 0);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return mat;
    }

    private Material GetUnlitBase()
    {
        if (unlitBase != null) return unlitBase;

        // Fallback: try Resources
        unlitBase = Resources.Load<Material>("Materials/URPUnlit");
        if (unlitBase != null) return unlitBase;

        // Last resort: Shader.Find (works in editor, may fail in WebGL)
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
        {
            unlitBase = new Material(shader);
            Debug.LogWarning("RuntimeMaterials: Using Shader.Find fallback for Unlit — assign base material in Inspector for WebGL builds");
        }
        else
        {
            Debug.LogError("RuntimeMaterials: Cannot find URP Unlit shader!");
            unlitBase = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        return unlitBase;
    }

    private Material GetLitBase()
    {
        if (litBase != null) return litBase;

        // Fallback: try Resources
        litBase = Resources.Load<Material>("Materials/URPLit");
        if (litBase != null) return litBase;

        // Last resort: Shader.Find
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
        {
            litBase = new Material(shader);
            Debug.LogWarning("RuntimeMaterials: Using Shader.Find fallback for Lit — assign base material in Inspector for WebGL builds");
        }
        else
        {
            Debug.LogError("RuntimeMaterials: Cannot find URP Lit shader!");
            litBase = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        return litBase;
    }
}
