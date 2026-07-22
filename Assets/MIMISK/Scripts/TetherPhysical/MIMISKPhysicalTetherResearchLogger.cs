using System;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// CSV logger for physical tether validation. This is separate from the
/// existing MIMISKUnifiedTetherResearchLogger so the current logger remains
/// untouched.
/// </summary>
[DisallowMultipleComponent]
public class MIMISKPhysicalTetherResearchLogger : MonoBehaviour
{
    [Header("References")]
    public bool autoFindReferences = true;
    public MIMISKPhysicalTetherModel physicalTether;
    public MIMISKPhysicalTetherSafetyGuard safetyGuard;
    public MIMISKUnifiedTetherManager unifiedTether;
    public MIMISKDroneCoreTetherManager tetherManager;
    public MIMISKTetherSmartWinchController smartWinch;
    public MIMISKPhysicalTetherRovSyncMonitor rovSyncMonitor;
    public Rigidbody droneRigidbody;
    public Rigidbody miniRovRigidbody;

    [Header("Logging")]
    public bool enableLogging = false;
    public float logHz = 30.0f;
    public bool flushEveryLine = false;
    public string logFolderName = "MIMISK_PhysicalTether";

    [Header("Runtime")]
    public string currentLogPath;
    public int linesWritten;

    private StreamWriter writer;
    private float timer;
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private void Awake()
    {
        if (autoFindReferences)
        {
            AutoFindReferences();
        }
    }

    private void OnEnable()
    {
        if (enableLogging && Application.isPlaying)
        {
            OpenLog();
        }
    }

    private void FixedUpdate()
    {
        if (!enableLogging || !Application.isPlaying)
        {
            return;
        }

        if (autoFindReferences)
        {
            AutoFindReferencesIfMissing();
        }

        if (writer == null)
        {
            OpenLog();
        }

        timer += Time.fixedDeltaTime;
        float period = 1.0f / Mathf.Max(1.0f, logHz);

        if (timer >= period)
        {
            timer -= period;
            WriteLine();
        }
    }

    private void OnDisable()
    {
        CloseLog();
    }

    private void OnDestroy()
    {
        CloseLog();
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (physicalTether == null)
        {
            physicalTether = GetComponent<MIMISKPhysicalTetherModel>();
        }

        if (safetyGuard == null)
        {
            safetyGuard = GetComponent<MIMISKPhysicalTetherSafetyGuard>();
        }

        if (unifiedTether == null)
        {
            unifiedTether = GetComponent<MIMISKUnifiedTetherManager>();
        }

        if (tetherManager == null)
        {
            tetherManager = GetComponent<MIMISKDroneCoreTetherManager>();
        }

        if (smartWinch == null)
        {
            smartWinch = GetComponent<MIMISKTetherSmartWinchController>();
        }

        if (rovSyncMonitor == null)
        {
            rovSyncMonitor = GetComponent<MIMISKPhysicalTetherRovSyncMonitor>();
        }

        if (droneRigidbody == null)
        {
            droneRigidbody = GetComponent<Rigidbody>();
        }

        if (unifiedTether != null && miniRovRigidbody == null)
        {
            miniRovRigidbody = unifiedTether.miniRovRigidbody;
        }

        if (tetherManager != null && miniRovRigidbody == null)
        {
            miniRovRigidbody = tetherManager.miniRovRigidbody;
        }
    }

    private void AutoFindReferencesIfMissing()
    {
        if (physicalTether == null || safetyGuard == null || unifiedTether == null || tetherManager == null || rovSyncMonitor == null)
        {
            AutoFindReferences();
        }
    }

    [ContextMenu("Open Log")]
    public void OpenLog()
    {
        CloseLog();
        AutoFindReferences();

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string logDir = Path.Combine(projectRoot, "Logs", logFolderName);
        Directory.CreateDirectory(logDir);

        string fileName = "mimisk_physical_tether_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";

        currentLogPath = Path.Combine(logDir, fileName);
        writer = new StreamWriter(currentLogPath, false);
        WriteHeader();
        linesWritten = 0;
    }

