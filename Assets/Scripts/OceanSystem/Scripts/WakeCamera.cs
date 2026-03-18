using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Owns the top-down orthographic camera that renders wake stamps into the
/// WakeRenderTexture, and runs the per-frame decay blit that fades old stamps.
///
/// The resulting RenderTexture is pushed to the ocean material each frame via
/// the global shader property <c>_WakeRT</c> and the world-space transform
/// parameters <c>_WakeRTWorldPos</c> and <c>_WakeRTWorldSize</c>, which the
/// ocean shader uses to project the wake into world space.
///
/// Setup:
///   1. Add this component to the boat (or any GameObject).
///   2. Assign <see cref="oceanMaterial"/> (the ocean surface material).
///   3. Assign <see cref="wakeStampMaterial"/> (Ocean/WakeRenderer material).
///   4. Assign <see cref="wakeDecayMaterial"/> (Ocean/WakeDecay material).
///   5. Assign <see cref="boatController"/> for speed-scaled stamp rate.
/// </summary>
[ExecuteAlways]
public class WakeCamera : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────────────────────────
    [Header("References")]
    public Material  oceanMaterial;
    public Material  wakeStampMaterial;
    public Material  wakeDecayMaterial;
    public BoatController boatController;

    [Header("Render Texture")]
    [Tooltip("Resolution of the wake RT. 512 is sufficient for most cases; " +
             "use 1024 for large, detailed wakes.")]
    public int  rtResolution  = 512;

    [Tooltip("World-space size of the area the RT covers (square). Should be at " +
             "least as wide as the longest expected wake trail.")]
    [Range(20f, 500f)] public float rtWorldSize = 120f;

    [Header("Decay")]
    [Tooltip("Per-frame multiplier on existing wake intensity. " +
             "0.97 = slow fade; 0.90 = fast fade. Applied at 60 fps baseline.")]
    [Range(0.8f, 0.999f)] public float decayRate = 0.975f;

    [Header("Stamp Rate")]
    [Tooltip("Minimum boat speed (m/s) before any stamps are placed.")]
    [Range(0f, 5f)] public float minSpeed = 0.5f;

    [Tooltip("World-space distance between successive wake stamps. Smaller = " +
             "denser, more continuous trail.")]
    [Range(0.2f, 5f)] public float stampSpacing = 1.2f;

    // ─────────────────────────────────────────────────────────────────────────
    // Private
    // ─────────────────────────────────────────────────────────────────────────
    RenderTexture _wakeRT;
    RenderTexture _tempRT;  // ping-pong for decay blit

    Camera        _wakeCamera;
    GameObject    _wakeCameraGO;

    // Current world-space centre the RT is anchored to (follows boat in steps
    // to avoid the RT sliding while the boat moves)
    Vector3       _rtCenter;
    bool          _rtCenterSet;

    Vector3       _lastStampPos;
    bool          _lastStampPosSet;

    // Shader property IDs
    static readonly int ID_WakeRT        = Shader.PropertyToID("_WakeRT");
    static readonly int ID_WakeRTPos     = Shader.PropertyToID("_WakeRTWorldPos");
    static readonly int ID_WakeRTSize    = Shader.PropertyToID("_WakeRTWorldSize");
    static readonly int ID_DecayRate     = Shader.PropertyToID("_DecayRate");
    static readonly int ID_SpeedFade     = Shader.PropertyToID("_SpeedFade");

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────
    void OnEnable()
    {
        BuildRT();
        BuildCamera();
    }

    void OnDisable()
    {
        TearDown();
    }

    void OnDestroy()
    {
        TearDown();
    }

    void LateUpdate()
    {
        if (_wakeRT == null) BuildRT();
        if (_wakeCameraGO == null) BuildCamera();

        UpdateRTCenter();
        RunDecay();
        TryPlaceStamp();
        PushToOceanMaterial();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RT and Camera construction
    // ─────────────────────────────────────────────────────────────────────────
    void BuildRT()
    {
        if (_wakeRT != null) _wakeRT.Release();
        if (_tempRT != null) _tempRT.Release();

        var desc = new RenderTextureDescriptor(rtResolution, rtResolution,
            RenderTextureFormat.ARGBHalf, 0);
        desc.useMipMap        = false;
        desc.autoGenerateMips = false;

        _wakeRT  = new RenderTexture(desc) { name = "WakeRT",  wrapMode = TextureWrapMode.Clamp };
        _tempRT  = new RenderTexture(desc) { name = "WakeTmp", wrapMode = TextureWrapMode.Clamp };
        _wakeRT.Create();
        _tempRT.Create();

        // Clear to black
        var prev = RenderTexture.active;
        RenderTexture.active = _wakeRT;
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = prev;
    }

    void BuildCamera()
    {
        if (_wakeCameraGO != null) return;

        _wakeCameraGO = new GameObject("__WakeCamera") { hideFlags = HideFlags.HideAndDontSave };
        _wakeCamera   = _wakeCameraGO.AddComponent<Camera>();
        _wakeCamera.orthographic         = true;
        _wakeCamera.orthographicSize     = rtWorldSize * 0.5f;
        _wakeCamera.clearFlags           = CameraClearFlags.Nothing;
        _wakeCamera.cullingMask          = LayerMask.GetMask("WakeStamps");
        _wakeCamera.nearClipPlane        = 0.1f;
        _wakeCamera.farClipPlane         = 100f;
        _wakeCamera.enabled              = false;   // we render manually
        _wakeCamera.targetTexture        = _wakeRT;
        _wakeCamera.allowHDR             = false;
        _wakeCamera.allowMSAA            = false;

        // Point straight down
        _wakeCameraGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    void TearDown()
    {
        if (_wakeRT  != null) { _wakeRT.Release();  _wakeRT  = null; }
        if (_tempRT  != null) { _tempRT.Release();  _tempRT  = null; }
        if (_wakeCameraGO != null)
        {
            if (Application.isPlaying) Destroy(_wakeCameraGO);
            else                       DestroyImmediate(_wakeCameraGO);
            _wakeCameraGO = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RT centre tracking
    // RT follows the boat in coarse steps (half the world size) to avoid
    // the ocean UV sliding continuously — the ocean shader handles fine
    // sub-texel offset via the world position parameters.
    // ─────────────────────────────────────────────────────────────────────────
    void UpdateRTCenter()
    {
        Vector3 boatPos = transform.position;

        if (!_rtCenterSet)
        {
            _rtCenter    = boatPos;
            _rtCenterSet = true;
        }

        // Step the RT center if the boat has moved more than 1/4 of the world size
        float stepThreshold = rtWorldSize * 0.25f;
        float dx = boatPos.x - _rtCenter.x;
        float dz = boatPos.z - _rtCenter.z;

        if (Mathf.Abs(dx) > stepThreshold)
            _rtCenter.x = boatPos.x;
        if (Mathf.Abs(dz) > stepThreshold)
            _rtCenter.z = boatPos.z;

        // Keep camera over the RT center
        if (_wakeCameraGO != null)
        {
            _wakeCameraGO.transform.position = new Vector3(
                _rtCenter.x, transform.position.y + 50f, _rtCenter.z);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Decay blit
    // ─────────────────────────────────────────────────────────────────────────
    void RunDecay()
    {
        if (wakeDecayMaterial == null || _wakeRT == null || _tempRT == null) return;

        // Normalise decay to 60 fps so it feels consistent regardless of frame rate
        float frameDecay = Mathf.Pow(decayRate, Time.deltaTime * 60f);
        wakeDecayMaterial.SetFloat(ID_DecayRate, frameDecay);

        // Blit wakeRT → tempRT with decay, then copy back
        Graphics.Blit(_wakeRT, _tempRT, wakeDecayMaterial);
        Graphics.Blit(_tempRT, _wakeRT);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Wake stamp placement
    // ─────────────────────────────────────────────────────────────────────────
    void TryPlaceStamp()
    {
        if (wakeStampMaterial == null) return;

        float speed = boatController != null ? boatController.CurrentSpeed : 0f;
        if (speed < minSpeed) return;

        Vector3 pos = transform.position;
        if (!_lastStampPosSet)
        {
            _lastStampPos    = pos;
            _lastStampPosSet = true;
            return;
        }

        if (Vector3.Distance(pos, _lastStampPos) < stampSpacing) return;

        // Direction the boat is moving (for stamp orientation)
        Vector3 dir = (pos - _lastStampPos).normalized;
        PlaceStamp(pos, dir, speed);
        _lastStampPos = pos;
    }

    void PlaceStamp(Vector3 worldPos, Vector3 forwardDir, float speed)
    {
        if (_wakeCamera == null || _wakeRT == null) return;

        float speedFrac = boatController != null
            ? Mathf.Clamp01(speed / boatController.maxSpeed)
            : 1f;

        wakeStampMaterial.SetFloat(ID_SpeedFade, speedFrac);

        // Convert world pos to RT UV space [0,1]
        Vector2 uv = WorldToWakeUV(worldPos);

        // Stamp size in UV space — wider at higher speeds
        float stampWorldWidth = Mathf.Lerp(1.5f, 4f, speedFrac);
        float stampWorldLen   = Mathf.Lerp(2f,   8f, speedFrac);
        float uvW = stampWorldWidth / rtWorldSize;
        float uvH = stampWorldLen   / rtWorldSize;

        // Stamp angle matches boat heading in the RT
        float yaw = Mathf.Atan2(forwardDir.x, forwardDir.z) * Mathf.Rad2Deg;

        // Draw stamp quad into the RT using GL
        var prevRT = RenderTexture.active;
        RenderTexture.active = _wakeRT;

        GL.PushMatrix();
        GL.LoadOrtho();

        wakeStampMaterial.SetPass(0);

        // Build a tiny rotated quad around the UV position
        float hw = uvW * 0.5f;
        float hh = uvH * 0.5f;
        float rad = -yaw * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        Vector2 TL = RotateUV(new Vector2(-hw,  hh), cos, sin) + uv;
        Vector2 TR = RotateUV(new Vector2( hw,  hh), cos, sin) + uv;
        Vector2 BL = RotateUV(new Vector2(-hw, -hh), cos, sin) + uv;
        Vector2 BR = RotateUV(new Vector2( hw, -hh), cos, sin) + uv;

        GL.Begin(GL.QUADS);
        GL.Color(new Color(1f, 1f, 1f, speedFrac)); // alpha = speed
        GL.TexCoord2(0, 0); GL.Vertex3(BL.x, BL.y, 0);
        GL.TexCoord2(1, 0); GL.Vertex3(BR.x, BR.y, 0);
        GL.TexCoord2(1, 1); GL.Vertex3(TR.x, TR.y, 0);
        GL.TexCoord2(0, 1); GL.Vertex3(TL.x, TL.y, 0);
        GL.End();

        GL.PopMatrix();
        RenderTexture.active = prevRT;
    }

    Vector2 WorldToWakeUV(Vector3 worldPos)
    {
        float u = (worldPos.x - _rtCenter.x) / rtWorldSize + 0.5f;
        float v = (worldPos.z - _rtCenter.z) / rtWorldSize + 0.5f;
        return new Vector2(u, v);
    }

    static Vector2 RotateUV(Vector2 v, float cos, float sin)
        => new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);

    // ─────────────────────────────────────────────────────────────────────────
    // Push RT + world transform to ocean material
    // ─────────────────────────────────────────────────────────────────────────
    void PushToOceanMaterial()
    {
        if (oceanMaterial == null || _wakeRT == null) return;

        oceanMaterial.SetTexture(ID_WakeRT,    _wakeRT);
        oceanMaterial.SetVector( ID_WakeRTPos, new Vector4(_rtCenter.x, _rtCenter.z, 0, 0));
        oceanMaterial.SetFloat(  ID_WakeRTSize, rtWorldSize);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Editor helper — draw RT coverage in scene view
    // ─────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!_rtCenterSet) return;
        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
        Gizmos.DrawWireCube(
            new Vector3(_rtCenter.x, transform.position.y, _rtCenter.z),
            new Vector3(rtWorldSize, 0.1f, rtWorldSize));
    }
#endif
}
