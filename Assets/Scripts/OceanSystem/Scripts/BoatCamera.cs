using UnityEngine;

/// <summary>
/// Dual-mode boat camera supporting seamless switching between third-person
/// orbit and first-person cockpit views.
///
/// Third-person:  Spring-arm orbit with mouse look, zoom, wave compensation,
///                position smoothing, and collision avoidance.
///
/// First-person:  Camera snaps directly to the cockpit point with zero position
///                smoothing. Mouse look rotates freely with configurable clamps.
///                Wave motion passes through fully so the player feels the swell.
///
/// Switch modes:  Call SetCameraMode() or press switchKey (default: V).
///
/// Setup:
///   1. Add this component to your Camera GameObject.
///   2. Assign target (the boat root Transform).
///   3. Assign firstPersonAnchor — an empty child Transform placed at the
///      cockpit/helm eye position. If null, firstPersonOffset is used as a
///      local offset from the boat root instead.
///   4. Optionally assign boatController for speed-based FOV.
/// </summary>
public class BoatCamera : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Enums
    // ─────────────────────────────────────────────────────────────────────────
    public enum CameraMode { ThirdPerson, FirstPerson }

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Mode switching
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Mode")]
    [Tooltip("Starting camera mode.")]
    public CameraMode startingMode = CameraMode.ThirdPerson;

    [Tooltip("Key to toggle between first and third person.")]
    public KeyCode switchKey = KeyCode.V;

    [Tooltip("How quickly the camera lerps back out to the spring-arm when " +
             "switching from first to third person.")]
    [Range(1f, 30f)] public float modeTransitionSpeed = 12f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Shared target
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Target")]
    [Tooltip("The boat root Transform to follow.")]
    public Transform target;

    [Tooltip("Assign a BoatController for speed-based FOV in both modes.")]
    public BoatController boatController;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Third-person settings
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Third Person – Orbit")]
    [Tooltip("World-space offset from the boat pivot the camera looks at.")]
    public Vector3 lookAtOffset = new Vector3(0f, 1.2f, 0f);

    [Tooltip("Horizontal mouse sensitivity.")]
    [Range(0.5f, 10f)] public float sensitivityX = 3.5f;

    [Tooltip("Vertical mouse sensitivity.")]
    [Range(0.5f, 10f)] public float sensitivityY = 3.0f;

    [Tooltip("Hold right mouse button to orbit. If false, mouse always orbits.")]
    public bool requireRightMouseButton = true;

    [Tooltip("Minimum vertical (pitch) angle.")]
    [Range(-30f, 0f)]  public float pitchMin = -15f;

    [Tooltip("Maximum vertical (pitch) angle.")]
    [Range(5f, 80f)]   public float pitchMax = 65f;

    [Header("Third Person – Distance")]
    [Range(2f, 40f)] public float defaultDistance = 12f;
    [Range(2f, 40f)] public float minDistance     = 3f;
    [Range(2f, 40f)] public float maxDistance     = 30f;
    [Range(1f, 20f)] public float zoomSpeed       = 6f;
    [Range(1f, 20f)] public float zoomSmoothing   = 8f;

    [Header("Third Person – Follow Smoothing")]
    [Tooltip("How quickly the camera pivot catches up to the boat. Lower = laggier.")]
    [Range(1f, 30f)] public float positionSmoothing = 10f;

    [Tooltip("How quickly orbit rotation follows mouse input.")]
    [Range(1f, 30f)] public float rotationSmoothing = 12f;

    [Header("Third Person – Wave Compensation")]
    [Tooltip("How much of the boat roll/pitch is cancelled from the view. " +
             "1 = horizon always flat, 0 = full boat tilt.")]
    [Range(0f, 1f)]  public float waveCompensation = 0.7f;

    [Tooltip("Speed of the wave compensation smoothing.")]
    [Range(1f, 20f)] public float compensationSpeed = 5f;

    [Header("Third Person – Collision")]
    public bool      enableCollision = true;
    public LayerMask collisionMask   = ~0;
    [Range(0f, 1f)] public float collisionRadius = 0.25f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – First-person settings
    // ─────────────────────────────────────────────────────────────────────────
    [Header("First Person – Anchor")]
    [Tooltip("Child Transform at the cockpit/helm eye position. " +
             "If null, firstPersonOffset is used as a local offset from the boat root.")]
    public Transform firstPersonAnchor;

    [Tooltip("Local-space eye offset from the boat root when no anchor is assigned.")]
    public Vector3 firstPersonOffset = new Vector3(0f, 2.0f, 1.5f);

    [Header("First Person – Look")]
    [Tooltip("Horizontal sensitivity in first-person mode.")]
    [Range(0.5f, 10f)] public float fpSensitivityX = 3.0f;

    [Tooltip("Vertical sensitivity in first-person mode.")]
    [Range(0.5f, 10f)] public float fpSensitivityY = 2.8f;

    [Tooltip("Downward pitch limit in first-person.")]
    [Range(-90f, 0f)]  public float fpPitchMin = -60f;

    [Tooltip("Upward pitch limit in first-person.")]
    [Range(0f, 90f)]   public float fpPitchMax = 70f;

    [Tooltip("Horizontal arc the player can look through. 360 = unlimited.")]
    [Range(10f, 360f)] public float fpYawRange = 360f;

    [Tooltip("How much of the boat's wave roll/pitch the first-person view inherits. " +
             "1 = full motion sickness, 0 = perfectly stabilised.")]
    [Range(0f, 1f)] public float fpWaveInfluence = 1.0f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Shared FOV
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Speed FOV (both modes)")]
    [Range(40f, 90f)] public float baseFOV      = 60f;
    [Range(0f, 30f)]  public float maxFOVBoost  = 15f;
    [Range(1f, 10f)]  public float fovSmoothing = 4f;

    // ─────────────────────────────────────────────────────────────────────────
    // Public state
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Currently active camera mode. Use SetCameraMode() to change.</summary>
    public CameraMode CurrentMode { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Private – third-person state
    // ─────────────────────────────────────────────────────────────────────────
    float      _tpYaw;
    float      _tpPitch;
    float      _tpTargetYaw;
    float      _tpTargetPitch;
    float      _tpTargetDist;
    float      _tpCurrentDist;
    Vector3    _tpPivot;        // smoothed pivot world position
    Quaternion _tpCompRot;      // accumulated wave compensation rotation

    // ─────────────────────────────────────────────────────────────────────────
    // Private – first-person state
    // ─────────────────────────────────────────────────────────────────────────
    float _fpYaw;               // relative yaw offset from boat heading
    float _fpPitch;

    // ─────────────────────────────────────────────────────────────────────────
    // Private – mode transition
    // ─────────────────────────────────────────────────────────────────────────
    Camera     _cam;
    bool       _transitioning;
    Vector3    _transitionFromPos;
    Quaternion _transitionFromRot;
    float      _transitionT;    // 0→1

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        _cam = GetComponent<Camera>();

        if (target != null)
        {
            _tpPivot     = target.position + lookAtOffset;
            _tpYaw       = target.eulerAngles.y;
            _tpTargetYaw = _tpYaw;
        }

        _tpTargetPitch  = 20f;
        _tpPitch        = _tpTargetPitch;
        _tpTargetDist   = defaultDistance;
        _tpCurrentDist  = defaultDistance;
        _tpCompRot      = Quaternion.identity;

        CurrentMode = startingMode;
        ApplyCursorState();
    }

    void Update()
    {
        if (target == null) return;

        HandleModeSwitch();

        if (CurrentMode == CameraMode.ThirdPerson)
        {
            HandleTPMouseOrbit();
            HandleTPZoom();
        }
        else
        {
            HandleFPMouseLook();
        }

        UpdateFOV();
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (CurrentMode == CameraMode.ThirdPerson)
            UpdateThirdPerson();
        else
            UpdateFirstPerson();

        if (_transitioning)
            TickTransition();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Mode switching
    // ─────────────────────────────────────────────────────────────────────────
    void HandleModeSwitch()
    {
        if (Input.GetKeyDown(switchKey))
            ToggleCameraMode();
    }

    /// <summary>Toggle between first and third person.</summary>
    public void ToggleCameraMode()
    {
        SetCameraMode(CurrentMode == CameraMode.ThirdPerson
            ? CameraMode.FirstPerson
            : CameraMode.ThirdPerson);
    }

    /// <summary>
    /// Switch to the specified mode.
    ///
    /// First person snaps instantly — no position smoothing by design.
    /// Third person transitions smoothly from wherever the camera currently sits.
    /// </summary>
    public void SetCameraMode(CameraMode mode)
    {
        if (mode == CurrentMode) return;

        CurrentMode = mode;
        ApplyCursorState();

        if (mode == CameraMode.FirstPerson)
        {
            // Carry the third-person look direction into first-person relative angles
            // so there is no jarring heading jump on switch.
            _fpYaw   = _tpYaw - target.eulerAngles.y;
            _fpPitch = Mathf.Clamp(_tpPitch, fpPitchMin, fpPitchMax);

            // First person: position is always immediate — cancel any active transition.
            _transitioning = false;
        }
        else
        {
            // Carry first-person angles back into third-person so orbit continues
            // from wherever the player was looking.
            _tpYaw         = _fpYaw + target.eulerAngles.y;
            _tpTargetYaw   = _tpYaw;
            _tpPitch        = Mathf.Clamp(_fpPitch, pitchMin, pitchMax);
            _tpTargetPitch  = _tpPitch;

            // Begin smooth transition from current FP position to spring-arm position.
            _transitionFromPos = transform.position;
            _transitionFromRot = transform.rotation;
            _transitionT       = 0f;
            _transitioning     = true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cursor management
    // ─────────────────────────────────────────────────────────────────────────
    void ApplyCursorState()
    {
        if (CurrentMode == CameraMode.FirstPerson)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }
        else
        {
            if (requireRightMouseButton)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible   = false;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Third-person input
    // ─────────────────────────────────────────────────────────────────────────
    void HandleTPMouseOrbit()
    {
        bool canOrbit = !requireRightMouseButton || Input.GetMouseButton(1);

        if (canOrbit)
        {
            if (requireRightMouseButton && Input.GetMouseButtonDown(1))
                Cursor.lockState = CursorLockMode.Locked;

            _tpTargetYaw   += Input.GetAxis("Mouse X") * sensitivityX;
            _tpTargetPitch -= Input.GetAxis("Mouse Y") * sensitivityY;
            _tpTargetPitch  = Mathf.Clamp(_tpTargetPitch, pitchMin, pitchMax);
        }
        else
        {
            if (requireRightMouseButton && Input.GetMouseButtonUp(1))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
            }
        }
    }

    void HandleTPZoom()
    {
        float scroll  = Input.GetAxis("Mouse ScrollWheel");
        _tpTargetDist -= scroll * zoomSpeed;
        _tpTargetDist  = Mathf.Clamp(_tpTargetDist, minDistance, maxDistance);
        _tpCurrentDist = Mathf.Lerp(_tpCurrentDist, _tpTargetDist, zoomSmoothing * Time.deltaTime);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // First-person input
    // ─────────────────────────────────────────────────────────────────────────
    void HandleFPMouseLook()
    {
        _fpYaw   += Input.GetAxis("Mouse X") * fpSensitivityX;
        _fpPitch -= Input.GetAxis("Mouse Y") * fpSensitivityY;
        _fpPitch  = Mathf.Clamp(_fpPitch, fpPitchMin, fpPitchMax);

        if (fpYawRange < 360f)
        {
            float half = fpYawRange * 0.5f;
            _fpYaw = Mathf.Clamp(_fpYaw, -half, half);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Third-person camera update
    // ─────────────────────────────────────────────────────────────────────────
    void UpdateThirdPerson()
    {
        // Position smoothing — pivot follows the boat with lag
        Vector3 desiredPivot = target.position + lookAtOffset;
        _tpPivot = Vector3.Lerp(_tpPivot, desiredPivot, positionSmoothing * Time.deltaTime);

        // Rotation smoothing
        _tpYaw   = Mathf.LerpAngle(_tpYaw,   _tpTargetYaw,   rotationSmoothing * Time.deltaTime);
        _tpPitch = Mathf.LerpAngle(_tpPitch, _tpTargetPitch, rotationSmoothing * Time.deltaTime);

        // Wave compensation
        Quaternion boatRot      = target.rotation;
        Quaternion boatTiltOnly = Quaternion.Euler(boatRot.eulerAngles.x, 0f, boatRot.eulerAngles.z);
        Quaternion cancelTilt   = Quaternion.Inverse(boatTiltOnly);
        Quaternion compensation = Quaternion.Slerp(Quaternion.identity, cancelTilt, waveCompensation);
        _tpCompRot              = Quaternion.Slerp(_tpCompRot, compensation, compensationSpeed * Time.deltaTime);

        Quaternion orbitRot = Quaternion.Euler(_tpPitch, _tpYaw, 0f);
        Quaternion finalRot = orbitRot * _tpCompRot;

        Vector3 desiredPos = _tpPivot + finalRot * (Vector3.back * _tpCurrentDist);
        if (enableCollision)
            desiredPos = CollisionCheck(_tpPivot, desiredPos);

        // Only write position/rotation when not mid-transition (transition handles it)
        if (!_transitioning)
        {
            transform.position = desiredPos;
            transform.LookAt(_tpPivot, Vector3.up);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // First-person camera update
    // ─────────────────────────────────────────────────────────────────────────
    void UpdateFirstPerson()
    {
        // ── Position: zero smoothing — glued to the anchor every frame ──────
        transform.position = GetFirstPersonEyePosition();

        // ── Rotation: boat yaw + relative look + wave influence ──────────────
        float boatYaw   = target.eulerAngles.y;
        float boatPitch = target.eulerAngles.x;
        float boatRoll  = target.eulerAngles.z;

        // World yaw = boat heading + player look offset
        Quaternion yawRot   = Quaternion.Euler(0f, boatYaw + _fpYaw, 0f);
        Quaternion pitchRot = Quaternion.Euler(_fpPitch, 0f, 0f);

        // Scale how much wave roll/pitch bleeds into the FP view
        Quaternion waveRot = Quaternion.Euler(
            boatPitch * fpWaveInfluence,
            0f,
            boatRoll  * fpWaveInfluence);

        transform.rotation = yawRot * pitchRot * waveRot;
    }

    Vector3 GetFirstPersonEyePosition()
    {
        if (firstPersonAnchor != null)
            return firstPersonAnchor.position;

        return target.TransformPoint(firstPersonOffset);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Smooth transition back to third-person
    // ─────────────────────────────────────────────────────────────────────────
    void TickTransition()
    {
        _transitionT += modeTransitionSpeed * Time.deltaTime * 0.1f;
        _transitionT  = Mathf.Clamp01(_transitionT);

        float t = Mathf.SmoothStep(0f, 1f, _transitionT);

        // Re-derive current TP target position to blend toward
        Quaternion orbitRot = Quaternion.Euler(_tpPitch, _tpYaw, 0f);
        Quaternion finalRot = orbitRot * _tpCompRot;
        Vector3    tpPos    = _tpPivot + finalRot * (Vector3.back * _tpCurrentDist);
        if (enableCollision)
            tpPos = CollisionCheck(_tpPivot, tpPos);
        Quaternion tpRot = Quaternion.LookRotation(_tpPivot - tpPos, Vector3.up);

        transform.position = Vector3.Lerp(_transitionFromPos, tpPos, t);
        transform.rotation = Quaternion.Slerp(_transitionFromRot, tpRot, t);

        if (_transitionT >= 1f)
            _transitioning = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Collision
    // ─────────────────────────────────────────────────────────────────────────
    Vector3 CollisionCheck(Vector3 from, Vector3 desired)
    {
        Vector3 dir = desired - from;
        if (Physics.SphereCast(from, collisionRadius, dir.normalized,
                out RaycastHit hit, dir.magnitude, collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            return from + dir.normalized * (hit.distance - collisionRadius);
        }
        return desired;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Speed-based FOV (shared by both modes)
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
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Snaps the third-person orbit yaw to match the boat's current heading.</summary>
    public void SnapToBoatHeading()
    {
        if (target == null) return;
        _tpTargetYaw = _tpYaw = target.eulerAngles.y;
    }

    /// <summary>
    /// Returns a flat (XZ-plane) forward vector from the camera's current yaw.
    /// Useful for mapping camera-relative WASD movement.
    /// </summary>
    public Vector3 GetCameraForwardFlat()
    {
        Vector3 f = transform.forward;
        f.y = 0f;
        return f.normalized;
    }

    /// <summary>Resets first-person look to straight ahead along the boat's heading.</summary>
    public void ResetFirstPersonLook()
    {
        _fpYaw   = 0f;
        _fpPitch = 0f;
    }
}
