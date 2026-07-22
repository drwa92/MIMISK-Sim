using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class MIMISKDroneRtkIKDynamicPathTrackerLogger : MonoBehaviour
{
    public MIMISKDroneRtkIKDynamicPathTracker tracker;
    public MIMISKDroneAquaLocEstimator aquaLoc;

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
        if (tracker == null)
        {
            tracker = GetComponent<MIMISKDroneRtkIKDynamicPathTracker>();
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
        if (!enableLogging || tracker == null)
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

        string fileName =
            "drone_rtk_ikdyntrack_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";

        currentLogPath = Path.Combine(logDir, fileName);

        writer = new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,mission_active,track_state,segment_index,segment_u,last_event," +
            "trajectory_progress,closest_progress,progress_speed_scale," +
            "ref_x,ref_y,ref_z,ref_vx,ref_vy,ref_vz," +
            "est_x,est_y,est_z,est_vx,est_vy,est_vz,est_yaw," +
            "true_x,true_y,true_z,true_vx,true_vy,true_vz,true_yaw," +
            "err_x,err_y,err_z,tracking_error,dist_to_final,horizontal_speed," +
            "des_vx,des_vy,des_vz,cmd_fwd,cmd_right,cmd_yaw,cmd_alt," +
            "forward_gain,forward_damping,forward_bias," +
            "right_gain,right_damping,right_bias," +
            "forward_axis_velocity,right_axis_velocity,forward_axis_accel,right_axis_accel"
        );

        writer.Flush();
    }

    private void WriteLine()
    {
        Vector3 trueP = Vector3.zero;
        Vector3 trueV = Vector3.zero;
        float trueYaw = 0.0f;

        if (aquaLoc != null && aquaLoc.estimatorReady)
        {
            trueP = aquaLoc.truePositionWorld;
            trueV = aquaLoc.trueVelocityWorld;
            trueYaw = aquaLoc.trueYawDeg;
        }

        Vector3 refP = tracker.referencePositionWorld;
        Vector3 refV = tracker.referenceVelocityWorld;
        Vector3 estP = tracker.estimatedPositionWorld;
        Vector3 estV = tracker.estimatedVelocityWorld;
        Vector3 err = tracker.positionErrorWorld;
        Vector3 desV = tracker.desiredVelocityWorld;
        Vector4 cmd = tracker.filteredCommandForwardRightYawAlt;

        string line = string.Join(",",
            F(Time.time),
            tracker.missionActive ? "1" : "0",
            tracker.trackState.ToString(),
            tracker.currentSegmentIndex.ToString(Culture),
            F(tracker.currentSegmentU),
            SafeText(tracker.lastEvent),

            F(tracker.trajectoryProgress),
            F(tracker.closestProgress),
            F(tracker.progressSpeedScale),

            F(refP.x), F(refP.y), F(refP.z),
            F(refV.x), F(refV.y), F(refV.z),

            F(estP.x), F(estP.y), F(estP.z),
            F(estV.x), F(estV.y), F(estV.z),
            F(tracker.estimatedYawDeg),

            F(trueP.x), F(trueP.y), F(trueP.z),
            F(trueV.x), F(trueV.y), F(trueV.z),
            F(trueYaw),

            F(err.x), F(err.y), F(err.z),
            F(tracker.trackingErrorM),
            F(tracker.distanceToFinalM),
            F(tracker.horizontalSpeedMS),

            F(desV.x), F(desV.y), F(desV.z),
            F(cmd.x), F(cmd.y), F(cmd.z), F(cmd.w),

            F(tracker.forwardAxis.commandGain),
            F(tracker.forwardAxis.damping),
            F(tracker.forwardAxis.bias),

            F(tracker.rightAxis.commandGain),
            F(tracker.rightAxis.damping),
            F(tracker.rightAxis.bias),

            F(tracker.forwardAxisVelocity),
            F(tracker.rightAxisVelocity),
            F(tracker.forwardAxisAccel),
            F(tracker.rightAxisAccel)
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

    private string SafeText(string value)
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
}
