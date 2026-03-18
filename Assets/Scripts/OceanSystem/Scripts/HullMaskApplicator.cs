using UnityEngine;

/// <summary>
/// Automatically appends the HullMask material to every MeshRenderer in the
/// boat hull hierarchy at startup, so the stencil mask is always in sync with
/// whatever mesh is used for the boat — no manual material slot management needed.
///
/// Setup:
///   1. Add this component to the boat's root GameObject (or any parent of
///      the hull renderers you want to mask).
///   2. Assign the HullMask material (using Ocean/HullMask shader) to the
///      <see cref="hullMaskMaterial"/> field.
///   3. Optionally restrict which child renderers are masked via
///      <see cref="hullRenderers"/>; leave empty to apply to all children.
///
/// How it works:
///   At Awake, the component appends the HullMask material as the LAST material
///   slot on each target renderer.  URP renders material slots in order, so the
///   HullMask pass always executes after the hull's own surface shading, writing
///   the stencil value before the ocean transparent pass runs.
/// </summary>
public class HullMaskApplicator : MonoBehaviour
{
    [Header("Hull Mask")]
    [Tooltip("Material using the Ocean/HullMask shader. Create one in your project " +
             "and assign it here.")]
    public Material hullMaskMaterial;

    [Tooltip("Specific renderers to mask. Leave empty to auto-find all MeshRenderers " +
             "on this GameObject and its children.")]
    public MeshRenderer[] hullRenderers;

    [Tooltip("If true, the mask material is added at runtime (Awake). " +
             "Disable if you prefer to manage material slots manually.")]
    public bool applyOnAwake = true;

    // Track which renderers we modified so we can cleanly revert if needed
    MeshRenderer[] _applied;

    // -------------------------------------------------------------------------
    void Awake()
    {
        if (applyOnAwake)
            Apply();
    }

    // -------------------------------------------------------------------------
    /// <summary>
    /// Appends the HullMask material to all target renderers.
    /// Safe to call multiple times — will not add duplicates.
    /// </summary>
    public void Apply()
    {
        if (hullMaskMaterial == null)
        {
            Debug.LogWarning("[HullMaskApplicator] No HullMask material assigned.", this);
            return;
        }

        MeshRenderer[] targets = GetTargetRenderers();
        _applied = targets;

        foreach (MeshRenderer mr in targets)
        {
            if (AlreadyHasMask(mr)) continue;

            // Append the mask material as an extra slot
            Material[] current  = mr.sharedMaterials;
            Material[] expanded = new Material[current.Length + 1];
            current.CopyTo(expanded, 0);
            expanded[current.Length] = hullMaskMaterial;
            mr.sharedMaterials = expanded;
        }
    }

    // -------------------------------------------------------------------------
    /// <summary>
    /// Removes the HullMask material from all renderers this component modified.
    /// Useful when hot-swapping hull meshes at runtime.
    /// </summary>
    public void Remove()
    {
        if (_applied == null) return;

        foreach (MeshRenderer mr in _applied)
        {
            if (mr == null) continue;
            RemoveMaskFrom(mr);
        }
    }

    // -------------------------------------------------------------------------
    MeshRenderer[] GetTargetRenderers()
    {
        if (hullRenderers != null && hullRenderers.Length > 0)
            return hullRenderers;

        return GetComponentsInChildren<MeshRenderer>(includeInactive: true);
    }

    bool AlreadyHasMask(MeshRenderer mr)
    {
        foreach (Material m in mr.sharedMaterials)
            if (m == hullMaskMaterial) return true;
        return false;
    }

    void RemoveMaskFrom(MeshRenderer mr)
    {
        var mats = new System.Collections.Generic.List<Material>(mr.sharedMaterials);
        mats.RemoveAll(m => m == hullMaskMaterial);
        mr.sharedMaterials = mats.ToArray();
    }

    // -------------------------------------------------------------------------
#if UNITY_EDITOR
    void OnValidate()
    {
        // Warn immediately in editor if the material uses the wrong shader
        if (hullMaskMaterial != null &&
            hullMaskMaterial.shader != null &&
            hullMaskMaterial.shader.name != "Ocean/HullMask")
        {
            Debug.LogWarning(
                $"[HullMaskApplicator] '{hullMaskMaterial.name}' does not use the " +
                "Ocean/HullMask shader. Hull clipping will not work correctly.", this);
        }
    }
#endif
}
