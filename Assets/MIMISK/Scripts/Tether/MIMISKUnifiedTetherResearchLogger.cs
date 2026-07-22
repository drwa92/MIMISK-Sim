using System;
using System.Globalization;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKUnifiedTetherResearchLogger : MonoBehaviour
{
    [Header("References")]
    public MIMISKUnifiedTetherManager unifiedTether;
    public MIMISKDroneCoreTetherManager tetherManager;
    public MIMISKTetherSmartWinchController smartWinch;
    public MIMISKSingleYellowTetherVisualAuthority cableVisual;

    public MIMISKDroneCoreFlightModeManager droneFlightManager;
    public MIMISKDroneCoreMissionManager droneMissionManager;
    public MIMISKMiniROVMissionManager miniRovMissionManager;
    public MIMISKMiniROVPlantBasedController miniRovController;

    public Rigidbody droneRigidbody;
    public Rigidbody miniRovRigidbody;

    [Header("Logging")]
    public bool enableLogging = true;
    public float logHz = 20.0f;
    public bool flushEveryLine = false;

    public string currentLogPath;
    public int linesWritten;

    private StreamWriter writer;
    private float timer;
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void OnEnable()
    {
        if (enableLogging)
        {
            OpenLog();
        }
    }

    private void FixedUpdate()
    {
        if (!enableLogging)
        {
            return;
        }

        if (writer == null)
        {
            OpenLog();
        }

        timer += Time.fixedDeltaTime;

        float period =
            1.0f / Mathf.Max(1.0f, logHz);

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
        if (unifiedTether == null)
        {
            unifiedTether =
                GetComponent<MIMISKUnifiedTetherManager>();
        }

        if (tetherManager == null)
        {
            tetherManager =
                GetComponent<MIMISKDroneCoreTetherManager>();
        }

        if (smartWinch == null)
        {
            smartWinch =
                GetComponent<MIMISKTetherSmartWinchController>();
        }

        if (cableVisual == null)
        {
            cableVisual =
                GetComponent<MIMISKSingleYellowTetherVisualAuthority>();
        }

        if (droneFlightManager == null)
        {
            droneFlightManager =
                GetComponent<MIMISKDroneCoreFlightModeManager>();
        }

        if (droneMissionManager == null)
        {
            droneMissionManager =
                GetComponent<MIMISKDroneCoreMissionManager>();
        }

        if (droneRigidbody == null)
        {
            droneRigidbody =
                GetComponent<Rigidbody>();
        }

        if (unifiedTether != null)
        {
            if (miniRovRigidbody == null)
            {
                miniRovRigidbody =
                    unifiedTether.miniRovRigidbody;
            }

            if (miniRovMissionManager == null)
            {
                miniRovMissionManager =
                    unifiedTether.miniRovMissionManager;
            }

            if (miniRovController == null)
            {
                miniRovController =
                    unifiedTether.miniRovPlantController;
            }
        }
    }

    [ContextMenu("Open Log")]
    public void OpenLog()
    {
        AutoFindReferences();

        string projectRoot =
            Directory.GetParent(Application.dataPath).FullName;

        string logDir =
            Path.Combine(projectRoot, "Logs", "MIMISK_Tether");

        Directory.CreateDirectory(logDir);

        string fileName =
            "mimisk_tether_research_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss") +
            ".csv";

        currentLogPath =
            Path.Combine(logDir, fileName);

        writer =
            new StreamWriter(currentLogPath, false);

        WriteHeader();
        linesWritten = 0;
    }

    private void WriteHeader()
    {
        writer.WriteLine(
            "time_s," +
            "unified_state,unified_event,drone_flight_mode,drone_mission_state,minirov_mission_state,minirov_control_mode," +
            "drone_x,drone_y,drone_z,rov_x,rov_y,rov_z,rov_vx,rov_vy,rov_vz," +
            "tether_state,deployed_length_m,target_length_m,winch_rate_m_s,straight_distance_m,slack_m,stretch_m,tension_n,raw_tension_n," +
            "smart_active,smart_decision,smart_required_length_m,smart_desired_length_m,smart_error_m,smart_error_rate_m_s,smart_projected_rov_speed_m_s,smart_winch_speed_cmd_m_s," +
            "visual_mode,visual_start_x,visual_start_y,visual_start_z,visual_end_x,visual_end_y,visual_end_z,visual_slack_m,visual_sag_m,visual_side_curve_m," +
            "deployment_home_recorded,distance_to_deployment_home_m,drone_motion_allowed,safe_to_deploy"
        );
    }

    private void WriteLine()
    {
        AutoFindReferences();

        Vector3 dronePos =
            droneRigidbody != null
                ? droneRigidbody.position
                : transform.position;

        Vector3 rovPos =
            miniRovRigidbody != null
                ? miniRovRigidbody.position
                : Vector3.zero;

        Vector3 rovVel =
            miniRovRigidbody != null
                ? miniRovRigidbody.linearVelocity
                : Vector3.zero;

        string line =
            F(Time.time) + "," +
            Q(unifiedTether != null ? unifiedTether.tetherState.ToString() : "missing") + "," +
            Q(unifiedTether != null ? unifiedTether.lastEvent : "missing") + "," +
            Q(droneFlightManager != null ? droneFlightManager.flightMode.ToString() : "missing") + "," +
            Q(droneMissionManager != null ? droneMissionManager.missionState.ToString() : "missing") + "," +
            Q(miniRovMissionManager != null ? miniRovMissionManager.missionState.ToString() : "missing") + "," +
            Q(miniRovController != null ? miniRovController.controlMode.ToString() : "missing") + "," +

            F(dronePos.x) + "," + F(dronePos.y) + "," + F(dronePos.z) + "," +
            F(rovPos.x) + "," + F(rovPos.y) + "," + F(rovPos.z) + "," +
            F(rovVel.x) + "," + F(rovVel.y) + "," + F(rovVel.z) + "," +

            Q(tetherManager != null ? tetherManager.tetherState.ToString() : "missing") + "," +
            F(tetherManager != null ? tetherManager.deployedLengthM : float.NaN) + "," +
            F(tetherManager != null ? tetherManager.targetLengthM : float.NaN) + "," +
            F(tetherManager != null ? tetherManager.winchCommandRateMS : float.NaN) + "," +
            F(tetherManager != null ? tetherManager.straightDistanceM : float.NaN) + "," +
            F(tetherManager != null ? tetherManager.slackM : float.NaN) + "," +
            F(tetherManager != null ? tetherManager.stretchM : float.NaN) + "," +
            F(tetherManager != null ? tetherManager.tensionN : float.NaN) + "," +
            F(tetherManager != null ? tetherManager.rawTensionN : float.NaN) + "," +

            B(smartWinch != null && smartWinch.smartTmsActive) + "," +
            Q(smartWinch != null ? smartWinch.tmsDecision : "missing") + "," +
            F(smartWinch != null ? smartWinch.requiredLengthM : float.NaN) + "," +
            F(smartWinch != null ? smartWinch.desiredLengthM : float.NaN) + "," +
            F(smartWinch != null ? smartWinch.lengthErrorM : float.NaN) + "," +
            F(smartWinch != null ? smartWinch.lengthErrorRateMS : float.NaN) + "," +
            F(smartWinch != null ? smartWinch.projectedRovSpeedMS : float.NaN) + "," +
            F(smartWinch != null ? smartWinch.winchSpeedCommandMS : float.NaN) + "," +

            Q(cableVisual != null ? cableVisual.cableMode : "missing") + "," +
            F(cableVisual != null ? cableVisual.startWorld.x : float.NaN) + "," +
            F(cableVisual != null ? cableVisual.startWorld.y : float.NaN) + "," +
            F(cableVisual != null ? cableVisual.startWorld.z : float.NaN) + "," +
            F(cableVisual != null ? cableVisual.endWorld.x : float.NaN) + "," +
            F(cableVisual != null ? cableVisual.endWorld.y : float.NaN) + "," +
            F(cableVisual != null ? cableVisual.endWorld.z : float.NaN) + "," +
            F(cableVisual != null ? cableVisual.slackM : float.NaN) + "," +
            F(cableVisual != null ? cableVisual.sagAmplitudeM : float.NaN) + "," +
            F(cableVisual != null ? cableVisual.sideCurveM : float.NaN) + "," +

            B(unifiedTether != null && unifiedTether.deploymentHomeRecorded) + "," +
            F(unifiedTether != null ? unifiedTether.distanceToDeploymentHomeM : float.NaN) + "," +
            B(unifiedTether != null && unifiedTether.droneMotionAllowed) + "," +
            B(unifiedTether != null && unifiedTether.safeToDeploy);

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

    private void CloseLog()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }
    }
}
