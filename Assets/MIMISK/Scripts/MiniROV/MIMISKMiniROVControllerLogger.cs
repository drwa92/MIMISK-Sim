using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKMiniROVControllerLogger : MonoBehaviour
{
    [Header("References")]
    public MIMISKMiniROVPlantBasedController controller;
    public ControlManager controlManager;
    public Rigidbody rb;

    [Header("Logging")]
    public bool autoStartOnPlay = false;
    public bool logOnlyWhenControllerEnabled = true;
    public float logRateHz = 50.0f;
    public string testName = "minirov_controller_test";
    public string logSubfolder = "Logs/MiniROV";

    [Header("Runtime")]
    public bool logging;
    public string currentLogPath = "";
    public string currentSummaryPath = "";
    public int sampleCount;
    public string lastEvent = "idle";

    private StreamWriter writer;
    private float nextLogTime;
    private float startTime;

    private double sumSurgeSq;
    private double sumSurgeAbs;
    private double maxSurgeAbs;
    private double finalSurgeError;

    private double sumYawSq;
    private double sumYawAbs;
    private double maxYawAbs;
    private double finalYawError;

    private double sumDepthSq;
    private double sumDepthAbs;
    private double maxDepthAbs;
    private double finalDepthError;

    private int pathSampleCount;
    private double sumTrackingSq;
    private double sumTrackingAbs;
    private double maxTrackingAbs;
    private double finalTrackingError;

    private double sumCrossTrackSq;
    private double sumCrossTrackAbs;
    private double maxCrossTrackAbs;

    private int thrusterSaturationCount;
    private int ballastSaturationCount;

    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        if (autoStartOnPlay)
        {
            StartNewLog();
        }
    }

    private void FixedUpdate()
    {
        if (!logging)
        {
            return;
        }

        if (controller == null)
        {
            lastEvent = "missing_controller";
            return;
        }

        if (logOnlyWhenControllerEnabled && !controller.controllerEnabled)
        {
            return;
        }

        if (Time.time < nextLogTime)
        {
            return;
        }

        float period =
            1.0f / Mathf.Max(1.0f, logRateHz);

        nextLogTime =
            Time.time + period;

        WriteSample();
    }

    private void OnDisable()
    {
        if (logging)
        {
            StopAndWriteSummary();
        }
    }

    private void OnDestroy()
    {
        if (logging)
        {
            StopAndWriteSummary();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (controller == null)
        {
            controller =
                GetComponent<MIMISKMiniROVPlantBasedController>();
        }

        if (controlManager == null)
        {
            controlManager =
                GetComponent<ControlManager>();
        }

        if (rb == null)
        {
            rb =
                GetComponent<Rigidbody>();
        }
    }

    [ContextMenu("Start New Log")]
    public void StartNewLog()
    {
        AutoFindReferences();

        StopWriterOnly();

        ResetMetrics();

        string root =
            Directory.GetParent(Application.dataPath).FullName;

        string dir =
            Path.Combine(root, logSubfolder);

        Directory.CreateDirectory(dir);

        string stamp =
            DateTime.Now.ToString("yyyyMMdd_HHmmss");

        string safeTest =
            MakeSafeFileName(testName);

        currentLogPath =
            Path.Combine(
                dir,
                "minirov_" + safeTest + "_" + stamp + ".csv"
            );

        currentSummaryPath =
            Path.Combine(
                dir,
                "minirov_" + safeTest + "_" + stamp + "_summary.md"
            );

        writer =
            new StreamWriter(currentLogPath, false, Encoding.UTF8);

        WriteHeader();

        startTime =
            Time.time;

        nextLogTime =
            Time.time;

        logging =
            true;

        lastEvent =
            "logging_started_" + currentLogPath;

        Debug.Log("[MIMISK] MiniROV controller logger started: " + currentLogPath);
    }

    [ContextMenu("Stop And Write Summary")]
    public void StopAndWriteSummary()
    {
        if (!logging && writer == null)
        {
            return;
        }

        logging =
            false;

        StopWriterOnly();
        WriteSummary();

        lastEvent =
            "logging_stopped_summary_written";

        Debug.Log("[MIMISK] MiniROV controller logger stopped. Summary: " + currentSummaryPath);
    }

    private void StopWriterOnly()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer.Dispose();
            writer = null;
        }
    }

    private void WriteHeader()
    {
        writer.WriteLine(
            "time_s,dt_s,test_name,mode," +
            "world_x,world_y,world_z,depth_m,yaw_deg," +
            "surge_speed_m_s,depth_rate_m_s,yaw_rate_rad_s,yaw_rate_deg_s," +
            "target_surge_speed_m_s,target_yaw_deg,target_depth_m," +
            "target_point_x,target_point_y,target_point_z,target_point_depth_m," +
            "surge_error_m_s,yaw_error_deg,depth_error_m,distance_to_target_m," +
            "los_cross_track_error_m,los_tracking_error_m,path_segment_index,path_complete," +
            "surge_cmd,yaw_cmd,ballast_cmd," +
            "left_pwm,right_pwm,dc_port_pwm,dc_starboard_pwm," +
            "cm_left_force,cm_right_force,cm_propulsion_force,cm_yaw_differential_force," +
            "cm_left_command,cm_right_command,cm_left_raw,cm_right_raw"
        );
    }

    private void WriteSample()
    {
        if (writer == null || controller == null)
        {
            return;
        }

        float t =
            Time.time - startTime;

        float dt =
            Time.fixedDeltaTime;

        string mode =
            controller.controlMode.ToString();

        Vector3 pos =
            rb != null ? rb.position : transform.position;

        float yawRateDeg =
            controller.yawRateRadS * Mathf.Rad2Deg;

        float cmLeftForce =
            ReadFirstFloat(controlManager, "leftThrusterForce", "leftForce", "portThrusterForce");

        float cmRightForce =
            ReadFirstFloat(controlManager, "rightThrusterForce", "rightForce", "starboardThrusterForce");

        float cmPropForce =
            ReadFirstFloat(controlManager, "propulsionForce", "surgeForce", "forwardForce");

        float cmYawDiff =
            ReadFirstFloat(controlManager, "yawDifferentialForce", "yawForce", "differentialForce");

        float cmLeftCmd =
            ReadFirstFloat(controlManager, "leftThrusterCommand", "leftCommand", "leftCmd");

        float cmRightCmd =
            ReadFirstFloat(controlManager, "rightThrusterCommand", "rightCommand", "rightCmd");

        float cmLeftRaw =
            ReadFirstFloat(controlManager, "leftThrusterRaw", "leftRaw", "lastLeftThruster");

        float cmRightRaw =
            ReadFirstFloat(controlManager, "rightThrusterRaw", "rightRaw", "lastRightThruster");

        StringBuilder sb =
            new StringBuilder(1024);

        Append(sb, t);
        Append(sb, dt);
        AppendText(sb, testName);
        AppendText(sb, mode);

        Append(sb, pos.x);
        Append(sb, pos.y);
        Append(sb, pos.z);
        Append(sb, controller.depthM);
        Append(sb, controller.yawDeg);

        Append(sb, controller.surgeSpeedMS);
        Append(sb, controller.depthRateMS);
        Append(sb, controller.yawRateRadS);
        Append(sb, yawRateDeg);

        Append(sb, controller.targetSurgeSpeedMS);
        Append(sb, controller.targetYawDeg);
        Append(sb, controller.targetDepthM);

        Append(sb, controller.targetPointWorld.x);
        Append(sb, controller.targetPointWorld.y);
        Append(sb, controller.targetPointWorld.z);
        Append(sb, controller.targetPointDepthM);

        Append(sb, controller.surgeErrorMS);
        Append(sb, controller.yawErrorDeg);
        Append(sb, controller.depthErrorM);
        Append(sb, controller.distanceToTargetM);

        Append(sb, controller.losCrossTrackErrorM);
        Append(sb, controller.losTrackingErrorM);
        Append(sb, controller.pathSegmentIndex);
        Append(sb, controller.pathComplete ? 1 : 0);

        Append(sb, controller.surgeCmd);
        Append(sb, controller.yawCmd);
        Append(sb, controller.ballastCmd);

        Append(sb, controller.leftPwm);
        Append(sb, controller.rightPwm);
        Append(sb, controller.dcPortPwm);
        Append(sb, controller.dcStarboardPwm);

        Append(sb, cmLeftForce);
        Append(sb, cmRightForce);
        Append(sb, cmPropForce);
        Append(sb, cmYawDiff);

        Append(sb, cmLeftCmd);
        Append(sb, cmRightCmd);
        Append(sb, cmLeftRaw);
        AppendLast(sb, cmRightRaw);

        writer.WriteLine(sb.ToString());

        UpdateMetrics();

        if (sampleCount % 50 == 0)
        {
            writer.Flush();
        }
    }

    private void UpdateMetrics()
    {
        sampleCount++;

        double surge =
            controller.surgeErrorMS;

        double yaw =
            controller.yawErrorDeg;

        double depth =
            controller.depthErrorM;

        finalSurgeError =
            surge;

        finalYawError =
            yaw;

        finalDepthError =
            depth;

        sumSurgeSq += surge * surge;
        sumSurgeAbs += Math.Abs(surge);
        maxSurgeAbs =
            Math.Max(maxSurgeAbs, Math.Abs(surge));

        sumYawSq += yaw * yaw;
        sumYawAbs += Math.Abs(yaw);
        maxYawAbs =
            Math.Max(maxYawAbs, Math.Abs(yaw));

        sumDepthSq += depth * depth;
        sumDepthAbs += Math.Abs(depth);
        maxDepthAbs =
            Math.Max(maxDepthAbs, Math.Abs(depth));

        bool pathMode =
            controller.controlMode ==
                MIMISKMiniROVPlantBasedController.ControlMode.PolylineLOS ||
            controller.controlMode ==
                MIMISKMiniROVPlantBasedController.ControlMode.CircleLOS;

        if (pathMode)
        {
            double tracking =
                controller.losTrackingErrorM;

            double cross =
                controller.losCrossTrackErrorM;

            pathSampleCount++;

            sumTrackingSq += tracking * tracking;
            sumTrackingAbs += Math.Abs(tracking);
            maxTrackingAbs =
                Math.Max(maxTrackingAbs, Math.Abs(tracking));

            finalTrackingError =
                tracking;

            sumCrossTrackSq += cross * cross;
            sumCrossTrackAbs += Math.Abs(cross);
            maxCrossTrackAbs =
                Math.Max(maxCrossTrackAbs, Math.Abs(cross));
        }

        if (Math.Abs(controller.leftPwm) >= 250 ||
            Math.Abs(controller.rightPwm) >= 250)
        {
            thrusterSaturationCount++;
        }

        if (Math.Abs(controller.dcPortPwm) >= 250 ||
            Math.Abs(controller.dcStarboardPwm) >= 250)
        {
            ballastSaturationCount++;
        }
    }

    private void WriteSummary()
    {
        if (string.IsNullOrEmpty(currentSummaryPath))
        {
            return;
        }

        double n =
            Math.Max(1, sampleCount);

        double pathN =
            Math.Max(1, pathSampleCount);

        StringBuilder sb =
            new StringBuilder();

        sb.AppendLine("# MiniROV Unity Controller Log Summary");
        sb.AppendLine();
        sb.AppendLine("## File");
        sb.AppendLine();
        sb.AppendLine("- CSV: `" + currentLogPath + "`");
        sb.AppendLine("- samples: `" + sampleCount + "`");
        sb.AppendLine("- path samples: `" + pathSampleCount + "`");
        sb.AppendLine();

        sb.AppendLine("## Axis Errors");
        sb.AppendLine();
        sb.AppendLine("- RMS surge error: `" + F(Math.Sqrt(sumSurgeSq / n)) + "` m/s");
        sb.AppendLine("- mean absolute surge error: `" + F(sumSurgeAbs / n) + "` m/s");
        sb.AppendLine("- max absolute surge error: `" + F(maxSurgeAbs) + "` m/s");
        sb.AppendLine("- final surge error: `" + F(finalSurgeError) + "` m/s");
        sb.AppendLine();

        sb.AppendLine("- RMS yaw error: `" + F(Math.Sqrt(sumYawSq / n)) + "` deg");
        sb.AppendLine("- mean absolute yaw error: `" + F(sumYawAbs / n) + "` deg");
        sb.AppendLine("- max absolute yaw error: `" + F(maxYawAbs) + "` deg");
        sb.AppendLine("- final yaw error: `" + F(finalYawError) + "` deg");
        sb.AppendLine();

        sb.AppendLine("- RMS depth error: `" + F(Math.Sqrt(sumDepthSq / n)) + "` m");
        sb.AppendLine("- mean absolute depth error: `" + F(sumDepthAbs / n) + "` m");
        sb.AppendLine("- max absolute depth error: `" + F(maxDepthAbs) + "` m");
        sb.AppendLine("- final depth error: `" + F(finalDepthError) + "` m");
        sb.AppendLine();

        if (pathSampleCount > 0)
        {
            sb.AppendLine("## LOS / Path Errors");
            sb.AppendLine();
            sb.AppendLine("- RMS tracking error: `" + F(Math.Sqrt(sumTrackingSq / pathN)) + "` m");
            sb.AppendLine("- mean absolute tracking error: `" + F(sumTrackingAbs / pathN) + "` m");
            sb.AppendLine("- max absolute tracking error: `" + F(maxTrackingAbs) + "` m");
            sb.AppendLine("- final tracking error: `" + F(finalTrackingError) + "` m");
            sb.AppendLine("- RMS cross-track error: `" + F(Math.Sqrt(sumCrossTrackSq / pathN)) + "` m");
            sb.AppendLine("- mean absolute cross-track error: `" + F(sumCrossTrackAbs / pathN) + "` m");
            sb.AppendLine("- max absolute cross-track error: `" + F(maxCrossTrackAbs) + "` m");
            sb.AppendLine();
        }

        sb.AppendLine("## Saturation");
        sb.AppendLine();
        sb.AppendLine("- thruster saturation fraction: `" + F(thrusterSaturationCount / n) + "`");
        sb.AppendLine("- ballast saturation fraction: `" + F(ballastSaturationCount / n) + "`");
        sb.AppendLine();

        sb.AppendLine("## Reference Python Acceptance Targets");
        sb.AppendLine();
        sb.AppendLine("- nominal line RMS: about `0.04–0.05 m`");
        sb.AppendLine("- nominal square RMS: about `0.05–0.06 m`");
        sb.AppendLine("- nominal circle RMS: about `0.06–0.07 m`");
        sb.AppendLine("- surge RMS speed error: about `0.02 m/s`");
        sb.AppendLine("- yaw settling: slow, around `15 s`");
        sb.AppendLine("- depth settling: slow, around `38 s`");
        sb.AppendLine();

        File.WriteAllText(
            currentSummaryPath,
            sb.ToString(),
            Encoding.UTF8
        );
    }

    private void ResetMetrics()
    {
        sampleCount = 0;

        sumSurgeSq = 0.0;
        sumSurgeAbs = 0.0;
        maxSurgeAbs = 0.0;
        finalSurgeError = 0.0;

        sumYawSq = 0.0;
        sumYawAbs = 0.0;
        maxYawAbs = 0.0;
        finalYawError = 0.0;

        sumDepthSq = 0.0;
        sumDepthAbs = 0.0;
        maxDepthAbs = 0.0;
        finalDepthError = 0.0;

        pathSampleCount = 0;
        sumTrackingSq = 0.0;
        sumTrackingAbs = 0.0;
        maxTrackingAbs = 0.0;
        finalTrackingError = 0.0;

        sumCrossTrackSq = 0.0;
        sumCrossTrackAbs = 0.0;
        maxCrossTrackAbs = 0.0;

        thrusterSaturationCount = 0;
        ballastSaturationCount = 0;
    }

    private float ReadFirstFloat(object target, params string[] names)
    {
        if (target == null || names == null)
        {
            return float.NaN;
        }

        for (int i = 0; i < names.Length; i++)
        {
            float v;

            if (TryReadFloat(target, names[i], out v))
            {
                return v;
            }
        }

        return float.NaN;
    }

    private bool TryReadFloat(object target, string name, out float value)
    {
        value =
            float.NaN;

        if (target == null || string.IsNullOrEmpty(name))
        {
            return false;
        }

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        Type t =
            target.GetType();

        FieldInfo f =
            t.GetField(name, flags);

        if (f != null)
        {
            object raw =
                f.GetValue(target);

            return ConvertToFloat(raw, out value);
        }

        PropertyInfo p =
            t.GetProperty(name, flags);

        if (p != null && p.CanRead)
        {
            object raw =
                p.GetValue(target, null);

            return ConvertToFloat(raw, out value);
        }

        return false;
    }

    private bool ConvertToFloat(object raw, out float value)
    {
        value =
            float.NaN;

        if (raw == null)
        {
            return false;
        }

        try
        {
            value =
                Convert.ToSingle(raw, CI);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void Append(StringBuilder sb, float value)
    {
        sb.Append(value.ToString("G9", CI));
        sb.Append(',');
    }

    private static void Append(StringBuilder sb, double value)
    {
        sb.Append(value.ToString("G17", CI));
        sb.Append(',');
    }

    private static void Append(StringBuilder sb, int value)
    {
        sb.Append(value.ToString(CI));
        sb.Append(',');
    }

    private static void Append(StringBuilder sb, short value)
    {
        sb.Append(value.ToString(CI));
        sb.Append(',');
    }

    private static void AppendText(StringBuilder sb, string value)
    {
        sb.Append('"');
        sb.Append((value ?? "").Replace("\"", "\"\""));
        sb.Append('"');
        sb.Append(',');
    }

    private static void AppendLast(StringBuilder sb, float value)
    {
        sb.Append(value.ToString("G9", CI));
    }

    private static string F(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return "nan";
        }

        return value.ToString("F6", CI);
    }

    private static string MakeSafeFileName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "test";
        }

        char[] invalid =
            Path.GetInvalidFileNameChars();

        string s =
            value;

        for (int i = 0; i < invalid.Length; i++)
        {
            s =
                s.Replace(invalid[i], '_');
        }

        return s.Replace(' ', '_');
    }
}
