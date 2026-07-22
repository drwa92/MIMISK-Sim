using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MIMISKDroneTetherHandoffMission : MonoBehaviour
{
    public enum HandoffState
    {
        Disabled,
        WaitingForMissionReady,
        ReadyForDeploy,
        DeployingToWater,
        DynamicStabilizing,
        ROVControlActive,
        HoldingDeployed,
        Recovering,
        Recovered,
        Fault
    }

    [Header("References")]
    public MIMISKDroneCoreMissionManager missionManager;
    public MIMISKDroneCoreFlightModeManager flightManager;
    public MIMISKDroneCoreTetherManager tetherManager;
    public MIMISKMiniROVRealisticDeploymentManager rovDeployment;

    [Header("Handoff")]
    public bool handoffEnabled = true;

    [Tooltip("Full mission should hold at ReadyForTetherDeployment while this subsystem deploys the MiniROV.")]
    public bool forceMissionHoldAtTetherReady = true;

    [Tooltip("If ON, deployment starts automatically once the drone is ready. If OFF, press U.")]
    public bool autoDeployWhenReady = false;

    [Tooltip("Allow tether deployment if SurfaceStable even when mission manager is missing.")]
    public bool allowManualWhenSurfaceStable = true;

    [Header("Command Input Ownership")]
    [Tooltip("When OFF, keyboard tether commands are ignored. Use this for TetherAgent/system-orchestrator control.")]
    public bool acceptKeyboardCommands = true;

    [Header("Keyboard")]
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
    public HandoffState handoffState = HandoffState.WaitingForMissionReady;
    public bool missionReady;
    public bool surfaceStable;
    public bool safeToDeploy;
    public float stateTimerS;
    public float handoffTimerS;
    public string lastHandoffEvent = "waiting";

    private bool autoDeployConsumed;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        ConfigureSubsystems();
    }

    private void Update()
    {
        if (!handoffEnabled || !acceptKeyboardCommands)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
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
            HoldWinch();
        }

        if (Keyboard.current[reattachKey].wasPressedThisFrame)
        {
            ReattachROV();
        }

        if (Keyboard.current[resetFaultKey].wasPressedThisFrame)
        {
            ResetFault();
        }
    }

    private void FixedUpdate()
    {
        if (!handoffEnabled)
        {
            return;
        }

        float dt = Time.fixedDeltaTime;
        stateTimerS += dt;
        handoffTimerS += dt;

        RefreshReadiness();
        UpdateStateFromROVDeployment();

        if (handoffState == HandoffState.ReadyForDeploy &&
            autoDeployWhenReady &&
            !autoDeployConsumed)
        {
            autoDeployConsumed = true;
            StartDeployment();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (missionManager == null)
        {
            missionManager = GetComponent<MIMISKDroneCoreMissionManager>();
        }

        if (flightManager == null)
        {
            flightManager = GetComponent<MIMISKDroneCoreFlightModeManager>();
        }

        if (tetherManager == null)
        {
            tetherManager = GetComponent<MIMISKDroneCoreTetherManager>();
        }

        if (rovDeployment == null)
        {
            rovDeployment = GetComponent<MIMISKMiniROVRealisticDeploymentManager>();
        }
    }

    [ContextMenu("Configure Subsystems")]
    public void ConfigureSubsystems()
    {
        AutoFindReferences();

        if (missionManager != null && forceMissionHoldAtTetherReady)
        {
            missionManager.holdAtReadyForTetherDeployment = true;
        }

        if (tetherManager != null)
        {
            tetherManager.acceptKeyboardCommands = false;
            tetherManager.targetDeployLengthM = targetDeployLengthM;
            tetherManager.payoutSpeedMS = payoutSpeedMS;
            tetherManager.recoverySpeedMS = recoverySpeedMS;

            tetherManager.enableTetherForceWhenMiniRovAttached = false;
            tetherManager.tetherStiffnessNPerM = 0.0f;
            tetherManager.tetherDampingNPerMS = 0.0f;
            tetherManager.maximumSafeTensionN = 999999.0f;
        }

        if (rovDeployment != null)
        {
            rovDeployment.acceptKeyboardCommands = false;

            rovDeployment.targetDeployLengthM = targetDeployLengthM;
            rovDeployment.payoutSpeedMS = payoutSpeedMS;
            rovDeployment.recoverySpeedMS = recoverySpeedMS;

            rovDeployment.releaseDepthBelowSurfaceM = releaseDepthBelowSurfaceM;
            rovDeployment.minimumPayoutBeforeReleaseM = minimumPayoutBeforeReleaseM;
            rovDeployment.postWaterTouchStabilizationS = stabilizationSeconds;

            rovDeployment.disableTetherForceForNow = true;
            rovDeployment.adaptiveSlackManagement = true;
            rovDeployment.enableRovControlAfterStabilization = true;
            rovDeployment.stopReelAtWaterTouch = true;
            rovDeployment.recordHomeOnDynamicRelease = true;
            rovDeployment.setMiniRovHomeOnDynamicRelease = true;
            rovDeployment.levelRovOnDynamicRelease = true;
            rovDeployment.setRovYawZeroOnRelease = true;
            rovDeployment.releaseYawDeg = 0.0f;
            rovDeployment.zeroAngularVelocityOnRelease = true;
            rovDeployment.zeroHorizontalVelocityOnRelease = true;
            rovDeployment.requireMiniRovRecoveryReadyBeforeKinematicRecovery = true;
            rovDeployment.requestMiniRovReturnHomeWhenRecoverRequested = true;
            rovDeployment.enableTetherLocalizationEstimate = true;
        }

        lastHandoffEvent = "subsystems_configured";
    }

    private void RefreshReadiness()
    {
        surfaceStable = false;

        if (flightManager != null)
        {
            surfaceStable =
                flightManager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceStable ||
                flightManager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceHold;
        }

        missionReady = false;

        if (missionManager != null)
        {
            missionReady =
                missionManager.IsReadyForTetherDeployment();
        }

        // Important: manual/agent landing to SurfaceStable is also valid.
        // Full drone mission is not required if the drone is already stable on the surface.
        if (allowManualWhenSurfaceStable && surfaceStable)
        {
            missionReady = true;
        }

        safeToDeploy =
            missionReady &&
            surfaceStable &&
            tetherManager != null &&
            rovDeployment != null &&
            handoffState != HandoffState.Fault;

        if ((handoffState == HandoffState.WaitingForMissionReady ||
             handoffState == HandoffState.Recovered) &&
            safeToDeploy)
        {
            EnterState(HandoffState.ReadyForDeploy, "ready_for_tether_handoff_deployment");

            if (rovDeployment != null)
            {
                rovDeployment.AttachRovToCableEndpoint();
            }
        }
    }

    [ContextMenu("Start Deployment")]
    public void StartDeployment()
    {
        ConfigureSubsystems();
        RefreshReadiness();

        if (!safeToDeploy)
        {
            lastHandoffEvent =
                "deploy_rejected_not_ready_mission_" +
                (missionManager != null ? missionManager.missionState.ToString() : "none") +
                "_flight_" +
                (flightManager != null ? flightManager.flightMode.ToString() : "none");

            Debug.LogWarning("[MIMISK] Tether handoff deployment rejected: " + lastHandoffEvent);
            return;
        }

        if (rovDeployment == null)
        {
            EnterState(HandoffState.Fault, "deploy_failed_missing_rov_deployment");
            return;
        }

        rovDeployment.StartDeployment();

        if (rovDeployment.deploymentState ==
            MIMISKMiniROVRealisticDeploymentManager.DeploymentState.Fault)
        {
            EnterState(HandoffState.Fault, "deploy_failed_rov_deployment_fault");
            return;
        }

        EnterState(HandoffState.DeployingToWater, "handoff_reel_payout_started");
    }

    [ContextMenu("Start Recovery")]
    public void StartRecovery()
    {
        if (rovDeployment == null)
        {
            EnterState(HandoffState.Fault, "recovery_failed_missing_rov_deployment");
            return;
        }

        rovDeployment.StartRecovery();
        EnterState(HandoffState.Recovering, "handoff_recovery_started");
    }

    [ContextMenu("Hold Winch")]
    public void HoldWinch()
    {
        if (rovDeployment != null)
        {
            rovDeployment.StopWinchHold();
        }
        else if (tetherManager != null)
        {
            tetherManager.StopWinch();
        }

        EnterState(HandoffState.HoldingDeployed, "handoff_winch_hold");
    }

    [ContextMenu("Reattach ROV")]
    public void ReattachROV()
    {
        if (rovDeployment != null)
        {
            rovDeployment.AttachRovToCableEndpoint();
        }

        EnterState(HandoffState.ReadyForDeploy, "handoff_rov_reattached");
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

        EnterState(HandoffState.WaitingForMissionReady, "handoff_fault_reset");
    }

    private void UpdateStateFromROVDeployment()
    {
        if (rovDeployment == null)
        {
            return;
        }

        MIMISKMiniROVRealisticDeploymentManager.DeploymentState s =
            rovDeployment.deploymentState;

        if (s == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.CablePayoutToWater)
        {
            if (handoffState != HandoffState.DeployingToWater)
            {
                EnterState(HandoffState.DeployingToWater, "detected_reel_payout_to_water");
            }
        }
        else if (s == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.DynamicStabilizing)
        {
            if (handoffState != HandoffState.DynamicStabilizing)
            {
                EnterState(HandoffState.DynamicStabilizing, "detected_rov_dynamic_stabilizing");
            }
        }
        else if (s == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.ROVControlActive)
        {
            if (handoffState != HandoffState.ROVControlActive)
            {
                EnterState(HandoffState.ROVControlActive, "detected_rov_control_active");
            }
        }
        else if (s == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.RecoveringKinematic)
        {
            if (handoffState != HandoffState.Recovering)
            {
                EnterState(HandoffState.Recovering, "detected_kinematic_recovery");
            }
        }
        else if (s == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.RecoveredAttached)
        {
            if (handoffState != HandoffState.Recovered)
            {
                EnterState(HandoffState.Recovered, "detected_recovered_attached");
            }
        }
        else if (s == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.Fault)
        {
            if (handoffState != HandoffState.Fault)
            {
                EnterState(HandoffState.Fault, "detected_rov_deployment_fault");
            }
        }
    }

    private void EnterState(HandoffState newState, string eventText)
    {
        handoffState = newState;
        stateTimerS = 0.0f;
        lastHandoffEvent = eventText;

        Debug.Log("[MIMISK] Tether handoff: " + newState + " / " + eventText);
    }
}
