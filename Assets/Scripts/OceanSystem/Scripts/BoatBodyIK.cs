using UnityEngine;

/// <summary>
/// Drives an Animator's hip and chest IK targets based on the direction the
/// first-person camera is looking, giving the character's body the appearance
/// of turning to face wherever the player is looking.
///
/// Designed to work with Unity's built-in Humanoid IK (Animator.SetIKPosition /
/// SetIKRotation / SetIKPositionWeight / SetIKRotationWeight) called from
/// OnAnimatorIK.
///
/// ── How the IK targets work ──────────────────────────────────────────────────
///
///   Hip target    — Pulls the pelvis / lower spine toward the look direction.
///                   Responds primarily to horizontal (yaw) camera movement so
///                   the hips twist with the torso. Vertical movement has a
///                   smaller influence, simulating a squat / lean.
///
///   Chest target  — Pulls the upper spine further into the look direction.
///                   Has a larger yaw and pitch range than the hip, so the
///                   upper body leads the look direction while the hips lag,
///                   creating a natural S-curve through the spine.
///
///   Head target   — Uses Unity's SetLookAtPosition / SetLookAtWeight API to
///                   aim the head and eyes at a world-space point along the
///                   camera forward vector. A secondary SetBoneLocalRotation
///                   pass on the Neck and Head bones adds explicit pitch
///                   control. The head tracks significantly faster than the
///                   spine so it leads the look direction naturally, with
///                   the torso following behind.
///
///   All targets are positioned at a configurable radius in front of the
///   character's current bone position, projected along the look direction.
///   The weights ramp in/out smoothly so the IK activates only while in
///   first-person mode and deactivates gracefully when switching back.
///
/// ── Requirements ─────────────────────────────────────────────────────────────
///   • Humanoid rig with an Animator component.
///   • The Animator's Avatar must have a valid Humanoid definition (hips, spine,
///     chest bones mapped).
///   • Unity's IK Pass must be enabled on the Animator Controller layer that
///     runs this character's locomotion animation. Tick "IK Pass" in the
///     Animator window for the relevant layer.
///   • BoatCamera component assigned (reads FP look angles).
///
/// ── Setup ────────────────────────────────────────────────────────────────────
///   1. Add this component to the same GameObject as the Animator.
///   2. Assign boatCamera.
///   3. Adjust the weight, radius and smoothing fields to taste.
///   4. Press Play.
/// </summary>
[RequireComponent(typeof(Animator))]
public class BoatBodyIK : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – References
    // ─────────────────────────────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("The BoatCamera component. Its first-person look direction drives the IK.")]
    public BoatCamera boatCamera;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Hip IK
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Hip IK")]
    [Tooltip("Maximum IK position/rotation weight applied to the hips. " +
             "Keep below 0.6 to avoid unnatural pelvis snapping.")]
    [Range(0f, 1f)]  public float hipPositionWeight  = 0.35f;
    [Range(0f, 1f)]  public float hipRotationWeight  = 0.45f;

    [Tooltip("How many units in front of the hip bone the IK target is placed. " +
             "Larger = more aggressive twist.")]
    [Range(0.1f, 3f)] public float hipTargetRadius   = 0.8f;

    [Tooltip("How much of the camera yaw (horizontal look) drives the hip twist. " +
             "Range −1 to 1 maps to this fraction of the yaw angle.")]
    [Range(0f, 1f)]  public float hipYawInfluence    = 0.5f;

    [Tooltip("How much of the camera pitch (vertical look) drives the hip lean.")]
    [Range(0f, 1f)]  public float hipPitchInfluence  = 0.2f;

    [Tooltip("Maximum hip twist angle in degrees (clamped either side of forward).")]
    [Range(0f, 90f)] public float hipMaxYawDegrees   = 45f;

    [Tooltip("Maximum hip pitch lean angle in degrees.")]
    [Range(0f, 45f)] public float hipMaxPitchDegrees = 20f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Chest IK
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Chest IK")]
    [Tooltip("Maximum IK position/rotation weight applied to the chest/upper spine.")]
    [Range(0f, 1f)]  public float chestPositionWeight = 0.5f;
    [Range(0f, 1f)]  public float chestRotationWeight = 0.65f;

    [Tooltip("How many units in front of the chest bone the IK target is placed.")]
    [Range(0.1f, 3f)] public float chestTargetRadius  = 1.0f;

    [Tooltip("How much of the camera yaw drives the chest twist. " +
             "Should be higher than hipYawInfluence so the chest leads the hips.")]
    [Range(0f, 1f)]  public float chestYawInfluence   = 0.85f;

    [Tooltip("How much of the camera pitch drives the chest lean.")]
    [Range(0f, 1f)]  public float chestPitchInfluence = 0.6f;

    [Tooltip("Maximum chest twist angle in degrees.")]
    [Range(0f, 90f)] public float chestMaxYawDegrees  = 70f;

    [Tooltip("Maximum chest pitch lean angle in degrees.")]
    [Range(0f, 60f)] public float chestMaxPitchDegrees = 40f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Head IK
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Head IK")]
    [Tooltip("Weight of Unity's built-in look-at solver applied to the head/neck. " +
             "Controls how strongly the neck IK pulls toward the look-at point.")]
    [Range(0f, 1f)]  public float headLookAtWeight   = 0.9f;

    [Tooltip("How much the body root participates in the look-at solve. " +
             "Keep low (0.1–0.2) so the body is driven by hip/chest IK instead.")]
    [Range(0f, 1f)]  public float headBodyWeight     = 0.1f;

    [Tooltip("How much the eyes participate in the look-at solve. " +
             "1 = eyes track the target fully.")]
    [Range(0f, 1f)]  public float headEyeWeight      = 1.0f;

    [Tooltip("Clamp weight for the look-at solver — prevents extreme head angles. " +
             "1 = unclamped, 0 = maximum clamping.")]
    [Range(0f, 1f)]  public float headClampWeight    = 0.5f;

    [Tooltip("How far in front of the head bone (metres) the look-at target is placed.")]
    [Range(0.5f, 10f)] public float headLookAtRadius = 3.0f;

    [Tooltip("Additional rotation weight applied via SetBoneLocalRotation on the " +
             "Head and Neck bones for explicit pitch/yaw control beyond the look-at.")]
    [Range(0f, 1f)]  public float headBoneWeight     = 0.55f;

    [Tooltip("Maximum yaw the head can turn from the chest forward direction. " +
             "Prevents anatomy-breaking rotations.")]
    [Range(0f, 90f)] public float headMaxYawDegrees  = 80f;

    [Tooltip("Maximum pitch the head can tilt from neutral.")]
    [Range(0f, 80f)] public float headMaxPitchDegrees = 60f;

    [Tooltip("Fraction of yaw carried by the neck vs the head. " +
             "0.4 = neck does 40%, head does the rest.")]
    [Range(0f, 1f)]  public float neckYawShare       = 0.4f;

    [Tooltip("Fraction of pitch carried by the neck vs the head.")]
    [Range(0f, 1f)]  public float neckPitchShare     = 0.35f;

    [Tooltip("How quickly the head tracks the camera look direction. " +
             "Should be higher than trackingSpeed — heads lead the body.")]
    [Range(1f, 40f)] public float headTrackingSpeed  = 18f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Smoothing
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Smoothing")]
    [Tooltip("How quickly the IK target angles track the camera look direction. " +
             "Higher = snappier, lower = more inertia / body lag.")]
    [Range(1f, 30f)] public float trackingSpeed = 8f;

    [Tooltip("How quickly IK weights ramp up when entering first-person mode " +
             "and ramp down when leaving.")]
    [Range(1f, 20f)] public float weightBlendSpeed = 6f;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector – Activation
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Activation")]
    [Tooltip("If true, IK is only active when BoatCamera is in FirstPerson mode. " +
             "If false, IK is always active (useful for testing from TP).")]
    public bool firstPersonOnly = true;

    [Tooltip("Draw the IK target positions in the Scene view.")]
    public bool debugDraw = true;

    // ─────────────────────────────────────────────────────────────────────────
    // Private state
    // ─────────────────────────────────────────────────────────────────────────
    Animator _anim;

    // Smoothed look angles (degrees) fed to IK this frame
    float _smoothedYaw;
    float _smoothedPitch;

    // Current blended weights (0 when TP, ramp to full when FP)
    float _currentWeightScale;

    // Head IK — separate fast-tracking smoothed angles
    float _headSmoothedYaw;
    float _headSmoothedPitch;

    // Cached world-space IK target positions and rotations (for debug gizmos)
    Vector3    _dbgHipTargetPos;
    Quaternion _dbgHipTargetRot;
    Vector3    _dbgChestTargetPos;
    Quaternion _dbgChestTargetRot;
    Vector3    _dbgHeadLookAtPos;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        _anim = GetComponent<Animator>();
    }

    void Update()
    {
        // Determine whether IK should be active
        bool shouldBeActive = !firstPersonOnly ||
            (boatCamera != null && boatCamera.CurrentMode == BoatCamera.CameraMode.FirstPerson);

        float targetWeightScale = shouldBeActive ? 1f : 0f;
        _currentWeightScale = Mathf.MoveTowards(
            _currentWeightScale, targetWeightScale,
            weightBlendSpeed * Time.deltaTime);

        // Read and smooth the look angles
        if (boatCamera != null)
        {
            Vector2 lookAngles = boatCamera.GetFPLookAngles(); // x=pitch, y=yaw
            float targetPitch  = lookAngles.x;
            float targetYaw    = lookAngles.y;

            _smoothedPitch = Mathf.LerpAngle(_smoothedPitch, targetPitch, trackingSpeed * Time.deltaTime);
            _smoothedYaw   = Mathf.LerpAngle(_smoothedYaw,   targetYaw,   trackingSpeed * Time.deltaTime);

            // Head tracks faster than the body — it leads the look direction
            _headSmoothedPitch = Mathf.LerpAngle(_headSmoothedPitch, targetPitch, headTrackingSpeed * Time.deltaTime);
            _headSmoothedYaw   = Mathf.LerpAngle(_headSmoothedYaw,   targetYaw,   headTrackingSpeed * Time.deltaTime);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IK callback — called by Unity after animation playback each frame
    // ─────────────────────────────────────────────────────────────────────────
    void OnAnimatorIK(int layerIndex)
    {
        if (_anim == null) return;
        if (_currentWeightScale < 0.001f) return;

        // ── Hip ──────────────────────────────────────────────────────────────
        Vector3    hipBonePos = _anim.GetBoneTransform(HumanBodyBones.Hips)?.position
                             ?? transform.position;

        float hipYaw   = Mathf.Clamp(_smoothedYaw   * hipYawInfluence,
                            -hipMaxYawDegrees,   hipMaxYawDegrees);
        float hipPitch = Mathf.Clamp(_smoothedPitch * hipPitchInfluence,
                            -hipMaxPitchDegrees, hipMaxPitchDegrees);

        Quaternion hipTargetRot = CalculateTargetRotation(hipYaw, hipPitch);
        Vector3    hipTargetPos = hipBonePos + hipTargetRot * (Vector3.forward * hipTargetRadius);

        _anim.SetIKPositionWeight(AvatarIKGoal.LeftFoot,  0f); // not driving feet
        _anim.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);

        // Unity's built-in Humanoid IK doesn't expose a dedicated "body" IK goal,
        // but SetBoneLocalRotation on Hips / Spine / Chest is the closest equivalent.
        // We use it here alongside the bodyPosition / bodyRotation APIs.
        ApplyBodyIK(hipTargetPos, hipTargetRot, hipPitch);

        // ── Chest ─────────────────────────────────────────────────────────────
        Transform chestBone = _anim.GetBoneTransform(HumanBodyBones.UpperChest)
                           ?? _anim.GetBoneTransform(HumanBodyBones.Chest)
                           ?? _anim.GetBoneTransform(HumanBodyBones.Spine);

        if (chestBone != null)
        {
            float chestYaw   = Mathf.Clamp(_smoothedYaw   * chestYawInfluence,
                                -chestMaxYawDegrees,   chestMaxYawDegrees);
            float chestPitch = Mathf.Clamp(_smoothedPitch * chestPitchInfluence,
                                -chestMaxPitchDegrees, chestMaxPitchDegrees);

            Quaternion chestTargetRot = CalculateTargetRotation(chestYaw, chestPitch);
            Vector3    chestTargetPos = chestBone.position
                                      + chestTargetRot * (Vector3.forward * chestTargetRadius);

            ApplyChestIK(chestBone, chestTargetPos, chestTargetRot, chestPitch, chestYaw);

            _dbgChestTargetPos = chestTargetPos;
            _dbgChestTargetRot = chestTargetRot;
        }

        _dbgHipTargetPos = hipTargetPos;
        _dbgHipTargetRot = hipTargetRot;

        // ── Head ──────────────────────────────────────────────────────────────
        ApplyHeadIK();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IK helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a world-space rotation that represents looking in the direction
    /// defined by the given yaw (relative to boat) and pitch.
    /// </summary>
    Quaternion CalculateTargetRotation(float yawDeg, float pitchDeg)
    {
        // Yaw is relative to the boat's heading; convert to world space
        float boatWorldYaw = transform.eulerAngles.y;
        return Quaternion.Euler(pitchDeg, boatWorldYaw + yawDeg, 0f);
    }

    /// <summary>
    /// Drives the Animator body position and body rotation (affects hips / pelvis).
    /// Unity's bodyPosition/bodyRotation are in world space and blend with the
    /// root motion / animation so they need weight clamping to avoid popping.
    /// </summary>
    void ApplyBodyIK(Vector3 targetPos, Quaternion targetRot, float pitchDeg)
    {
        float w = _currentWeightScale;

        // bodyPosition: lerp toward a point slightly ahead of and above/below the hip
        // We only adjust Y slightly to avoid lifting the character off the ground.
        Vector3 currentBodyPos = _anim.bodyPosition;
        float   yShift         = Mathf.Sin(pitchDeg * Mathf.Deg2Rad) * hipTargetRadius * 0.25f;
        Vector3 bodyTarget     = new Vector3(targetPos.x, currentBodyPos.y + yShift, targetPos.z);
        _anim.bodyPosition     = Vector3.Lerp(currentBodyPos, bodyTarget, hipPositionWeight * w);

        // bodyRotation: blend the hip twist into the current body rotation
        Quaternion currentBodyRot = _anim.bodyRotation;
        _anim.bodyRotation        = Quaternion.Slerp(currentBodyRot, targetRot, hipRotationWeight * w);
    }

    /// <summary>
    /// Drives the upper-body twist using SetBoneLocalRotation on Spine and
    /// Chest / UpperChest bones. This is the correct Unity API for spine bending
    /// that doesn't conflict with root motion.
    /// </summary>
    void ApplyChestIK(Transform chestBone, Vector3 targetPos, Quaternion targetRot,
                      float pitchDeg, float yawDeg)
    {
        float w = _currentWeightScale * chestRotationWeight;
        if (w < 0.001f) return;

        // ── Spine twist ───────────────────────────────────────────────────────
        Transform spineBone = _anim.GetBoneTransform(HumanBodyBones.Spine);
        if (spineBone != null)
        {
            // Convert the world-space rotation delta into local spine space
            Quaternion spineWorldRot  = spineBone.rotation;
            Quaternion spineTarget    = Quaternion.Euler(
                pitchDeg * 0.3f,    // spine takes 30% of pitch
                yawDeg   * 0.4f,    // spine takes 40% of yaw
                0f) * spineWorldRot;

            Quaternion spineLocal     = Quaternion.Inverse(spineBone.parent != null
                ? spineBone.parent.rotation : Quaternion.identity) * spineTarget;

            _anim.SetBoneLocalRotation(HumanBodyBones.Spine,
                Quaternion.Slerp(spineBone.localRotation, spineLocal, w));
        }

        // ── Chest twist ───────────────────────────────────────────────────────
        Quaternion chestWorldRot = chestBone.rotation;
        Quaternion chestTarget   = Quaternion.Euler(
            pitchDeg * 0.7f,    // chest takes remaining pitch
            yawDeg   * 0.6f,    // chest takes remaining yaw
            0f) * chestWorldRot;

        Quaternion chestLocal    = Quaternion.Inverse(chestBone.parent != null
            ? chestBone.parent.rotation : Quaternion.identity) * chestTarget;

        // Determine which bone enum to use
        HumanBodyBones chestEnum = HumanBodyBones.Chest;
        if (_anim.GetBoneTransform(HumanBodyBones.UpperChest) != null)
            chestEnum = HumanBodyBones.UpperChest;

        _anim.SetBoneLocalRotation(chestEnum,
            Quaternion.Slerp(chestBone.localRotation, chestLocal, w));

        // ── Chest position weight ─────────────────────────────────────────────
        // The chest doesn't have an AvatarIKGoal; position influence comes from
        // the body position bleed-through. We nudge it slightly toward the target.
        // (Left/Right hand goals could be driven here in a future extension.)
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Head IK
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Drives head rotation using two complementary Unity APIs:
    ///   1. SetLookAtPosition / SetLookAtWeight — Unity's built-in neck/eye solver.
    ///   2. SetBoneLocalRotation on Neck + Head — explicit bone rotation for full
    ///      pitch and yaw control that the look-at solver alone doesn't guarantee.
    /// </summary>
    void ApplyHeadIK()
    {
        float w = _currentWeightScale;
        if (w < 0.001f)
        {
            _anim.SetLookAtWeight(0f);
            return;
        }

        Transform headBone = _anim.GetBoneTransform(HumanBodyBones.Head);
        Transform neckBone = _anim.GetBoneTransform(HumanBodyBones.Neck);
        if (headBone == null) return;

        // ── Clamp head angles independently (head can move more than torso) ──
        float headYaw   = Mathf.Clamp(_headSmoothedYaw,   -headMaxYawDegrees,   headMaxYawDegrees);
        float headPitch = Mathf.Clamp(_headSmoothedPitch, -headMaxPitchDegrees, headMaxPitchDegrees);

        // ── 1. Look-at target (world-space point ahead of the head bone) ─────
        // Build a world-space look direction from the camera's full yaw + pitch
        float   boatWorldYaw = transform.eulerAngles.y;
        Vector3 lookDir      = Quaternion.Euler(headPitch, boatWorldYaw + headYaw, 0f) * Vector3.forward;
        Vector3 lookAtPoint  = headBone.position + lookDir * headLookAtRadius;
        _dbgHeadLookAtPos    = lookAtPoint;

        // SetLookAtWeight(weight, bodyWeight, headWeight, eyesWeight, clampWeight)
        _anim.SetLookAtWeight(
            headLookAtWeight * w,
            headBodyWeight,
            1f,               // head weight within the look-at solve — always full
            headEyeWeight,
            headClampWeight);
        _anim.SetLookAtPosition(lookAtPoint);

        // ── 2. Explicit bone rotation — neck ──────────────────────────────────
        if (neckBone != null)
        {
            float neckYaw   = headYaw   * neckYawShare;
            float neckPitch = headPitch * neckPitchShare;

            // Compute target rotation in world space, then convert to local
            Quaternion neckWorldTarget = Quaternion.Euler(
                neckPitch,
                boatWorldYaw + neckYaw,
                0f);
            Quaternion neckLocal = Quaternion.Inverse(
                neckBone.parent != null ? neckBone.parent.rotation : Quaternion.identity)
                * neckWorldTarget;

            _anim.SetBoneLocalRotation(HumanBodyBones.Neck,
                Quaternion.Slerp(neckBone.localRotation, neckLocal, headBoneWeight * w));
        }

        // ── 3. Explicit bone rotation — head ──────────────────────────────────
        // The head takes the remaining yaw/pitch after the neck has contributed.
        float remainingYaw   = headYaw   * (1f - neckYawShare);
        float remainingPitch = headPitch * (1f - neckPitchShare);

        Quaternion headWorldTarget = Quaternion.Euler(
            remainingPitch,
            boatWorldYaw + remainingYaw,
            0f);
        Quaternion headLocal = Quaternion.Inverse(
            headBone.parent != null ? headBone.parent.rotation : Quaternion.identity)
            * headWorldTarget;

        _anim.SetBoneLocalRotation(HumanBodyBones.Head,
            Quaternion.Slerp(headBone.localRotation, headLocal, headBoneWeight * w));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Gizmos
    // ─────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!debugDraw || !Application.isPlaying) return;

        // Hip target
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        Gizmos.DrawSphere(_dbgHipTargetPos, 0.07f);
        Gizmos.DrawLine(
            _anim != null && _anim.GetBoneTransform(HumanBodyBones.Hips) != null
                ? _anim.GetBoneTransform(HumanBodyBones.Hips).position
                : transform.position,
            _dbgHipTargetPos);

        // Chest target
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.9f);
        Transform chestBone = _anim != null
            ? (_anim.GetBoneTransform(HumanBodyBones.UpperChest)
            ?? _anim.GetBoneTransform(HumanBodyBones.Chest)
            ?? _anim.GetBoneTransform(HumanBodyBones.Spine))
            : null;
        if (chestBone != null)
        {
            Gizmos.DrawSphere(_dbgChestTargetPos, 0.07f);
            Gizmos.DrawLine(chestBone.position, _dbgChestTargetPos);
        }

        // Head look-at target
        if (_anim != null)
        {
            Transform headBone = _anim.GetBoneTransform(HumanBodyBones.Head);
            if (headBone != null)
            {
                Gizmos.color = new Color(1f, 0.2f, 0.8f, 0.9f);
                Gizmos.DrawSphere(_dbgHeadLookAtPos, 0.06f);
                Gizmos.DrawLine(headBone.position, _dbgHeadLookAtPos);

                // Neck bone indicator
                Transform neckBone = _anim.GetBoneTransform(HumanBodyBones.Neck);
                if (neckBone != null)
                {
                    Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.7f);
                    Gizmos.DrawSphere(neckBone.position, 0.04f);
                }
            }
        }

        // Look direction ray from camera
        if (boatCamera != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(boatCamera.transform.position,
                boatCamera.GetCameraForward() * 2f);
        }
    }
#endif
}
