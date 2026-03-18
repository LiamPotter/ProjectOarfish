using UnityEngine;

/// <summary>
/// Snappy, physics-based boat controller that works on top of the Buoyancy
/// component and OceanManager's Gerstner waves.
///
/// Setup:
///   1. Add this component to a GameObject that already has:
///        - Rigidbody
///        - Buoyancy (from OceanSystem)
///   2. Assign propeller / engine transforms if available (optional, for VFX).
///   3. Controls: W/S = throttle,  A/D = rudder,  Shift = boost
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BoatController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Engine
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Engine")]
    [Tooltip("Maximum forward thrust force in Newtons.")]
    [Range(100f, 20000f)] public float maxThrust         = 5000f;

    [Tooltip("Reverse thrust as fraction of forward thrust.")]
    [Range(0f, 1f)]       public float reverseThrustRatio = 0.5f;

    [Tooltip("Hold Shift to apply this multiplier to thrust.")]
    [Range(1f, 3f)]       public float boostMultiplier   = 1.8f;

    [Tooltip("How quickly the engine builds up / winds down thrust. " +
             "Higher = snappier acceleration.")]
    [Range(1f, 20f)]      public float throttleResponse  = 6f;

    [Tooltip("Maximum forward speed (m/s). Physics drag is tuned to cap here.")]
    [Range(1f, 40f)]      public float maxSpeed          = 18f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Steering
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Steering")]
    [Tooltip("Maximum yaw torque applied by the rudder.")]
    [Range(100f, 10000f)] public float maxSteerTorque    = 2500f;

    [Tooltip("How quickly the rudder reaches full deflection.")]
    [Range(1f, 20f)]      public float steerResponse     = 8f;

    [Tooltip("Rudder effectiveness scales with speed. This is the minimum " +
             "effectiveness at zero speed (0 = no turning when stopped).")]
    [Range(0f, 1f)]       public float minSteerEffectiveness = 0.15f;

    [Tooltip("Lateral drag that makes the boat skid realistically rather than " +
             "pivoting on the spot.")]
    [Range(0f, 5f)]       public float lateralDragCoeff  = 2.0f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Wave reaction
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Wave Reaction")]
    [Tooltip("Multiplier on the wave-induced roll/pitch from Buoyancy.")]
    [Range(0f, 3f)]       public float waveReactionScale  = 1.2f;

    [Tooltip("Additional angular drag when riding over waves to prevent tumbling.")]
    [Range(0f, 10f)]      public float waveAngularDamping = 3f;

    [Tooltip("Spray / impact force multiplier when hull hits a wave crest.")]
    [Range(0f, 5f)]       public float waveImpactScale    = 1.5f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Propulsion point
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Propulsion")]
    [Tooltip("World-space point where thrust is applied. Defaults to rear of boat. " +
             "Assign a child Transform at the stern/propeller location.")]
    public Transform thrustPoint;

    [Tooltip("Optional propeller transform. Its Z rotation will be animated.")]
    public Transform propellerTransform;

    [Tooltip("Propeller spin speed multiplier relative to throttle.")]
    [Range(0f, 2000f)]    public float propellerSpinSpeed = 800f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Audio hooks (assign clips in BoatAudio or directly here)
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Events / State – Read-only")]
    [Tooltip("Current throttle position (−1 to 1). Readable by other systems.")]
    [Range(-1f, 1f)] public float CurrentThrottle;
    [Tooltip("Current rudder position (−1 to 1).")]
    [Range(-1f, 1f)] public float CurrentRudder;
    [Tooltip("Current speed in m/s along the boat's forward axis.")]
    public float CurrentSpeed;
    [Tooltip("Whether any part of the boat is currently underwater.")]
    public bool  IsInWater;

    public bool IsBoosting { get; private set; }
    // ─────────────────────────────────────────────────────────────────────────
    // Private
    // ─────────────────────────────────────────────────────────────────────────
    Rigidbody  _rb;
    Buoyancy   _buoyancy;

    float _throttleTarget;
    float _rudderTarget;
    float _smoothedThrottle;
    float _smoothedRudder;

    Vector3 _prevVelocity;   // for wave impact detection
    float   _prevWaveY;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        _rb       = GetComponent<Rigidbody>();
        _buoyancy = GetComponent<Buoyancy>();

        // Tune Rigidbody for a boat feel
        _rb.linearDamping     = 0.5f;
        _rb.angularDamping    = 2.0f;
        _rb.maxAngularVelocity = 2.5f;
        _rb.interpolation     = RigidbodyInterpolation.Interpolate;

        if (thrustPoint == null)
            thrustPoint = transform; // fallback to centre if not set
    }

    void Update()
    {
        GatherInput();
        AnimatePropeller();
        UpdateReadouts();
    }

    void FixedUpdate()
    {
        ApplyThrust();
        ApplySteering();
        ApplyLateralDrag();
        ApplyWaveAngularDamping();
        ClampSpeed();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Input
    // ─────────────────────────────────────────────────────────────────────────
    void GatherInput()
    {
        // Throttle: W = forward, S = reverse
        float rawThrottle = Input.GetAxisRaw("Vertical");    // W/S or Up/Down
        float rawRudder   = Input.GetAxisRaw("Horizontal");  // A/D or Left/Right
        bool  boost       = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        IsBoosting = boost;
        _throttleTarget = rawThrottle * (boost ? boostMultiplier : 1f);
        _throttleTarget = Mathf.Clamp(_throttleTarget, -reverseThrustRatio, boost ? boostMultiplier : 1f);
        _rudderTarget   = rawRudder;

        // Smooth toward target for snappy-but-not-instant feel
        _smoothedThrottle = Mathf.MoveTowards(_smoothedThrottle, _throttleTarget,
            throttleResponse * Time.deltaTime);
        _smoothedRudder   = Mathf.MoveTowards(_smoothedRudder, _rudderTarget,
            steerResponse * Time.deltaTime);

        CurrentThrottle = _smoothedThrottle;
        CurrentRudder   = _smoothedRudder;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Thrust
    // ─────────────────────────────────────────────────────────────────────────
    void ApplyThrust()
    {
        if (!IsInWater) return;

        float throttle = _smoothedThrottle;

        // Reverse gets a weaker force
        if (throttle < 0f) throttle *= reverseThrustRatio;

        // Scale thrust so the boat feels snappy at low speed but hits a soft cap
        float speedFraction = Mathf.Clamp01(CurrentSpeed / maxSpeed);
        float thrustCurve   = 1f - Mathf.Pow(speedFraction, 2f); // ease off near max speed

        Vector3 thrustForce = transform.forward * (throttle * maxThrust * thrustCurve);
        _rb.AddForceAtPosition(thrustForce, thrustPoint.position, ForceMode.Force);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Steering – torque around Y axis, effectiveness tied to speed
    // ─────────────────────────────────────────────────────────────────────────
    void ApplySteering()
    {
        if (!IsInWater) return;

        float speedFraction   = Mathf.Clamp01(CurrentSpeed / maxSpeed);
        float effectiveness   = Mathf.Lerp(minSteerEffectiveness, 1f, speedFraction);

        // Rudder only turns the boat meaningfully when there's forward motion
        float torque = _smoothedRudder * maxSteerTorque * effectiveness;
        _rb.AddTorque(transform.up * torque, ForceMode.Force);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lateral drag – prevents unrealistic sideways sliding
    // ─────────────────────────────────────────────────────────────────────────
    void ApplyLateralDrag()
    {
        Vector3 localVelocity    = transform.InverseTransformDirection(_rb.linearVelocity);
        // Oppose the lateral (X) component in local space
        Vector3 lateralVelocity  = transform.right * localVelocity.x;
        Vector3 lateralDragForce = -lateralVelocity * lateralDragCoeff * _rb.mass;
        _rb.AddForce(lateralDragForce, ForceMode.Force);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Wave angular damping – smooths out violent wave tumbling
    // ─────────────────────────────────────────────────────────────────────────
    void ApplyWaveAngularDamping()
    {
        _rb.AddTorque(-_rb.angularVelocity * waveAngularDamping, ForceMode.Force);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Speed cap
    // ─────────────────────────────────────────────────────────────────────────
    void ClampSpeed()
    {
        float boostCap = maxSpeed * boostMultiplier;
        if (_rb.linearVelocity.magnitude > boostCap)
            _rb.linearVelocity = _rb.linearVelocity.normalized * boostCap;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Propeller animation
    // ─────────────────────────────────────────────────────────────────────────
    void AnimatePropeller()
    {
        if (propellerTransform == null) return;
        float spin = _smoothedThrottle * propellerSpinSpeed * Time.deltaTime;
        propellerTransform.Rotate(Vector3.forward, spin, Space.Self);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public readouts
    // ─────────────────────────────────────────────────────────────────────────
    void UpdateReadouts()
    {
        // Speed along local forward
        CurrentSpeed = Vector3.Dot(_rb.linearVelocity, transform.forward);
        CurrentSpeed = Mathf.Max(0f, CurrentSpeed); // don't report negative speed

        // Water presence from buoyancy component
        IsInWater = _buoyancy != null && _buoyancy.enabled;
    }

    /// <summary>Public speed in knots (handy for HUD).</summary>
    public float GetSpeedKnots() => CurrentSpeed * 1.94384f;

    // ─────────────────────────────────────────────────────────────────────────
    // Gizmos
    // ─────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (thrustPoint == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(thrustPoint.position, 0.12f);
        Gizmos.DrawRay(thrustPoint.position, transform.forward * 2f);
    }
#endif
}
