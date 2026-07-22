using System;
using System.Globalization;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKMiniROVRealisticDeploymentLogger : MonoBehaviour
{
    public MIMISKMiniROVRealisticDeploymentManager deployment;
    public MIMISKDroneCoreTetherManager tether;
    public MIMISKDroneCoreMissionManager missionManager;
    public MIMISKDroneCoreFlightModeManager flightManager;

    public bool enableLogging = true;
    public float logHz = 50.0f;
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

        float period = 1.0f / Mathf.Max(1.0f, logHz);

        if (timer >= period)
        {
            timer -= period;
            WriteLine();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (deployment == null)
        {
            deployment = GetComponent<MIMISKMiniROVRealisticDeploymentManager>();
        }

        if (tether == null)
        {
            tether = GetComponent<MIMISKDroneCoreTetherManager>();
        }

        if (missionManager == null)
        {
            missionManager = GetComponent<MIMISKDroneCoreMissionManager>();
        }

        if (flightManager == null)
        {
            flightManager = GetComponent<MIMISKDroneCoreFlightModeManager>();
        }
    }

    private void OpenLog()
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath).FullName;

        string logDir =
            Path.Combine(projectRoot, "Logs", "MIMISKDrone");

        Directory.CreateDirectory(logDir);

        string fileName =
            "drone_minirov_realistic_deployment_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss") +
            ".csv";

        currentLogPath =
            Path.Combine(logDir, fileName);

        writer = new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,deployment_state,last_event,mission_state,flight_mode," +
            "safe_to_deploy,safe_to_recover,water_contact,rov_dynamic,rov_control_active," +
            "rov_x,rov_y,rov_z,rov_vx,rov_vy,rov_vz,rov_depth," +
            "cable_x,cable_y,cable_z,anchor_to_cable_distance," +
            "tether_state,deployed_length,target_length,winch_rate," +
            "required_length,adaptive_target_length,tension,slack,stretch," +
            "deployment_home_recorded,deployment_x,deployment_y,deployment_z,deployment_depth,deployment_yaw," +
            "tether_est_range,tether_est_bearing,tether_est_error"
        );

        writer.Flush();
    }

    private void WriteLine()
    {
        if (deployment == null)
        {
            return;
        }

        string missionState =
            missionManager != null ? missionManager.missionState.ToString() : "none";

        string flightMode =
            flightManager != null ? flightManager.flightMode.ToString() : "none";

        string tetherState =
            tether != null ? tether.tetherState.ToString() : "none";

        Vector3 rovP = Vector3.zero;
        Vector3 rovV = Vector3.zero;

        if (deployment.miniRovRigidbody != null)
        {
            rovP = deployment.miniRovRigidbody.position;
            rovV = deployment.miniRovRigidbody.linearVelocity;
        }
        else if (deployment.miniRovRoot != null)
        {
            rovP = deployment.miniRovRoot.position;
        }

        Vector3 cableP =
            deployment.yellowCableEndPoint != null
                ? deployment.yellowCableEndPoint.position
                : Vector3.zero;

        string line = string.Join(",",
            F(Time.time),
            deployment.deploymentState.ToString(),
            Safe(deployment.lastEvent),
            missionState,
            flightMode,

            deployment.safeToDeploy ? "1" : "0",
            deployment.safeToRecover ? "1" : "0",
            deployment.waterContactDetected ? "1" : "0",
            deployment.rovDynamic ? "1" : "0",
            deployment.rovControlActive ? "1" : "0",

            F(rovP.x), F(rovP.y), F(rovP.z),
            F(rovV.x), F(rovV.y), F(rovV.z),
            F(deployment.rovDepthBelowSurfaceM),

            F(cableP.x), F(cableP.y), F(cableP.z),
            F(deployment.distanceRovAnchorToCableEndM),

            tetherState,
            tether != null ? F(tether.deployedLengthM) : "0",
            tether != null ? F(tether.targetLengthM) : "0",
            tether != null ? F(tether.winchCommandRateMS) : "0",

            F(deployment.currentRequiredCableLengthM),
            F(deployment.adaptiveTargetLengthM),

            tether != null ? F(tether.tensionN) : "0",
            tether != null ? F(tether.slackM) : "0",
            tether != null ? F(tether.stretchM) : "0"
        );

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

    private string Safe(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "none";
        }

        return value.Replace(",", "_");
    }

    private void CloseLog()
    {
        if (writer == null)
        {
            return;
        }

        writer.Flush();
        writer.Close();
        writer.Dispose();
        writer = null;
    }

    private void OnDisable()
    {
        CloseLog();
    }

    private void OnApplicationQuit()
    {
        CloseLog();
    }
}
