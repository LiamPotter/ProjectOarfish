using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Generates a subdivided quad mesh suitable for the Ocean shader at runtime
/// or from the editor menu (Tools → Ocean → Create Ocean Mesh).
///
/// Higher resolution gives smoother waves but costs more vertex work.
/// The shader's Gerstner displacement happens in the vertex stage,
/// so more vertices = better visual fidelity.
/// </summary>
[ExecuteAlways]
public class OceanMeshGenerator : MonoBehaviour
{
    [Header("Mesh Settings")]
    [Tooltip("Total world-space size of the ocean plane.")]
    public float size = 200f;

    [Tooltip("Number of quad subdivisions along each axis. " +
             "Higher = smoother waves. Recommended: 100–300.")]
    [Range(10, 500)] public int resolution = 150;

    [Tooltip("Regenerate the mesh automatically when values change in the Inspector.")]
    public bool autoRebuild = true;

    MeshFilter _mf;

    void Awake()   => Build();
    void OnValidate()
    {
        if (autoRebuild) Build();
    }

    /// <summary>
    /// Builds and assigns the ocean mesh to the attached MeshFilter.
    /// Call this at runtime to dynamically resize the ocean.
    /// </summary>
    public void Build()
    {
        _mf = GetComponent<MeshFilter>();
        if (_mf == null) _mf = gameObject.AddComponent<MeshFilter>();

        Mesh mesh = GenerateMesh(size, resolution);
        mesh.name = "OceanMesh";
        _mf.sharedMesh = mesh;
    }

    public static Mesh GenerateMesh(float size, int resolution)
    {
        int vCount = resolution + 1;
        int totalVerts = vCount * vCount;

        Vector3[] verts  = new Vector3[totalVerts];
        Vector2[] uvs    = new Vector2[totalVerts];
        int[]     tris   = new int[resolution * resolution * 6];

        float halfSize = size * 0.5f;
        float step     = size / resolution;

        for (int z = 0; z <= resolution; z++)
        for (int x = 0; x <= resolution; x++)
        {
            int idx   = z * vCount + x;
            verts[idx] = new Vector3(x * step - halfSize, 0f, z * step - halfSize);
            uvs[idx]   = new Vector2(x / (float)resolution, z / (float)resolution);
        }

        int ti = 0;
        for (int z = 0; z < resolution; z++)
        for (int x = 0; x < resolution; x++)
        {
            int bl = z * vCount + x;
            int br = bl + 1;
            int tl = bl + vCount;
            int tr = tl + 1;

            tris[ti++] = bl; tris[ti++] = tl; tris[ti++] = tr;
            tris[ti++] = bl; tris[ti++] = tr; tris[ti++] = br;
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // support > 65k verts
        mesh.vertices    = verts;
        mesh.uv          = uvs;
        mesh.triangles   = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

#if UNITY_EDITOR
    [MenuItem("Tools/Ocean/Create Ocean Mesh")]
    static void CreateOcean()
    {
        GameObject go = new GameObject("Ocean");
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        var gen = go.AddComponent<OceanMeshGenerator>();
        gen.Build();

        // Try to set ocean material if one already exists in the project
        string[] matGuids = AssetDatabase.FindAssets("t:Material OceanSurface");
        if (matGuids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(matGuids[0]);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // Also add an OceanManager if none exists
        if (FindAnyObjectByType<OceanManager>() == null)
        {
            var mgr = go.AddComponent<OceanManager>();
            mgr.oceanRenderer = go.GetComponent<MeshRenderer>();
        }

        Selection.activeGameObject = go;
        Undo.RegisterCreatedObjectUndo(go, "Create Ocean");
        Debug.Log("[Ocean] GameObject created. Assign an Ocean material in the MeshRenderer.");
    }
#endif
}
