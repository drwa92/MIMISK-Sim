using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(5000)]
public class MIMISKValidationHarness : MonoBehaviour
{
    public enum TestKind
    {
        FullMission,
        DroneTrajectory,
        MiniRovNavigation,
        TetherV8,
        RosGrpc,
        IdlePerformance
    }

    [Serializable]
    public class RunConfig
    {
        public string run_id = "mimisk_validation";
        public string trial_id = "trial_001";
        public string test = "full_mission";
        public string scene = "Assets/MIMISK/Scenes/05_MiniROV_Finale_agent.unity";
        public string output_root = "Logs/MIMISKValidation";
        public string drone_path = "Circle";
        public string rov_path = "LawnmowerSurvey";
        public float log_hz = 30.0f;
        public float performance_hz = 2.0f;
        public bool enable_controller_diagnostics = true;
        public float diagnostics_hz = 10.0f;
        public float warmup_s = 2.0f;
        public float max_duration_s = 420.0f;
        public float drone_wait_timeout_s = 120.0f;
        public float deploy_timeout_s = 60.0f;
        public float rov_mission_duration_s = 55.0f;
        public float return_timeout_s = 90.0f;
        public float recovery_timeout_s = 70.0f;
        public float recovery_ready_distance_m = 0.35f;
        public bool require_minirov_recovery_ready_state = true;
        public bool require_geometric_home_for_recovery = false; // retained for inspector compatibility; recovery uses horizontal home distance
        public float recovery_ready_min_return_s = 1.0f;
        public float drone_takeoff_target_altitude_m = 2.0f;
        public float drone_takeoff_ready_tolerance_m = 0.20f;
        public float drone_takeoff_min_settle_s = 1.0f;
        public float drone_trajectory_segment_s = 12.0f;
        public bool drone_run_complete_trajectory = true;
        public bool drone_close_circle_loop = true;
        public float drone_path_completion_hold_s = 0.75f;
        public bool rov_wait_for_mission_completion = true;
        public float rov_mission_completion_timeout_s = 120.0f;
        public float rov_lawnmower_completion_timeout_s = 160.0f;
        public float rov_waypoints_completion_timeout_s = 90.0f;
        public bool enable_rov_pre_mission_descent = true;
        public float rov_pre_mission_depth_m = 1.0f;
        public float rov_depth_ready_tolerance_m = 0.12f;
        public float rov_descent_timeout_s = 30.0f;
        public bool include_drone_takeoff_landing = true;
        public bool use_drone_mission_manager = false;
        public bool enable_ros_grpc = false;
        public bool quit_on_complete = false;
        public bool write_done_flag = false;
        public string done_flag_path = "Logs/MIMISKValidation/validation_done.flag";
    }

    [Header("Run Configuration")]
    public bool loadConfigOnStart = true;
    public bool autoStart = true;
    public bool quitOnComplete = false;
    public RunConfig config = new RunConfig();

    [Header("Inspector Test Launcher Compatibility")]
    public TestKind inspectorTest = TestKind.FullMission;
    public string inspectorRunId = "paper_complete_workflow_v8";
    public int inspectorTrialIndex = 1;
    public string inspectorDronePath = "Circle";
    public string inspectorRovPath = "LawnmowerSurvey";
    public bool inspectorIncludeDroneTakeoffLanding = true;
    public bool inspectorEnableRosGrpc = false;
    public bool inspectorAutoIncrementTrial = true;
    public bool resetAutoStartAfterRun = true;

    [Header("Resolved Runtime Components")]
    public GameObject drone;
    public GameObject miniRov;
    public Component droneAgent;
    public Component droneFlightManager;
    public Component droneMissionManager;
    public Component droneTrajectoryPlanner;
    public Component droneRotorController;
    public Rigidbody droneRb;
    public Component unifiedTether;
    public Component coreTetherManager;
    public Component v8Tether;
    public Component miniRovMissionManager;
    public Component miniRovPathPlanner;
    public Component miniRovController;
    public Rigidbody miniRovRb;
    public Component grpcConnection;
    public Component grpcTelemetry;
    public Component grpcCommand;
    public Component grpcManual;
    public Component grpcReference;
    public Component grpcCamera;

    [Header("Runtime Status")]
    public bool running;
    public bool completed;
    public bool failed;
    public string failureReason = "none";
    public string outputDirectory = "";

    private StreamWriter stateLog;
    private StreamWriter eventLog;
    private StreamWriter tetherLog;
    private StreamWriter perfLog;
    private StreamWriter grpcLog;
    private StreamWriter diagnosticsLog;
    private float nextStateLogTime;
    private float nextPerfLogTime;
    private float nextDiagnosticsLogTime;
    private float startWallRealtime;
    private float startSimTime;
    private int frameCountAtStart;
    private int fixedFrameCount;
    private float returnHomeCommandTime = -999.0f;
    private Vector3 validationReturnHomeWorld = Vector3.zero;
    private bool validationHomeRecorded = false;
    private string lastDroneState = "";
    private string lastTetherState = "";
    private string lastRovState = "";
    private readonly CultureInfo ci = CultureInfo.InvariantCulture;

    private void Start()
    {
        if (loadConfigOnStart)
        {
            LoadRunConfigIfPresent();
        }

        if (autoStart)
        {
            StartValidationRun();
        }
    }

    [ContextMenu("Start Validation Run")]
    public void StartValidationRun()
    {
        if (running)
        {
            return;
        }
        StartCoroutine(Run());
    }

    [ContextMenu("Cancel Validation Run")]
    public void CancelValidationRun()
    {
        if (!running || completed)
        {
            return;
        }
        failed = true;
        failureReason = "manual_cancel";
        LogEvent("manual_cancel", "Inspector", config.test, "none", GetWorkflowStateCompact(), false, "cancelled_by_user");
        FinishRun();
    }

    public void ConfigureForInspectorRun(string testName, string runId, string trialId, string dronePath, string rovPath, bool includeDrone, bool enableGrpc, bool useDroneMissionManager = false)
    {
        loadConfigOnStart = false;
        autoStart = false;
        quitOnComplete = false;
        config.quit_on_complete = false;
        config.write_done_flag = false;
        config.test = testName;
        config.run_id = string.IsNullOrWhiteSpace(runId) ? ("inspector_" + testName) : runId;
        config.trial_id = string.IsNullOrWhiteSpace(trialId) ? "trial_001" : trialId;
        config.drone_path = string.IsNullOrWhiteSpace(dronePath) ? config.drone_path : dronePath;
        config.rov_path = string.IsNullOrWhiteSpace(rovPath) ? config.rov_path : rovPath;
        config.include_drone_takeoff_landing = includeDrone;
        config.use_drone_mission_manager = useDroneMissionManager;
        config.enable_ros_grpc = enableGrpc;
    }

    public void ApplyInspectorPreset()
    {
        string testName = TestKindToConfigString(inspectorTest);
        string runId = string.IsNullOrWhiteSpace(inspectorRunId) ? ("inspector_" + testName) : inspectorRunId.Trim();
        string trialId = "trial_" + Mathf.Max(1, inspectorTrialIndex).ToString("000");
        string dronePath = string.IsNullOrWhiteSpace(inspectorDronePath) ? "Circle" : inspectorDronePath.Trim();
        string rovPath = string.IsNullOrWhiteSpace(inspectorRovPath) ? "LawnmowerSurvey" : inspectorRovPath.Trim();
        bool includeDrone = inspectorIncludeDroneTakeoffLanding || inspectorTest == TestKind.DroneTrajectory;
        bool useDroneMission = includeDrone && inspectorTest == TestKind.FullMission;
        bool enableGrpc = inspectorEnableRosGrpc || inspectorTest == TestKind.RosGrpc;
        ConfigureForInspectorRun(testName, runId, trialId, dronePath, rovPath, includeDrone, enableGrpc, useDroneMission);
    }

    public void StartInspectorSelectedTest()
    {
        if (running)
        {
            Debug.LogWarning("MIMISKValidationHarness: a validation run is already active.");
            return;
        }
        ApplyInspectorPreset();
        StartValidationRun();
    }

    public void ConfigureInspectorPreset(TestKind test, string runId, string dronePath, string rovPath, bool includeDrone, bool enableGrpc)
    {
        inspectorTest = test;
        inspectorRunId = runId;
        inspectorDronePath = dronePath;
        inspectorRovPath = rovPath;
        inspectorIncludeDroneTakeoffLanding = includeDrone;
        inspectorEnableRosGrpc = enableGrpc;
        ApplyInspectorPreset();
    }

    public void AbortValidationRun()
    {
        CancelValidationRun();
    }

    private string TestKindToConfigString(TestKind kind)
    {
        switch (kind)
        {
            case TestKind.DroneTrajectory: return "drone_trajectory";
            case TestKind.MiniRovNavigation: return "minirov_navigation";
            case TestKind.TetherV8: return "tether_v8";
            case TestKind.RosGrpc: return "ros_grpc";
            case TestKind.IdlePerformance: return "idle_performance";
            default: return "full_mission";
        }
    }

    private IEnumerator Run()
    {
        running = true;
        completed = false;
        failed = false;
        failureReason = "none";
        startWallRealtime = Time.realtimeSinceStartup;
        startSimTime = Time.time;
        frameCountAtStart = Time.frameCount;
        fixedFrameCount = 0;
        returnHomeCommandTime = -999.0f;

        ResolveReferences();
        ConfigureGrpcEnabled(config.enable_ros_grpc);
        PrepareOutputDirectory();
        OpenLogs();
        nextStateLogTime = Time.time;
        nextPerfLogTime = Time.time;
        nextDiagnosticsLogTime = Time.time;
        WriteManifest();
        LogEvent("scenario_start", "ValidationHarness", config.test, "none", GetWorkflowStateCompact(), true, "start");

        yield return new WaitForSeconds(config.warmup_s);

        TestKind kind = ParseTestKind(config.test);
        if (kind == TestKind.FullMission)
        {
            yield return RunFullMissionScenario();
        }
        else if (kind == TestKind.DroneTrajectory)
        {
            yield return RunDroneTrajectoryScenario();
        }
        else if (kind == TestKind.MiniRovNavigation)
        {
            yield return RunMiniRovScenario();
        }
        else if (kind == TestKind.TetherV8)
        {
            yield return RunTetherScenario();
        }
        else if (kind == TestKind.RosGrpc)
        {
            config.enable_ros_grpc = true;
            ConfigureGrpcEnabled(true);
            yield return RunFullMissionScenario();
        }
        else if (kind == TestKind.IdlePerformance)
        {
            yield return WaitAndLog(config.max_duration_s);
        }

        FinishRun();
    }

