using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class MIMISKDroneAquaLocWaypointNavigatorLogger : MonoBehaviour
{
    public MIMISKDroneAquaLocWaypointNavigator navigator;
    public MIMISKDroneAquaLocEstimator aquaLoc;
    public MIMISKDroneAquaLocPositionHold aquaHold;

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
        if (navigator == null)
        {
            navigator = GetComponent<MIMISKDroneAquaLocWaypointNavigator>();
        }

        if (aquaLoc == null)
        {
            aquaLoc = GetComponent<MIMISKDroneAquaLocEstimator>();
        }

        if (aquaHold == null)
        {
            aquaHold = GetComponent<MIMISKDroneAquaLocPositionHold>();
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
        if (!enableLogging || navigator == null)
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

        string fileName = "drone_aqunav_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        currentLogPath = Path.Combine(logDir, fileName);

        writer = new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,mission_active,nav_state,segment_index,last_event," +
            "target_x,target_y,target_z," +
            "raw_target_x,raw_target_y,raw_target_z," +
            "cross_track_correction_x,cross_track_correction_y,cross_track_correction_z," +
            "segment_start_x,segment_start_y,segment_start_z," +
            "segment_end_x,segment_end_y,segment_end_z," +
            "closest_x,closest_y,closest_z," +
            "along_track_m,along_track_01,cross_track_error_m," +
            "dist_to_waypoint,predicted_dist_to_waypoint,horizontal_speed," +
            "final_stable_timer,final_loiter_timer,mission_timer," +
            "aqua_hold_state,aqua_hold_target_reached," +
            "est_x,est_y,est_z,est_vx,est_vy,est_vz,est_yaw," +
            "true_x,true_y,true_z,true_vx,true_vy,true_vz,true_yaw"
        );

        writer.Flush();

        Debug.Log("[MIMISKDroneAquaLocWaypointNavigatorLogger] Logging to: " + currentLogPath);
    }

    private void WriteLine()
    {
        Vector3 estP = Vector3.zero;
        Vector3 estV = Vector3.zero;
        float estYaw = 0.0f;

        Vector3 trueP = Vector3.zero;
        Vector3 trueV = Vector3.zero;
        float trueYaw = 0.0f;

        if (aquaLoc != null && aquaLoc.estimatorReady)
        {
            estP = aquaLoc.estimatedPositionWorld;
            estV = aquaLoc.estimatedVelocityWorld;
            estYaw = aquaLoc.estimatedYawDeg;

            trueP = aquaLoc.truePositionWorld;
            trueV = aquaLoc.trueVelocityWorld;
            trueYaw = aquaLoc.trueYawDeg;
        }

        string holdState = "none";
        string holdReached = "0";

        if (aquaHold != null)
        {
            holdState = aquaHold.holdState.ToString();
            holdReached = aquaHold.targetReached ? "1" : "0";
        }

        Vector3 target = navigator.currentTargetWorld;
        Vector3 rawTarget = navigator.rawCorridorTargetWorld;
        Vector3 correction = navigator.crossTrackCorrectionWorld;

        Vector3 segmentStart = navigator.currentSegmentStartWorld;
        Vector3 segmentEnd = navigator.currentSegmentEndWorld;
        Vector3 closest = navigator.closestPointOnSegmentWorld;

        string line = string.Join(",",
            F(Time.time),
            navigator.missionActive ? "1" : "0",
            navigator.navState.ToString(),
            navigator.currentSegmentIndex.ToString(Culture),
            SafeText(navigator.lastEvent),

            F(target.x), F(target.y), F(target.z),

            F(rawTarget.x), F(rawTarget.y), F(rawTarget.z),

            F(correction.x), F(correction.y), F(correction.z),

            F(segmentStart.x), F(segmentStart.y), F(segmentStart.z),

            F(segmentEnd.x), F(segmentEnd.y), F(segmentEnd.z),

            F(closest.x), F(closest.y), F(closest.z),

            F(navigator.alongTrackM),
            F(navigator.alongTrack01),
            F(navigator.crossTrackErrorM),

            F(navigator.distanceToCurrentWaypointM),
            F(navigator.predictedDistanceToWaypointM),
            F(navigator.horizontalSpeedMS),

            F(navigator.finalStableTimerS),
            F(navigator.finalLoiterTimerS),
            F(navigator.missionTimerS),

            holdState,
            holdReached,

            F(estP.x), F(estP.y), F(estP.z),
            F(estV.x), F(estV.y), F(estV.z),
            F(estYaw),

            F(trueP.x), F(trueP.y), F(trueP.z),
            F(trueV.x), F(trueV.y), F(trueV.z),
            F(trueYaw)
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

        Debug.Log("[MIMISKDroneAquaLocWaypointNavigatorLogger] Closed log: " + currentLogPath);
    }
}
