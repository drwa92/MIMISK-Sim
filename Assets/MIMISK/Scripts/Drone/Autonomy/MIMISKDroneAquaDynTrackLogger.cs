using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class MIMISKDroneAquaDynTrackLogger : MonoBehaviour
{
    public MIMISKDroneAquaDynTrackController tracker;
    public MIMISKDroneAquaPFCommandObserver pfObserver;
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
            tracker = GetComponent<MIMISKDroneAquaDynTrackController>();
        }

        if (pfObserver == null)
        {
            pfObserver = GetComponent<MIMISKDroneAquaPFCommandObserver>();
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

        string fileName = "drone_aquadyntrack_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        currentLogPath = Path.Combine(logDir, fileName);

        writer = new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,mission_active,track_state,segment_index,segment_u,last_event," +
            "trajectory_progress,closest_progress,desired_progress,progress_speed_scale," +
            "desired_x,desired_y,desired_z,ref_vx,ref_vy,ref_vz,des_vx,des_vy,des_vz," +
            "pf_x,pf_y,pf_z,pf_vx,pf_vy,pf_vz,pf_yaw,pf_error," +
            "aqualoc_x,aqualoc_y,aqualoc_z,aqualoc_yaw," +
            "true_x,true_y,true_z,true_vx,true_vy,true_vz,true_yaw," +
            "pos_err_x,pos_err_y,pos_err_z,tracking_error,dist_to_final,horizontal_speed," +
            "cmd_fwd,cmd_right,cmd_yaw,cmd_alt," +
            "forward_a,forward_b,forward_d,right_a,right_b,right_d," +
            "forward_axis_v,right_axis_v,forward_axis_acc,right_axis_acc"
        );

        writer.Flush();
    }

    private void WriteLine()
    {
        Vector3 trueP = Vector3.zero;
        Vector3 trueV = Vector3.zero;
        float trueYaw = 0.0f;

        Vector3 locP = Vector3.zero;
        float locYaw = 0.0f;

        if (aquaLoc != null && aquaLoc.estimatorReady)
        {
            trueP = aquaLoc.truePositionWorld;
            trueV = aquaLoc.trueVelocityWorld;
            trueYaw = aquaLoc.trueYawDeg;

            locP = aquaLoc.estimatedPositionWorld;
            locYaw = aquaLoc.estimatedYawDeg;
        }

        Vector3 pfP = Vector3.zero;
        Vector3 pfV = Vector3.zero;
        float pfYaw = 0.0f;
        float pfError = 0.0f;

        if (pfObserver != null && pfObserver.observerReady)
        {
            pfP = pfObserver.pfPositionWorld;
            pfV = pfObserver.pfVelocityWorld;
            pfYaw = pfObserver.pfYawDeg;
            pfError = pfObserver.positionErrorM;
        }

        Vector3 desiredP = tracker.desiredPositionWorld;
        Vector3 refV = tracker.referenceVelocityWorld;
        Vector3 desV = tracker.desiredVelocityWorld;
        Vector3 posErr = tracker.positionErrorWorld;
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
            F(tracker.desiredProgress),
            F(tracker.progressSpeedScale),

            F(desiredP.x), F(desiredP.y), F(desiredP.z),
            F(refV.x), F(refV.y), F(refV.z),
            F(desV.x), F(desV.y), F(desV.z),

            F(pfP.x), F(pfP.y), F(pfP.z),
            F(pfV.x), F(pfV.y), F(pfV.z),
            F(pfYaw),
            F(pfError),

            F(locP.x), F(locP.y), F(locP.z),
            F(locYaw),

            F(trueP.x), F(trueP.y), F(trueP.z),
            F(trueV.x), F(trueV.y), F(trueV.z),
            F(trueYaw),

            F(posErr.x), F(posErr.y), F(posErr.z),
            F(tracker.trackingErrorM),
            F(tracker.distanceToFinalM),
            F(tracker.horizontalSpeedMS),

            F(cmd.x), F(cmd.y), F(cmd.z), F(cmd.w),

            F(tracker.forwardAxis.a),
            F(tracker.forwardAxis.b),
            F(tracker.forwardAxis.d),
            F(tracker.rightAxis.a),
            F(tracker.rightAxis.b),
            F(tracker.rightAxis.d),

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
