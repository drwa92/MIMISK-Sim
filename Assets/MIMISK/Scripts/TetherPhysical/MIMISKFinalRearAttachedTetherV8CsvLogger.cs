using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// CSV logger for the final V8/V9 runtime tether.
/// This logger reads only the V8 tether component and the original MIMISK managers.
/// It does not command the winch, change mission state, or modify the MiniROV home.
/// </summary>
[DefaultExecutionOrder(33000)]
[DisallowMultipleComponent]
public class MIMISKFinalRearAttachedTetherV8CsvLogger : MonoBehaviour
{
    [Header("Logging")]
    public bool enableLogging = true;
    public float sampleIntervalS = 0.033333f;
    public bool createLogsFolderInProjectRoot = true;
    public string logsFolderName = "Logs";
    public string filePrefix = "mimisk_v8_tether";
    public string currentFilePath = "";

    [Header("References")]
    public MIMISKFinalRearAttachedTetherV8 tether;
    public MIMISKUnifiedTetherManager unifiedTether;
    public MIMISKDroneCoreTetherManager droneCoreTether;
    public Rigidbody droneRigidbody;
    public Rigidbody miniRovRigidbody;

    private StreamWriter writer;
    private float nextSampleTime;
    private readonly CultureInfo culture = CultureInfo.InvariantCulture;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        if (enableLogging)
        {
            OpenLog();
        }
    }

    private void LateUpdate()
    {
        if (!enableLogging || tether == null)
        {
            return;
        }

        if (writer == null)
        {
            OpenLog();
        }

        if (Time.time + 0.0001f < nextSampleTime)
        {
            return;
        }
        nextSampleTime = Time.time + Mathf.Max(0.005f, sampleIntervalS);
        WriteRow();
    }

    private void OnDisable()
    {
        CloseLog();
    }

    private void OnDestroy()
    {
        CloseLog();
    }

    private void OnApplicationQuit()
    {
        CloseLog();
    }

    public void AutoFindReferences()
    {
        if (tether == null) tether = GetComponent<MIMISKFinalRearAttachedTetherV8>();
        if (tether == null) tether = FindFirstObjectByType<MIMISKFinalRearAttachedTetherV8>();

        if (unifiedTether == null) unifiedTether = GetComponent<MIMISKUnifiedTetherManager>();
        if (unifiedTether == null) unifiedTether = FindFirstObjectByType<MIMISKUnifiedTetherManager>();

        if (droneCoreTether == null) droneCoreTether = GetComponent<MIMISKDroneCoreTetherManager>();
        if (droneCoreTether == null) droneCoreTether = FindFirstObjectByType<MIMISKDroneCoreTetherManager>();

        if (droneRigidbody == null) droneRigidbody = GetComponent<Rigidbody>();
        if (miniRovRigidbody == null)
        {
            GameObject rov = GameObject.Find("MiniROV");
            if (rov != null) miniRovRigidbody = rov.GetComponent<Rigidbody>();
        }
    }

    public void OpenLog()
    {
        if (writer != null) return;
        AutoFindReferences();

        string folder;
        if (createLogsFolderInProjectRoot)
        {
            folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, logsFolderName);
        }
        else
        {
            folder = Path.Combine(Application.persistentDataPath, logsFolderName);
        }
        Directory.CreateDirectory(folder);

        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", culture);
        currentFilePath = Path.Combine(folder, filePrefix + "_" + stamp + ".csv");
        writer = new StreamWriter(currentFilePath, false, Encoding.UTF8);
        WriteHeader();
        nextSampleTime = Time.time;
        Debug.Log("[MIMISK V8/V9 Tether Logger] Logging to: " + currentFilePath);
    }

    public void CloseLog()
    {
        if (writer == null) return;
        writer.Flush();
        writer.Close();
        writer = null;
    }

    private void WriteHeader()
    {
        writer.WriteLine(string.Join(",", new string[]
        {
            "time_s",
            "unified_state",
            "legacy_tether_state",
            "smart_winch_decision",
            "v8_endpoint_mode",
            "v8_start_x","v8_start_y","v8_start_z",
            "v8_end_x","v8_end_y","v8_end_z",
            "v8_commanded_length_m",
            "v8_visual_length_m",
            "v8_effective_extension_m",
            "v8_straight_distance_m",
            "v8_geometric_length_m",
            "v8_slack_m",
            "v8_sag_depth_m",
            "v8_runtime_node_count",
            "v8_rendered_point_count",
            "v8_rear_anchor_error_m",
            "v8_solver_healthy",
            "v8_contact_enabled",
            "v8_contact_active",
            "v8_contact_node_count",
            "v8_contact_length_m",
            "v8_max_contact_penetration_m",
            "v8_lowest_node_clearance_m",
            "v8_contact_surface_source",
            "v8_apply_forces",
            "drone_x","drone_y","drone_z",
            "rov_x","rov_y","rov_z",
            "rov_vx","rov_vy","rov_vz",
            "original_deployed_length_m",
            "original_target_length_m",
            "original_maximum_length_m",
            "original_rov_control_active",
            "original_minirov_dynamic"
        }));
    }

    private void WriteRow()
    {
        if (writer == null || tether == null) return;

        Vector3 dronePos = droneRigidbody != null ? droneRigidbody.position : transform.position;
        Vector3 rovPos = miniRovRigidbody != null ? miniRovRigidbody.position : Vector3.zero;
        Vector3 rovVel = miniRovRigidbody != null ? miniRovRigidbody.linearVelocity : Vector3.zero;

        string unifiedState = unifiedTether != null ? unifiedTether.tetherState.ToString() : "none";
        string legacyState = droneCoreTether != null ? droneCoreTether.tetherState.ToString() : "none";
        string winchDecision = droneCoreTether != null ? droneCoreTether.lastEvent : "none";

        float originalDeployed = droneCoreTether != null ? droneCoreTether.deployedLengthM : 0.0f;
        float originalTarget = droneCoreTether != null ? droneCoreTether.targetLengthM : 0.0f;
        float originalMaximum = droneCoreTether != null ? droneCoreTether.maximumLengthM : 0.0f;
        bool rovControlActive = unifiedTether != null && unifiedTether.rovControlActive;
        bool miniRovDynamic = unifiedTether != null && unifiedTether.miniRovDynamic;

        writer.WriteLine(string.Join(",", new string[]
        {
            F(Time.time),
            S(unifiedState),
            S(legacyState),
            S(winchDecision),
            S(tether.endpointModeText),
            F(tether.startWorld.x),F(tether.startWorld.y),F(tether.startWorld.z),
            F(tether.endWorld.x),F(tether.endWorld.y),F(tether.endWorld.z),
            F(tether.commandedLengthM),
            F(tether.visualCableLengthM),
            F(tether.effectiveVisualLengthExtensionM),
            F(tether.straightDistanceM),
            F(tether.geometricCableLengthM),
            F(tether.slackM),
            F(tether.sagDepthM),
            tether.runtimeNodeCount.ToString(culture),
            tether.renderedPointCount.ToString(culture),
            F(tether.rearAnchorErrorM),
            B(tether.solverHealthy),
            B(tether.enableContactProjection),
            B(tether.contactProjectionActive),
            tether.contactNodeCount.ToString(culture),
            F(tether.contactLengthM),
            F(tether.maxContactPenetrationM),
            F(tether.lowestNodeClearanceM),
            S(tether.contactSurfaceSource),
            B(tether.applyForces),
            F(dronePos.x),F(dronePos.y),F(dronePos.z),
            F(rovPos.x),F(rovPos.y),F(rovPos.z),
            F(rovVel.x),F(rovVel.y),F(rovVel.z),
            F(originalDeployed),
            F(originalTarget),
            F(originalMaximum),
            B(rovControlActive),
            B(miniRovDynamic)
        }));
    }

    private string F(float v)
    {
        return v.ToString("0.######", culture);
    }

    private string B(bool v)
    {
        return v ? "1" : "0";
    }

    private string S(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace(",", "_").Replace("\n", " ").Replace("\r", " ");
    }
}
