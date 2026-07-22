using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MIMISKTetherDeploymentTestMission : MonoBehaviour
{
    public enum TestMissionState
    {
        Idle,
        PreparingSurfaceStable,
        ReadyForDeploy,
        DeployingToWater,
        DynamicStabilizing,
        RovControlActive,
        HoldingDeployed,
        Recovering,
        Recovered,
        Fault
    }

    [Header("References")]
    public MIMISKDroneCoreRotorController core;
    public MIMISKDroneCoreFlightModeManager flightManager;
    public MIMISKDroneCoreMissionManager missionManager;
    public MIMISKDroneCoreTetherManager tetherManager;
    public MIMISKMiniROVRealisticDeploymentManager rovDeployment;
    public Rigidbody droneRigidbody;

    [Header("Test Mission")]
    public bool testMissionEnabled = true;
    public bool disableFullMissionManagerDuringTest = true;

    [Tooltip("This lets us test tether deployment without running the full aerial mission.")]
    public bool forceReadyForTetherDeployment = true;

    [Tooltip("If ON, pressing P prepares SurfaceStable/Ready state. If OFF, it only starts when current system is already ready.")]
    public bool forceSurfaceStableModeForTest = true;

    [Tooltip("If ON, deployment starts automatically after prepare. If OFF, press U after P.")]
    public bool autoDeployAfterPrepare = false;

    [Header("Keyboard")]
    public Key prepareKey = Key.P;
    public Key deployKey = Key.U;
    public Key recoverKey = Key.R;
    public Key stopKey = Key.K;
    public Key reattachKey = Key.D;
    public Key resetFaultKey = Key.F;

    [Header("Deployment Settings")]
    public float targetDeployLengthM = 3.0f;
    public float payoutSpeedMS = 0.22f;
    public float recoverySpeedMS = 0.25f;
    public float releaseDepthBelowSurfaceM = 0.25f;
    public float minimumPayoutBeforeReleaseM = 0.45f;
    public float stabilizationSeconds = 1.5f;

    [Header("Runtime")]
    public TestMissionState testState = TestMissionState.Idle;
    public float testTimerS;
    public float stateTimerS;
    public string lastTestEvent = "idle";

    [Header("Logging")]
    public bool enableLogging = true;
    public float logHz = 50.0f;
    public bool flushEveryLine = false;
    public string currentLogPath;
    public int linesWritten;

    private StreamWriter writer;
    private float logTimer;
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        if (enableLogging)
        {
            OpenLog();
        }
    }

    private void Update()
    {
        if (!testMissionEnabled)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[prepareKey].wasPressedThisFrame)
        {
            PrepareTetherDeploymentTest();
        }

        if (Keyboard.current[deployKey].wasPressedThisFrame)
        {
            StartDeployment();
        }

        if (Keyboard.current[recoverKey].wasPressedThisFrame)
        {
            StartRecovery();
        }

        if (Keyboard.current[stopKey].wasPressedThisFrame)
        {
            StopWinch();
        }

        if (Keyboard.current[reattachKey].wasPressedThisFrame)
        {
            ReattachRov();
        }

        if (Keyboard.current[resetFaultKey].wasPressedThisFrame)
        {
            ResetFault();
        }
    }

    private void FixedUpdate()
    {
        if (!testMissionEnabled)
        {
            return;
        }

        float dt = Time.fixedDeltaTime;

        testTimerS += dt;
        stateTimerS += dt;

        UpdateStateFromSubsystems();

        if (enableLogging)
        {
            WriteLogIfDue(dt);
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (core == null)
        {
            core = GetComponent<MIMISKDroneCoreRotorController>();
        }

        if (flightManager == null)
        {
            flightManager = GetComponent<MIMISKDroneCoreFlightModeManager>();
        }

        if (missionManager == null)
        {
            missionManager = GetComponent<MIMISKDroneCoreMissionManager>();
        }

        if (tetherManager == null)
        {
            tetherManager = GetComponent<MIMISKDroneCoreTetherManager>();
        }

        if (rovDeployment == null)
        {
            rovDeployment = GetComponent<MIMISKMiniROVRealisticDeploymentManager>();
        }

        if (droneRigidbody == null)
        {
            droneRigidbody = GetComponent<Rigidbody>();
        }
    }

    [ContextMenu("Prepare Tether Deployment Test")]
    public void PrepareTetherDeploymentTest()
    {
        AutoFindReferences();

        if (disableFullMissionManagerDuringTest && missionManager != null)
        {
            missionManager.missionEnabled = false;
            missionManager.missionActive = false;
        }

        if (forceReadyForTetherDeployment && missionManager != null)
        {
            missionManager.missionState =
                MIMISKDroneCoreMissionManager.MissionState.ReadyForTetherDeployment;

            missionManager.lastMissionEvent =
                "tether_deployment_test_forced_ready";
        }

        if (forceSurfaceStableModeForTest && flightManager != null)
        {
            flightManager.flightMode =
                MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceStable;

            flightManager.lastModeEvent =
                "tether_test_forced_surface_stable";
        }

        if (core != null)
        {
            core.controlMode =
                MIMISKDroneCoreRotorController.ControlMode.Disabled;

            core.CutMotors();
        }

        if (droneRigidbody != null)
        {
            droneRigidbody.linearVelocity = Vector3.zero;
            droneRigidbody.angularVelocity = Vector3.zero;
        }

        if (tetherManager != null)
        {
            tetherManager.acceptKeyboardCommands = false;
            tetherManager.tetherSystemEnabled = true;
            tetherManager.requireSurfaceStable = true;
            tetherManager.allowManualDeploymentWhenSurfaceStable = true;

            tetherManager.deployedLengthM =
                Mathf.Clamp(
                    tetherManager.minimumLengthM,
                    tetherManager.minimumLengthM,
                    tetherManager.maximumLengthM
                );

            tetherManager.targetLengthM = tetherManager.deployedLengthM;
            tetherManager.targetDeployLengthM =
                Mathf.Clamp(
                    targetDeployLengthM,
                    tetherManager.minimumLengthM,
                    tetherManager.maximumLengthM
                );

            tetherManager.payoutSpeedMS = payoutSpeedMS;
            tetherManager.recoverySpeedMS = recoverySpeedMS;

            tetherManager.tetherState =
                MIMISKDroneCoreTetherManager.TetherState.Ready;

            tetherManager.enableTetherForceWhenMiniRovAttached = false;
            tetherManager.tetherStiffnessNPerM = 0.0f;
            tetherManager.tetherDampingNPerMS = 0.0f;
            tetherManager.maximumSafeTensionN = 999999.0f;

            tetherManager.lastEvent =
                "tether_test_ready";
        }

        if (rovDeployment != null)
        {
            rovDeployment.acceptKeyboardCommands = false;
            rovDeployment.deploymentEnabled = true;

            rovDeployment.targetDeployLengthM = targetDeployLengthM;
            rovDeployment.payoutSpeedMS = payoutSpeedMS;
            rovDeployment.recoverySpeedMS = recoverySpeedMS;

            rovDeployment.releaseDepthBelowSurfaceM = releaseDepthBelowSurfaceM;
            rovDeployment.minimumPayoutBeforeReleaseM = minimumPayoutBeforeReleaseM;
            rovDeployment.postWaterTouchStabilizationS = stabilizationSeconds;

            rovDeployment.requireMissionReady = true;
            rovDeployment.requireSurfaceStable = true;

            rovDeployment.disableTetherForceForNow = true;
            rovDeployment.adaptiveSlackManagement = true;

            rovDeployment.AttachRovToCableEndpoint();
        }

        EnterState(TestMissionState.ReadyForDeploy, "test_ready_for_tether_deployment");

        if (autoDeployAfterPrepare)
        {
            StartDeployment();
        }
    }

    [ContextMenu("Start Deployment")]
    public void StartDeployment()
    {
        if (rovDeployment == null)
        {
            EnterState(TestMissionState.Fault, "deploy_failed_missing_realistic_deployment_manager");
            return;
        }

        if (testState == TestMissionState.Idle)
        {
            PrepareTetherDeploymentTest();
        }

        rovDeployment.StartDeployment();

        if (rovDeployment.deploymentState ==
            MIMISKMiniROVRealisticDeploymentManager.DeploymentState.Fault)
        {
            EnterState(TestMissionState.Fault, "rov_deployment_rejected");
            return;
        }

        EnterState(TestMissionState.DeployingToWater, "test_reel_payout_started");
    }

    [ContextMenu("Start Recovery")]
    public void StartRecovery()
    {
        if (rovDeployment == null)
        {
            EnterState(TestMissionState.Fault, "recovery_failed_missing_realistic_deployment_manager");
            return;
        }

        rovDeployment.StartRecovery();
        EnterState(TestMissionState.Recovering, "test_recovery_started");
    }

    [ContextMenu("Stop Winch")]
    public void StopWinch()
    {
        if (rovDeployment != null)
        {
            rovDeployment.StopWinchHold();
        }

        EnterState(TestMissionState.HoldingDeployed, "test_winch_hold");
    }

    [ContextMenu("Reattach ROV")]
    public void ReattachRov()
    {
        if (rovDeployment != null)
        {
            rovDeployment.AttachRovToCableEndpoint();
        }

        EnterState(TestMissionState.ReadyForDeploy, "test_rov_reattached");
    }

    [ContextMenu("Reset Fault")]
    public void ResetFault()
    {
        if (rovDeployment != null)
        {
            rovDeployment.ResetFault();
        }

        if (tetherManager != null)
        {
            tetherManager.ResetFault();
        }

        EnterState(TestMissionState.ReadyForDeploy, "test_fault_reset");
    }

    private void UpdateStateFromSubsystems()
    {
        if (rovDeployment == null)
        {
            return;
        }

        MIMISKMiniROVRealisticDeploymentManager.DeploymentState s =
            rovDeployment.deploymentState;

        if (s == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.CablePayoutToWater)
        {
            if (testState != TestMissionState.DeployingToWater)
            {
                EnterState(TestMissionState.DeployingToWater, "detected_reel_payout_to_water");
            }
        }
        else if (s == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.DynamicStabilizing)
        {
            if (testState != TestMissionState.DynamicStabilizing)
            {
                EnterState(TestMissionState.DynamicStabilizing, "detected_dynamic_stabilizing");
            }
        }
        else if (s == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.ROVControlActive)
        {
            if (testState != TestMissionState.RovControlActive)
            {
                EnterState(TestMissionState.RovControlActive, "detected_rov_control_active");
            }
        }
        else if (s == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.RecoveringKinematic)
        {
            if (testState != TestMissionState.Recovering)
            {
                EnterState(TestMissionState.Recovering, "detected_kinematic_recovery");
            }
        }
        else if (s == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.RecoveredAttached)
        {
            if (testState != TestMissionState.Recovered)
            {
                EnterState(TestMissionState.Recovered, "detected_recovered_attached");
            }
        }
        else if (s == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.Fault)
        {
            if (testState != TestMissionState.Fault)
            {
                EnterState(TestMissionState.Fault, "detected_fault");
            }
        }
    }

    private void EnterState(TestMissionState newState, string eventText)
    {
        testState = newState;
        stateTimerS = 0.0f;
        lastTestEvent = eventText;

        Debug.Log("[MIMISK] Tether test mission: " + newState + " / " + eventText);
    }

    private void OpenLog()
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath).FullName;

        string logDir =
            Path.Combine(projectRoot, "Logs", "MIMISKDrone");

        Directory.CreateDirectory(logDir);

        string fileName =
            "drone_tether_deployment_test_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss") +
            ".csv";

        currentLogPath =
            Path.Combine(logDir, fileName);

        writer =
            new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,test_state,test_event," +
            "mission_state,flight_mode,rov_deployment_state,rov_event," +
            "tether_state,tether_event," +
            "deployed_length,target_length,winch_rate," +
            "safe_to_deploy,water_contact,rov_dynamic,rov_control_active," +
            "rov_x,rov_y,rov_z,cable_x,cable_y,cable_z,anchor_to_cable_distance"
        );

        writer.Flush();
    }

    private void WriteLogIfDue(float dt)
    {
        if (writer == null)
        {
            OpenLog();
        }

        if (writer == null)
        {
            return;
        }

        logTimer += dt;

        float period =
            1.0f / Mathf.Max(1.0f, logHz);

        if (logTimer < period)
        {
            return;
        }

        logTimer -= period;

        string missionState =
            missionManager != null ? missionManager.missionState.ToString() : "none";

        string flightMode =
            flightManager != null ? flightManager.flightMode.ToString() : "none";

        string rovState =
            rovDeployment != null ? rovDeployment.deploymentState.ToString() : "none";

        string rovEvent =
            rovDeployment != null ? rovDeployment.lastEvent : "none";

        string tetherState =
            tetherManager != null ? tetherManager.tetherState.ToString() : "none";

        string tetherEvent =
            tetherManager != null ? tetherManager.lastEvent : "none";

        Vector3 rovP =
            rovDeployment != null && rovDeployment.miniRovRoot != null
                ? rovDeployment.miniRovRoot.position
                : Vector3.zero;

        Vector3 cableP =
            rovDeployment != null && rovDeployment.yellowCableEndPoint != null
                ? rovDeployment.yellowCableEndPoint.position
                : Vector3.zero;

        string line = string.Join(",",
            F(Time.time),
            testState.ToString(),
            Safe(lastTestEvent),

            missionState,
            flightMode,
            rovState,
            Safe(rovEvent),

            tetherState,
            Safe(tetherEvent),

            tetherManager != null ? F(tetherManager.deployedLengthM) : "0",
            tetherManager != null ? F(tetherManager.targetLengthM) : "0",
            tetherManager != null ? F(tetherManager.winchCommandRateMS) : "0",

            rovDeployment != null && rovDeployment.safeToDeploy ? "1" : "0",
            rovDeployment != null && rovDeployment.waterContactDetected ? "1" : "0",
            rovDeployment != null && rovDeployment.rovDynamic ? "1" : "0",
            rovDeployment != null && rovDeployment.rovControlActive ? "1" : "0",

            F(rovP.x), F(rovP.y), F(rovP.z),
            F(cableP.x), F(cableP.y), F(cableP.z),

            rovDeployment != null ? F(rovDeployment.distanceRovAnchorToCableEndM) : "0"
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
