using System;
using System.Globalization;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKMiniROVWaterReleaseLogger : MonoBehaviour
{
    public MIMISKMiniROVWaterReleaseController waterRelease;
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
        if (waterRelease == null)
        {
            waterRelease = GetComponent<MIMISKMiniROVWaterReleaseController>();
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
            "drone_minirov_water_release_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss") +
            ".csv";

        currentLogPath =
            Path.Combine(logDir, fileName);

        writer = new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,release_state,last_event,mission_state,flight_mode," +
            "water_contact,dynamic,control_enabled,cable_attached," +
            "rov_x,rov_y,rov_z,rov_vx,rov_vy,rov_vz," +
            "cable_x,cable_y,cable_z,distance_to_cable," +
            "tether_state,deployed_length,target_length,winch_rate,tension,slack,stretch"
        );

        writer.Flush();
    }

    private void WriteLine()
    {
        if (waterRelease == null)
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

        if (waterRelease.miniRovRigidbody != null)
        {
            rovP = waterRelease.miniRovRigidbody.position;
            rovV = waterRelease.miniRovRigidbody.linearVelocity;
        }
        else if (waterRelease.miniRovRoot != null)
        {
            rovP = waterRelease.miniRovRoot.position;
        }

        Vector3 cableP =
            waterRelease.yellowCableEndPoint != null
                ? waterRelease.yellowCableEndPoint.position
                : Vector3.zero;

        string line = string.Join(",",
            F(Time.time),
            waterRelease.releaseState.ToString(),
            Safe(waterRelease.lastEvent),
            missionState,
            flightMode,

            waterRelease.waterContactDetected ? "1" : "0",
            waterRelease.miniRovIsDynamic ? "1" : "0",
            waterRelease.miniRovControlEnabled ? "1" : "0",
            waterRelease.cableVisuallyAttachedToRov ? "1" : "0",

            F(rovP.x), F(rovP.y), F(rovP.z),
            F(rovV.x), F(rovV.y), F(rovV.z),

            F(cableP.x), F(cableP.y), F(cableP.z),
            F(waterRelease.distanceToCableEndM),

            tetherState,
            tether != null ? F(tether.deployedLengthM) : "0",
            tether != null ? F(tether.targetLengthM) : "0",
            tether != null ? F(tether.winchCommandRateMS) : "0",
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