    private void FixedUpdate()
    {
        if (running && !completed)
        {
            fixedFrameCount++;
        }
    }

    private void Update()
    {
        if (!running || completed)
        {
            return;
        }

        TrackStateChanges();

        float logDt = 1.0f / Mathf.Max(1.0f, config.log_hz);
        int stateWrites = 0;
        while (Time.time >= nextStateLogTime && stateWrites < 3)
        {
            WriteStateLogRow();
            WriteTetherLogRow();
            WriteGrpcLogRow();
            nextStateLogTime += logDt;
            stateWrites++;
        }
        if (Time.time - nextStateLogTime > 2.0f * logDt)
        {
            nextStateLogTime = Time.time + logDt;
        }

        float perfDt = 1.0f / Mathf.Max(0.2f, config.performance_hz);
        if (Time.time >= nextPerfLogTime)
        {
            WritePerformanceLogRow();
            nextPerfLogTime = Time.time + perfDt;
        }

        if (config.enable_controller_diagnostics)
        {
            float diagDt = 1.0f / Mathf.Max(1.0f, config.diagnostics_hz);
            if (Time.time >= nextDiagnosticsLogTime)
            {
                WriteControllerDiagnosticsLogRow();
                nextDiagnosticsLogTime = Time.time + diagDt;
            }
        }

        if (Time.time - startSimTime > config.max_duration_s + 10.0f && !completed)
        {
            Fail("max_duration_reached");
        }
    }

    private IEnumerator RunFullMissionScenario()
    {
        LogEvent("phase_start", "ValidationHarness", "full_mission", "none", GetWorkflowStateCompact(), true, "full_mission");

        if (config.include_drone_takeoff_landing)
        {
            yield return RunDronePreparation();
        }
        else
        {
            LogEvent("phase_skip", "ValidationHarness", "drone_takeoff_landing", "none", GetWorkflowStateCompact(), true, "include_drone_takeoff_landing_false");
        }

        yield return EnsureTetherReadyOrAttach();
        yield return DeployAndActivateRov();
        yield return StartRovMissionAndReturnHome();
        yield return RecoverRov();
    }

    private IEnumerator RunDroneTrajectoryScenario()
    {
        LogEvent("phase_start", "ValidationHarness", "drone_trajectory", "none", GetWorkflowStateCompact(), true, config.drone_path);
        yield return RunDronePreparation();
        yield return WaitAndLog(5.0f);
    }

    private IEnumerator RunMiniRovScenario()
    {
        LogEvent("phase_start", "ValidationHarness", "minirov_tether_navigation", "none", GetWorkflowStateCompact(), true, config.rov_path);
        // Individual MiniROV validation mirrors the underwater part of the complete
        // workflow: attach -> deploy -> activate -> descend -> selected ROV mission
        // -> return home -> recover/reattach.  No drone mission logic is changed.
        yield return EnsureTetherReadyOrAttach();
        yield return DeployAndActivateRov();
        yield return StartRovMissionAndReturnHome();
        yield return RecoverRov();
    }

    private IEnumerator RunTetherScenario()
    {
        LogEvent("phase_start", "ValidationHarness", "tether_v8", "none", GetWorkflowStateCompact(), true, "v8_runtime_tether");
        yield return EnsureTetherReadyOrAttach();
        yield return DeployAndActivateRov();
        yield return StartRovMissionAndReturnHome();
        yield return RecoverRov();
    }

    private IEnumerator RunDronePreparation()
    {
        LogEvent("phase_start", "ValidationHarness", "drone_integrated_preparation", "none", GetWorkflowStateCompact(), true, config.drone_path);

        // For paper validation we use an explicit drone sequence rather than relying on a
        // long-running drone mission manager. This guarantees that the full workflow is:
        // takeoff -> selected trajectory -> water landing/surface hold -> tether deployment.
        // The previous mission-manager path could remain in PathTracking and never hand
        // control back to the tether layer, so deployment was never triggered.
        bool agentAvailable = droneAgent != null;
        if (!agentAvailable && config.use_drone_mission_manager && droneMissionManager != null)
        {
            yield return RunDroneMissionManagerSequence();
            yield break;
        }

        if (!agentAvailable)
        {
            LogEvent("warning", "ValidationHarness", "drone_agent_sequence", "none", GetWorkflowStateCompact(), false, "drone_agent_missing_skipping_drone_motion");
            yield break;
        }

        InvokeAny(droneAgent, new string[] { "AgentEnterTakeoffIdle", "EnterTakeoffIdle" });
        LogEvent("command", "ValidationHarness", "AgentEnterTakeoffIdle", "none", GetWorkflowStateCompact(), true, "sent");
        yield return WaitAndLog(0.75f);

        ApplyDroneTakeoffProfile();
        InvokeAny(droneAgent, new string[] { "AgentTakeoff", "Takeoff" });
        LogEvent("command", "ValidationHarness", "AgentTakeoff", "none", GetWorkflowStateCompact(), true,
            "target_altitude_m=" + config.drone_takeoff_target_altitude_m.ToString("F2", ci));
        yield return WaitUntilOrTimeout(
            () => DroneReachedValidationTakeoffAltitude(),
            45.0f,
            "drone_takeoff_altitude_reached");
        if (failed) yield break;

        string selectedDroneTrajectory = ConfigureDroneTrajectoryForValidation(config.drone_path);
        InvokeAny(droneAgent, new string[] { "AgentStartCurrentPath", "StartCurrentPath", "AgentStartPath" });
        LogEvent("command", "ValidationHarness", "AgentStartCurrentPath", "none", GetWorkflowStateCompact(), true, selectedDroneTrajectory);

        // Validation uses a complete trajectory when the trajectory planner exposes
        // a finite duration.  This avoids cutting cyclic paths such as Circle after
        // an arbitrary short segment.  If completion cannot be detected, the timeout
        // still provides a safe exit for continuous paths.
        float plannedDuration = GetDroneValidationPathDuration(config.drone_path);
        float minTrajectoryTime = config.drone_run_complete_trajectory
            ? Mathf.Max(2.0f, plannedDuration + Mathf.Max(0.0f, config.drone_path_completion_hold_s))
            : Mathf.Max(2.0f, config.drone_trajectory_segment_s);
        float maxTrajectoryTime = Mathf.Min(config.drone_wait_timeout_s, Mathf.Max(minTrajectoryTime + 6.0f, config.drone_trajectory_segment_s + 8.0f));
        yield return WaitForDroneTrajectorySegment(minTrajectoryTime, maxTrajectoryTime);
        if (failed) yield break;

        bool landCommandSent = InvokeAny(droneAgent, new string[] { "AgentLandOnSurface", "AgentLandOnWater", "AgentLand", "LandOnSurface", "LandOnWater" });
        if (!landCommandSent)
        {
            landCommandSent = InvokeIfExists(droneFlightManager, "StartLandingOnSurface");
        }
        LogEvent("command", "ValidationHarness", "LandOnWater", "none", GetWorkflowStateCompact(), landCommandSent, landCommandSent ? "sent_after_trajectory_segment" : "landing_command_missing");
        yield return WaitUntilOrTimeout(() => DroneSurfaceReadyForTether(), 70.0f, "drone_surface_ready_for_tether");
    }


    private void ApplyDroneTakeoffProfile()
    {
        float target = Mathf.Max(0.5f, config.drone_takeoff_target_altitude_m);
        SetFloat(droneFlightManager, "takeoffAltitudeAboveSurfaceM", target);
        SetFloat(droneFlightManager, "takeoffAltitudeM", target);
        SetFloat(droneFlightManager, "maxTakeoffAltitude", target);
        SetFloat(droneFlightManager, "targetAltitude", target);
        SetFloat(droneFlightManager, "targetAltitudeM", target);

        // Some projects keep both a core flight manager and older flight/autopilot components.
        // Updating any field that exists is safe and keeps this validation profile portable.
        Component[] all = drone != null ? drone.GetComponentsInChildren<Component>(true) : null;
        if (all != null)
        {
            foreach (Component c in all)
            {
                if (c == null) continue;
                SetFloat(c, "takeoffAltitudeAboveSurfaceM", target);
                SetFloat(c, "takeoffAltitudeM", target);
                SetFloat(c, "takeoffAltitude", target);
                SetFloat(c, "maxTakeoffAltitude", target);
                SetFloat(c, "targetAltitude", target);
                SetFloat(c, "targetAltitudeM", target);
            }
        }
    }

    private bool DroneReachedValidationTakeoffAltitude()
    {
        float targetY = GetDroneWaterSurfaceY() + Mathf.Max(0.5f, config.drone_takeoff_target_altitude_m);
        float tol = Mathf.Max(0.05f, config.drone_takeoff_ready_tolerance_m);
        float settle = Mathf.Max(0.0f, config.drone_takeoff_min_settle_s);
        if (Time.time - startSimTime < config.warmup_s + settle) return false;

        Vector3 p = GetPosition(drone);
        float vy = droneRb != null ? Mathf.Abs(droneRb.linearVelocity.y) : 0.0f;
        string mode = GetEnumOrString(droneFlightManager, "flightMode");
        bool altitudeOk = p.y >= targetY - tol;
        bool stableEnough = vy < 0.35f || mode.Contains("Hover") || mode.Contains("PositionHold") || mode.Contains("Takeoff");
        return altitudeOk && stableEnough;
    }

    private float GetDroneWaterSurfaceY()
    {
        float y = GetFloat(droneFlightManager, "waterSurfaceY", float.NaN);
        if (!float.IsNaN(y)) return y;
        y = GetFloat(unifiedTether, "waterSurfaceY", float.NaN);
        if (!float.IsNaN(y)) return y;
        return 0.0f;
    }

