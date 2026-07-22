using System;
using System.Globalization;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKMiniROVCableEndAttachmentLogger : MonoBehaviour
{
    public MIMISKMiniROVCableEndAttachmentManager attachment;
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
        if (attachment == null)
        {
            attachment = GetComponent<MIMISKMiniROVCableEndAttachmentManager>();
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
            "drone_minirov_cable_attachment_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss") +
            ".csv";

        currentLogPath =
            Path.Combine(logDir, fileName);

        writer =
            new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,attachment_state,last_event,mission_state,flight_mode," +
            "safe_to_deploy,safe_to_recover,attached_to_cable," +
            "rov_x,rov_y,rov_z,cable_x,cable_y,cable_z,distance_to_cable," +
            "tether_state,deployed_length,target_length,winch_rate,tension,slack,stretch"
        );

        writer.Flush();
    }

    private void WriteLine()
    {
        if (attachment == null)
        {
            return;
        }

        string missionState =
            missionManager != null ? missionManager.missionState.ToString() : "none";

        string flightMode =
            flightManager != null ? flightManager.flightMode.ToString() : "none";

        string tetherState =
            tether != null ? tether.tetherState.ToString() : "none";

        Vector3 rovP =
            attachment.miniRovRoot != null ? attachment.miniRovRoot.position : Vector3.zero;

        Vector3 cableP =
            attachment.yellowCableEndPoint != null ? attachment.yellowCableEndPoint.position : Vector3.zero;

        string line = string.Join(",",
            F(Time.time),
            attachment.cableMiniRovState.ToString(),
            Safe(attachment.lastEvent),
            missionState,
            flightMode,

            attachment.safeToDeploy ? "1" : "0",
            attachment.safeToRecover ? "1" : "0",
            attachment.miniRovAttachedToCableEnd ? "1" : "0",

            F(rovP.x), F(rovP.y), F(rovP.z),
            F(cableP.x), F(cableP.y), F(cableP.z),
            F(attachment.distanceToCableEndM),

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
