using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class MIMISKDroneAquaTrackPathFollowerLogger : MonoBehaviour
{
    public MIMISKDroneAquaTrackPathFollower tracker;
    public MIMISKDroneAquaPFObserver aquaPF;
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
            tracker = GetComponent<MIMISKDroneAquaTrackPathFollower>();
        }

        if (aquaPF == null)
        {
            aquaPF = GetComponent<MIMISKDroneAquaPFObserver>();
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

        string fileName = "drone_aquatrack_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        currentLogPath = Path.Combine(logDir, fileName);

        writer = new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,mission_active,track_state,segment_index,last_event," +
            "target_x,target_y,target_z," +
            "closest_x,closest_y,closest_z," +
            "segment_start_x,segment_start_y,segment_start_z," +
            "segment_end_x,segment_end_y,segment_end_z," +
            "along_track_m,along_track_01,cross_track_error_m,dist_to_end,horizontal_speed," +
            "pf_x,pf_y,pf_z,pf_vx,pf_vy,pf_vz,pf_yaw,pf_error," +
            "aqualoc_x,aqualoc_y,aqualoc_z,aqualoc_yaw," +
            "true_x,true_y,true_z,true_vx,true_vy,true_vz,true_yaw," +
            "des_vx,des_vy,des_vz,cmd_fwd,cmd_right,cmd_yaw,cmd_alt"
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

        if (aquaPF != null && aquaPF.observerReady)
        {
            pfP = aquaPF.pfPositionWorld;
            pfV = aquaPF.pfVelocityWorld;
            pfYaw = aquaPF.pfYawDeg;
            pfError = aquaPF.positionErrorM;
        }

        Vector3 target = tracker.currentTargetWorld;
        Vector3 closest = tracker.closestPointWorld;
        Vector3 a = tracker.segmentStartWorld;
        Vector3 b = tracker.segmentEndWorld;
        Vector3 des = tracker.desiredVelocityWorld;
        Vector4 cmd = tracker.filteredCommandForwardRightYawAlt;

        string line = string.Join(",",
            F(Time.time),
            tracker.missionActive ? "1" : "0",
            tracker.trackState.ToString(),
            tracker.currentSegmentIndex.ToString(Culture),
            SafeText(tracker.lastEvent),

            F(target.x), F(target.y), F(target.z),
            F(closest.x), F(closest.y), F(closest.z),
            F(a.x), F(a.y), F(a.z),
            F(b.x), F(b.y), F(b.z),

            F(tracker.alongTrackM),
            F(tracker.alongTrack01),
            F(tracker.crossTrackErrorM),
            F(tracker.distanceToSegmentEndM),
            F(tracker.horizontalSpeedMS),

            F(pfP.x), F(pfP.y), F(pfP.z),
            F(pfV.x), F(pfV.y), F(pfV.z),
            F(pfYaw),
            F(pfError),

            F(locP.x), F(locP.y), F(locP.z),
            F(locYaw),

            F(trueP.x), F(trueP.y), F(trueP.z),
            F(trueV.x), F(trueV.y), F(trueV.z),
            F(trueYaw),

            F(des.x), F(des.y), F(des.z),
            F(cmd.x), F(cmd.y), F(cmd.z), F(cmd.w)
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