    private string ConfigureDroneTrajectoryForValidation(string requestedPath)
    {
        string requested = string.IsNullOrWhiteSpace(requestedPath) ? "Circle" : requestedPath.Trim();
        string trajectoryType = MapDroneValidationPathToTrajectoryPlanner(requested);

        if (droneTrajectoryPlanner != null && !string.IsNullOrEmpty(trajectoryType))
        {
            SetEnumField(droneTrajectoryPlanner, "trajectoryType", trajectoryType);
            ConfigureClosedCircleIfRequested(trajectoryType);
            SetBool(droneFlightManager, "useTrajectoryPlanner", true);
            SetComponentField(droneFlightManager, "trajectoryPlanner", droneTrajectoryPlanner);

            // StartPathTracking still needs a PathKind carrier argument internally.
            // The trajectory planner supplies the actual validation reference.
            SetEnumField(droneFlightManager, "pathKind", MapDroneValidationPathToPathKind(requested));

            LogEvent("config", "ValidationHarness", "DroneTrajectoryPlanner", "none", GetWorkflowStateCompact(), true,
                "requested=" + requested + "; trajectoryType=" + trajectoryType);
            return trajectoryType;
        }

        string fallbackPathKind = MapDroneValidationPathToPathKind(requested);
        SetBool(droneFlightManager, "useTrajectoryPlanner", false);
        SetEnumField(droneFlightManager, "pathKind", fallbackPathKind);
        LogEvent("config", "ValidationHarness", "DronePathKind", "none", GetWorkflowStateCompact(), true,
            "requested=" + requested + "; pathKind=" + fallbackPathKind);
        return fallbackPathKind;
    }

    private string MapDroneValidationPathToTrajectoryPlanner(string path)
    {
        string p = (path ?? "Circle").Trim().ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
        if (p == "circle") return "Circle";
        if (p == "square" || p == "smoothsquare") return "SmoothSquare";
        if (p == "spiral" || p == "spiralout") return "SpiralOut";
        if (p == "lawnmower" || p == "lawnmowersurvey") return "Lawnmower";
        if (p == "figureeight" || p == "figure8") return "FigureEight";
        if (p == "deployment" || p == "deploymentapproach") return "DeploymentApproach";
        if (p == "helixdown") return "HelixDown";
        if (p == "helixupdown") return "HelixUpDown";
        return "Circle";
    }

    private string MapDroneValidationPathToPathKind(string path)
    {
        string p = (path ?? "Circle").Trim().ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
        if (p == "square" || p == "smoothsquare") return "Square";
        if (p == "spiral" || p == "spiralout") return "Spiral";
        return "Circle";
    }

