using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class MIMISKMiniROVRuntimeCsvLogger : MonoBehaviour
{
    public enum LogProfile
    {
        Lean,
        FullDiagnostics
    }

    [Header("References")]
    public Rigidbody rb;
    public ControlManager controlManager;
    public UnityVirtualESP32 unityVirtualESP32;
    public MIMISKMiniROVPlantBasedController plantBasedController;
    public MIMISKMiniROVDirectRaspberryBypassInput directBypassInput;
    public MIMISKMiniROVPathPlanner pathPlanner;
    public MIMISKMiniROVMissionManager missionManager;

    [Header("Logging")]
    public bool autoStartOnPlay = true;
    public LogProfile logProfile = LogProfile.Lean;

    [Tooltip("Use 5-10 Hz for smooth Unity playback. Use 50 Hz only for short debugging runs.")]
    public float logRateHz = 10.0f;

    [Tooltip("0 means unlimited. Use 60-180 seconds for small files.")]
    public float maxLogDurationS = 0.0f;

    [Tooltip("Flush less often for better performance.")]
    public int flushEverySamples = 200;

    public string testName = "minirov_runtime";
    public string logSubfolder = "Logs/MiniROV";
    public float waterLevelY = 0.0f;

    [Header("Runtime")]
    public bool logging;
    public string currentCsvPath = "";
    public string currentSummaryPath = "";
    public int sampleCount;
    public string activeCommandOwner = "unknown";
    public string lastEvent = "idle";

    private StreamWriter writer;
    private float startTime;
    private float nextLogTime;
    private bool applicationQuitting;

    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        if (autoStartOnPlay)
        {
            StartLog();
        }
    }

    private void FixedUpdate()
    {
        if (!logging)
        {
            return;
        }

        float elapsed = Time.time - startTime;

        if (maxLogDurationS > 0.0f && elapsed >= maxLogDurationS)
        {
            StopLog();
            return;
        }

        if (Time.time < nextLogTime)
        {
            return;
        }

        float period = 1.0f / Mathf.Max(1.0f, logRateHz);
        nextLogTime = Time.time + period;

        WriteSample();
    }

    private void OnApplicationQuit()
    {
        applicationQuitting = true;

        if (logging)
        {
            StopLog();
        }
    }

    private void OnDisable()
    {
        if (!applicationQuitting && logging)
        {
            StopLog();
        }
    }

    private void OnDestroy()
    {
        if (logging)
        {
            StopLog();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (controlManager == null)
        {
            controlManager = GetComponent<ControlManager>();
        }

        if (unityVirtualESP32 == null)
        {
            unityVirtualESP32 = GetComponent<UnityVirtualESP32>();
        }

        if (plantBasedController == null)
        {
            plantBasedController = GetComponent<MIMISKMiniROVPlantBasedController>();
        }

        if (directBypassInput == null)
        {
            directBypassInput = GetComponent<MIMISKMiniROVDirectRaspberryBypassInput>();
        }

        if (pathPlanner == null)
        {
            pathPlanner = GetComponent<MIMISKMiniROVPathPlanner>();
        }

        if (missionManager == null)
        {
            missionManager = GetComponent<MIMISKMiniROVMissionManager>();
        }
    }

    [ContextMenu("Start Log")]
    public void StartLog()
    {
        AutoFindReferences();
        StopWriterOnly();

        sampleCount = 0;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string dir = Path.Combine(projectRoot, logSubfolder);
        Directory.CreateDirectory(dir);

        string scene = MakeSafeFileName(SceneManager.GetActiveScene().name);
        string test = MakeSafeFileName(testName);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        currentCsvPath = Path.Combine(dir, "minirov_" + scene + "_" + test + "_" + stamp + ".csv");
        currentSummaryPath = Path.Combine(dir, "minirov_" + scene + "_" + test + "_" + stamp + "_summary.md");

        writer = new StreamWriter(currentCsvPath, false, Encoding.UTF8, 65536);

        WriteHeader();

        startTime = Time.time;
        nextLogTime = Time.time;
        logging = true;
        lastEvent = "logging_started";

        Debug.Log("[MIMISK] MiniROV lean CSV logger started: " + currentCsvPath);
    }

    [ContextMenu("Stop Log")]
    public void StopLog()
    {
        logging = false;
        StopWriterOnly();
        WriteSummary();

        lastEvent = "logging_stopped";

        Debug.Log("[MIMISK] MiniROV lean CSV logger stopped: " + currentCsvPath);
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
        StringBuilder sb = new StringBuilder(2048);

        AppendHeader(sb, "time_s");
        AppendHeader(sb, "frame");
        AppendHeader(sb, "scene");
        AppendHeader(sb, "test_name");
        AppendHeader(sb, "active_command_owner");

        AppendHeader(sb, "mission_state");
        AppendHeader(sb, "mission_last_event");
        AppendHeader(sb, "mission_active");
        AppendHeader(sb, "mission_recovery_ready");
        AppendHeader(sb, "mission_distance_to_home_m");
        AppendHeader(sb, "mission_current_speed_m_s");
        AppendHeader(sb, "mission_dwell_active");
        AppendHeader(sb, "mission_dwell_phase");
        AppendHeader(sb, "mission_dwell_timer_s");
        AppendHeader(sb, "mission_dwell_settle_timer_s");
        AppendHeader(sb, "mission_dwell_target_yaw_deg");
        AppendHeader(sb, "mission_dwell_cancel_reason");
        AppendHeader(sb, "mission_stoplook_active");
        AppendHeader(sb, "mission_stoplook_index");
        AppendHeader(sb, "mission_stoplook_total");
        AppendHeader(sb, "mission_stoplook_dwell_timer_s");
        AppendHeader(sb, "mission_stoplook_phase");
        AppendHeader(sb, "mission_stoplook_target_yaw_deg");
        AppendHeader(sb, "mission_stoplook_settle_timer_s");
        AppendHeader(sb, "mission_stoplook_ready_to_dwell");
        AppendHeader(sb, "mission_stoplook_settle_status");
        AppendHeader(sb, "mission_stoplook_cancel_reason");
        AppendHeader(sb, "mission_stoplook_skipped_waypoints");

        AppendHeader(sb, "planner_selected_path_type");
        AppendHeader(sb, "planner_selected_yaw_policy");
        AppendHeader(sb, "planner_selected_speed_profile");
        AppendHeader(sb, "planner_last_plan_name");
        AppendHeader(sb, "planner_last_algorithm_name");
        AppendHeader(sb, "planner_last_event");
        AppendHeader(sb, "planner_last_path_point_count");
        AppendHeader(sb, "planner_last_estimated_path_length_m");
        AppendHeader(sb, "planner_applied_mission_speed_m_s");
        AppendHeader(sb, "planner_applied_lookahead_m");
        AppendHeader(sb, "planner_default_polyline_arrival_radius_m");
        AppendHeader(sb, "planner_depth_polyline_arrival_radius_m");

        AppendHeader(sb, "position_x");
        AppendHeader(sb, "position_y");
        AppendHeader(sb, "position_z");
        AppendHeader(sb, "depth_m");
        AppendHeader(sb, "yaw_deg");

        AppendHeader(sb, "vel_x");
        AppendHeader(sb, "vel_y");
        AppendHeader(sb, "vel_z");
        AppendHeader(sb, "body_surge_u_m_s");
        AppendHeader(sb, "body_lateral_v_m_s");
        AppendHeader(sb, "depth_rate_m_s");
        AppendHeader(sb, "yaw_rate_rad_s");

        AppendHeader(sb, "plant_enabled");
        AppendHeader(sb, "plant_controller_enabled");
        AppendHeader(sb, "plant_mode");
        AppendHeader(sb, "plant_target_surge_m_s");
        AppendHeader(sb, "plant_target_yaw_deg");
        AppendHeader(sb, "plant_target_depth_m");
        AppendHeader(sb, "plant_target_point_x");
        AppendHeader(sb, "plant_target_point_y");
        AppendHeader(sb, "plant_target_point_z");
        AppendHeader(sb, "plant_target_point_depth_m");
        AppendHeader(sb, "plant_distance_to_target_m");
        AppendHeader(sb, "plant_station_hold_error_m");
        AppendHeader(sb, "plant_surge_error_m_s");
        AppendHeader(sb, "plant_yaw_error_deg");
        AppendHeader(sb, "plant_path_tangent_yaw_deg");
        AppendHeader(sb, "plant_los_desired_yaw_deg");
        AppendHeader(sb, "plant_los_heading_correction_deg");
        AppendHeader(sb, "plant_active_travel_yaw_deg");
        AppendHeader(sb, "plant_active_control_yaw_deg");
        AppendHeader(sb, "plant_depth_error_m");
        AppendHeader(sb, "plant_los_cross_track_error_m");
        AppendHeader(sb, "plant_los_tracking_error_m");
        AppendHeader(sb, "plant_mission_completed");
        AppendHeader(sb, "plant_completion_reason");
        AppendHeader(sb, "plant_surge_cmd");
        AppendHeader(sb, "plant_yaw_cmd");
        AppendHeader(sb, "plant_ballast_cmd");
        AppendHeader(sb, "plant_left_pwm");
        AppendHeader(sb, "plant_right_pwm");
        AppendHeader(sb, "plant_dc_port_pwm");
        AppendHeader(sb, "plant_dc_starboard_pwm");

        AppendHeader(sb, "bypass_enabled");
        AppendHeader(sb, "bypass_receiver_enabled");
        AppendHeader(sb, "bypass_connected");
        AppendHeader(sb, "bypass_packet_fresh");
        AppendHeader(sb, "bypass_packets_received");
        AppendHeader(sb, "bypass_lx");
        AppendHeader(sb, "bypass_ly");
        AppendHeader(sb, "bypass_lt");
        AppendHeader(sb, "bypass_rt");
        AppendHeader(sb, "bypass_throttle");
        AppendHeader(sb, "bypass_yaw");
        AppendHeader(sb, "bypass_vertical_dc");
        AppendHeader(sb, "bypass_left_pwm");
        AppendHeader(sb, "bypass_right_pwm");
        AppendHeader(sb, "bypass_dc_port_pwm");
        AppendHeader(sb, "bypass_dc_starboard_pwm");

        AppendHeader(sb, "esp32_enabled");
        AppendHeader(sb, "esp32_motor_rx_connected");
        AppendHeader(sb, "esp32_last_left_thruster");
        AppendHeader(sb, "esp32_last_right_thruster");
        AppendHeader(sb, "esp32_last_dc_port");
        AppendHeader(sb, "esp32_last_dc_starboard");

        AppendHeader(sb, "control_enabled");
        AppendHeader(sb, "control_auto_open_on_start");

        AppendHeader(sb, "rb_mass_kg");
        AppendHeader(sb, "rb_linear_damping");
        AppendHeader(sb, "rb_angular_damping");
        AppendHeader(sb, "rb_use_gravity");
        AppendHeader(sb, "rb_is_kinematic");
        AppendHeader(sb, "rb_constraints");
        AppendHeader(sb, "control_thruster_max_force");
        AppendHeader(sb, "control_deadzone_pwm");
        AppendHeader(sb, "control_use_thruster_transform_forward");

        if (logProfile == LogProfile.FullDiagnostics)
        {
            AppendHeader(sb, "control_left_raw");
            AppendHeader(sb, "control_right_raw");
            AppendHeader(sb, "control_left_command");
            AppendHeader(sb, "control_right_command");
            AppendHeader(sb, "control_left_force");
            AppendHeader(sb, "control_right_force");
            AppendHeader(sb, "control_propulsion_force");
            AppendHeader(sb, "control_yaw_differential_force");
        }

        EndLine(sb);
        writer.WriteLine(sb.ToString());
    }

    private void WriteSample()
    {
        if (writer == null)
        {
            return;
        }

        sampleCount++;

        Vector3 pos = rb != null ? rb.position : transform.position;
        Vector3 linVel = rb != null ? rb.linearVelocity : Vector3.zero;
        Vector3 angVel = rb != null ? rb.angularVelocity : Vector3.zero;

        float depth = waterLevelY - pos.y;
        float yawDeg = transform.eulerAngles.y;

        float bodyU = Vector3.Dot(linVel, transform.forward);
        float bodyV = Vector3.Dot(linVel, transform.right);
        float depthRate = -linVel.y;
        float yawRate = angVel.y;

        activeCommandOwner = DetermineActiveCommandOwner();

        StringBuilder sb = new StringBuilder(4096);

        Append(sb, Time.time - startTime);
        Append(sb, Time.frameCount);
        AppendText(sb, SceneManager.GetActiveScene().name);
        AppendText(sb, testName);
        AppendText(sb, activeCommandOwner);

        AppendText(sb, missionManager != null ? missionManager.missionState.ToString() : "missing");
        AppendText(sb, missionManager != null ? missionManager.lastEvent : "missing");
        AppendBool(sb, missionManager != null && missionManager.missionActive);
        AppendBool(sb, missionManager != null && missionManager.recoveryReady);
        Append(sb, missionManager != null ? missionManager.distanceToHomeM : float.NaN);
        Append(sb, missionManager != null ? missionManager.currentSpeedMS : float.NaN);
        AppendBool(sb, missionManager != null && missionManager.stopLookActive);
        Append(sb, missionManager != null ? missionManager.stopLookCurrentIndex : -1);
        Append(sb, missionManager != null ? missionManager.stopLookTotalWaypoints : -1);
        Append(sb, missionManager != null ? missionManager.stopLookDwellTimerS : float.NaN);
        AppendText(sb, missionManager != null ? missionManager.stopLookPhase : "missing");
        Append(sb, missionManager != null ? missionManager.stopLookCurrentTargetYawDeg : float.NaN);
        Append(sb, missionManager != null ? missionManager.stopLookSettleTimerS : float.NaN);
        AppendBool(sb, missionManager != null && missionManager.stopLookReadyToDwell);
        AppendText(sb, missionManager != null ? missionManager.stopLookSettleStatus : "missing");
        AppendText(sb, missionManager != null ? missionManager.stopLookCancelReason : "missing");
        Append(sb, missionManager != null ? missionManager.stopLookSkippedWaypoints : -1);

        AppendText(sb, pathPlanner != null ? pathPlanner.selectedPathType.ToString() : "missing");
        AppendText(sb, pathPlanner != null ? pathPlanner.selectedYawPolicy.ToString() : "missing");
        AppendText(sb, pathPlanner != null ? pathPlanner.selectedSpeedProfile.ToString() : "missing");
        AppendText(sb, pathPlanner != null ? pathPlanner.lastPlanName : "missing");
        AppendText(sb, pathPlanner != null ? pathPlanner.lastAlgorithmName : "missing");
        AppendText(sb, pathPlanner != null ? pathPlanner.lastEvent : "missing");
        Append(sb, pathPlanner != null ? pathPlanner.lastPathPointCount : -1);
        Append(sb, pathPlanner != null ? pathPlanner.lastEstimatedPathLengthM : float.NaN);
        Append(sb, pathPlanner != null ? pathPlanner.appliedMissionSpeedMS : float.NaN);
        Append(sb, pathPlanner != null ? pathPlanner.appliedLookaheadM : float.NaN);
        Append(sb, pathPlanner != null ? pathPlanner.defaultPolylineArrivalRadiusM : float.NaN);
        Append(sb, pathPlanner != null ? pathPlanner.depthVaryingPolylineArrivalRadiusM : float.NaN);

        Append(sb, pos.x);
        Append(sb, pos.y);
        Append(sb, pos.z);
        Append(sb, depth);
        Append(sb, yawDeg);

        Append(sb, linVel.x);
        Append(sb, linVel.y);
        Append(sb, linVel.z);
        Append(sb, bodyU);
        Append(sb, bodyV);
        Append(sb, depthRate);
        Append(sb, yawRate);

        WritePlantFields(sb);
        WriteBypassFields(sb);
        WriteEsp32Fields(sb);

        AppendBool(sb, controlManager != null && controlManager.enabled);
        AppendBool(sb, controlManager != null && ReadBool(controlManager, false, "autoOpenOnStart"));

        Append(sb, rb != null ? rb.mass : float.NaN);
        Append(sb, rb != null ? rb.linearDamping : float.NaN);
        Append(sb, rb != null ? rb.angularDamping : float.NaN);
        AppendBool(sb, rb != null && rb.useGravity);
        AppendBool(sb, rb != null && rb.isKinematic);
        AppendText(sb, rb != null ? rb.constraints.ToString() : "missing");
        Append(sb, ReadFirstFloat(controlManager, "thrusterMaxForce", "maxThrusterForce"));
        Append(sb, ReadFirstFloat(controlManager, "thrusterDeadzonePwm", "deadzonePwm"));
        AppendBool(sb, ReadBool(controlManager, false, "useThrusterTransformForward"));

        if (logProfile == LogProfile.FullDiagnostics)
        {
            Append(sb, ReadFirstFloat(controlManager, "leftThrusterRaw", "leftRaw", "lastLeftThruster", "thrusterPortRaw"));
            Append(sb, ReadFirstFloat(controlManager, "rightThrusterRaw", "rightRaw", "lastRightThruster", "thrusterStarboardRaw"));
            Append(sb, ReadFirstFloat(controlManager, "leftThrusterCommand", "leftCommand", "leftCmd"));
            Append(sb, ReadFirstFloat(controlManager, "rightThrusterCommand", "rightCommand", "rightCmd"));
            Append(sb, ReadFirstFloat(controlManager, "leftThrusterForce", "leftForce", "portThrusterForce"));
            Append(sb, ReadFirstFloat(controlManager, "rightThrusterForce", "rightForce", "starboardThrusterForce"));
            Append(sb, ReadFirstFloat(controlManager, "propulsionForce", "surgeForce", "forwardForce"));
            Append(sb, ReadFirstFloat(controlManager, "yawDifferentialForce", "differentialForce", "yawForce"));
        }

        EndLine(sb);
        writer.WriteLine(sb.ToString());

        if (sampleCount % Mathf.Max(10, flushEverySamples) == 0)
        {
            writer.Flush();
        }
    }

    private void WritePlantFields(StringBuilder sb)
    {
        if (plantBasedController == null)
        {
            AppendMissingPlant(sb);
            return;
        }

        AppendBool(sb, plantBasedController.enabled);
        AppendBool(sb, plantBasedController.controllerEnabled);
        AppendText(sb, plantBasedController.controlMode.ToString());

        Append(sb, plantBasedController.targetSurgeSpeedMS);
        Append(sb, plantBasedController.targetYawDeg);
        Append(sb, plantBasedController.targetDepthM);

        Append(sb, plantBasedController.targetPointWorld.x);
        Append(sb, plantBasedController.targetPointWorld.y);
        Append(sb, plantBasedController.targetPointWorld.z);
        Append(sb, plantBasedController.targetPointDepthM);
        Append(sb, plantBasedController.distanceToTargetM);
        Append(sb, plantBasedController.stationHoldErrorM);

        Append(sb, plantBasedController.surgeErrorMS);
        Append(sb, plantBasedController.yawErrorDeg);
        Append(sb, plantBasedController.pathTangentYawDeg);
        Append(sb, plantBasedController.losDesiredYawDeg);
        Append(sb, plantBasedController.losHeadingCorrectionDeg);
        Append(sb, plantBasedController.activeTravelYawDeg);
        Append(sb, plantBasedController.activeControlYawDeg);
        Append(sb, plantBasedController.depthErrorM);

        Append(sb, plantBasedController.losCrossTrackErrorM);
        Append(sb, plantBasedController.losTrackingErrorM);
        AppendBool(sb, plantBasedController.missionCompleted);
        AppendText(sb, plantBasedController.completionReason);

        Append(sb, plantBasedController.surgeCmd);
        Append(sb, plantBasedController.yawCmd);
        Append(sb, plantBasedController.ballastCmd);

        Append(sb, plantBasedController.leftPwm);
        Append(sb, plantBasedController.rightPwm);
        Append(sb, plantBasedController.dcPortPwm);
        Append(sb, plantBasedController.dcStarboardPwm);
    }

    private void AppendMissingPlant(StringBuilder sb)
    {
        AppendBool(sb, false);
        AppendBool(sb, false);
        AppendText(sb, "missing");

        for (int i = 0; i < 28; i++)
        {
            Append(sb, float.NaN);
        }
    }

    private void WriteBypassFields(StringBuilder sb)
    {
        if (directBypassInput == null)
        {
            AppendMissingBypass(sb);
            return;
        }

        AppendBool(sb, directBypassInput.enabled);
        AppendBool(sb, directBypassInput.receiverEnabled);
        AppendBool(sb, directBypassInput.connected);
        AppendBool(sb, directBypassInput.packetFresh);
        Append(sb, directBypassInput.packetsReceived);

        Append(sb, directBypassInput.lx);
        Append(sb, directBypassInput.ly);
        Append(sb, directBypassInput.lt);
        Append(sb, directBypassInput.rt);

        Append(sb, directBypassInput.throttle);
        Append(sb, directBypassInput.yaw);
        Append(sb, directBypassInput.verticalDc);

        Append(sb, directBypassInput.thrusterPort);
        Append(sb, directBypassInput.thrusterStarboard);
        Append(sb, directBypassInput.dcPort);
        Append(sb, directBypassInput.dcStarboard);
    }

    private void AppendMissingBypass(StringBuilder sb)
    {
        AppendBool(sb, false);
        AppendBool(sb, false);
        AppendBool(sb, false);
        AppendBool(sb, false);

        for (int i = 0; i < 13; i++)
        {
            Append(sb, float.NaN);
        }
    }

    private void WriteEsp32Fields(StringBuilder sb)
    {
        AppendBool(sb, unityVirtualESP32 != null && unityVirtualESP32.enabled);
        AppendBool(sb, ReadBool(unityVirtualESP32, false, "motorRxConnected"));

        Append(sb, ReadFirstFloat(unityVirtualESP32, "lastLeftThruster", "leftThruster", "thrusterPort"));
        Append(sb, ReadFirstFloat(unityVirtualESP32, "lastRightThruster", "rightThruster", "thrusterStarboard"));
        Append(sb, ReadFirstFloat(unityVirtualESP32, "lastDcPort", "dcPort"));
        Append(sb, ReadFirstFloat(unityVirtualESP32, "lastDcStarboard", "dcStarboard"));
    }

    private string DetermineActiveCommandOwner()
    {
        if (plantBasedController != null &&
            plantBasedController.enabled &&
            plantBasedController.controllerEnabled)
        {
            return "plant_based_controller";
        }

        if (directBypassInput != null &&
            directBypassInput.enabled &&
            directBypassInput.receiverEnabled)
        {
            return "direct_raspberry_bypass";
        }

        if (unityVirtualESP32 != null &&
            unityVirtualESP32.enabled &&
            ReadBool(unityVirtualESP32, false, "motorRxConnected"))
        {
            return "unity_virtual_esp32";
        }

        return "idle";
    }

    private void WriteSummary()
    {
        if (string.IsNullOrEmpty(currentSummaryPath))
        {
            return;
        }

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("# MiniROV Lean Runtime CSV Log");
        sb.AppendLine();
        sb.AppendLine("- CSV: `" + currentCsvPath + "`");
        sb.AppendLine("- Scene: `" + SceneManager.GetActiveScene().name + "`");
        sb.AppendLine("- Test name: `" + testName + "`");
        sb.AppendLine("- Profile: `" + logProfile + "`");
        sb.AppendLine("- Log rate Hz: `" + logRateHz.ToString("F1", CI) + "`");
        sb.AppendLine("- Samples: `" + sampleCount + "`");
        sb.AppendLine("- Last active command owner: `" + activeCommandOwner + "`");
        sb.AppendLine();
        sb.AppendLine("Upload the CSV file for detailed analysis.");

        File.WriteAllText(currentSummaryPath, sb.ToString(), Encoding.UTF8);
    }

    private float ReadFirstFloat(object target, params string[] names)
    {
        if (names == null)
        {
            return float.NaN;
        }

        for (int i = 0; i < names.Length; i++)
        {
            float v = ReadFloat(target, float.NaN, names[i]);

            if (!float.IsNaN(v))
            {
                return v;
            }
        }

        return float.NaN;
    }

    private float ReadFloat(object target, float fallback, string name)
    {
        object raw;

        if (!TryReadMember(target, name, out raw))
        {
            return fallback;
        }

        try
        {
            return Convert.ToSingle(raw, CI);
        }
        catch
        {
            return fallback;
        }
    }

    private bool ReadBool(object target, bool fallback, string name)
    {
        object raw;

        if (!TryReadMember(target, name, out raw))
        {
            return fallback;
        }

        try
        {
            return Convert.ToBoolean(raw, CI);
        }
        catch
        {
            return fallback;
        }
    }

    private bool TryReadMember(object target, string name, out object value)
    {
        value = null;

        if (target == null || string.IsNullOrEmpty(name))
        {
            return false;
        }

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        Type t = target.GetType();

        FieldInfo f = t.GetField(name, flags);

        if (f != null)
        {
            value = f.GetValue(target);
            return true;
        }

        PropertyInfo p = t.GetProperty(name, flags);

        if (p != null && p.CanRead)
        {
            value = p.GetValue(target, null);
            return true;
        }

        return false;
    }

    private static void AppendHeader(StringBuilder sb, string text)
    {
        sb.Append(text);
        sb.Append(',');
    }

    private static void Append(StringBuilder sb, float value)
    {
        sb.Append(value.ToString("G9", CI));
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

    private static void AppendBool(StringBuilder sb, bool value)
    {
        sb.Append(value ? "1" : "0");
        sb.Append(',');
    }

    private static void AppendText(StringBuilder sb, string value)
    {
        sb.Append('"');
        sb.Append((value ?? "").Replace("\"", "\"\""));
        sb.Append('"');
        sb.Append(',');
    }

    private static void EndLine(StringBuilder sb)
    {
        if (sb.Length > 0 && sb[sb.Length - 1] == ',')
        {
            sb.Length -= 1;
        }
    }

    private static string MakeSafeFileName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "test";
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        string output = value;

        for (int i = 0; i < invalid.Length; i++)
        {
            output = output.Replace(invalid[i], '_');
        }

        return output.Replace(' ', '_');
    }
}
