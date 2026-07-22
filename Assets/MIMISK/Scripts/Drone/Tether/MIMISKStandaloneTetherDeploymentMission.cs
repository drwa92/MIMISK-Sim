using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MIMISKStandaloneTetherDeploymentMission : MonoBehaviour
{
    public enum StandaloneState
    {
        Disabled,
        Idle,
        ReadyForDeploy,
        DeployingToWater,
        DynamicStabilizing,
        ROVControlActive,
        Holding,
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

    [Header("Mission")]
    public bool standaloneEnabled = true;

    [Tooltip("Disable the full aerial mission while testing tether deployment only.")]
    public bool disableFullMissionDuringStandaloneTest = true;

    [Tooltip("Force the drone to be treated as already surface-stable for tether testing.")]
    public bool forceSurfaceStableForStandaloneTest = true;

    [Header("Keyboard")]
    public Key prepareKey = Key.P;
    public Key deployKey = Key.U;
    public Key recoverKey = Key.R;
    public Key holdKey = Key.K;
    public Key reattachKey = Key.D;
    public Key resetFaultKey = Key.F;
    public Key forceDynamicReleaseKey = Key.J;

    [Header("Deployment Parameters")]
    public float targetDeployLengthM = 3.0f;
    public float payoutSpeedMS = 0.22f;
    public float recoverySpeedMS = 0.25f;
    public float releaseDepthBelowSurfaceM = 0.25f;
    public float minimumPayoutBeforeReleaseM = 0.45f;
    public float stabilizationSeconds = 1.50f;

    [Header("Runtime")]
    public StandaloneState state = StandaloneState.Idle;
    public float stateTimerS;
    public float missionTimerS;
    public string lastEvent = "idle";

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Update()
    {
        if (!standaloneEnabled)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[prepareKey].wasPressedThisFrame)
        {
            PrepareStandaloneTetherTest();
        }

        if (Keyboard.current[deployKey].wasPressedThisFrame)
        {
            StartDeployment();
        }

        if (Keyboard.current[recoverKey].wasPressedThisFrame)
        {
            StartRecovery();
        }

        if (Keyboard.current[holdKey].wasPressedThisFrame)
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

        if (Keyboard.current[forceDynamicReleaseKey].wasPressedThisFrame)
        {
            ForceDynamicRelease();
        }
    }

    private void FixedUpdate()
    {
        if (!standaloneEnabled)
        {
            return;
        }

        stateTimerS += Time.fixedDeltaTime;
        missionTimerS += Time.fixedDeltaTime;

        UpdateStateFromDeployment();
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

    [ContextMenu("Prepare Standalone Tether Test")]
    public void PrepareStandaloneTetherTest()
    {
        AutoFindReferences();

        if (disableFullMissionDuringStandaloneTest && missionManager != null)
        {
            missionManager.missionEnabled = false;
            missionManager.missionActive = false;
            missionManager.missionState =
                MIMISKDroneCoreMissionManager.MissionState.ReadyForTetherDeployment;

            missionManager.lastMissionEvent =
                "standalone_tether_ready";
        }

        if (forceSurfaceStableForStandaloneTest && flightManager != null)
        {
            flightManager.flightMode =
                MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceStable;

            flightManager.lastModeEvent =
                "standalone_tether_surface_stable";
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

        ConfigureTetherAndROV();

        if (rovDeployment != null)
        {
            rovDeployment.AttachRovToCableEndpoint();
        }

        EnterState(StandaloneState.ReadyForDeploy, "standalone_ready_for_deploy");
    }

    private void ConfigureTetherAndROV()
    {
        if (tetherManager != null)
        {
            tetherManager.acceptKeyboardCommands = false;
            tetherManager.tetherSystemEnabled = true;

            tetherManager.deployedLengthM = tetherManager.minimumLengthM;
            tetherManager.targetLengthM = tetherManager.minimumLengthM;
            tetherManager.targetDeployLengthM = targetDeployLengthM;

            tetherManager.payoutSpeedMS = payoutSpeedMS;
            tetherManager.recoverySpeedMS = recoverySpeedMS;

            tetherManager.tetherState =
                MIMISKDroneCoreTetherManager.TetherState.Ready;

            tetherManager.enableTetherForceWhenMiniRovAttached = false;
            tetherManager.tetherStiffnessNPerM = 0.0f;
            tetherManager.tetherDampingNPerMS = 0.0f;
            tetherManager.maximumSafeTensionN = 999999.0f;

            tetherManager.lastEvent =
                "standalone_tether_ready";
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

            rovDeployment.stopReelAtWaterTouch = true;
            rovDeployment.disableTetherForceForNow = true;
            rovDeployment.adaptiveSlackManagement = true;
            rovDeployment.enableRovControlAfterStabilization = true;

            rovDeployment.ignoreMiniRovDroneCollisions = true;
            rovDeployment.usePhysicsIgnoreCollision = false;
            rovDeployment.disableMiniRovCollidersDuringCableFollow = true;
            rovDeployment.disableMiniRovCollidersDuringRecovery = true;
        }
    }

    [ContextMenu("Start Deployment")]
    public void StartDeployment()
    {
        if (state == StandaloneState.Idle)
        {
            PrepareStandaloneTetherTest();
        }

        ConfigureTetherAndROV();

        if (rovDeployment == null)
        {
            EnterState(StandaloneState.Fault, "deploy_failed_missing_rov_deployment");
            return;
        }

        rovDeployment.StartDeployment();

        if (rovDeployment.deploymentState ==
            MIMISKMiniROVRealisticDeploymentManager.DeploymentState.Fault)
        {
            EnterState(StandaloneState.Fault, "deploy_failed_rov_fault");
            return;
        }

        EnterState(StandaloneState.DeployingToWater, "standalone_deploying_to_water");
    }


    [ContextMenu("Force Dynamic Release")]
    public void ForceDynamicRelease()
    {
        if (rovDeployment == null)
        {
            EnterState(StandaloneState.Fault, "force_dynamic_release_failed_missing_rov_deployment");
            return;
        }

        rovDeployment.ForceDynamicReleaseFromKinematicHold();

        if (rovDeployment.deploymentState ==
            MIMISKMiniROVRealisticDeploymentManager.DeploymentState.WaterTouchDetected)
        {
            EnterState(StandaloneState.DynamicStabilizing, "manual_dynamic_release_started");
        }
    }

    [ContextMenu("Start Recovery")]
    public void StartRecovery()
    {
        if (rovDeployment == null)
        {
            EnterState(StandaloneState.Fault, "recovery_failed_missing_rov_deployment");
            return;
        }

        rovDeployment.StartRecovery();
        EnterState(StandaloneState.Recovering, "standalone_recovery_started");
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

        EnterState(StandaloneState.Holding, "standalone_hold");
    }

    [ContextMenu("Reattach ROV")]
    public void ReattachROV()
    {
        ConfigureTetherAndROV();

        if (rovDeployment != null)
        {
            rovDeployment.AttachRovToCableEndpoint();
        }

        EnterState(StandaloneState.ReadyForDeploy, "standalone_rov_reattached");
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

        EnterState(StandaloneState.ReadyForDeploy, "standalone_fault_reset");
    }

    private void UpdateStateFromDeployment()
    {
        if (rovDeployment == null)
        {
            return;
        }

        MIMISKMiniROVRealisticDeploymentManager.DeploymentState d =
            rovDeployment.deploymentState;

        if (d == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.CablePayoutToWater)
        {
            if (state != StandaloneState.DeployingToWater)
            {
                EnterState(StandaloneState.DeployingToWater, "detected_cable_payout");
            }
        }
        else if (d == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.DynamicStabilizing)
        {
            if (state != StandaloneState.DynamicStabilizing)
            {
                EnterState(StandaloneState.DynamicStabilizing, "detected_dynamic_stabilizing");
            }
        }
        else if (d == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.KinematicDeployedHolding)
        {
            if (state != StandaloneState.Holding)
            {
                EnterState(StandaloneState.Holding, "detected_kinematic_hold_at_release_depth");
            }
        }
        else if (d == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.ROVControlActive)
        {
            if (state != StandaloneState.ROVControlActive)
            {
                EnterState(StandaloneState.ROVControlActive, "detected_rov_control_active");
            }
        }
        else if (d == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.RecoveringKinematic)
        {
            if (state != StandaloneState.Recovering)
            {
                EnterState(StandaloneState.Recovering, "detected_recovery");
            }
        }
        else if (d == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.RecoveredAttached)
        {
            if (state != StandaloneState.Recovered)
            {
                EnterState(StandaloneState.Recovered, "detected_recovered");
            }
        }
        else if (d == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.Fault)
        {
            if (state != StandaloneState.Fault)
            {
                EnterState(StandaloneState.Fault, "detected_fault");
            }
        }
    }

    private void EnterState(StandaloneState newState, string eventText)
    {
        state = newState;
        stateTimerS = 0.0f;
        lastEvent = eventText;

        Debug.Log("[MIMISK] Standalone tether mission: " + newState + " / " + eventText);
    }
}
