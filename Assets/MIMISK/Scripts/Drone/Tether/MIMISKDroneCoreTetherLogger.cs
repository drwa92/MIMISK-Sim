using System;
using System.Globalization;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKDroneCoreTetherLogger : MonoBehaviour
{
    public MIMISKDroneCoreTetherManager tether;
    public MIMISKDroneCoreMissionManager missionManager;
    public MIMISKDroneCoreFlightModeManager flightManager;
    public MIMISKDroneSurfaceBuoyancy surfaceBuoyancy;

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

        float period =
            1.0f / Mathf.Max(1.0f, logHz);

        if (timer >= period)
        {
            timer -= period;
            WriteLine();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
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

        if (surfaceBuoyancy == null)
        {
            surfaceBuoyancy = GetComponent<MIMISKDroneSurfaceBuoyancy>();
        }
    }

    private void OpenLog()
    {
        AutoFindReferences();

        string projectRoot =
            Directory.GetParent(Application.dataPath).FullName;

        string logDir =
            Path.Combine(projectRoot, "Logs", "MIMISKDrone");

        Directory.CreateDirectory(logDir);

        string fileName =
            "drone_core_tether_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss") +
            ".csv";

        currentLogPath =
            Path.Combine(logDir, fileName);

        writer =
            new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,tether_state,last_event,ready,safe_to_deploy,safe_to_recover," +
            "mission_state,flight_mode,buoyancy_active_points," +
            "deployed_length,target_length,winch_rate," +
            "start_x,start_y,start_z,end_x,end_y,end_z," +
            "straight_distance,slack,stretch,tension,raw_tension"
        );

        writer.Flush();
        linesWritten = 0;
    }

    private void WriteLine()
    {
        if (tether == null)
        {
            return;
        }

        string missionState =
            missionManager != null ? missionManager.missionState.ToString() : "none";

        string flightMode =
            flightManager != null ? flightManager.flightMode.ToString() : "none";

        int activeBuoyancy =
            surfaceBuoyancy != null ? surfaceBuoyancy.activePointCount : 0;

        Vector3 start = tether.tetherStartWorld;
        Vector3 end = tether.tetherEndWorld;

        string line = string.Join(",",
            F(Time.time),
            tether.tetherState.ToString(),
            Safe(tether.lastEvent),
            tether.readyForDeployment ? "1" : "0",
            tether.safeToDeploy ? "1" : "0",
            tether.safeToRecover ? "1" : "0",

            missionState,
            flightMode,
            activeBuoyancy.ToString(Culture),

            F(tether.deployedLengthM),
            F(tether.targetLengthM),
            F(tether.winchCommandRateMS),

            F(start.x), F(start.y), F(start.z),
            F(end.x), F(end.y), F(end.z),

            F(tether.straightDistanceM),
            F(tether.slackM),
            F(tether.stretchM),
            F(tether.tensionN),
            F(tether.rawTensionN)
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
