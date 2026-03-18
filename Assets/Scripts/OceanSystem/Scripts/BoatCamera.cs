using UnityEngine;

/// <summary>
/// Third-person spring-arm camera that orbits a boat with the mouse,
/// follows it smoothly, and gently compensates for wave-induced tilting
/// so the horizon stays readable.
///
/// Setup:
///   1. Add this component to your Camera GameObject.
///   2. Assign the boat's Transform to <see cref="target"/>.
///   3. Optionally assign the <see cref="BoatController"/> for speed-based FOV.
///
/// Mouse:  RMB hold (or always) to orbit.
/// Scroll: Zoom in / out.
/// </summary>
public class BoatCamera : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Target
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Target")]
    [Tooltip("The boat Transform to follow.")]
    public Transform target;

    [Tooltip("Offset from the boat pivot that the camera looks at (e.g. raise to cockpit height).")]
    public Vector3 lookAtOffset = new Vector3(0f, 1.2f, 0f);

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Orbit
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Orbit")]
    [Tooltip("Horizontal mouse sensitivity.")]
    [Range(0.5f, 10f)] public float sensitivityX  = 3.5f;
    [Tooltip("Vertical mouse sensitivity.")]
    [Range(0.5f, 10f)] public float sensitivityY  = 3.0f;

    [Tooltip("Hold right mouse button to orbit. If false, mouse always controls camera.")]
    public bool requireRightMouseButton = true;

    [Tooltip("Minimum vertical (pitch) angle in degrees.")]
    [Range(-30f, 0f)]  public float pitchMin = -15f;
    [Tooltip("Maximum vertical (pitch) angle in degrees.")]
    [Range(5f, 80f)]   public float pitchMax =  65f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Distance / Zoom
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Distance")]
    [Range(2f, 40f)]   public float defaultDistance = 12f;
    [Range(2f, 40f)]   public float minDistance     = 3f;
    [Range(2f, 40f)]   public float maxDistance     = 30f;
    [Range(1f, 20f)]   public float zoomSpeed       = 6f;
    [Range(1f, 20f)]   public float zoomSmoothing   = 8f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Follow smoothing
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Follow Smoothing")]
    [Tooltip("How quickly the camera pivot position catches up to the boat. " +
             "Lower = laggier but smoother.")]
    [Range(1f, 30f)]   public float positionSmoothing = 10f;

    [Tooltip("How quickly the camera rotation follows orbit input.")]
    [Range(1f, 30f)]   public float rotationSmoothing = 12f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Wave compensation
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Wave Compensation")]
    [Tooltip("How much of the boat's roll/pitch is cancelled in the camera view. " +
             "1 = horizon always flat, 0 = camera copies boat tilt exactly.")]
    [Range(0f, 1f)]    public float waveCompensation  = 0.7f;

    [Tooltip("Smoothing speed for wave compensation rotation.")]
    [Range(1f, 20f)]   public float compensationSpeed = 5f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Speed FOV
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Speed FOV")]
    [Tooltip("Assign BoatController for speed-based FOV zoom effect.")]
    public BoatController boatController;

    [Range(40f, 90f)]  public float baseFOV     = 60f;
    [Range(0f, 30f)]   public float maxFOVBoost = 15f;
    [Range(1f, 10f)]   public float fovSmoothing = 4f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Collision
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Collision")]
    [Tooltip("Pull the camera in if it clips into geometry or the ocean.")]
    public bool enableCollision = true;
    [Tooltip("Layers to test camera collision against.")]
    public LayerMask collisionMask = ~0;
    [Tooltip("Minimum clearance radius around the camera.")]
    [Range(0f, 1f)]    public float collisionRadius = 0.25f;

    // ─────────────────────────────────────────────────────────────────────────
    // Private state
    // ─────────────────────────────────────────────────────────────────────────
    float     _yaw;             // current horizontal orbit angle
    float     _pitch;           // current vertical orbit angle
    float     _targetYaw;
    float     _targetPitch;
    float     _targetDistance;
    float     _currentDistance;

    Vector3   _pivotPosition;   // smoothed pivot in world space
    Quaternion _compensatedRot; // wave-compensated camera orientation

    Camera _cam;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        _cam = GetComponent<Camera>();

        if (target != null)
        {
            _pivotPosition = target.position + lookAtOffset;
            // Initialise yaw to boat's current facing
            _yaw        = target.eulerAngles.y;
            _targetYaw  = _yaw;
        }

        _targetPitch    = 20f;   // reasonable default elevation
        _pitch          = _targetPitch;
        _targetDistance = defaultDistance;
        _currentDistance = defaultDistance;
        _compensatedRot  = Quaternion.identity;

        if (requireRightMouseButton)
            Cursor.lockState = CursorLockMode.None;
        else
            Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        if (target == null) return;

        HandleMouseOrbit();
        HandleZoom();
        UpdateFOV();
    }

    void LateUpdate()
    {
        if (target == null) return;

        SmoothPivot();
        SmoothRotation();
        PositionCamera();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Mouse orbit
    // ─────────────────────────────────────────────────────────────────────────
    void HandleMouseOrbit()
    {
        bool canOrbit = !requireRightMouseButton || Input.GetMouseButton(1);

        if (canOrbit)
        {
            // Lock / hide cursor while orbiting
            if (requireRightMouseButton && Input.GetMouseButtonDown(1))
                Cursor.lockState = CursorLockMode.Locked;

            _targetYaw   += Input.GetAxis("Mouse X") * sensitivityX;
            _targetPitch -= Input.GetAxis("Mouse Y") * sensitivityY;
            _targetPitch  = Mathf.Clamp(_targetPitch, pitchMin, pitchMax);
        }
        else
        {
            if (requireRightMouseButton && Input.GetMouseButtonUp(1))
                Cursor.lockState = CursorLockMode.None;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scroll zoom
    // ─────────────────────────────────────────────────────────────────────────
    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        _targetDistance -= scroll * zoomSpeed;
        _targetDistance  = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
        _currentDistance = Mathf.Lerp(_currentDistance, _targetDistance, zoomSmoothing * Time.deltaTime);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Smoothly move pivot to boat position
    // ─────────────────────────────────────────────────────────────────────────
    void SmoothPivot()
    {
        Vector3 desiredPivot = target.position + lookAtOffset;
        _pivotPosition = Vector3.Lerp(_pivotPosition, desiredPivot,
            positionSmoothing * Time.deltaTime);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Smooth orbit rotation
    // ─────────────────────────────────────────────────────────────────────────
    void SmoothRotation()
    {
        _yaw   = Mathf.LerpAngle(_yaw,   _targetYaw,   rotationSmoothing * Time.deltaTime);
        _pitch = Mathf.LerpAngle(_pitch, _targetPitch, rotationSmoothing * Time.deltaTime);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Place camera on spring arm behind pivot
    // ─────────────────────────────────────────────────────────────────────────
    void PositionCamera()
    {
        // Base orbit rotation (pure yaw + pitch, world-upright)
        Quaternion orbitRot = Quaternion.Euler(_pitch, _yaw, 0f);

        // Wave compensation: partially cancel the boat's roll/pitch
        Quaternion boatRot        = target.rotation;
        Quaternion boatEulerOnly  = Quaternion.Euler(boatRot.eulerAngles.x, 0f, boatRot.eulerAngles.z);
        Quaternion cancelTilt     = Quaternion.Inverse(boatEulerOnly);
        // Blend between no compensation (identity) and full compensation (cancelTilt)
        Quaternion compensation   = Quaternion.Slerp(Quaternion.identity, cancelTilt, waveCompensation);
        _compensatedRot           = Quaternion.Slerp(_compensatedRot, compensation,
            compensationSpeed * Time.deltaTime);

        Quaternion finalRot = orbitRot * _compensatedRot;

        // Spring arm: start at pivot, reach back along -Z
        Vector3 desiredPos = _pivotPosition + finalRot * (Vector3.back * _currentDistance);

        // Collision pull-in
        if (enableCollision)
            desiredPos = CollisionCheck(_pivotPosition, desiredPos);

        transform.position = desiredPos;
        transform.LookAt(_pivotPosition, Vector3.up);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Camera collision
    // ─────────────────────────────────────────────────────────────────────────
    Vector3 CollisionCheck(Vector3 from, Vector3 desired)
    {
        Vector3 dir       = desired - from;
        float   wantedDist = dir.magnitude;

        if (Physics.SphereCast(from, collisionRadius, dir.normalized,
            out RaycastHit hit, wantedDist, collisionMask,
            QueryTriggerInteraction.Ignore))
        {
            return from + dir.normalized * (hit.distance - collisionRadius);
        }
        return desired;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Speed-based FOV
    // ─────────────────────────────────────────────────────────────────────────
    void UpdateFOV()
    {
        if (_cam == null) return;

        float targetFOV = baseFOV;
        if (boatController != null)
        {
            float speedFrac = Mathf.Clamp01(boatController.CurrentSpeed / boatController.maxSpeed);
            targetFOV += speedFrac * maxFOVBoost;
        }
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFOV, fovSmoothing * Time.deltaTime);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Snaps the orbit yaw instantly to face the boat's current heading.</summary>
    public void SnapToBoatHeading()
    {
        if (target == null) return;
        _targetYaw = _yaw = target.eulerAngles.y;
    }

    /// <summary>
    /// Returns a world-space forward direction aligned to the camera's yaw
    /// (useful for translating input relative to camera for on-foot sections).
    /// </summary>
    public Vector3 GetCameraForwardFlat()
    {
        Vector3 f = transform.forward;
        f.y = 0f;
        return f.normalized;
    }
}