    [ContextMenu("Close Log")]
    public void CloseLog()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }
    }

    private void WriteHeader()
    {
        writer.WriteLine(
            "time_s," +
            "unified_state,legacy_tether_state,smart_winch_decision,guard_action," +
            "drone_x,drone_y,drone_z,drone_vx,drone_vy,drone_vz," +
            "rov_x,rov_y,rov_z,rov_vx,rov_vy,rov_vz," +
            "deployed_length_m,commanded_deployed_length_m,effective_length_extension_m,cable_point_count,winch_rate_m_s,straight_distance_m,geometric_length_m," +
            "slack_m,geometric_stretch_m,elastic_stretch_m,max_segment_strain," +
            "start_tension_n,end_tension_n,max_tension_n,mean_tension_n," +
            "sag_depth_m,submerged_fraction,physical_state,endpoint_mode," +
            "start_x,start_y,start_z,end_x,end_y,end_z," +
            "endpoint_attachment_error_m,end_anchor_child_of_rov,end_anchor_local_x,end_anchor_local_y,end_anchor_local_z," +
            "sync_required_length_m,sync_desired_length_m,sync_current_deployed_length_m,sync_length_error_m,sync_endpoint_locked,sync_length_ok," +
            "solver_healthy," +
            "guard_warning_tension,guard_critical_tension,guard_too_short,guard_too_slack,guard_recovery_unsafe,guard_rov_control_unsafe," +
            "force_rov_x,force_rov_y,force_rov_z,force_drone_x,force_drone_y,force_drone_z"
        );
    }

    private void WriteLine()
    {
        if (writer == null)
        {
            return;
        }

        Vector3 dronePos = droneRigidbody != null ? droneRigidbody.position : transform.position;
        Vector3 droneVel = droneRigidbody != null ? droneRigidbody.linearVelocity : Vector3.zero;
        Vector3 rovPos = miniRovRigidbody != null ? miniRovRigidbody.position : Vector3.zero;
        Vector3 rovVel = miniRovRigidbody != null ? miniRovRigidbody.linearVelocity : Vector3.zero;
        Vector3 tetherStart = physicalTether != null ? physicalTether.startWorld : Vector3.zero;
        Vector3 tetherEnd = physicalTether != null ? physicalTether.endWorld : Vector3.zero;
        Vector3 endLocal = physicalTether != null ? physicalTether.endAnchorLocalOnMiniRov : Vector3.zero;

        string line =
            F(Time.time) + "," +
            Q(unifiedTether != null ? unifiedTether.tetherState.ToString() : "missing") + "," +
            Q(tetherManager != null ? tetherManager.tetherState.ToString() : "missing") + "," +
            Q(smartWinch != null ? smartWinch.tmsDecision : "missing") + "," +
            Q(safetyGuard != null ? safetyGuard.lastAction : "missing") + "," +

            F(dronePos.x) + "," + F(dronePos.y) + "," + F(dronePos.z) + "," +
            F(droneVel.x) + "," + F(droneVel.y) + "," + F(droneVel.z) + "," +
            F(rovPos.x) + "," + F(rovPos.y) + "," + F(rovPos.z) + "," +
            F(rovVel.x) + "," + F(rovVel.y) + "," + F(rovVel.z) + "," +

            F(physicalTether != null ? physicalTether.deployedLengthM : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.commandedDeployedLengthM : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.effectiveLengthExtensionM : float.NaN) + "," +
            (physicalTether != null ? physicalTether.CablePointCount.ToString(Culture) : "0") + "," +
            F(physicalTether != null ? physicalTether.winchRateMS : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.straightDistanceM : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.geometricCableLengthM : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.slackM : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.geometricStretchM : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.elasticStretchM : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.maxSegmentStrain : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.startTensionN : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.endTensionN : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.maxTensionN : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.meanTensionN : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.sagDepthM : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.submergedFraction : float.NaN) + "," +
            Q(physicalTether != null ? physicalTether.physicalState.ToString() : "missing") + "," +
            Q(physicalTether != null ? physicalTether.endpointMode : "missing") + "," +
            F(tetherStart.x) + "," + F(tetherStart.y) + "," + F(tetherStart.z) + "," +
            F(tetherEnd.x) + "," + F(tetherEnd.y) + "," + F(tetherEnd.z) + "," +
            F(physicalTether != null ? physicalTether.endpointAttachmentErrorM : float.NaN) + "," +
            B(physicalTether != null && physicalTether.endAnchorIsChildOfMiniRov) + "," +
            F(endLocal.x) + "," + F(endLocal.y) + "," + F(endLocal.z) + "," +
            F(rovSyncMonitor != null ? rovSyncMonitor.requiredLengthM : float.NaN) + "," +
            F(rovSyncMonitor != null ? rovSyncMonitor.desiredLengthWithSlackM : float.NaN) + "," +
            F(rovSyncMonitor != null ? rovSyncMonitor.currentDeployedLengthM : float.NaN) + "," +
            F(rovSyncMonitor != null ? rovSyncMonitor.lengthErrorM : float.NaN) + "," +
            B(rovSyncMonitor != null && rovSyncMonitor.endAnchorLockedToRear) + "," +
            B(rovSyncMonitor != null && rovSyncMonitor.winchLengthSynchronizedToRov) + "," +
            B(physicalTether != null && physicalTether.solverHealthy) + "," +

            B(safetyGuard != null && safetyGuard.warningTension) + "," +
            B(safetyGuard != null && safetyGuard.criticalTension) + "," +
            B(safetyGuard != null && safetyGuard.tooShort) + "," +
            B(safetyGuard != null && safetyGuard.tooSlack) + "," +
            B(safetyGuard != null && safetyGuard.recoveryUnsafe) + "," +
            B(safetyGuard != null && safetyGuard.rovControlUnsafe) + "," +

            F(physicalTether != null ? physicalTether.filteredForceOnMiniRovN.x : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.filteredForceOnMiniRovN.y : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.filteredForceOnMiniRovN.z : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.filteredForceOnDroneN.x : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.filteredForceOnDroneN.y : float.NaN) + "," +
            F(physicalTether != null ? physicalTether.filteredForceOnDroneN.z : float.NaN);

        writer.WriteLine(line);
        linesWritten++;

        if (flushEveryLine)
        {
            writer.Flush();
        }
    }

    private string F(float value)
    {
        return value.ToString("G9", Culture);
    }

    private string B(bool value)
    {
        return value ? "1" : "0";
    }

    private string Q(string value)
    {
        if (value == null)
        {
            value = "";
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
