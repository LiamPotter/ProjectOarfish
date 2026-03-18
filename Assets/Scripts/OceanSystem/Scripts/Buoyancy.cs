using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Physics-based buoyancy that reacts to OceanManager's Gerstner waves.
///
/// The component automatically generates "voxel" sample points from the
/// object's colliders (or a custom set of points) and applies per-point
/// buoyancy forces, producing realistic tilting and bobbing that matches
/// the shape of the object.
///
/// Requirements:
///   • A Rigidbody on this or a parent GameObject.
///   • OceanManager present in the scene.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Buoyancy : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector – Buoyancy feel
    // -------------------------------------------------------------------------
    [Header("Buoyancy")]
    [Tooltip("How much upward force is applied per submerged voxel.")]
    [Range(0f, 50f)] public float buoyancyForce = 10f;

    [Tooltip("Water density equivalent. Higher = more upward force per depth.")]
    [Range(0f, 5f)]  public float waterDensity  = 1f;

    [Tooltip("Vertical offset applied to the ocean plane for this object. " +
             "Use to fine-tune how deep the object sits.")]
    [Range(-2f, 2f)] public float submersionOffset = 0f;

    // -------------------------------------------------------------------------
    // Inspector – Drag (damping in water)
    // -------------------------------------------------------------------------
    [Header("Water Drag")]
    [Tooltip("Linear drag applied when any part is underwater.")]
    [Range(0f, 10f)] public float waterLinearDrag  = 2.5f;
    [Tooltip("Angular drag applied when any part is underwater.")]
    [Range(0f, 10f)] public float waterAngularDrag = 1.5f;

    // -------------------------------------------------------------------------
    // Inspector – Wave alignment
    // -------------------------------------------------------------------------
    [Header("Wave Tilt")]
    [Tooltip("Whether the object tilts to align with the local wave surface normal.")]
    public bool alignToWaves = true;
    [Tooltip("How quickly the object rotates to align with the wave normal.")]
    [Range(0f, 10f)] public float alignmentSpeed = 2f;
    [Tooltip("Maximum tilt angle in degrees (prevents unrealistic flipping).")]
    [Range(0f, 45f)] public float maxTiltAngle   = 25f;

    // -------------------------------------------------------------------------
    // Inspector – Float point sampling
    // -------------------------------------------------------------------------
    [Header("Float Point Sampling")]
    [Tooltip("If empty, float points are auto-generated from colliders.\n" +
             "Assign custom transforms here for precise control (e.g. corners of a boat hull).")]
    public Transform[] customFloatPoints;

    [Tooltip("How many sample points to generate per collider axis when auto-generating.")]
    [Range(1, 5)] public int samplesPerAxis = 2;

    [Tooltip("Draw float points and force vectors in the Scene view.")]
    public bool debugDraw = true;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------
    Rigidbody  _rb;
    float      _defaultLinearDrag;
    float      _defaultAngularDrag;

    // World-space float points used each physics tick
    readonly List<Vector3> _floatPoints = new List<Vector3>();

    // Whether each float point is currently submerged (for debug draw)
    readonly List<bool>    _submerged   = new List<bool>();

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------
    void Awake()
    {
        _rb = GetComponentInParent<Rigidbody>();
        if (_rb == null)
            _rb = GetComponent<Rigidbody>();

        _defaultLinearDrag  = _rb.linearDamping;
        _defaultAngularDrag = _rb.angularDamping;
    }

    void Start()
    {
        BuildFloatPoints();
    }

    void FixedUpdate()
    {
        if (OceanManager.Instance == null) return;

        ApplyBuoyancy();
        ApplyWaveAlignment();
    }

    // -------------------------------------------------------------------------
    // Float point construction
    // -------------------------------------------------------------------------

    /// <summary>
    /// Re-generates internal float points. Call this at runtime if the
    /// object's shape changes (e.g. a cargo door opens).
    /// </summary>
    public void BuildFloatPoints()
    {
        _floatPoints.Clear();

        if (customFloatPoints != null && customFloatPoints.Length > 0)
        {
            // Use designer-placed points
            foreach (var t in customFloatPoints)
                if (t != null)
                    _floatPoints.Add(t.position); // updated each frame in ApplyBuoyancy
            return;
        }

        // Auto-generate from all colliders on this or child objects
        Collider[] cols = GetComponentsInChildren<Collider>();
        if (cols.Length == 0)
        {
            // Fallback: single centre point
            _floatPoints.Add(transform.position);
            Debug.LogWarning($"[Buoyancy] No colliders found on '{name}'. Using centre point.", this);
            return;
        }

        foreach (var col in cols)
            GeneratePointsForCollider(col);

        if (_floatPoints.Count == 0)
            _floatPoints.Add(transform.position);

        Debug.Log($"[Buoyancy] Generated {_floatPoints.Count} float points from {cols.Length} collider(s).", this);
    }

    void GeneratePointsForCollider(Collider col)
    {
        Bounds bounds = col.bounds;
        int    n      = samplesPerAxis;

        for (int xi = 0; xi < n; xi++)
        for (int yi = 0; yi < n; yi++)
        for (int zi = 0; zi < n; zi++)
        {
            // Lerp across the bounding box
            float tx = n == 1 ? 0.5f : xi / (float)(n - 1);
            float ty = n == 1 ? 0.5f : yi / (float)(n - 1);
            float tz = n == 1 ? 0.5f : zi / (float)(n - 1);

            Vector3 worldPt = new Vector3(
                Mathf.Lerp(bounds.min.x, bounds.max.x, tx),
                Mathf.Lerp(bounds.min.y, bounds.max.y, ty),
                Mathf.Lerp(bounds.min.z, bounds.max.z, tz)
            );

            // Only keep point if it's inside the collider (roughly)
            // We use a small sphere overlap test relative to the collider centre
            Vector3 closest = col.ClosestPoint(worldPt);
            if (Vector3.Distance(worldPt, closest) < 0.01f)
                _floatPoints.Add(worldPt); // stored as offset from transform root
        }
    }

    // -------------------------------------------------------------------------
    // Buoyancy physics
    // -------------------------------------------------------------------------
    void ApplyBuoyancy()
    {
        // Update float point list size tracker
        while (_submerged.Count < _floatPoints.Count)
            _submerged.Add(false);

        int submergedCount = 0;
        int totalPoints    = _floatPoints.Count;

        // Refresh custom point world positions each frame
        bool usingCustom = customFloatPoints != null && customFloatPoints.Length > 0;
        if (usingCustom)
        {
            for (int i = 0; i < customFloatPoints.Length && i < _floatPoints.Count; i++)
                if (customFloatPoints[i] != null)
                    _floatPoints[i] = customFloatPoints[i].position;
        }
        else
        {
            // Auto-generated points are in world-space at build time;
            // we need to re-derive them in local space and re-transform each frame.
            // For simplicity we rebuild each frame from colliders (cheap at low sampleCount).
            RebuildFloatPointsWorldSpace();
        }

        for (int i = 0; i < _floatPoints.Count; i++)
        {
            Vector3 pt          = _floatPoints[i];
            float   waveY       = OceanManager.Instance.GetWaveHeight(pt) + submersionOffset;
            float   depthBelow  = waveY - pt.y;   // +ve means submerged
            bool    isSubmerged = depthBelow > 0f;

            _submerged[i] = isSubmerged;

            if (!isSubmerged) continue;
            submergedCount++;

            // Buoyancy force: F = ρ × g × V (simplified as depth × configurable force)
            float forceMag = waterDensity * buoyancyForce * depthBelow * Physics.gravity.magnitude;
            // Distribute evenly across sample points
            forceMag /= totalPoints;

            _rb.AddForceAtPosition(Vector3.up * forceMag, pt, ForceMode.Force);
        }

        // Apply water drag when partially or fully submerged
        if (submergedCount > 0)
        {
            _rb.linearDamping  = Mathf.Lerp(_defaultLinearDrag,  waterLinearDrag,  (float)submergedCount / totalPoints);
            _rb.angularDamping = Mathf.Lerp(_defaultAngularDrag, waterAngularDrag, (float)submergedCount / totalPoints);
        }
        else
        {
            _rb.linearDamping  = _defaultLinearDrag;
            _rb.angularDamping = _defaultAngularDrag;
        }
    }

    /// <summary>
    /// Regenerate world-space float points every frame so they follow the
    /// object as it moves and rotates.
    /// </summary>
    void RebuildFloatPointsWorldSpace()
    {
        _floatPoints.Clear();
        Collider[] cols = GetComponentsInChildren<Collider>();
        foreach (var col in cols)
            GeneratePointsForCollider(col);

        if (_floatPoints.Count == 0)
            _floatPoints.Add(transform.position);

        while (_submerged.Count < _floatPoints.Count)
            _submerged.Add(false);
    }

    // -------------------------------------------------------------------------
    // Wave alignment
    // -------------------------------------------------------------------------
    void ApplyWaveAlignment()
    {
        if (!alignToWaves) return;

        // Sample normal at the object's centre
        Vector3 waveNormal = OceanManager.Instance.GetWaveNormal(transform.position);

        // Build a target rotation that aligns Up with the wave normal
        Quaternion targetRot = Quaternion.FromToRotation(transform.up, waveNormal) * transform.rotation;

        // Clamp tilt angle to prevent flipping
        float angle = Quaternion.Angle(transform.rotation, targetRot);
        if (angle > maxTiltAngle)
            targetRot = Quaternion.RotateTowards(transform.rotation, targetRot, maxTiltAngle);

        // Smoothly drive the Rigidbody
        Quaternion smoothed = Quaternion.Slerp(transform.rotation, targetRot,
            alignmentSpeed * Time.fixedDeltaTime);

        _rb.MoveRotation(smoothed);
    }

    // -------------------------------------------------------------------------
    // Editor debug visualisation
    // -------------------------------------------------------------------------
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!debugDraw) return;

        for (int i = 0; i < _floatPoints.Count; i++)
        {
            bool sub = i < _submerged.Count && _submerged[i];
            Gizmos.color = sub ? new Color(0f, 0.8f, 1f, 0.9f) : new Color(1f, 0.4f, 0f, 0.7f);
            Gizmos.DrawSphere(_floatPoints[i], 0.05f);

            if (sub && OceanManager.Instance != null)
            {
                float waveY    = OceanManager.Instance.GetWaveHeight(_floatPoints[i]) + submersionOffset;
                float depthBel = waveY - _floatPoints[i].y;
                Gizmos.color   = new Color(0f, 1f, 0.5f, 0.5f);
                Gizmos.DrawLine(_floatPoints[i], _floatPoints[i] + Vector3.up * depthBel);
            }
        }

        // Draw wave surface normal at object centre
        if (OceanManager.Instance != null)
        {
            Vector3 n = OceanManager.Instance.GetWaveNormal(transform.position);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, n);
        }
    }
#endif
}
