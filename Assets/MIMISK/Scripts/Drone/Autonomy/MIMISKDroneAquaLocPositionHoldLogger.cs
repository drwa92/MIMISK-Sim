using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class MIMISKDroneAquaLocPositionHoldLogger : MonoBehaviour
{
    public MIMISKDroneAquaLocPositionHold hold;
    public MIMISKDroneAquaLocEstimator aquaLoc;

    [Header("Logging")]
    public bool enableLogging = true;
    public float logHz = 50.0f;
    public bool flushEveryLine = false;

    [Header("Runtime")]
    public string currentLogPath;
    public int linesWritten;

    private StreamWriter writer;
    private float timer;
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private void Awake()
    {
        if (hold == null)
        {
            hold = GetComponent<MIMISKDroneAquaLocPositionHold>();
        }

        if (aquaLoc == null)
        {
            aquaLoc = GetComponent<MIMISKDroneAquaLocEstimator>();
        }
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
        if (!enableLogging || hold == null)
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

    private void OnDisable()
    {
        CloseLog();
    }

    private void OnApplicationQuit()
    {
        CloseLog();
    }

    private void OpenLog()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string logDir = Path.Combine(projectRoot, "Logs", "MIMISKDrone");

        Directory.CreateDirectory(logDir);

        string fileName = "drone_aquahold_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        currentLogPath = Path.Combine(logDir, fileName);

        writer = new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,guidance_enabled,guidance_mode,hold_state,target_reached," +
            "target_x,target_y,target_z,target_yaw," +
            "active_target_x,active_target_y,active_target_z," +
            "est_x,est_y,est_z,est_vx,est_vy,est_vz,est_yaw," +
            "true_x,true_y,true_z,true_vx,true_vy,true_vz,true_yaw," +
            "err_x,err_y,err_z,dist_to_target,final_dist_to_target,horizontal_speed," +
            "desired_vx,desired_vy,desired_vz," +
            "disturbance_vx,disturbance_vy,disturbance_vz," +
            "cmd_fwd,cmd_right,cmd_yaw,cmd_alt,cmd_saturated," +
            "physical_drift_from_activation,estimated_drift_from_activation," +
            "time_since_activation,stable_hold_timer,capture_blend"
        );

        writer.Flush();

        Debug.Log("[MIMISKDroneAquaLocPositionHoldLogger] Logging to: " + currentLogPath);
    }

    private void WriteLine()
    {
        Vector3 target = hold.targetPositionWorld;
        Vector3 activeTarget = hold.activeControlTargetWorld;

        Vector3 estP = hold.estimatedPositionWorld;
        Vector3 estV = hold.estimatedVelocityWorld;
        float estYaw = hold.estimatedYawDeg;

        Vector3 trueP = Vector3.zero;
        Vector3 trueV = Vector3.zero;
        float trueYaw = 0.0f;

        if (aquaLoc != null && aquaLoc.estimatorReady)
        {
            trueP = aquaLoc.truePositionWorld;
            trueV = aquaLoc.trueVelocityWorld;
            trueYaw = aquaLoc.trueYawDeg;
        }

        Vector3 err = target - estP;
        Vector3 desV = hold.desiredVelocityWorld;
        Vector3 dist = hold.disturbanceVelocityEstimateWorld;
        Vector4 cmd = hold.filteredCommandForwardRightYawAlt;

        string line = string.Join(",",
            F(Time.time),
            hold.enableGuidance ? "1" : "0",
            hold.guidanceMode.ToString(),
            hold.holdState.ToString(),
            hold.targetReached ? "1" : "0",

            F(target.x), F(target.y), F(target.z), F(hold.targetYawDeg),

            F(activeTarget.x), F(activeTarget.y), F(activeTarget.z),

            F(estP.x), F(estP.y), F(estP.z),
            F(estV.x), F(estV.y), F(estV.z),
            F(estYaw),

            F(trueP.x), F(trueP.y), F(trueP.z),
            F(trueV.x), F(trueV.y), F(trueV.z),
            F(trueYaw),

            F(err.x), F(err.y), F(err.z),
            F(hold.distanceToTargetM),
            F(hold.finalTargetDistanceM),
            F(hold.horizontalSpeedMS),

            F(desV.x), F(desV.y), F(desV.z),

            F(dist.x), F(dist.y), F(dist.z),

            F(cmd.x), F(cmd.y), F(cmd.z), F(cmd.w),
            hold.commandSaturated ? "1" : "0",

            F(hold.physicalDriftFromActivationM),
            F(hold.estimatedDriftFromActivationM),

            F(hold.timeSinceActivationS),
            F(hold.stableHoldTimerS),
            F(hold.captureBlend01)
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

        Debug.Log("[MIMISKDroneAquaLocPositionHoldLogger] Closed log: " + currentLogPath);
    }
}
