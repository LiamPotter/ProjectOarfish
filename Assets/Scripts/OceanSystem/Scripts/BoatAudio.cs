using UnityEngine;

/// <summary>
/// Reactive audio for the boat controller.
/// Modulates engine pitch/volume with throttle and speed,
/// and fires a splash/impact sound when the hull hits a wave crest.
///
/// Requires: BoatController on the same or parent GameObject.
/// Assign AudioClips in the Inspector.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BoatAudio : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Engine")]
    [Tooltip("Looping engine idle / running sound.")]
    public AudioClip engineLoop;
    [Tooltip("Engine pitch at idle (no throttle).")]
    [Range(0.3f, 1.5f)] public float enginePitchIdle = 0.6f;
    [Tooltip("Engine pitch at full throttle.")]
    [Range(0.5f, 3f)]   public float enginePitchMax  = 1.8f;
    [Tooltip("Engine volume at idle.")]
    [Range(0f, 1f)]     public float engineVolumeIdle = 0.25f;
    [Tooltip("Engine volume at full throttle.")]
    [Range(0f, 1f)]     public float engineVolumeMax  = 0.9f;
    [Tooltip("How quickly pitch and volume track throttle changes.")]
    [Range(1f, 15f)]    public float engineResponse   = 5f;

    [Header("Impacts")]
    [Tooltip("Splash / hull-slam sounds. One is chosen at random per impact.")]
    public AudioClip[] impactClips;
    [Tooltip("Minimum speed (m/s) for an impact sound to play.")]
    [Range(0f, 10f)]    public float impactSpeedThreshold = 3f;
    [Tooltip("Minimum time (s) between impact sounds to avoid spam.")]
    [Range(0.1f, 3f)]   public float impactCooldown       = 0.5f;
    [Range(0f, 1f)]     public float impactVolume          = 0.8f;

    [Header("Water")]
    [Tooltip("Looping water/wake sound that grows with speed.")]
    public AudioClip wakeLoop;
    [Range(0f, 1f)]     public float wakeVolumeMax         = 0.5f;

    // ─────────────────────────────────────────────────────────────────────────
    // Private
    // ─────────────────────────────────────────────────────────────────────────
    BoatController _boat;
    AudioSource    _engineSource;
    AudioSource    _wakeSource;
    AudioSource    _impactSource;

    float _impactTimer;
    float _prevSpeed;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        _boat = GetComponentInParent<BoatController>();
        if (_boat == null) _boat = GetComponent<BoatController>();

        // Primary AudioSource = engine
        _engineSource          = GetComponent<AudioSource>();
        _engineSource.clip     = engineLoop;
        _engineSource.loop     = true;
        _engineSource.spatialBlend = 1f;
        _engineSource.playOnAwake  = false;

        // Wake source
        _wakeSource = gameObject.AddComponent<AudioSource>();
        _wakeSource.clip          = wakeLoop;
        _wakeSource.loop          = true;
        _wakeSource.spatialBlend  = 1f;
        _wakeSource.volume        = 0f;
        _wakeSource.playOnAwake   = false;

        // Impact source (one-shots)
        _impactSource = gameObject.AddComponent<AudioSource>();
        _impactSource.spatialBlend = 1f;
        _impactSource.playOnAwake  = false;
    }

    void OnEnable()
    {
        if (engineLoop != null)  _engineSource.Play();
        if (wakeLoop != null)    _wakeSource.Play();
    }

    void OnDisable()
    {
        _engineSource.Stop();
        _wakeSource.Stop();
    }

    void Update()
    {
        if (_boat == null) return;

        UpdateEngine();
        UpdateWake();
        UpdateImpacts();

        _impactTimer -= Time.deltaTime;
        _prevSpeed    = _boat.CurrentSpeed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Engine modulation
    // ─────────────────────────────────────────────────────────────────────────
    void UpdateEngine()
    {
        float throttleAbs = Mathf.Abs(_boat.CurrentThrottle);
        float t           = throttleAbs; // 0–1

        t *= _boat.IsBoosting ? 1 : 0.75f;

        float targetPitch  = Mathf.Lerp(enginePitchIdle,   enginePitchMax,   t);
        float targetVolume = Mathf.Lerp(engineVolumeIdle,  engineVolumeMax,  t);

        _engineSource.pitch  = Mathf.Lerp(_engineSource.pitch,  targetPitch,  engineResponse * Time.deltaTime);
        _engineSource.volume = Mathf.Lerp(_engineSource.volume, targetVolume, engineResponse * Time.deltaTime);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Wake / water noise
    // ─────────────────────────────────────────────────────────────────────────
    void UpdateWake()
    {
        if (wakeLoop == null) return;
        float speedFrac = Mathf.Clamp01(_boat.CurrentSpeed / _boat.maxSpeed);
        _wakeSource.volume = Mathf.Lerp(_wakeSource.volume, speedFrac * wakeVolumeMax,
            3f * Time.deltaTime);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Hull impact (wave slam)
    // ─────────────────────────────────────────────────────────────────────────
    void UpdateImpacts()
    {
        if (impactClips == null || impactClips.Length == 0) return;
        if (_impactTimer > 0f) return;
        if (_boat.CurrentSpeed < impactSpeedThreshold) return;

        // Detect sudden downward deceleration (hull hitting water after a wave)
        float speedDelta = _boat.CurrentSpeed - _prevSpeed;
        if (speedDelta < -2f) // speed dropped this frame — likely wave impact
        {
            AudioClip clip = impactClips[Random.Range(0, impactClips.Length)];
            _impactSource.PlayOneShot(clip, impactVolume);
            _impactTimer = impactCooldown;
        }
    }
}
