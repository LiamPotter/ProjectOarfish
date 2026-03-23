#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor wizard that scaffolds a complete boat GameObject hierarchy
/// with all required components pre-configured.
///
/// Access via:  Tools → Ocean → Create Boat
/// </summary>
public class BoatSetupWizard : EditorWindow
{
    string boatName     = "Boat";
    float  boatLength   = 8f;
    float  boatWidth    = 3f;
    float  boatHeight   = 1.5f;
    bool   addAudio     = true;
    bool   addCamera    = true;

    [MenuItem("Tools/Ocean/Create Boat")]
    static void ShowWindow()
    {
        var w = GetWindow<BoatSetupWizard>("Create Boat");
        w.minSize = new Vector2(320, 260);
    }

    void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Boat Setup Wizard", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Creates a ready-to-play boat hierarchy with BoatController, " +
            "Buoyancy, BoatCamera and optional BoatAudio.\n\n" +
            "Requires an OceanManager in the scene.",
            MessageType.Info);

        EditorGUILayout.Space(6);
        boatName   = EditorGUILayout.TextField("Boat Name",   boatName);
        boatLength = EditorGUILayout.FloatField("Hull Length (m)", boatLength);
        boatWidth  = EditorGUILayout.FloatField("Hull Width (m)",  boatWidth);
        boatHeight = EditorGUILayout.FloatField("Hull Height (m)", boatHeight);
        addCamera  = EditorGUILayout.Toggle("Add Camera",  addCamera);
        addAudio   = EditorGUILayout.Toggle("Add Audio",   addAudio);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Create Boat", GUILayout.Height(36)))
            CreateBoat();
    }

    void CreateBoat()
    {
        // ── Root ──────────────────────────────────────────────────────────────
        var root = new GameObject(boatName);
        Undo.RegisterCreatedObjectUndo(root, "Create Boat");

        // Rigidbody
        var rb           = root.AddComponent<Rigidbody>();
        rb.mass          = 800f;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 2f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // ── Hull mesh (simple box as placeholder) ─────────────────────────────
        var hullGO       = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hullGO.name      = "Hull_Visual";
        hullGO.transform.SetParent(root.transform, false);
        hullGO.transform.localScale    = new Vector3(boatWidth, boatHeight * 0.5f, boatLength);
        hullGO.transform.localPosition = new Vector3(0, boatHeight * 0.25f, 0);
        // Remove auto-added collider from primitive (we add our own below)
        DestroyImmediate(hullGO.GetComponent<Collider>());

        // ── Hull collider ─────────────────────────────────────────────────────
        var colGO        = new GameObject("Hull_Collider");
        colGO.transform.SetParent(root.transform, false);
        colGO.transform.localPosition = new Vector3(0, boatHeight * 0.25f, 0);
        var boxCol       = colGO.AddComponent<BoxCollider>();
        boxCol.size      = new Vector3(boatWidth, boatHeight * 0.5f, boatLength);

        // ── Buoyancy ──────────────────────────────────────────────────────────
        var buoyancy     = root.AddComponent<Buoyancy>();
        buoyancy.buoyancyForce    = 15f;
        buoyancy.waterDensity     = 1f;
        buoyancy.samplesPerAxis   = 2;
        buoyancy.alignToWaves     = true;
        buoyancy.alignmentSpeed   = 2f;
        buoyancy.maxTiltAngle     = 20f;
        buoyancy.waterLinearDrag  = 2.5f;
        buoyancy.waterAngularDrag = 1.5f;

        // ── Thrust point (stern) ──────────────────────────────────────────────
        var thrustGO     = new GameObject("ThrustPoint");
        thrustGO.transform.SetParent(root.transform, false);
        thrustGO.transform.localPosition = new Vector3(0, 0, -boatLength * 0.45f);

        // ── Propeller (stub) ──────────────────────────────────────────────────
        var propGO       = new GameObject("Propeller");
        propGO.transform.SetParent(thrustGO.transform, false);
        propGO.transform.localPosition = Vector3.zero;

        // ── BoatController ────────────────────────────────────────────────────
        var ctrl              = root.AddComponent<BoatController>();
        ctrl.maxThrust        = 5000f;
        ctrl.maxSpeed         = 18f;
        ctrl.maxSteerTorque   = 2500f;
        ctrl.thrustPoint      = thrustGO.transform;
        ctrl.propellerTransform = propGO.transform;

        // ── Audio ─────────────────────────────────────────────────────────────
        if (addAudio)
        {
            root.AddComponent<AudioSource>(); // BoatAudio requires one
            root.AddComponent<BoatAudio>();
        }

        // ── Camera ────────────────────────────────────────────────────────────
        if (addCamera)
        {
            Camera existingCam = Camera.main;
            GameObject camGO;

            if (existingCam != null)
            {
                camGO = existingCam.gameObject;
                Debug.Log("[BoatSetupWizard] Reusing existing Main Camera.");
            }
            else
            {
                camGO = new GameObject("BoatCamera");
                camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
                Debug.Log("[BoatSetupWizard] Making new Camera.");
            }

            var camComp           = camGO.GetOrAddComponent<BoatCamera>();
            camComp.target        = root.transform;
            camComp.boatController = ctrl;
            camComp.lookAtOffset  = new Vector3(0, boatHeight * 0.8f, 0);

            // Position camera behind boat to start
            camGO.transform.position = root.transform.position
                + -root.transform.forward * camComp.defaultDistance
                + Vector3.up * 4f;
        }

        // ── Select the new boat ───────────────────────────────────────────────
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Close();

        Debug.Log($"[BoatSetupWizard] '{boatName}' created. " +
                  "Replace Hull_Visual mesh and assign audio clips in BoatAudio.", root);
    }
}

static class GameObjectExtensions
{
    public static T GetOrAddComponent<T>(this GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }
}
#endif
