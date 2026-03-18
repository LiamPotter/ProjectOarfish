using UnityEngine;

/// <summary>
/// Central manager for the ocean simulation.
/// Mirrors the Gerstner wave math from the shader so that C# systems
/// (e.g. buoyancy) can query the exact water height at any world position.
///
/// Place this component on the same GameObject as your ocean mesh / material.
/// </summary>
[ExecuteAlways]
public class OceanManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Singleton
    // -------------------------------------------------------------------------
    public static OceanManager Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Inspector – Wave Parameters  (must match shader properties)
    // -------------------------------------------------------------------------
    [Header("Wave Parameters")]
    [Tooltip("Vertical scale of all waves.")]
    [Range(0f, 10f)]  public float waveHeight    = 1f;
    [Tooltip("Playback speed multiplier applied to all waves.")]
    [Range(0f, 5f)]   public float waveSpeed     = 1f;
    [Tooltip("Dominant wave length in world units.")]
    [Range(1f, 100f)] public float waveLength    = 20f;
    [Tooltip("Gerstner steepness (0 = sine, 1 = sharp crests).")]
    [Range(0f, 1f)]   public float waveSteepness = 0.5f;

    [Space]
    [Tooltip("Primary wave propagation direction (XZ).")]
    public Vector2 waveDirection1 = new Vector2(1f,    0.5f);
    [Tooltip("Secondary wave propagation direction (XZ).")]
    public Vector2 waveDirection2 = new Vector2(0.8f,  0.6f);
    [Tooltip("Tertiary wave propagation direction (XZ).")]
    public Vector2 waveDirection3 = new Vector2(0.3f, -0.9f);

    // -------------------------------------------------------------------------
    // Inspector – Shader / Material link
    // -------------------------------------------------------------------------
    [Header("Material")]
    [Tooltip("Renderer whose material uses Ocean.shader.")]
    public Renderer oceanRenderer;

    // Cached material property IDs
    static readonly int ID_WaveHeight    = Shader.PropertyToID("_WaveHeight");
    static readonly int ID_WaveSpeed     = Shader.PropertyToID("_WaveSpeed");
    static readonly int ID_WaveLength    = Shader.PropertyToID("_WaveLength");
    static readonly int ID_WaveSteepness = Shader.PropertyToID("_WaveSteepness");
    static readonly int ID_WaveDir1      = Shader.PropertyToID("_WaveDirection1");
    static readonly int ID_WaveDir2      = Shader.PropertyToID("_WaveDirection2");
    static readonly int ID_WaveDir3      = Shader.PropertyToID("_WaveDirection3");

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[OceanManager] Duplicate instance destroyed.", this);
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        PushToMaterial();
    }

    // -------------------------------------------------------------------------
    // Material sync
    // -------------------------------------------------------------------------
    void PushToMaterial()
    {
        if (oceanRenderer == null) return;
        Material mat = Application.isPlaying
            ? oceanRenderer.material
            : oceanRenderer.sharedMaterial;

        if (mat == null) return;

        mat.SetFloat(ID_WaveHeight,    waveHeight);
        mat.SetFloat(ID_WaveSpeed,     waveSpeed);
        mat.SetFloat(ID_WaveLength,    waveLength);
        mat.SetFloat(ID_WaveSteepness, waveSteepness);
        mat.SetVector(ID_WaveDir1, new Vector4(waveDirection1.x, 0, waveDirection1.y, 0));
        mat.SetVector(ID_WaveDir2, new Vector4(waveDirection2.x, 0, waveDirection2.y, 0));
        mat.SetVector(ID_WaveDir3, new Vector4(waveDirection3.x, 0, waveDirection3.y, 0));
    }

    // -------------------------------------------------------------------------
    // Public API – Wave sampling
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the world-space Y position (height) of the ocean surface
    /// directly above/below the given world XZ position.
    /// </summary>
    public float GetWaveHeight(Vector3 worldPosition)
    {
        return GetWaveDisplacement(worldPosition).y + transform.position.y;
    }

    /// <summary>
    /// Returns the full Gerstner displacement (XYZ) at the given world position.
    /// Y component is the vertical wave offset from the ocean plane.
    /// </summary>
    public Vector3 GetWaveDisplacement(Vector3 worldPosition)
    {
        float t = Application.isPlaying ? Time.time : (float)UnityEditor.EditorApplication.timeSinceStartup;
        float scaledTime = t * waveSpeed;

        Vector3 total = Vector3.zero;
        total += GerstnerWave(worldPosition, waveDirection1.normalized,   waveSteepness,       waveLength,        scaledTime);
        total += GerstnerWave(worldPosition, waveDirection2.normalized,   waveSteepness * 0.7f, waveLength * 0.6f, scaledTime);
        total += GerstnerWave(worldPosition, waveDirection3.normalized,   waveSteepness * 0.5f, waveLength * 0.4f, scaledTime);

        total.y *= waveHeight;
        return total;
    }

    /// <summary>
    /// Returns the surface normal at the given world position.
    /// </summary>
    public Vector3 GetWaveNormal(Vector3 worldPosition)
    {
        float t = Application.isPlaying ? Time.time : (float)UnityEditor.EditorApplication.timeSinceStartup;
        float scaledTime = t * waveSpeed;

        Vector3 n = Vector3.zero;
        n += GerstnerNormal(worldPosition, waveDirection1.normalized,   waveSteepness,       waveLength,        scaledTime);
        n += GerstnerNormal(worldPosition, waveDirection2.normalized,   waveSteepness * 0.7f, waveLength * 0.6f, scaledTime);
        n += GerstnerNormal(worldPosition, waveDirection3.normalized,   waveSteepness * 0.5f, waveLength * 0.4f, scaledTime);
        return n.normalized;
    }

    // -------------------------------------------------------------------------
    // Gerstner math (mirrors the shader)
    // -------------------------------------------------------------------------
    static Vector3 GerstnerWave(Vector3 pos, Vector2 dir, float steepness, float wavelength, float time)
    {
        dir = dir.normalized;
        float k = 2f * Mathf.PI / wavelength;
        float c = Mathf.Sqrt(9.8f / k);
        float a = steepness / k;
        float f = k * (Vector2.Dot(dir, new Vector2(pos.x, pos.z)) - c * time);

        return new Vector3(
            dir.x * (a * Mathf.Cos(f)),
            a      *  Mathf.Sin(f),
            dir.y * (a * Mathf.Cos(f))
        );
    }

    static Vector3 GerstnerNormal(Vector3 pos, Vector2 dir, float steepness, float wavelength, float time)
    {
        dir = dir.normalized;
        float k = 2f * Mathf.PI / wavelength;
        float c = Mathf.Sqrt(9.8f / k);
        float a = steepness / k;
        float f = k * (Vector2.Dot(dir, new Vector2(pos.x, pos.z)) - c * time);

        return new Vector3(
            -(dir.x * k * a * Mathf.Cos(f)),
            1f - (steepness * Mathf.Sin(f)),
            -(dir.y * k * a * Mathf.Cos(f))
        );
    }

    // -------------------------------------------------------------------------
    // Editor helper
    // -------------------------------------------------------------------------
#if UNITY_EDITOR
    void OnValidate()
    {
        PushToMaterial();
    }
#endif
}
