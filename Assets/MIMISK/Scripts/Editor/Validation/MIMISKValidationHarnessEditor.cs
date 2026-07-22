#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MIMISKValidationHarness))]
public class MIMISKValidationHarnessEditor : Editor
{
    private string customRunId = "";
    private string customTrialId = "trial_001";
    private bool includeDroneTakeoffLanding = false;
    private bool useDroneMissionManager = false;
    private bool enableRosGrpc = false;
    private int trialCounter = 1;

    private static readonly string[] DronePaths = { "Circle", "Square", "Spiral", "Lawnmower", "FigureEight", "DeploymentApproach" };
    private static readonly string[] RovPaths = { "Square", "LawnmowerSurvey", "CircleInspection", "FigureEight", "Waypoints" };
    private int dronePathIndex = 0;
    private int rovPathIndex = 1;

    public override void OnInspectorGUI()
    {
        MIMISKValidationHarness h = (MIMISKValidationHarness)target;

        serializedObject.Update();

        DrawStatus(h);
        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Inspector Test Runner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Use this panel in Play Mode. It runs one validation trial and writes manifest.json, state_log.csv, mission_events.csv, tether_log.csv, performance_log.csv, ros_grpc_log.csv, and controller_diagnostics_log.csv under Logs/MIMISKValidation/<run_id>/<trial_id>.",
            MessageType.Info);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Common Settings", EditorStyles.boldLabel);
            customRunId = EditorGUILayout.TextField("Run ID", customRunId);
            customTrialId = EditorGUILayout.TextField("Trial ID", customTrialId);
            trialCounter = EditorGUILayout.IntField("Auto Trial Counter", Mathf.Max(1, trialCounter));
            includeDroneTakeoffLanding = EditorGUILayout.Toggle("Include Drone Takeoff/Landing", includeDroneTakeoffLanding);
            useDroneMissionManager = EditorGUILayout.Toggle("Use Drone Mission Manager (advanced)", useDroneMissionManager);
            enableRosGrpc = EditorGUILayout.Toggle("Enable ROS2/gRPC", enableRosGrpc);
            dronePathIndex = EditorGUILayout.Popup("Drone Path", dronePathIndex, DronePaths);
            rovPathIndex = EditorGUILayout.Popup("MiniROV Path", rovPathIndex, RovPaths);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Complete Workflow Profile", EditorStyles.boldLabel);
            h.config.drone_takeoff_target_altitude_m = EditorGUILayout.FloatField("Drone Takeoff Height [m]", Mathf.Max(0.5f, h.config.drone_takeoff_target_altitude_m));
            h.config.drone_trajectory_segment_s = EditorGUILayout.FloatField("Fallback Drone Segment [s]", Mathf.Max(2.0f, h.config.drone_trajectory_segment_s));
            h.config.drone_run_complete_trajectory = EditorGUILayout.Toggle("Run Complete Drone Trajectory", h.config.drone_run_complete_trajectory);
            h.config.drone_close_circle_loop = EditorGUILayout.Toggle("Close Circle Trajectory", h.config.drone_close_circle_loop);
            h.config.drone_path_completion_hold_s = EditorGUILayout.FloatField("Path Completion Hold [s]", Mathf.Max(0.0f, h.config.drone_path_completion_hold_s));
            h.config.rov_wait_for_mission_completion = EditorGUILayout.Toggle("Wait For ROV Mission Complete", h.config.rov_wait_for_mission_completion);
            h.config.rov_mission_completion_timeout_s = EditorGUILayout.FloatField("ROV Mission Timeout [s]", Mathf.Max(5.0f, h.config.rov_mission_completion_timeout_s));
            h.config.rov_lawnmower_completion_timeout_s = EditorGUILayout.FloatField("ROV Lawnmower Timeout [s]", Mathf.Max(5.0f, h.config.rov_lawnmower_completion_timeout_s));
            h.config.enable_rov_pre_mission_descent = EditorGUILayout.Toggle("ROV Descend Before Mission", h.config.enable_rov_pre_mission_descent);
            h.config.rov_pre_mission_depth_m = EditorGUILayout.FloatField("ROV Mission Depth [m]", Mathf.Max(0.05f, h.config.rov_pre_mission_depth_m));
            h.config.rov_depth_ready_tolerance_m = EditorGUILayout.FloatField("ROV Depth Tolerance [m]", Mathf.Max(0.03f, h.config.rov_depth_ready_tolerance_m));
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Diagnostics Logging", EditorStyles.boldLabel);
            h.config.enable_controller_diagnostics = EditorGUILayout.Toggle("Controller Diagnostics", h.config.enable_controller_diagnostics);
            h.config.diagnostics_hz = EditorGUILayout.FloatField("Diagnostics Rate [Hz]", Mathf.Max(1.0f, h.config.diagnostics_hz));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Auto Trial ID"))
            {
                customTrialId = "trial_" + trialCounter.ToString("000");
            }
            if (GUILayout.Button("Next Trial"))
            {
                trialCounter++;
                customTrialId = "trial_" + trialCounter.ToString("000");
            }
            EditorGUILayout.EndHorizontal();
        }

        bool canRun = Application.isPlaying && !h.running;
        bool canCancel = Application.isPlaying && h.running;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Press Play first, then click a validation test button. The harness is intentionally not auto-started when installed from the inspector menu.", MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(!canRun))
        {
            DrawRunButtons(h);
        }

        using (new EditorGUI.DisabledScope(!canCancel))
        {
            if (GUILayout.Button("Cancel Running Validation", GUILayout.Height(24)))
            {
                h.CancelValidationRun();
                EditorUtility.SetDirty(h);
            }
        }

        EditorGUILayout.Space(8);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Current Harness Configuration", EditorStyles.boldLabel);
            DrawDefaultInspector();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawStatus(MIMISKValidationHarness h)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Running", h.running ? "YES" : "no");
            EditorGUILayout.LabelField("Completed", h.completed ? "YES" : "no");
            EditorGUILayout.LabelField("Failed", h.failed ? "YES" : "no");
            EditorGUILayout.LabelField("Failure Reason", h.failureReason ?? "none");
            EditorGUILayout.LabelField("Output", string.IsNullOrEmpty(h.outputDirectory) ? "not started" : h.outputDirectory);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(h.outputDirectory)))
            {
                if (GUILayout.Button("Reveal Output Folder"))
                {
                    EditorUtility.RevealInFinder(h.outputDirectory);
                }
            }
        }
    }

    private void DrawRunButtons(MIMISKValidationHarness h)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("One-Click Paper Tests", EditorStyles.boldLabel);

            if (GUILayout.Button("Run Complete Workflow Trial (Drone + Tether + MiniROV)", GUILayout.Height(32)))
            {
                // The complete workflow uses the explicit drone validation sequence:
                // takeoff -> selected trajectory -> water landing -> tether/MiniROV workflow.
                RunPreset(h, "full_mission", "paper_complete_workflow_v8", true, false, false);
            }

            if (GUILayout.Button("Run Tether + MiniROV Trial Only", GUILayout.Height(24)))
            {
                RunPreset(h, "full_mission", "paper_tether_minirov_only_v8", false, false, false);
            }

            if (GUILayout.Button("Run V8 Tether Trial", GUILayout.Height(24)))
            {
                RunPreset(h, "tether_v8", "paper_tether_v8", false, false);
            }

            if (GUILayout.Button("Run ROS2/gRPC Trial", GUILayout.Height(24)))
            {
                RunPreset(h, "ros_grpc", "paper_ros_grpc", false, true);
            }
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Drone Trajectory Validation", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Circle")) RunDrone(h, "Circle");
            if (GUILayout.Button("Square")) RunDrone(h, "Square");
            if (GUILayout.Button("Spiral")) RunDrone(h, "Spiral");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Lawnmower")) RunDrone(h, "Lawnmower");
            if (GUILayout.Button("FigureEight")) RunDrone(h, "FigureEight");
            if (GUILayout.Button("DeployApproach")) RunDrone(h, "DeploymentApproach");
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Selected Drone Path: " + DronePaths[dronePathIndex])) RunDrone(h, DronePaths[dronePathIndex]);
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("MiniROV + Tether Navigation Validation", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Square")) RunRov(h, "Square");
            if (GUILayout.Button("Lawnmower")) RunRov(h, "LawnmowerSurvey");
            if (GUILayout.Button("Circle")) RunRov(h, "CircleInspection");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("FigureEight")) RunRov(h, "FigureEight");
            if (GUILayout.Button("Waypoints")) RunRov(h, "Waypoints");
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Selected MiniROV Path: " + RovPaths[rovPathIndex])) RunRov(h, RovPaths[rovPathIndex]);
        }
    }

    private void RunDrone(MIMISKValidationHarness h, string path)
    {
        h.config.drone_path = path;
        string run = "paper_drone_" + path.ToLowerInvariant();
        RunPreset(h, "drone_trajectory", run, true, enableRosGrpc, false);
    }

    private void RunRov(MIMISKValidationHarness h, string path)
    {
        h.config.rov_path = path;
        string run = "paper_minirov_" + path.ToLowerInvariant();
        RunPreset(h, "minirov_navigation", run, false, enableRosGrpc, false);
    }

    private void RunPreset(MIMISKValidationHarness h, string test, string defaultRunId, bool defaultIncludeDrone, bool forceGrpc, bool defaultUseDroneMissionManager = false)
    {
        string runId = string.IsNullOrWhiteSpace(customRunId) ? defaultRunId : customRunId;
        string trialId = string.IsNullOrWhiteSpace(customTrialId) ? ("trial_" + trialCounter.ToString("000")) : customTrialId;
        string dronePath = DronePaths[Mathf.Clamp(dronePathIndex, 0, DronePaths.Length - 1)];
        string rovPath = RovPaths[Mathf.Clamp(rovPathIndex, 0, RovPaths.Length - 1)];

        // Preserve button-specific paths if they were set immediately before calling this method.
        if (!string.IsNullOrWhiteSpace(h.config.drone_path)) dronePath = h.config.drone_path;
        if (!string.IsNullOrWhiteSpace(h.config.rov_path)) rovPath = h.config.rov_path;

        bool includeDrone = includeDroneTakeoffLanding || defaultIncludeDrone;
        bool useMissionManager = includeDrone && defaultUseDroneMissionManager;
        h.ConfigureForInspectorRun(test, runId, trialId, dronePath, rovPath, includeDrone, enableRosGrpc || forceGrpc, useMissionManager);
        EditorUtility.SetDirty(h);
        h.StartValidationRun();
    }
}
#endif