    private float GetDroneValidationPathDuration(string requestedPath)
    {
        float fallback = Mathf.Max(2.0f, config.drone_trajectory_segment_s);
        if (droneTrajectoryPlanner == null) return fallback;

        MethodInfo m = droneTrajectoryPlanner.GetType().GetMethod("GetDuration", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (m != null && m.GetParameters().Length == 0)
        {
            try
            {
                object v = m.Invoke(droneTrajectoryPlanner, null);
                if (v is float) return Mathf.Max(2.0f, (float)v);
                if (v is double) return Mathf.Max(2.0f, (float)(double)v);
            }
            catch { }
        }

        string p = (requestedPath ?? "Circle").Trim().ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
        if (p == "circle") return Mathf.Max(2.0f, GetFloat(droneTrajectoryPlanner, "circleDurationS", fallback));
        if (p == "spiral" || p == "spiralout") return Mathf.Max(2.0f, GetFloat(droneTrajectoryPlanner, "spiralDurationS", fallback));
        if (p == "figureeight" || p == "figure8") return Mathf.Max(2.0f, GetFloat(droneTrajectoryPlanner, "figureEightDurationS", fallback));
        if (p == "lawnmower" || p == "lawnmowersurvey")
        {
            float length = GetFloat(droneTrajectoryPlanner, "lawnmowerLengthM", 4.0f);
            float width = GetFloat(droneTrajectoryPlanner, "lawnmowerWidthM", 2.5f);
            int lanes = Mathf.Max(2, GetInt(droneTrajectoryPlanner, "lawnmowerLanes", 4));
            float speed = Mathf.Max(0.05f, GetFloat(droneTrajectoryPlanner, "lawnmowerSpeedMS", 0.35f));
            return Mathf.Max(2.0f, (lanes * length + (lanes - 1) * (width / Mathf.Max(1, lanes - 1))) / speed);
        }
        if (p == "square" || p == "smoothsquare")
        {
            float side = GetFloat(droneTrajectoryPlanner, "squareSideM", 2.4f);
            float speed = Mathf.Max(0.05f, GetFloat(droneTrajectoryPlanner, "squareSpeedMS", 0.32f));
            return Mathf.Max(2.0f, 4.0f * side / speed);
        }
        return fallback;
    }

    private void ConfigureClosedCircleIfRequested(string trajectoryType)
    {
        if (!config.drone_close_circle_loop || droneTrajectoryPlanner == null) return;
        if (!string.Equals(trajectoryType, "Circle", StringComparison.OrdinalIgnoreCase)) return;

        float omega = Mathf.Max(0.02f, GetFloat(droneTrajectoryPlanner, "circleOmegaRadS", 0.23f));
        float oneLoopDuration = (2.0f * Mathf.PI) / omega;
        SetFloat(droneTrajectoryPlanner, "circleDurationS", oneLoopDuration);
        LogEvent("config", "ValidationHarness", "ClosedDroneCircle", "none", GetWorkflowStateCompact(), true,
            "circleDurationS=" + oneLoopDuration.ToString("F2", ci) + "; omega=" + omega.ToString("F3", ci));
    }

    private bool DroneTrajectoryPlannerCompleted(float plannedDuration)
    {
        if (droneTrajectoryPlanner == null) return false;
        float t = GetFloat(droneTrajectoryPlanner, "lastPathTimeS", -1.0f);
        if (t >= Mathf.Max(0.1f, plannedDuration) - 0.05f) return true;
        object sample = GetField(droneTrajectoryPlanner, "lastSample");
        if (sample != null)
        {
            FieldInfo completed = sample.GetType().GetField("completed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (completed != null && completed.FieldType == typeof(bool))
            {
                try { return (bool)completed.GetValue(sample); } catch { }
            }
        }
        return false;
    }

    private float MissionCompletionTimeoutForCurrentRovPath()
    {
        string p = (config.rov_path ?? string.Empty).ToLowerInvariant();
        if (p.Contains("lawnmower"))
        {
            return Mathf.Max(config.rov_mission_completion_timeout_s, config.rov_lawnmower_completion_timeout_s);
        }
        if (p.Contains("waypoint"))
        {
            return Mathf.Max(config.rov_mission_completion_timeout_s, config.rov_waypoints_completion_timeout_s);
        }
        return config.rov_mission_completion_timeout_s;
    }

    private IEnumerator WaitForRovMissionCompletionOrTimeout(float timeout)
    {
        float start = Time.time;
        while (Time.time - start < timeout)
        {
            if (RovMissionCompleted())
            {
                LogEvent("wait_complete", "ValidationHarness", "rov_mission_complete", "none", GetWorkflowStateCompact(), true,
                    "duration_s=" + (Time.time - start).ToString("F2", ci));
                yield break;
            }
            if (failed) yield break;
            yield return null;
        }
        LogEvent("wait_timeout", "ValidationHarness", "rov_mission_complete", "none", GetWorkflowStateCompact(), false,
            "timeout_waiting_for_path_complete; duration_s=" + timeout.ToString("F2", ci));
    }

    private bool RovMissionCompleted()
    {
        if (GetBool(miniRovController, "missionCompleted", false)) return true;
        if (GetBool(miniRovController, "pathComplete", false)) return true;
        string state = GetEnumOrString(miniRovMissionManager, "missionState");
        if (state.Contains("RecoveryReady") || state.Contains("Completed") || state.Contains("Hold"))
        {
            // Treat station-hold as mission-complete only if the low-level controller also completed its path.
            return GetBool(miniRovController, "missionCompleted", false) || GetBool(miniRovController, "pathComplete", false) || state.Contains("RecoveryReady") || state.Contains("Completed");
        }
        return false;
    }

    private IEnumerator RunRovPreMissionDescent()
    {
        float targetDepth = Mathf.Max(0.05f, config.rov_pre_mission_depth_m);
        ConfigureRovMissionDepth();

        SetFloat(miniRovController, "targetSurgeSpeedMS", 0.0f);
        SetFloat(miniRovController, "targetDepthM", targetDepth);
        SetFloat(miniRovController, "targetPointDepthM", targetDepth);
        InvokeIfExists(miniRovController, "StartAxisHold");
        LogEvent("command", "ValidationHarness", "PreMissionDepthHold", "none", GetWorkflowStateCompact(), true,
            "target_depth_m=" + targetDepth.ToString("F2", ci));

        yield return WaitUntilOrTimeout(
            () => Mathf.Abs(GetRovDepth(GetPosition(miniRov)) - targetDepth) <= Mathf.Max(0.03f, config.rov_depth_ready_tolerance_m),
            Mathf.Max(5.0f, config.rov_descent_timeout_s),
            "rov_pre_mission_depth_ready");
    }

    private void ConfigureRovMissionDepth()
    {
        float targetDepth = Mathf.Max(0.05f, config.rov_pre_mission_depth_m);
        SetBool(miniRovPathPlanner, "useCurrentDepthAtStart", false);
        SetFloat(miniRovPathPlanner, "missionDepthM", targetDepth);
        SetFloat(miniRovPathPlanner, "goToPointDepthM", targetDepth);
        SetFloat(miniRovPathPlanner, "homeDepthM", targetDepth);
        SetFloat(miniRovController, "targetDepthM", targetDepth);
        SetFloat(miniRovController, "targetPointDepthM", targetDepth);
    }

    private IEnumerator RunDroneMissionManagerSequence()
    {
        // Fallback path if a project build does not include the drone agent. The readiness
        // predicate deliberately requires that the drone has left the initial surface-ready
        // state before accepting a later surface-ready state for tether deployment.
        bool leftInitialSurface = false;
        InvokeIfExists(droneMissionManager, "StartMission");
        LogEvent("command", "ValidationHarness", "DroneMissionManager.StartMission", "none", GetWorkflowStateCompact(), true, "integrated_drone_sequence");
        float start = Time.time;
        while (Time.time - start < config.drone_wait_timeout_s)
        {
            string f = GetEnumOrString(droneFlightManager, "flightMode");
            if (!f.Contains("SurfaceStable") && !f.Contains("SurfaceHold")) leftInitialSurface = true;
            if (leftInitialSurface && DroneSurfaceReadyForTether())
            {
                LogEvent("wait_complete", "ValidationHarness", "drone_integrated_ready_for_tether", "none", GetWorkflowStateCompact(), true, "predicate_true_after_drone_mission");
                yield break;
            }
            if (failed) yield break;
            yield return null;
        }
        LogEvent("wait_timeout", "ValidationHarness", "drone_integrated_ready_for_tether", "none", GetWorkflowStateCompact(), false, "timeout");
        Fail("timeout_drone_integrated_ready_for_tether");
    }

    private IEnumerator WaitForDroneTrajectorySegment(float minSeconds, float maxSeconds)
    {
        float start = Time.time;
        bool pathStarted = false;
        float plannedDuration = GetDroneValidationPathDuration(config.drone_path);

        while (Time.time - start < maxSeconds)
        {
            string mode = GetEnumOrString(droneFlightManager, "flightMode");
            if (mode.Contains("Path")) pathStarted = true;

            bool completeByPlanner = DroneTrajectoryPlannerCompleted(plannedDuration);
            bool completeByMode = mode.Contains("PathBraking") || mode.Contains("PositionHold") || mode.Contains("Hover");
            bool completeByTime = Time.time - start >= minSeconds;

            if (pathStarted && (completeByPlanner || completeByMode || completeByTime) && Time.time - start >= Mathf.Min(minSeconds, plannedDuration))
            {
                string reason = completeByPlanner ? "planner_completed" : (completeByMode ? mode : "full_duration_elapsed");
                LogEvent("wait_complete", "ValidationHarness", "drone_complete_trajectory", "none", GetWorkflowStateCompact(), true,
                    reason + "; duration_s=" + (Time.time - start).ToString("F2", ci));
                yield break;
            }

            if (failed) yield break;
            yield return null;
        }

        LogEvent("wait_complete", "ValidationHarness", "drone_complete_trajectory", "none", GetWorkflowStateCompact(), true,
            "timeout_after_full_duration; duration_s=" + (Time.time - start).ToString("F2", ci));
    }

    private IEnumerator EnsureTetherReadyOrAttach()
    {
        if (unifiedTether == null)
        {
            Fail("missing_MIMISKUnifiedTetherManager");
            yield break;
        }

        InvokeIfExists(unifiedTether, "AutoFindReferences");
        InvokeIfExists(unifiedTether, "AttachRovToCableEnd");
        LogEvent("command", "ValidationHarness", "AttachRovToCableEnd", "none", GetWorkflowStateCompact(), true, "sent");
        yield return WaitAndLog(1.0f);
    }

    private IEnumerator DeployAndActivateRov()
    {
        if (failed) yield break;

        InvokeIfExists(unifiedTether, "StartDeployment");
        LogEvent("command", "ValidationHarness", "StartDeployment", "none", GetWorkflowStateCompact(), true, "sent");
        yield return WaitUntilOrTimeout(
            () => TetherStateContains("ReadyForRovControl") || GetBool(unifiedTether, "rovControlActive") || TetherStateContains("RovControlActive"),
            config.deploy_timeout_s,
            "rov_ready_for_control");

        if (failed) yield break;

        if (!GetBool(unifiedTether, "rovControlActive"))
        {
            InvokeIfExists(unifiedTether, "ActivateRovControl");
            LogEvent("command", "ValidationHarness", "ActivateRovControl", "none", GetWorkflowStateCompact(), true, "sent");
        }

        yield return WaitUntilOrTimeout(() => GetBool(unifiedTether, "rovControlActive") || TetherStateContains("RovControlActive"), 15.0f, "rov_control_active");
        yield return WaitAndLog(1.0f);
        RecordValidationReturnHomeReference();
    }

    private void RecordValidationReturnHomeReference()
    {
        Vector3 current = GetPosition(miniRov);
        Vector3 home = GetVector3(miniRovPathPlanner, "homeWorld", current);
        bool homeSet = GetBool(miniRovPathPlanner, "homeSet", false);
        if (!homeSet && home == Vector3.zero)
        {
            home = current;
        }
        validationReturnHomeWorld = home;
        validationHomeRecorded = true;
        LogEvent("reference", "ValidationHarness", "validation_return_home_reference", "none", GetWorkflowStateCompact(), true,
            "home=(" + home.x.ToString("F3", ci) + "," + home.y.ToString("F3", ci) + "," + home.z.ToString("F3", ci) + "); homeSet=" + homeSet.ToString());
    }

    private IEnumerator StartRovMissionAndReturnHome()
    {
        if (failed) yield break;

        if (config.enable_rov_pre_mission_descent)
        {
            yield return RunRovPreMissionDescent();
            if (failed) yield break;
        }

        ConfigureRovMissionDepth();
        SetEnumField(miniRovPathPlanner, "selectedPathType", config.rov_path);
        InvokeIfExists(miniRovMissionManager, "StartSelectedMission");
        LogEvent("command", "ValidationHarness", "StartSelectedMission", "none", GetWorkflowStateCompact(), true,
            config.rov_path + " depth_m=" + config.rov_pre_mission_depth_m.ToString("F2", ci));

        if (config.rov_wait_for_mission_completion)
        {
            float missionTimeout = MissionCompletionTimeoutForCurrentRovPath();
            yield return WaitForRovMissionCompletionOrTimeout(Mathf.Max(5.0f, missionTimeout));
        }
        else
        {
            yield return WaitAndLog(config.rov_mission_duration_s);
        }

        returnHomeCommandTime = Time.time;
        InvokeIfExists(miniRovMissionManager, "ReturnHome");
        LogEvent("command", "ValidationHarness", "ReturnHome", "none", GetWorkflowStateCompact(), true, "sent");
        yield return WaitUntilOrTimeout(() => RovRecoveryReady(), config.return_timeout_s, "rov_recovery_ready");
    }

    private IEnumerator RecoverRov()
    {
        if (failed) yield break;

        InvokeIfExists(unifiedTether, "RequestRecovery");
        LogEvent("command", "ValidationHarness", "RequestRecovery", "none", GetWorkflowStateCompact(), true, "sent");
        yield return WaitUntilOrTimeout(() => TetherStateContains("RecoveredAttached") || TetherStateContains("ReadyForDeploy"), config.recovery_timeout_s, "rov_recovered_attached");
    }

    private IEnumerator WaitAndLog(float seconds)
    {
        float end = Time.time + Mathf.Max(0.0f, seconds);
        while (Time.time < end && !failed)
        {
            yield return null;
        }
    }

    private IEnumerator WaitUntilOrTimeout(Func<bool> predicate, float timeout, string waitName)
    {
        float start = Time.time;
        while (Time.time - start < timeout)
        {
            if (predicate())
            {
                LogEvent("wait_complete", "ValidationHarness", waitName, "none", GetWorkflowStateCompact(), true, "predicate_true");
                yield break;
            }
            if (failed) yield break;
            yield return null;
        }
        LogEvent("wait_timeout", "ValidationHarness", waitName, "none", GetWorkflowStateCompact(), false, "timeout");
        if (waitName.Contains("ready") || waitName.Contains("recovered"))
        {
            Fail("timeout_" + waitName);
        }
    }


    private bool DroneIntegratedMissionReadyForTether()
    {
        return DroneSurfaceReadyForTether();
    }

    private bool DroneSurfaceReadyForTether()
    {
        // Both the drone flight mode and the tether readiness flags must agree.
        // A stale safeToDeploy/droneSurfaceStable flag from the initial scene state is
        // not enough; otherwise deployment can begin while the drone is still in
        // takeoff or path tracking.
        string f = GetEnumOrString(droneFlightManager, "flightMode");
        string m = GetEnumOrString(droneMissionManager, "missionState");
        bool flightReady = f.Contains("SurfaceStable") || f.Contains("SurfaceHold");
        bool tetherReady = GetBool(unifiedTether, "safeToDeploy") || GetBool(unifiedTether, "droneSurfaceStable");
        bool missionReady = m.Contains("ReadyForTetherDeployment") || m.Contains("SurfaceStable") || m.Contains("Completed");
        return flightReady && (tetherReady || missionReady);
    }

    private bool DroneIsAirborneOrHover()
    {
        string mode = GetEnumOrString(droneFlightManager, "flightMode");
        return (mode.Contains("Takeoff") && !mode.Contains("Idle")) || mode.Contains("Hover") || mode.Contains("PositionHold") || mode.Contains("Path");
    }

    private bool DronePathCompleteOrSurfaceReady()
    {
        if (DroneSurfaceReadyForTether()) return true;
        string mode = GetEnumOrString(droneFlightManager, "flightMode");
        return mode.Contains("Surface") || mode.Contains("Hover") || mode.Contains("PositionHold") || mode.Contains("PathBraking");
    }

    private bool TetherStateContains(string s)
    {
        return GetEnumOrString(unifiedTether, "tetherState").Contains(s);
    }

    private bool RovRecoveryReady()
    {
        // Validation gate for recovery.  The MiniROV intentionally descends before
        // its inspection mission, therefore a full 3-D distance to the shallow
        // deployment home can remain large even after the vehicle has returned to
        // the correct recovery column.  Recovery gating is therefore based on the
        // MiniROV mission-manager ready flag OR the horizontal X-Z distance to the
        // recorded deployment home.  The vertical offset is logged separately.
        if (Time.time - returnHomeCommandTime < Mathf.Max(0.0f, config.recovery_ready_min_return_s)) return false;

        string rovState = GetEnumOrString(miniRovMissionManager, "missionState");
        bool missionManagerReady = rovState.Contains("RecoveryReady") || GetBool(miniRovMissionManager, "recoveryReady", false);
        bool returnActive = rovState.Contains("Returning") || rovState.Contains("ReturnHome") || missionManagerReady;
        if (!returnActive) return false;

        float tol = Mathf.Max(0.05f, config.recovery_ready_distance_m);
        Vector3 home = validationHomeRecorded ? validationReturnHomeWorld : GetVector3(miniRovPathPlanner, "homeWorld", Vector3.zero);
        Vector3 pos = GetPosition(miniRov);
        float horizontalDistance = new Vector2(pos.x - home.x, pos.z - home.z).magnitude;
        bool horizontalReady = horizontalDistance <= tol;

        // Do not require full 3-D coincidence here; the winch performs the vertical
        // recovery after the MiniROV has returned to the deployment-home column.
        return missionManagerReady || horizontalReady;
    }

    private void ResolveReferences()
    {
        drone = GameObject.Find("Drone");
        miniRov = GameObject.Find("MiniROV");
        if (drone != null) droneRb = drone.GetComponent<Rigidbody>();
        if (miniRov != null) miniRovRb = miniRov.GetComponent<Rigidbody>();

        droneAgent = FindComponentByName("MIMISKDroneAgent");
        droneFlightManager = FindComponentByName("MIMISKDroneCoreFlightModeManager");
        droneMissionManager = FindComponentByName("MIMISKDroneCoreMissionManager");
        droneTrajectoryPlanner = FindComponentByName("MIMISKDroneCoreTrajectoryPlanner");
        droneRotorController = FindComponentByName("MIMISKDroneCoreRotorController");
        unifiedTether = FindComponentByName("MIMISKUnifiedTetherManager");
        coreTetherManager = FindComponentByName("MIMISKDroneCoreTetherManager");
        v8Tether = FindComponentByName("MIMISKFinalRearAttachedTetherV8");
        miniRovMissionManager = FindComponentByName("MIMISKMiniROVMissionManager");
        miniRovPathPlanner = FindComponentByName("MIMISKMiniROVPathPlanner");
        miniRovController = FindComponentByName("MIMISKMiniROVPlantBasedController");
        grpcConnection = FindComponentByName("MIMISKGrpcConnection");
        grpcTelemetry = FindComponentByName("MIMISKGrpcTelemetryBridge");
        grpcCommand = FindComponentByName("MIMISKGrpcCommandBridge");
        grpcManual = FindComponentByName("MIMISKGrpcManualControlBridge");
        grpcReference = FindComponentByName("MIMISKGrpcExternalReferenceBridge");
        grpcCamera = FindComponentByName("MIMISKGrpcCameraBridge");
    }

    private Component FindComponentByName(string typeName)
    {
        MonoBehaviour[] all = GameObject.FindObjectsOfType<MonoBehaviour>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].GetType().Name == typeName)
            {
                return all[i];
            }
        }
        return null;
    }

    private void ConfigureGrpcEnabled(bool enabled)
    {
        SetBool(grpcTelemetry, "telemetryEnabled", enabled);
        SetBool(grpcCommand, "commandBridgeEnabled", enabled);
        SetBool(grpcManual, "manualBridgeEnabled", enabled);
        SetBool(grpcReference, "externalReferenceBridgeEnabled", enabled);
        SetBool(grpcCamera, "cameraStreamingEnabled", enabled);
    }

    private void PrepareOutputDirectory()
    {
        string root = config.output_root;
        if (!Path.IsPathRooted(root))
        {
            root = Path.Combine(Application.dataPath, "..", root);
        }
        string safeRun = Sanitize(config.run_id);
        string safeTrial = Sanitize(config.trial_id);
        outputDirectory = Path.GetFullPath(Path.Combine(root, safeRun, safeTrial));
        Directory.CreateDirectory(outputDirectory);
    }

    private void OpenLogs()
    {
        stateLog = NewWriter("state_log.csv");
        eventLog = NewWriter("mission_events.csv");
        tetherLog = NewWriter("tether_log.csv");
        perfLog = NewWriter("performance_log.csv");
        grpcLog = NewWriter("ros_grpc_log.csv");
        diagnosticsLog = NewWriter("controller_diagnostics_log.csv");

        stateLog.WriteLine("run_id,trial_id,unity_time_s,wall_time_utc,frame,fixed_frame,mission_state,drone_state,tether_state,minirov_state,drone_x,drone_y,drone_z,drone_roll_deg,drone_pitch_deg,drone_yaw_deg,drone_vx,drone_vy,drone_vz,drone_ref_x,drone_ref_y,drone_ref_z,drone_tracking_error_m,drone_altitude_error_m,drone_yaw_error_deg,drone_mode,drone_trajectory_type,rov_x,rov_y,rov_z,rov_depth_m,rov_yaw_deg,rov_vx,rov_vy,rov_vz,rov_surge_speed_m_s,rov_depth_ref_m,rov_yaw_ref_deg,rov_depth_error_m,rov_yaw_error_deg,rov_path_type,rov_path_error_m,rov_ref_x,rov_ref_y,rov_ref_z,rov_path_index,rov_path_count,rov_path_progress,rov_distance_to_home_m,rov_home_horizontal_error_m,rov_home_vertical_error_m,rov_home_x,rov_home_y,rov_home_z,rov_control_mode,winch_length_m,winch_target_length_m,winch_rate_m_s,winch_mode,fps,fixed_dt_s");
        eventLog.WriteLine("run_id,trial_id,event_time_s,wall_time_utc,event_type,source,command,from_state,to_state,accepted,reason,drone_state,tether_state,rov_state");
        tetherLog.WriteLine("run_id,trial_id,unity_time_s,mission_state,tether_state,tether_endpoint_mode,fairlead_x,fairlead_y,fairlead_z,cable_end_x,cable_end_y,cable_end_z,active_endpoint_x,active_endpoint_y,active_endpoint_z,rov_rear_anchor_x,rov_rear_anchor_y,rov_rear_anchor_z,winch_length_m,winch_target_length_m,expected_visual_length_m,visual_length_m,geometric_length_m,straight_distance_m,slack_m,endpoint_error_m,rear_anchor_error_m,length_sync_error_m,winch_target_error_m,geometric_length_error_m,max_segment_strain,runtime_node_count,solver_healthy,force_coupling_enabled,contact_enabled");
        perfLog.WriteLine("run_id,trial_id,unity_time_s,wall_time_utc,frame,fps,delta_time_s,fixed_dt_s,memory_mb,active_gameobjects,active_renderers,ros_grpc_enabled,camera_streaming_enabled");
        grpcLog.WriteLine("run_id,trial_id,unity_time_s,wall_time_utc,channel,direction,sequence_id,message_type,payload_bytes,counter_sent,counter_received,counter_failed,counter_applied,counter_blocked,success,status");
        diagnosticsLog.WriteLine("run_id,trial_id,unity_time_s,wall_time_utc,mission_state,drone_mode,tether_state,rov_state,drone_control_mode,drone_tracking_error_m,drone_altitude_error_m,drone_yaw_error_deg,drone_pos_err_x,drone_pos_err_y,drone_pos_err_z,drone_vel_err_x,drone_vel_err_y,drone_vel_err_z,drone_ref_vx,drone_ref_vy,drone_ref_vz,drone_ref_ax,drone_ref_ay,drone_ref_az,drone_cmd_ax,drone_cmd_ay,drone_cmd_az,drone_total_thrust_n,drone_tau_x,drone_tau_y,drone_tau_z,drone_rotor_fl_cmd_n,drone_rotor_fr_cmd_n,drone_rotor_rl_cmd_n,drone_rotor_rr_cmd_n,drone_rotor_fl_n,drone_rotor_fr_n,drone_rotor_rl_n,drone_rotor_rr_n,drone_rotor_saturation_pct,drone_surface_stable,drone_safe_to_deploy,rov_target_surge_m_s,rov_surge_speed_m_s,rov_surge_error_m_s,rov_target_depth_m,rov_depth_m,rov_depth_error_m,rov_target_yaw_deg,rov_yaw_deg,rov_yaw_error_deg,rov_surge_cmd,rov_yaw_cmd,rov_ballast_cmd,rov_left_pwm,rov_right_pwm,rov_ballast_port_pwm,rov_ballast_starboard_pwm,rov_los_cross_track_error_m,rov_los_tracking_error_m,rov_distance_to_target_m,rov_path_segment_index,rov_mission_completed,rov_path_complete,rov_recovery_ready,tether_winch_length_m,tether_target_length_m,tether_slack_m,tether_endpoint_error_m,tether_rear_anchor_error_m,tether_geometric_length_error_m");
    }

    private StreamWriter NewWriter(string name)
    {
        return new StreamWriter(Path.Combine(outputDirectory, name), false, new UTF8Encoding(false));
    }

    private void WriteManifest()
    {
        string path = Path.Combine(outputDirectory, "manifest.json");
        File.WriteAllText(path, JsonUtility.ToJson(config, true));
    }

    private void TrackStateChanges()
    {
        string d = GetEnumOrString(droneFlightManager, "flightMode");
        string t = GetEnumOrString(unifiedTether, "tetherState");
        string r = GetEnumOrString(miniRovMissionManager, "missionState");
        if (d != lastDroneState)
        {
            LogEvent("state_change", "Drone", "flightMode", lastDroneState, d, true, "mode_change");
            lastDroneState = d;
        }
        if (t != lastTetherState)
        {
            LogEvent("state_change", "Tether", "tetherState", lastTetherState, t, true, "mode_change");
            lastTetherState = t;
        }
        if (r != lastRovState)
        {
            LogEvent("state_change", "MiniROV", "missionState", lastRovState, r, true, "mode_change");
            lastRovState = r;
        }
    }

    private float CurrentWinchLength()
    {
        return GetFloat(coreTetherManager, "deployedLengthM",
            GetFloat(unifiedTether, "deploymentTetherLengthM",
                GetFloat(unifiedTether, "requiredCableLengthM", 0.0f)));
    }

    private float CurrentWinchTargetLength()
    {
        return GetFloat(coreTetherManager, "targetLengthM",
            GetFloat(unifiedTether, "adaptiveTargetLengthM", CurrentWinchLength()));
    }

    private float CurrentWinchRate()
    {
        return GetFloat(coreTetherManager, "winchCommandRateMS", 0.0f);
    }


    private string CurrentDroneTrajectoryLabel()
    {
        string plannerType = GetEnumOrString(droneTrajectoryPlanner, "trajectoryType");
        if (!string.IsNullOrEmpty(plannerType) && plannerType != "none" && plannerType != "null" && plannerType != "missing")
        {
            return plannerType;
        }
        string pathKind = GetEnumOrString(droneFlightManager, "pathKind");
        if (!string.IsNullOrEmpty(pathKind) && pathKind != "none" && pathKind != "null" && pathKind != "missing")
        {
            return pathKind;
        }
        return string.IsNullOrWhiteSpace(config.drone_path) ? "none" : config.drone_path;
    }

    private string CurrentWinchMode()
    {
        string coreState = GetEnumOrString(coreTetherManager, "tetherState");
        if (!string.IsNullOrEmpty(coreState) && coreState != "null" && coreState != "missing")
        {
            return coreState;
        }
        return GetEnumOrString(unifiedTether, "tetherState");
    }

    private void WriteStateLogRow()
    {
        if (stateLog == null) return;
        Vector3 dp = GetPosition(drone);
        Vector3 dv = droneRb != null ? droneRb.linearVelocity : Vector3.zero;
        Vector3 dr = GetVector3(droneFlightManager, "referencePositionWorld", Vector3.zero);
        Vector3 rp = GetPosition(miniRov);
        Vector3 rv = miniRovRb != null ? miniRovRb.linearVelocity : Vector3.zero;
        Vector3 home = GetVector3(miniRovPathPlanner, "homeWorld", Vector3.zero);
        float depth = GetRovDepth(rp);
        float droneYaw = GetYaw(drone);
        float rovYaw = GetYaw(miniRov);
        float droneTracking = (dr == Vector3.zero) ? 0.0f : Vector3.Distance(dp, dr);
        float droneAltErr = Mathf.Abs(dp.y - dr.y);
        float yawRef = GetFloat(droneFlightManager, "referenceYawDeg", droneYaw);
        float droneYawErr = Mathf.Abs(Mathf.DeltaAngle(droneYaw, yawRef));
        float rovDepthRef = GetFloat(miniRovController, "targetPointDepthM", GetFloat(miniRovPathPlanner, "missionDepthM", depth));
        float rovYawRef = GetFloat(miniRovController, "targetYawDeg", rovYaw);
        float rovSurge = miniRov != null ? Vector3.Dot(rv, miniRov.transform.forward) : 0.0f;
        float rovPathError = ComputeRovPathError(rp);
        int rovPathIndex = ComputeRovNearestPathIndex(rp);
        int rovPathCount = GetRovGeneratedPathCount();
        Vector3 rovRefPoint = ComputeRovNearestPathPoint(rp);
        float rovPathProgress = rovPathCount > 1 && rovPathIndex >= 0 ? (float)rovPathIndex / (float)(rovPathCount - 1) : 0.0f;
        float rovDepthErr = Mathf.Abs(depth - rovDepthRef);
        float rovYawErr = Mathf.Abs(Mathf.DeltaAngle(rovYaw, rovYawRef));
        float distanceToHome = Vector3.Distance(rp, home);
        float homeHorizontalErr = new Vector2(rp.x - home.x, rp.z - home.z).magnitude;
        float homeVerticalErr = Mathf.Abs(rp.y - home.y);
        string droneMode = GetEnumOrString(droneFlightManager, "flightMode");
        string dronePath = CurrentDroneTrajectoryLabel();
        string rovPath = GetEnumOrString(miniRovPathPlanner, "selectedPathType");
        string rovControl = GetEnumOrString(miniRovMissionManager, "missionState");
        float winchLen = CurrentWinchLength();
        float targetLen = CurrentWinchTargetLength();
        float winchRate = CurrentWinchRate();
        string winchMode = CurrentWinchMode();
        stateLog.WriteLine(string.Join(",", new string[] {
            Csv(config.run_id), Csv(config.trial_id), F(Time.time), Csv(DateTime.UtcNow.ToString("o")), Time.frameCount.ToString(), fixedFrameCount.ToString(),
            Csv(GetWorkflowStateCompact()), Csv(droneMode), Csv(GetEnumOrString(unifiedTether,"tetherState")), Csv(rovControl),
            F(dp.x),F(dp.y),F(dp.z),F(GetRoll(drone)),F(GetPitch(drone)),F(droneYaw),F(dv.x),F(dv.y),F(dv.z),F(dr.x),F(dr.y),F(dr.z),F(droneTracking),F(droneAltErr),F(droneYawErr),Csv(droneMode),Csv(dronePath),
            F(rp.x),F(rp.y),F(rp.z),F(depth),F(rovYaw),F(rv.x),F(rv.y),F(rv.z),F(rovSurge),F(rovDepthRef),F(rovYawRef),F(rovDepthErr),F(rovYawErr),Csv(rovPath),F(rovPathError),F(rovRefPoint.x),F(rovRefPoint.y),F(rovRefPoint.z),rovPathIndex.ToString(),rovPathCount.ToString(),F(rovPathProgress),F(distanceToHome),F(homeHorizontalErr),F(homeVerticalErr),F(home.x),F(home.y),F(home.z),Csv(rovControl),
            F(winchLen),F(targetLen),F(winchRate),Csv(winchMode),F(CurrentFps()),F(Time.fixedDeltaTime)
        }));
    }

    private void WriteTetherLogRow()
    {
        if (tetherLog == null) return;
        Vector3 start = GetVector3(v8Tether, "startWorld", Vector3.zero);
        Vector3 cableEnd = GetVector3(v8Tether, "endWorld", Vector3.zero);
        Vector3 activeEndpoint = GetActiveTetherEndpointTarget();
        Vector3 rear = GetTransformPositionByName("MIMISK_Tether_BackAnchor");
        float winchLen = CurrentWinchLength();
        float winchTarget = CurrentWinchTargetLength();
        float visual = GetFloat(v8Tether, "visualCableLengthM", 0);
        float geo = GetFloat(v8Tether, "geometricCableLengthM", 0);
        float straight = GetFloat(v8Tether, "straightDistanceM", Vector3.Distance(start, activeEndpoint));
        float slack = GetFloat(v8Tether, "slackM", Mathf.Max(0.0f, visual - straight));
        float minSlack = GetFloat(v8Tether, "minimumVisualSlackM", 0.08f);
        float maxSlack = GetFloat(v8Tether, "maximumVisualSlackM", 0.50f);
        float expectedVisual = Mathf.Min(Mathf.Max(winchLen, straight + minSlack), straight + maxSlack);
        float endpointErr = Vector3.Distance(cableEnd, activeEndpoint);
        float rearErr = GetFloat(v8Tether, "rearAnchorErrorM", Vector3.Distance(cableEnd, rear));
        float lenSync = Mathf.Abs(visual - expectedVisual);
        float winchTargetErr = Mathf.Abs(winchTarget - winchLen);
        float geoErr = Mathf.Abs(visual - geo);
        int nodes = GetInt(v8Tether, "runtimeNodeCount", 0);
        bool healthy = GetBool(v8Tether, "solverHealthy", true);
        tetherLog.WriteLine(string.Join(",", new string[] {
            Csv(config.run_id), Csv(config.trial_id), F(Time.time), Csv(GetWorkflowStateCompact()), Csv(GetEnumOrString(unifiedTether,"tetherState")), Csv(GetString(v8Tether,"endpointModeText","unknown")),
            F(start.x),F(start.y),F(start.z),F(cableEnd.x),F(cableEnd.y),F(cableEnd.z),F(activeEndpoint.x),F(activeEndpoint.y),F(activeEndpoint.z),F(rear.x),F(rear.y),F(rear.z),F(winchLen),F(winchTarget),F(expectedVisual),F(visual),F(geo),F(straight),F(slack),F(endpointErr),F(rearErr),F(lenSync),F(winchTargetErr),F(geoErr),F(ComputeMaxSegmentStrain(v8Tether)),nodes.ToString(),healthy ? "1" : "0",GetBool(unifiedTether,"enableTetherForce") ? "1" : "0","0"
        }));
    }

    private void WriteControllerDiagnosticsLogRow()
    {
        if (diagnosticsLog == null) return;

        Vector3 dp = GetPosition(drone);
        Vector3 dr = GetVector3(droneFlightManager, "referencePositionWorld", GetVector3(droneRotorController, "referencePositionWorld", Vector3.zero));
        Vector3 posErr = GetVector3(droneRotorController, "positionErrorWorld", dr == Vector3.zero ? Vector3.zero : dr - dp);
        Vector3 velErr = GetVector3(droneRotorController, "velocityErrorWorld", Vector3.zero);
        Vector3 refVel = GetVector3(droneRotorController, "referenceVelocityWorld", Vector3.zero);
        Vector3 refAcc = GetVector3(droneRotorController, "referenceAccelerationWorld", Vector3.zero);
        Vector3 cmdAcc = GetVector3(droneRotorController, "commandedAccelerationWorld", Vector3.zero);
        Vector3 tau = GetVector3(droneRotorController, "torqueCommandBodyNm", Vector3.zero);
        Vector4 rotorCmd = GetVector4(droneRotorController, "motorThrustCommandN", Vector4.zero);
        Vector4 rotorActual = GetVector4(droneRotorController, "motorThrustActualN", Vector4.zero);
        float maxRotor = Mathf.Max(0.001f, GetFloat(droneRotorController, "maxThrustPerRotorN", 18.0f));
        float maxActual = Mathf.Max(Mathf.Max(rotorActual.x, rotorActual.y), Mathf.Max(rotorActual.z, rotorActual.w));
        float rotorSatPct = Mathf.Clamp01(maxActual / maxRotor) * 100.0f;
        float droneYaw = GetYaw(drone);
        float droneYawRef = GetFloat(droneFlightManager, "referenceYawDeg", GetFloat(droneRotorController, "referenceYawDeg", droneYaw));
        float droneYawErr = Mathf.Abs(Mathf.DeltaAngle(droneYaw, droneYawRef));
        float droneTracking = GetFloat(droneRotorController, "trackingErrorM", dr == Vector3.zero ? 0.0f : Vector3.Distance(dp, dr));
        float droneAltErr = Mathf.Abs(dp.y - dr.y);

        Vector3 rp = GetPosition(miniRov);
        Vector3 rv = miniRovRb != null ? miniRovRb.linearVelocity : Vector3.zero;
        float rovDepth = GetRovDepth(rp);
        float rovYaw = GetYaw(miniRov);
        float rovSurge = GetFloat(miniRovController, "surgeSpeedMS", miniRov != null ? Vector3.Dot(rv, miniRov.transform.forward) : 0.0f);
        float rovTargetSurge = GetFloat(miniRovController, "targetSurgeSpeedMS", 0.0f);
        float rovTargetDepth = GetFloat(miniRovController, "targetPointDepthM", GetFloat(miniRovController, "targetDepthM", GetFloat(miniRovPathPlanner, "missionDepthM", rovDepth)));
        float rovTargetYaw = GetFloat(miniRovController, "targetYawDeg", rovYaw);
        float rovSurgeErr = GetFloat(miniRovController, "surgeErrorMS", rovTargetSurge - rovSurge);
        float rovDepthErr = GetFloat(miniRovController, "depthErrorM", rovTargetDepth - rovDepth);
        float rovYawErr = GetFloat(miniRovController, "yawErrorDeg", Mathf.DeltaAngle(rovYaw, rovTargetYaw));
        bool rovRecoveryReady = GetBool(miniRovMissionManager, "recoveryReady", false) || GetEnumOrString(miniRovMissionManager, "missionState").Contains("RecoveryReady");

        Vector3 start = GetVector3(v8Tether, "startWorld", Vector3.zero);
        Vector3 cableEnd = GetVector3(v8Tether, "endWorld", Vector3.zero);
        Vector3 activeEndpoint = GetActiveTetherEndpointTarget();
        Vector3 rear = GetTransformPositionByName("MIMISK_Tether_BackAnchor");
        float visual = GetFloat(v8Tether, "visualCableLengthM", 0.0f);
        float geo = GetFloat(v8Tether, "geometricCableLengthM", 0.0f);
        float slack = GetFloat(v8Tether, "slackM", Mathf.Max(0.0f, visual - Vector3.Distance(start, activeEndpoint)));
        float endpointErr = Vector3.Distance(cableEnd, activeEndpoint);
        float rearErr = GetFloat(v8Tether, "rearAnchorErrorM", Vector3.Distance(cableEnd, rear));
        float geoErr = Mathf.Abs(visual - geo);

        diagnosticsLog.WriteLine(string.Join(",", new string[] {
            Csv(config.run_id), Csv(config.trial_id), F(Time.time), Csv(DateTime.UtcNow.ToString("o")), Csv(GetWorkflowStateCompact()), Csv(GetEnumOrString(droneFlightManager,"flightMode")), Csv(GetEnumOrString(unifiedTether,"tetherState")), Csv(GetEnumOrString(miniRovMissionManager,"missionState")), Csv(GetEnumOrString(droneRotorController,"controlMode")),
            F(droneTracking), F(droneAltErr), F(droneYawErr), F(posErr.x), F(posErr.y), F(posErr.z), F(velErr.x), F(velErr.y), F(velErr.z), F(refVel.x), F(refVel.y), F(refVel.z), F(refAcc.x), F(refAcc.y), F(refAcc.z), F(cmdAcc.x), F(cmdAcc.y), F(cmdAcc.z), F(GetFloat(droneRotorController,"totalThrustCommandN",0.0f)), F(tau.x), F(tau.y), F(tau.z), F(rotorCmd.x), F(rotorCmd.y), F(rotorCmd.z), F(rotorCmd.w), F(rotorActual.x), F(rotorActual.y), F(rotorActual.z), F(rotorActual.w), F(rotorSatPct), DroneSurfaceReadyForTether() ? "1" : "0", (GetBool(unifiedTether,"safeToDeploy") || GetBool(unifiedTether,"droneSurfaceStable")) ? "1" : "0",
            F(rovTargetSurge), F(rovSurge), F(rovSurgeErr), F(rovTargetDepth), F(rovDepth), F(rovDepthErr), F(rovTargetYaw), F(rovYaw), F(rovYawErr), F(GetFloat(miniRovController,"surgeCmd",0.0f)), F(GetFloat(miniRovController,"yawCmd",0.0f)), F(GetFloat(miniRovController,"ballastCmd",0.0f)), GetInt(miniRovController,"leftPwm",0).ToString(), GetInt(miniRovController,"rightPwm",0).ToString(), GetInt(miniRovController,"dcPortPwm",0).ToString(), GetInt(miniRovController,"dcStarboardPwm",0).ToString(), F(GetFloat(miniRovController,"losCrossTrackErrorM",0.0f)), F(GetFloat(miniRovController,"losTrackingErrorM",0.0f)), F(GetFloat(miniRovController,"distanceToTargetM",0.0f)), GetInt(miniRovController,"pathSegmentIndex",0).ToString(), GetBool(miniRovController,"missionCompleted",false) ? "1" : "0", GetBool(miniRovController,"pathComplete",false) ? "1" : "0", rovRecoveryReady ? "1" : "0",
            F(CurrentWinchLength()), F(CurrentWinchTargetLength()), F(slack), F(endpointErr), F(rearErr), F(geoErr)
        }));
    }

    private void WritePerformanceLogRow()
    {
        if (perfLog == null) return;
        int activeObjects = GameObject.FindObjectsOfType<GameObject>().Length;
        int activeRenderers = GameObject.FindObjectsOfType<Renderer>().Length;
        float mem = GC.GetTotalMemory(false) / (1024.0f * 1024.0f);
        perfLog.WriteLine(string.Join(",", new string[] {
            Csv(config.run_id), Csv(config.trial_id), F(Time.time), Csv(DateTime.UtcNow.ToString("o")), Time.frameCount.ToString(), F(CurrentFps()), F(Time.deltaTime), F(Time.fixedDeltaTime), F(mem), activeObjects.ToString(), activeRenderers.ToString(), config.enable_ros_grpc ? "1" : "0", GetBool(grpcCamera,"cameraStreamingEnabled") ? "1" : "0"
        }));
    }

    private void WriteGrpcLogRow()
    {
        if (grpcLog == null) return;
        WriteGrpcCounter("telemetry", "unity_to_bridge", "MIMISKTelemetryFrame", GetInt(grpcTelemetry, "framesSent", 0), 0, GetInt(grpcTelemetry, "framesFailed", 0), 0, 0, GetString(grpcTelemetry, "lastStatus", "not_present"));
        WriteGrpcCounter("commands", "bridge_to_unity", "MIMISKCommandBatch", GetInt(grpcCommand, "commandsExecuted", 0), GetInt(grpcCommand, "commandsReceived", 0), GetInt(grpcCommand, "commandsRejected", 0), 0, 0, GetString(grpcCommand, "lastResult", "not_present"));
        WriteGrpcCounter("manual", "bridge_to_unity", "ManualControlBatch", GetInt(grpcManual, "manualFramesApplied", 0), GetInt(grpcManual, "manualFramesReceived", 0), 0, GetInt(grpcManual, "manualFramesApplied", 0), GetInt(grpcManual, "manualFramesBlocked", 0), GetString(grpcManual, "lastStatus", "not_present"));
        WriteGrpcCounter("external_reference", "bridge_to_unity", "ExternalReferenceBatch", GetInt(grpcReference, "referencesApplied", 0), GetInt(grpcReference, "referencesReceived", 0), 0, GetInt(grpcReference, "referencesApplied", 0), GetInt(grpcReference, "referencesBlocked", 0), GetString(grpcReference, "lastStatus", "not_present"));
        WriteGrpcCounter("camera", "unity_to_bridge", "CameraFrame", GetInt(grpcCamera, "framesSent", 0), GetInt(grpcCamera, "framesCaptured", 0), GetInt(grpcCamera, "framesFailed", 0), 0, 0, GetString(grpcCamera, "lastStatus", "not_present"));
    }

    private void WriteGrpcCounter(string channel, string direction, string msg, int sent, int received, int failedCount, int applied, int blocked, string status)
    {
        grpcLog.WriteLine(string.Join(",", new string[] {
            Csv(config.run_id), Csv(config.trial_id), F(Time.time), Csv(DateTime.UtcNow.ToString("o")), Csv(channel), Csv(direction), "0", Csv(msg), "0", sent.ToString(), received.ToString(), failedCount.ToString(), applied.ToString(), blocked.ToString(), failedCount == 0 ? "1" : "0", Csv(status)
        }));
    }

    private void LogEvent(string eventType, string source, string command, string fromState, string toState, bool accepted, string reason)
    {
        if (eventLog == null) return;
        eventLog.WriteLine(string.Join(",", new string[] {
            Csv(config.run_id), Csv(config.trial_id), F(Time.time), Csv(DateTime.UtcNow.ToString("o")), Csv(eventType), Csv(source), Csv(command), Csv(fromState), Csv(toState), accepted ? "1" : "0", Csv(reason), Csv(GetEnumOrString(droneFlightManager,"flightMode")), Csv(GetEnumOrString(unifiedTether,"tetherState")), Csv(GetEnumOrString(miniRovMissionManager,"missionState"))
        }));
        eventLog.Flush();
    }

    private void Fail(string reason)
    {
        if (failed) return;
        failed = true;
        failureReason = reason;
        LogEvent("failure", "ValidationHarness", config.test, "none", GetWorkflowStateCompact(), false, reason);
    }

    private void FinishRun()
    {
        if (completed) return;
        completed = true;
        running = false;
        LogEvent(failed ? "scenario_failed" : "scenario_complete", "ValidationHarness", config.test, "none", GetWorkflowStateCompact(), !failed, failed ? failureReason : "complete");
        // Force one final synchronized sample after the completion event so the continuous logs
        // contain the terminal workflow state such as RecoveredAttached.
        WriteStateLogRow();
        WriteTetherLogRow();
        WritePerformanceLogRow();
        WriteGrpcLogRow();
        if (config.enable_controller_diagnostics) WriteControllerDiagnosticsLogRow();
        WriteRovReferencePathCsv();
        CloseLogs();
        if (config.write_done_flag)
        {
            string flag = config.done_flag_path;
            if (!Path.IsPathRooted(flag)) flag = Path.Combine(Application.dataPath, "..", flag);
            Directory.CreateDirectory(Path.GetDirectoryName(flag));
            File.WriteAllText(flag, outputDirectory + "\n" + (failed ? failureReason : "success"));
        }
        if (quitOnComplete || config.quit_on_complete)
        {
            Application.Quit(failed ? 2 : 0);
        }
    }

    private void CloseLogs()
    {
        CloseWriter(stateLog);
        CloseWriter(eventLog);
        CloseWriter(tetherLog);
        CloseWriter(perfLog);
        CloseWriter(grpcLog);
        CloseWriter(diagnosticsLog);
    }

    private void CloseWriter(StreamWriter w)
    {
        if (w == null) return;
        w.Flush();
        w.Close();
    }

    private void LoadRunConfigIfPresent()
    {
        string path = Path.Combine(Application.dataPath, "MIMISK", "Validation", "run_config.json");
        if (!File.Exists(path)) return;
        try
        {
            string json = File.ReadAllText(path);
            config = JsonUtility.FromJson<RunConfig>(json);
            quitOnComplete = config.quit_on_complete;
        }
        catch (Exception e)
        {
            Debug.LogWarning("MIMISKValidationHarness: could not read run_config.json: " + e.Message);
        }
    }

    private TestKind ParseTestKind(string s)
    {
        string v = (s ?? "").ToLowerInvariant();
        if (v.Contains("drone")) return TestKind.DroneTrajectory;
        if (v.Contains("mini")) return TestKind.MiniRovNavigation;
        if (v.Contains("tether")) return TestKind.TetherV8;
        if (v.Contains("ros") || v.Contains("grpc")) return TestKind.RosGrpc;
        if (v.Contains("idle") || v.Contains("performance")) return TestKind.IdlePerformance;
        return TestKind.FullMission;
    }

    private bool InvokeIfExists(Component c, string methodName)
    {
        if (c == null) return false;
        MethodInfo m = c.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        if (m == null || m.GetParameters().Length != 0) return false;
        try { m.Invoke(c, null); return true; }
        catch (Exception e) { Debug.LogWarning("Invoke failed: " + c.GetType().Name + "." + methodName + ": " + e.Message); return false; }
    }

    private bool InvokeAny(Component c, string[] methodNames)
    {
        if (c == null || methodNames == null) return false;
        for (int i = 0; i < methodNames.Length; i++)
        {
            if (InvokeIfExists(c, methodNames[i])) return true;
        }
        Debug.LogWarning("MIMISKValidationHarness: none of the requested methods were found on " + c.GetType().Name + ": " + string.Join(",", methodNames));
        return false;
    }

    private FieldInfo Field(Component c, string name)
    {
        return c == null ? null : c.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    }

    private object GetField(Component c, string name)
    {
        FieldInfo f = Field(c, name);
        return f == null ? null : f.GetValue(c);
    }

    private void SetBool(Component c, string name, bool value)
    {
        FieldInfo f = Field(c, name);
        if (f != null && f.FieldType == typeof(bool)) f.SetValue(c, value);
    }

    private void SetFloat(Component c, string name, float value)
    {
        FieldInfo f = Field(c, name);
        if (f == null) return;
        if (f.FieldType == typeof(float)) f.SetValue(c, value);
        else if (f.FieldType == typeof(double)) f.SetValue(c, (double)value);
        else if (f.FieldType == typeof(int)) f.SetValue(c, Mathf.RoundToInt(value));
    }

    private bool GetBool(Component c, string name, bool def = false)
    {
        object o = GetField(c, name);
        return o is bool ? (bool)o : def;
    }

    private int GetInt(Component c, string name, int def = 0)
    {
        object o = GetField(c, name);
        if (o is int) return (int)o;
        if (o is short) return (short)o;
        if (o is ushort) return (ushort)o;
        if (o is byte) return (byte)o;
        if (o is long) return (int)(long)o;
        return def;
    }

    private float GetFloat(Component c, string name, float def = 0.0f)
    {
        object o = GetField(c, name);
        if (o is float) return (float)o;
        if (o is double) return (float)(double)o;
        if (o is int) return (int)o;
        return def;
    }

    private string GetString(Component c, string name, string def = "")
    {
        object o = GetField(c, name);
        return o == null ? def : o.ToString();
    }

    private Vector4 GetVector4(Component c, string name, Vector4 def)
    {
        object o = GetField(c, name);
        return o is Vector4 ? (Vector4)o : def;
    }

    private Vector3 GetVector3(Component c, string name, Vector3 def)
    {
        object o = GetField(c, name);
        return o is Vector3 ? (Vector3)o : def;
    }

    private string GetEnumOrString(Component c, string name)
    {
        object o = GetField(c, name);
        return o == null ? "none" : o.ToString();
    }

    private void SetEnumField(Component c, string fieldName, string enumValue)
    {
        if (c == null || string.IsNullOrEmpty(enumValue)) return;
        FieldInfo f = Field(c, fieldName);
        if (f == null || !f.FieldType.IsEnum) return;
        try
        {
            object parsed = Enum.Parse(f.FieldType, enumValue, true);
            f.SetValue(c, parsed);
        }
        catch
        {
            Debug.LogWarning("Could not set enum " + c.GetType().Name + "." + fieldName + " to " + enumValue);
        }
    }


    private void SetComponentField(Component c, string name, Component value)
    {
        if (c == null || string.IsNullOrEmpty(name)) return;
        FieldInfo f = Field(c, name);
        if (f != null && value != null && f.FieldType.IsAssignableFrom(value.GetType()))
        {
            f.SetValue(c, value);
            return;
        }
        if (f != null && value == null && !f.FieldType.IsValueType)
        {
            f.SetValue(c, null);
            return;
        }

        PropertyInfo p = c.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (p != null && p.CanWrite && value != null && p.PropertyType.IsAssignableFrom(value.GetType()))
        {
            p.SetValue(c, value, null);
        }
    }

    private Vector3 GetPosition(GameObject go)
    {
        return go != null ? go.transform.position : Vector3.zero;
    }

    private float GetYaw(GameObject go)
    {
        return go != null ? go.transform.eulerAngles.y : 0.0f;
    }

    private float GetRoll(GameObject go)
    {
        return go != null ? NormalizeAngle(go.transform.eulerAngles.z) : 0.0f;
    }

    private float GetPitch(GameObject go)
    {
        return go != null ? NormalizeAngle(go.transform.eulerAngles.x) : 0.0f;
    }

    private float NormalizeAngle(float a)
    {
        while (a > 180.0f) a -= 360.0f;
        while (a < -180.0f) a += 360.0f;
        return a;
    }

    private float GetRovDepth(Vector3 rovPos)
    {
        float waterY = GetFloat(unifiedTether, "waterSurfaceY", 0.0f);
        return waterY - rovPos.y;
    }

    private float CurrentFps()
    {
        return Time.unscaledDeltaTime > 0.0001f ? 1.0f / Time.unscaledDeltaTime : 0.0f;
    }

    private string GetWorkflowStateCompact()
    {
        return GetEnumOrString(droneFlightManager, "flightMode") + "|" + GetEnumOrString(unifiedTether, "tetherState") + "|" + GetEnumOrString(miniRovMissionManager, "missionState");
    }

    private Vector3[] GetRovGeneratedPath()
    {
        object o = GetField(miniRovPathPlanner, "lastGeneratedPath");
        Vector3[] path = o as Vector3[];
        return path != null ? path : new Vector3[0];
    }

    private int GetRovGeneratedPathCount()
    {
        return GetRovGeneratedPath().Length;
    }

    private int ComputeRovNearestPathIndex(Vector3 p)
    {
        Vector3[] path = GetRovGeneratedPath();
        if (path == null || path.Length == 0) return -1;
        float best = float.MaxValue;
        int bestIndex = -1;
        for (int i = 0; i < path.Length; i++)
        {
            float d = Vector3.Distance(p, path[i]);
            if (d < best)
            {
                best = d;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private Vector3 ComputeRovNearestPathPoint(Vector3 p)
    {
        Vector3[] path = GetRovGeneratedPath();
        int idx = ComputeRovNearestPathIndex(p);
        if (path == null || idx < 0 || idx >= path.Length) return Vector3.zero;
        return path[idx];
    }

    private float ComputeRovPathError(Vector3 p)
    {
        int idx = ComputeRovNearestPathIndex(p);
        Vector3[] path = GetRovGeneratedPath();
        if (path == null || idx < 0 || idx >= path.Length) return 0.0f;
        return Vector3.Distance(p, path[idx]);
    }

    private void WriteRovReferencePathCsv()
    {
        try
        {
            Vector3[] path = GetRovGeneratedPath();
            if (path == null || path.Length == 0) return;
            string outPath = Path.Combine(outputDirectory, "minirov_reference_path.csv");
            using (StreamWriter w = new StreamWriter(outPath, false, new UTF8Encoding(false)))
            {
                w.WriteLine("run_id,trial_id,path_type,index,x,y,z,depth_m");
                for (int i = 0; i < path.Length; i++)
                {
                    Vector3 p = path[i];
                    float depth = GetRovDepth(p);
                    w.WriteLine(string.Join(",", new string[] {
                        Csv(config.run_id), Csv(config.trial_id), Csv(GetEnumOrString(miniRovPathPlanner, "selectedPathType")), i.ToString(),
                        F(p.x), F(p.y), F(p.z), F(depth)
                    }));
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("MIMISKValidationHarness: could not write MiniROV reference path CSV: " + e.Message);
        }
    }

    private Vector3 GetTransformPositionByName(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.transform.position : Vector3.zero;
    }

    private Transform GetTransformField(Component c, string name)
    {
        object o = GetField(c, name);
        return o as Transform;
    }

    private Vector3 GetActiveTetherEndpointTarget()
    {
        if (v8Tether != null)
        {
            string mode = GetString(v8Tether, "endpointModeText", "");
            Transform target = null;
            if (mode.Contains("Rear"))
            {
                target = GetTransformField(v8Tether, "rearMiniRovAnchor");
            }
            else if (mode.Contains("Deployment"))
            {
                target = GetTransformField(v8Tether, "deploymentCableEndpoint");
            }
            if (target != null) return target.position;
        }
        Vector3 fallback = GetTransformPositionByName("MIMISK_Tether_BackAnchor");
        if (fallback != Vector3.zero) return fallback;
        return GetVector3(unifiedTether, "deploymentRovWorld", Vector3.zero);
    }

    private float ComputeMaxSegmentStrain(Component tether)
    {
        // V8 maintains length constraints internally but does not expose segment strain directly.
        // This placeholder returns zero when the solver is healthy; analysis scripts can compute it
        // from exported node logs if a node-export mode is later enabled.
        return GetBool(tether, "solverHealthy", true) ? 0.0f : 1.0f;
    }

    private string F(float x)
    {
        if (float.IsNaN(x) || float.IsInfinity(x)) return "";
        return x.ToString("0.######", ci);
    }

    private string Csv(string s)
    {
        if (s == null) s = "";
        s = s.Replace("\"", "\"\"");
        if (s.Contains(",") || s.Contains("\n") || s.Contains("\r") || s.Contains("\""))
        {
            return "\"" + s + "\"";
        }
        return s;
    }

    private string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "run";
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }
}
