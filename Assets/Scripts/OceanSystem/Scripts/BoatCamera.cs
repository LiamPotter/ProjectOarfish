using UnityEngine;

/// <summary>
/// Three-mode boat camera:
///
///   ThirdPerson      — Spring-arm orbit with mouse look, zoom, wave compensation,
///                      position smoothing and collision avoidance.
///
///   FirstPerson      — Snaps to the cockpit anchor with zero position smoothing.
///                      Mouse look rotates freely with configurable clamps.
///                      Wave motion passes through so the player feels the swell.
///
///   FirstPersonTrack — First-person position (same anchor, zero smoothing) but
///                      the camera auto-rotates to face a tracked object instead
///                      of reading mouse input. The tracked object is set via
///                      SetTrackTarget(). Useful for watching a cast lure, a fish,
///                      a buoy, etc. while keeping the body / IK system active.
///
/// Switch modes:  Call SetCameraMode() or press switchKey (default: V).
///               ToggleCameraMode() cycles ThirdPerson → FirstPerson → back.
///               FirstPersonTrack is entered only via SetCameraMode() or
///               SetTrackTarget(transform) which also switches the mode.
///
/// Third-person visibility:  Assign GameObjects to thirdPersonOnlyObjects. They
///               will be enabled in ThirdPerson and disabled in both FP modes,
///               allowing hull geometry / body meshes to be hidden in first-person
///               without any additional scripts.
/// </summary>
public class BoatCamera : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Enum
    // ─────────────────────────────────────────────────────────────────────────
    public enum CameraMode { ThirdPerson, FirstPerson, FirstPersonTrack }

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Mode
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Mode")]
    [Tooltip("Starting camera mode.")]
    public CameraMode startingMode = CameraMode.ThirdPerson;

    [Tooltip("Key to cycle between ThirdPerson ↔ FirstPerson. " +
             "FirstPersonTrack is only entered via code.")]
    public KeyCode switchKey = KeyCode.V;

    [Tooltip("How quickly the camera lerps back out to the spring-arm when " +
             "returning to ThirdPerson from either FP mode.")]
    [Range(1f, 30f)] public float modeTransitionSpeed = 12f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Shared target
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Target")]
    [Tooltip("The boat root Transform to follow.")]
    public Transform target;

    [Tooltip("Assign a BoatController for speed-based FOV in all modes.")]
    public BoatController boatController;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Third-person visibility objects
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Third Person – Visibility")]
    [Tooltip("GameObjects that should only be visible in ThirdPerson mode. " +
             "They are enabled when in ThirdPerson and disabled in both FP modes. " +
             "Useful for hiding the player body mesh or hull interior in first-person.")]
    public GameObject[] thirdPersonOnlyObjects;

    [Tooltip("GameObjects that should only be visible in FirstPerson or " +
             "FirstPersonTrack modes. They are enabled in both FP modes and " +
             "disabled in ThirdPerson. Useful for cockpit overlays, FP hands, " +
             "HUD elements, or any geometry that only makes sense up close.")]
    public GameObject[] firstPersonOnlyObjects;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Third-person orbit
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
    public UnityEngine.LayerMask collisionMask = ~0;
    [Range(0f, 1f)] public float collisionRadius = 0.25f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – First-person (shared by both FP modes)
    // ─────────────────────────────────────────────────────────────────────────
    [Header("First Person – Anchor")]
    [Tooltip("Child Transform at the cockpit/helm eye position. " +
             "If null, firstPersonOffset is used as a local offset from the boat root.")]
    public Transform firstPersonAnchor;

    [Tooltip("Local-space eye offset from the boat root when no anchor is assigned.")]
    public Vector3 firstPersonOffset = new Vector3(0f, 2.0f, 1.5f);

    [Header("First Person – Mouse Look")]
    [Tooltip("Horizontal sensitivity in FirstPerson mode.")]
    [Range(0.5f, 10f)] public float fpSensitivityX = 3.0f;

    [Tooltip("Vertical sensitivity in FirstPerson mode.")]
    [Range(0.5f, 10f)] public float fpSensitivityY = 2.8f;

    [Tooltip("Downward pitch limit in FirstPerson mode.")]
    [Range(-90f, 0f)]  public float fpPitchMin = -60f;

    [Tooltip("Upward pitch limit in FirstPerson mode.")]
    [Range(0f, 90f)]   public float fpPitchMax = 70f;

    [Tooltip("Horizontal arc the player can look through. 360 = unlimited.")]
    [Range(10f, 360f)] public float fpYawRange = 360f;

    [Tooltip("How much of the boat's wave roll/pitch the first-person view inherits. " +
             "1 = full motion, 0 = stabilised.")]
    [Range(0f, 1f)] public float fpWaveInfluence = 1.0f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – FirstPersonTrack mode
    // ─────────────────────────────────────────────────────────────────────────
    [Header("First Person Track – Settings")]
    [Tooltip("Initial object to track. Can be overridden at runtime via SetTrackTarget().")]
    public Transform initialTrackTarget;

    [Tooltip("How quickly the camera rotates to face the tracked object. " +
             "Lower = smoother cinematic feel; higher = snappy.")]
    [Range(1f, 30f)] public float trackRotationSpeed = 8f;

    [Tooltip("Maximum horizontal angle (degrees either side of boat forward) the " +
             "camera will rotate to track the object. 180 = full rotation.")]
    [Range(10f, 180f)] public float trackYawLimit = 150f;

    [Tooltip("Maximum vertical angle (degrees) the camera will tilt up/down " +
             "while tracking.")]
    [Range(5f, 90f)] public float trackPitchLimit = 70f;

    [Tooltip("How much of the boat's wave roll/pitch is inherited during tracking. " +
             "Usually lower than fpWaveInfluence for a steadier tracking shot.")]
    [Range(0f, 1f)] public float trackWaveInfluence = 0.5f;

    [Tooltip("When in FirstPersonTrack mode, narrow the FOV as the tracked object " +
             "gets closer and widen it as the object moves further away, simulating " +
             "a zoom lens that adjusts to keep the subject in frame.")]
    public bool trackFOVEnabled = true;

    [Tooltip("Distance at which the FOV reaches its narrowest value (telephoto). " +
             "Object closer than this uses trackFOVMin.")]
    [Range(0f, 100f)] public float trackFOVNearDistance = 5f;

    [Tooltip("Distance at which the FOV reaches its widest value (wide angle). " +
             "Object further than this uses trackFOVMax.")]
    [Range(0f, 200f)] public float trackFOVFarDistance  = 40f;

    [Tooltip("Narrowest FOV (degrees) used when the tracked object is at or closer " +
             "than trackFOVNearDistance.")]
    [Range(5f, 90f)]  public float trackFOVMin = 25f;

    [Tooltip("Widest FOV (degrees) used when the tracked object is at or further " +
             "than trackFOVFarDistance.")]
    [Range(10f, 120f)] public float trackFOVMax = 70f;

    [Tooltip("How quickly the FOV adjusts to distance changes in tracking mode. " +
             "Independent of the shared fovSmoothing so it can respond faster.")]
    [Range(0.5f, 20f)] public float trackFOVSmoothing = 3f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Shared FOV
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Speed FOV (all modes)")]
    [Range(40f, 90f)] public float baseFOV      = 60f;
    [Range(0f, 30f)]  public float maxFOVBoost  = 15f;
    [Range(1f, 10f)]  public float fovSmoothing = 4f;

    // ─────────────────────────────────────────────────────────────────────────
    // Public state
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Currently active camera mode. Use SetCameraMode() to change.</summary>
    public CameraMode CurrentMode { get; private set; }

    /// <summary>The object currently being tracked in FirstPersonTrack mode. Null if none.</summary>
    public Transform TrackTarget  { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Private – third-person state
    // ─────────────────────────────────────────────────────────────────────────
    float      _tpYaw;
    float      _tpPitch;
    float      _tpTargetYaw;
    float      _tpTargetPitch;
    float      _tpTargetDist;
    float      _tpCurrentDist;
    Vector3    _tpPivot;
    Quaternion _tpCompRot;

    // ─────────────────────────────────────────────────────────────────────────
    // Private – first-person state (shared by FP and FPTrack)
    // ─────────────────────────────────────────────────────────────────────────
    float _fpYaw;
    float _fpPitch;

    // ─────────────────────────────────────────────────────────────────────────
    // Private – FPTrack state
    // ─────────────────────────────────────────────────────────────────────────
    float _trackYaw;    // current smoothed yaw   (relative to boat heading)
    float _trackPitch;  // current smoothed pitch

    // ─────────────────────────────────────────────────────────────────────────
    // Private – transition
    // ─────────────────────────────────────────────────────────────────────────
    UnityEngine.Camera _cam;
    bool       _transitioning;
    Vector3    _transitionFromPos;
    Quaternion _transitionFromRot;
    float      _transitionT;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        _cam = GetComponent<UnityEngine.Camera>();

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

        TrackTarget = initialTrackTarget;

        CurrentMode = startingMode;
        ApplyCursorState();
        ApplyThirdPersonObjectVisibility();
    }

    void Update()
    {
        if (target == null) return;

        HandleModeSwitch();

        switch (CurrentMode)
        {
            case CameraMode.ThirdPerson:
                HandleTPMouseOrbit();
                HandleTPZoom();
                break;
            case CameraMode.FirstPerson:
                HandleFPMouseLook();
                break;
            // FirstPersonTrack reads no input — tracking is computed in LateUpdate
        }

        UpdateFOV();
    }

    void LateUpdate()
    {
        if (target == null) return;

        switch (CurrentMode)
        {
            case CameraMode.ThirdPerson:      UpdateThirdPerson();      break;
            case CameraMode.FirstPerson:      UpdateFirstPerson();      break;
            case CameraMode.FirstPersonTrack: UpdateFirstPersonTrack(); break;
        }

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

    /// <summary>
    /// Cycles between ThirdPerson and FirstPerson only.
    /// FirstPersonTrack is entered exclusively via SetCameraMode() or SetTrackTarget().
    /// </summary>
    public void ToggleCameraMode()
    {
        SetCameraMode(CurrentMode == CameraMode.ThirdPerson
            ? CameraMode.FirstPerson
            : CameraMode.ThirdPerson);
    }

    /// <summary>
    /// Switches to the given mode.
    ///
    ///   ThirdPerson      — smooth transition from wherever the camera is.
    ///   FirstPerson      — snaps instantly to anchor, no smoothing.
    ///   FirstPersonTrack — snaps instantly; camera auto-rotates toward TrackTarget.
    ///                      Set TrackTarget first with SetTrackTarget(), or pass the
    ///                      overload that accepts a Transform.
    /// </summary>
    public void SetCameraMode(CameraMode mode)
    {
        if (mode == CurrentMode) return;

        CameraMode previous = CurrentMode;
        CurrentMode = mode;
        ApplyCursorState();
        ApplyThirdPersonObjectVisibility();

        bool leavingTP = previous == CameraMode.ThirdPerson;
        bool enteringTP = mode   == CameraMode.ThirdPerson;

        if (enteringTP)
        {
            // Carry the look direction from whichever FP mode we were in
            float sourceYaw   = previous == CameraMode.FirstPersonTrack ? _trackYaw   : _fpYaw;
            float sourcePitch = previous == CameraMode.FirstPersonTrack ? _trackPitch : _fpPitch;

            _tpYaw        = sourceYaw + target.eulerAngles.y;
            _tpTargetYaw  = _tpYaw;
            _tpPitch       = Mathf.Clamp(sourcePitch, pitchMin, pitchMax);
            _tpTargetPitch = _tpPitch;

            _transitionFromPos = transform.position;
            _transitionFromRot = transform.rotation;
            _transitionT       = 0f;
            _transitioning     = true;
        }
        else if (mode == CameraMode.FirstPerson)
        {
            if (leavingTP)
            {
                _fpYaw   = _tpYaw - target.eulerAngles.y;
                _fpPitch = Mathf.Clamp(_tpPitch, fpPitchMin, fpPitchMax);
            }
            else
            {
                // Coming from FPTrack — carry those angles so no heading jump
                _fpYaw   = Mathf.Clamp(_trackYaw,   -fpYawRange * 0.5f, fpYawRange * 0.5f);
                _fpPitch = Mathf.Clamp(_trackPitch,  fpPitchMin, fpPitchMax);
            }
            _transitioning = false;
        }
        else if (mode == CameraMode.FirstPersonTrack)
        {
            // Carry current look angles into tracking state as the starting rotation
            _trackYaw   = leavingTP
                ? _tpYaw - target.eulerAngles.y
                : _fpYaw;
            _trackPitch = leavingTP
                ? Mathf.Clamp(_tpPitch, -trackPitchLimit, trackPitchLimit)
                : _fpPitch;

            _transitioning = false;
        }
    }

    /// <summary>
    /// Sets the object to track and immediately enters FirstPersonTrack mode.
    /// Pass null to clear the track target (camera holds its last direction).
    /// </summary>
    public void SetTrackTarget(Transform newTarget)
    {
        TrackTarget = newTarget;
        SetCameraMode(CameraMode.FirstPersonTrack);
    }

    /// <summary>
    /// Updates the tracked object without changing mode.
    /// If called while not in FirstPersonTrack, the new target is stored and
    /// will be used next time FirstPersonTrack is entered.
    /// </summary>
    public void ChangeTrackTarget(Transform newTarget)
    {
        TrackTarget = newTarget;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Third-person / first-person object visibility
    // ─────────────────────────────────────────────────────────────────────────
    void ApplyThirdPersonObjectVisibility()
    {
        bool isTP = CurrentMode == CameraMode.ThirdPerson;

        if (thirdPersonOnlyObjects != null)
        {
            foreach (GameObject go in thirdPersonOnlyObjects)
                if (go != null) go.SetActive(isTP);
        }

        if (firstPersonOnlyObjects != null)
        {
            foreach (GameObject go in firstPersonOnlyObjects)
                if (go != null) go.SetActive(!isTP);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cursor management
    // ─────────────────────────────────────────────────────────────────────────
    void ApplyCursorState()
    {
        if (CurrentMode == CameraMode.FirstPerson || CurrentMode == CameraMode.FirstPersonTrack)
        {
            // Both FP modes lock the cursor — tracking mode doesn't need mouse input
            // but keeps it hidden for consistency.
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
    // First-person mouse look input
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
    // Camera updates — one per mode
    // ─────────────────────────────────────────────────────────────────────────
    void UpdateThirdPerson()
    {
        Vector3 desiredPivot = target.position + lookAtOffset;
        _tpPivot = Vector3.Lerp(_tpPivot, desiredPivot, positionSmoothing * Time.deltaTime);

        _tpYaw   = Mathf.LerpAngle(_tpYaw,   _tpTargetYaw,   rotationSmoothing * Time.deltaTime);
        _tpPitch = Mathf.LerpAngle(_tpPitch, _tpTargetPitch, rotationSmoothing * Time.deltaTime);

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

        if (!_transitioning)
        {
            transform.position = desiredPos;
            transform.LookAt(_tpPivot, Vector3.up);
        }
    }

    void UpdateFirstPerson()
    {
        // Position: zero smoothing — glued to the anchor every frame
        transform.position = GetFirstPersonEyePosition();

        float boatYaw   = target.eulerAngles.y;
        float boatPitch = target.eulerAngles.x;
        float boatRoll  = target.eulerAngles.z;

        Quaternion yawRot   = Quaternion.Euler(0f, boatYaw + _fpYaw, 0f);
        Quaternion pitchRot = Quaternion.Euler(_fpPitch, 0f, 0f);
        Quaternion waveRot  = Quaternion.Euler(
            boatPitch * fpWaveInfluence,
            0f,
            boatRoll  * fpWaveInfluence);

        transform.rotation = yawRot * pitchRot * waveRot;
    }

    void UpdateFirstPersonTrack()
    {
        // Position: same zero-smoothing anchor as normal FP
        transform.position = GetFirstPersonEyePosition();

        if (TrackTarget != null)
        {
            // Compute the direction from the eye to the target
            Vector3 toTarget = TrackTarget.position - transform.position;

            if (toTarget.sqrMagnitude > 0.0001f)
            {
                // Convert world direction to angles relative to the boat's heading
                float boatYaw      = target.eulerAngles.y;
                Vector3 localDir   = Quaternion.Inverse(Quaternion.Euler(0f, boatYaw, 0f)) * toTarget.normalized;

                float desiredYaw   = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
                float desiredPitch = -Mathf.Asin(Mathf.Clamp(localDir.y, -1f, 1f)) * Mathf.Rad2Deg;

                // Clamp to configured tracking limits
                desiredYaw   = Mathf.Clamp(desiredYaw,   -trackYawLimit,   trackYawLimit);
                desiredPitch = Mathf.Clamp(desiredPitch, -trackPitchLimit,  trackPitchLimit);

                // Smoothly rotate toward the desired angles
                _trackYaw   = Mathf.LerpAngle(_trackYaw,   desiredYaw,   trackRotationSpeed * Time.deltaTime);
                _trackPitch = Mathf.LerpAngle(_trackPitch, desiredPitch, trackRotationSpeed * Time.deltaTime);
            }
        }
        // If TrackTarget is null, the camera simply holds its last direction

        // Apply rotation — same wave influence blend as FP
        float   bYaw   = target.eulerAngles.y;
        float   bPitch = target.eulerAngles.x;
        float   bRoll  = target.eulerAngles.z;

        Quaternion yawRot   = Quaternion.Euler(0f, bYaw + _trackYaw, 0f);
        Quaternion pitchRot = Quaternion.Euler(_trackPitch, 0f, 0f);
        Quaternion waveRot  = Quaternion.Euler(
            bPitch * trackWaveInfluence,
            0f,
            bRoll  * trackWaveInfluence);

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

        Quaternion orbitRot     = Quaternion.Euler(_tpPitch, _tpYaw, 0f);
        Quaternion finalRot     = orbitRot * _tpCompRot;
        Vector3    tpPos        = _tpPivot + finalRot * (Vector3.back * _tpCurrentDist);
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
    // Speed-based FOV
    // ─────────────────────────────────────────────────────────────────────────
    void UpdateFOV()
    {
        if (_cam == null) return;

        float targetFOV = baseFOV;

        // Speed FOV boost (all modes)
        if (boatController != null)
        {
            float speedFrac = Mathf.Clamp01(boatController.CurrentSpeed / boatController.maxSpeed);
            targetFOV += speedFrac * maxFOVBoost;
        }

        // Distance-based FOV in FirstPersonTrack mode
        // Near object → narrow FOV (telephoto zoom-in)
        // Far object  → wide FOV  (zoom-out to keep subject in frame)
        if (CurrentMode == CameraMode.FirstPersonTrack && trackFOVEnabled && TrackTarget != null)
        {
            float dist = Vector3.Distance(transform.position, TrackTarget.position);
            float t    = Mathf.InverseLerp(trackFOVNearDistance, trackFOVFarDistance, dist);
            float distFOV = Mathf.Lerp(trackFOVMin, trackFOVMax, t);

            // Blend toward the distance-driven FOV at its own smoothing rate
            targetFOV = Mathf.Lerp(_cam.fieldOfView, distFOV, trackFOVSmoothing * Time.deltaTime);
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

    /// <summary>
    /// Returns the first-person look angles as (x = pitch, y = yaw) in degrees.
    /// In FirstPersonTrack mode, returns the tracking angles instead.
    /// Used by BoatBodyIK to drive hip and chest IK targets.
    /// </summary>
    public Vector2 GetFPLookAngles()
    {
        if (CurrentMode == CameraMode.FirstPersonTrack)
            return new Vector2(_trackPitch, _trackYaw);

        return new Vector2(_fpPitch, _fpYaw);
    }

    /// <summary>
    /// Returns the camera's current world-space forward direction.
    /// Valid in all modes.
    /// </summary>
    public Vector3 GetCameraForward() => transform.forward;
}
