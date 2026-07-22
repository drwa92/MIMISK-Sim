using System;
using System.Globalization;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKMiniROVDeploymentLogger : MonoBehaviour
{
    public MIMISKMiniROVDeploymentManager deployment;
    public MIMISKDroneCoreTetherManager tether;
    public MIMISKDroneCoreMissionManager missionManager;

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
        if (deployment == null)
        {
            deployment = GetComponent<MIMISKMiniROVDeploymentManager>();
        }

        if (tether == null)
        {
            tether = GetComponent<MIMISKDroneCoreTetherManager>();
        }

        if (missionManager == null)
        {
            missionManager = GetComponent<MIMISKDroneCoreMissionManager>();
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
            "drone_minirov_deployment_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss") +
            ".csv";

        currentLogPath =
            Path.Combine(logDir, fileName);

        writer =
            new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,deployment_state,last_event,mission_state," +
            "safe_to_deploy,safe_to_recover,released,docked," +
            "rov_x,rov_y,rov_z,rov_vx,rov_vy,rov_vz,rov_speed," +
            "distance_to_carry_slot," +
            "tether_state,deployed_length,target_length,tension,slack,stretch"
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
            missionManager != null
                ? missionManager.missionState.ToString()
                : "none";

        string tetherState =
            tether != null
                ? tether.tetherState.ToString()
                : "none";

        Vector3 p = Vector3.zero;
        Vector3 v = Vector3.zero;

        if (deployment.miniRovRigidbody != null)
        {
            p = deployment.miniRovRigidbody.position;
            v = deployment.miniRovRigidbody.linearVelocity;
        }
        else if (deployment.miniRovRoot != null)
        {
            p = deployment.miniRovRoot.position;
        }

        string line = string.Join(",",
            F(Time.time),
            deployment.deploymentState.ToString(),
            Safe(deployment.lastEvent),
            missionState,

            deployment.safeToDeploy ? "1" : "0",
            deployment.safeToRecover ? "1" : "0",
            deployment.miniRovReleased ? "1" : "0",
            deployment.miniRovDocked ? "1" : "0",

            F(p.x), F(p.y), F(p.z),
            F(v.x), F(v.y), F(v.z),
            F(deployment.miniRovSpeedMS),

            F(deployment.distanceToCarrySlotM),

            tetherState,
            tether != null ? F(tether.deployedLengthM) : "0",
            tether != null ? F(tether.targetLengthM) : "0",
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
