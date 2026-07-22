using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MIMISKCommunicationBridge : MonoBehaviour
{
    public enum BridgeSystemState
    {
        Unknown,
        IdleSurface,
        DroneMissionRunning,
        DroneReadyForDeployment,
        TetherDeploying,
        ROVLoweredControlInactive,
        ROVControlActive,
        ROVRecoveryReady,
        Recovering,
        Recovered,
        Fault
    }

    public enum BridgeCommand
    {
        StartDroneMission,
        DeployTether,
        EnableMiniROVControl,
        StartMiniROVMission,
        RecoverMiniROV,
        HoldTether,
        ResetBridge
    }

    [Header("Common Interface")]
    public MIMISKCommonBus bus;
    public bool subscribeToCommonBus = true;
    public bool publishBridgeStateToBus = true;
    public float statePublishHz = 20.0f;

    [Header("Existing Drone Module")]
    public GameObject droneObject;
    public Rigidbody droneRigidbody;
    public MIMISKDroneCoreMissionManager droneMission;
    public MIMISKDroneCoreFlightModeManager droneFlightMode;

    [Header("Existing Tether Module")]
    public MIMISKDroneCoreTetherManager tetherManager;
    public MIMISKFinalMissionPlanner finalPlanner;

    [Header("Existing MiniROV Module")]
    public GameObject miniRovObject;
    public Rigidbody miniRovRigidbody;
    public MIMISKMiniROVModule miniRovModule;
    public MIMISKMiniROVMissionManager miniRovMission;
    public Transform rovTetherAnchor;

    [Header("Keyboard Commands")]
    public bool keyboardCommandsEnabled = true;

    [Tooltip("1: start drone mission")]
    public Key startDroneMissionKey = Key.Digit1;

    [Tooltip("2: deploy tether")]
    public Key deployTetherKey = Key.Digit2;

    [Tooltip("3: enable MiniROV control")]
    public Key enableMiniRovControlKey = Key.Digit3;

    [Tooltip("M: start MiniROV autonomous mission")]
    public Key startMiniRovMissionKey = Key.M;


    [Tooltip("4: recover MiniROV")]
    public Key recoverMiniRovKey = Key.Digit4;

    [Tooltip("5: hold tether")]
    public Key holdTetherKey = Key.Digit5;

    [Tooltip("F8: reset bridge")]
    public Key resetBridgeKey = Key.F8;

    [Header("Drone Readiness Policy")]
    public bool allowDeploymentFromPhysicalSurfaceContact = true;
    public bool blockDeploymentWhileDroneMissionActive = true;

    [Header("Tether Readiness Policy")]
    public float tetherAtMinimumToleranceM = 0.05f;
    public float tetherDeployedToleranceM = 0.04f;

    [Header("MiniROV Readiness Policy")]
    public bool requireMiniRovBackendReadyForControl = true;

    [Tooltip("If MiniROV is active, recovery is allowed only near the saved recovery point.")]
    public bool requireRovNearRecoveryPointWhenControlActive = true;

    public float recoveryRadiusM = 0.60f;

    [Tooltip("Emergency override for debug only. Keep OFF for normal demo.")]
    public bool allowEmergencyRecoveryOverride = false;

    [Header("Runtime State")]
    public BridgeSystemState systemState = BridgeSystemState.Unknown;

    public bool droneMissionActive;
    public bool droneMissionRunning;
    public bool droneMissionReady;
    public bool droneSurfaceReady;
    public bool droneMeasuredSurfaceContact;
    public string droneMissionState = "unknown";
    public string droneFlightModeState = "unknown";
    public string droneSurfaceReadinessSource = "unknown";

    public bool tetherBusy;
    public bool tetherDeploying;
    public bool tetherRecovering;
    public bool tetherAtMinimum;
    public bool tetherAwayFromMinimum;
    public bool tetherDeployedHolding;
    public string tetherState = "unknown";
    public float tetherLengthM;
    public float tetherTargetLengthM;
    public float tetherWinchRateMS;

    public bool miniRovPassiveOrCableOwned;
    public bool miniRovControlActive;
    public bool miniRovFault;
    public bool miniRovBackendReady;
    public string miniRovState = "unknown";
    public string miniRovBackendStatus = "unknown";

    public bool miniRovMissionActive;
    public bool miniRovMissionRecoveryReady;
    public string miniRovMissionState = "unknown";

    public bool recoveryPointCaptured;
    public Vector3 recoveryPointWorld;
    public float rovDistanceToRecoveryPointM = 999.0f;
    public bool rovRecoveryReady;

    [Header("Command Runtime")]
    public int commandsReceived;
    public int commandsApproved;
    public int commandsRejected;
    public string lastCommand = "none";
    public string lastDecision = "none";
    public string lastRejectReason = "none";
    public string lastExecutionEvent = "idle";

    private float publishTimerS;
    private Component[] cachedDroneSurfaceComponents;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void OnEnable()
    {
        AutoFindReferences();

        if (subscribeToCommonBus && bus != null)
        {
            bus.OnCommand += OnBusCommand;
        }
    }

    private void OnDisable()
    {
        if (bus != null)
        {
            bus.OnCommand -= OnBusCommand;
        }
    }

    private void Update()
    {
        if (!keyboardCommandsEnabled)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[startDroneMissionKey].wasPressedThisFrame)
        {
            RequestCommand(BridgeCommand.StartDroneMission, "keyboard_1");
        }

        if (Keyboard.current[deployTetherKey].wasPressedThisFrame)
        {
            RequestCommand(BridgeCommand.DeployTether, "keyboard_2");
        }

        if (Keyboard.current[enableMiniRovControlKey].wasPressedThisFrame)
        {
            RequestCommand(BridgeCommand.EnableMiniROVControl, "keyboard_3");
        }

        if (Keyboard.current[startMiniRovMissionKey].wasPressedThisFrame)
        {
            RequestCommand(BridgeCommand.StartMiniROVMission, "keyboard_M");
        }

        if (Keyboard.current[recoverMiniRovKey].wasPressedThisFrame)
        {
            RequestCommand(BridgeCommand.RecoverMiniROV, "keyboard_4");
        }

        if (Keyboard.current[holdTetherKey].wasPressedThisFrame)
        {
            RequestCommand(BridgeCommand.HoldTether, "keyboard_5");
        }

        if (Keyboard.current[resetBridgeKey].wasPressedThisFrame)
        {
            RequestCommand(BridgeCommand.ResetBridge, "keyboard_F8");
        }
    }

    private void FixedUpdate()
    {
        RefreshState();

        if (!publishBridgeStateToBus || bus == null)
        {
            return;
        }

        publishTimerS += Time.fixedDeltaTime;

        float period =
            1.0f / Mathf.Max(1.0f, statePublishHz);

        if (publishTimerS >= period)
        {
            publishTimerS -= period;
            PublishBridgeState();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (bus == null)
        {
            bus = MIMISKCommonBus.GetOrCreate();
        }

        if (droneObject == null)
        {
            droneObject = GameObject.Find("Drone");
        }

        if (droneObject != null)
        {
            if (droneRigidbody == null)
            {
                droneRigidbody = droneObject.GetComponent<Rigidbody>();
            }

            if (droneMission == null)
            {
                droneMission = droneObject.GetComponent<MIMISKDroneCoreMissionManager>();
            }

            if (droneFlightMode == null)
            {
                droneFlightMode = droneObject.GetComponent<MIMISKDroneCoreFlightModeManager>();
            }

            if (tetherManager == null)
            {
                tetherManager = droneObject.GetComponent<MIMISKDroneCoreTetherManager>();
            }

            if (finalPlanner == null)
            {
                finalPlanner = droneObject.GetComponent<MIMISKFinalMissionPlanner>();
            }

            cachedDroneSurfaceComponents =
                droneObject.GetComponentsInChildren<Component>(true);
        }

        if (miniRovObject == null)
        {
            miniRovObject = GameObject.Find("MiniROV");
        }

        if (miniRovObject != null)
        {
            if (miniRovRigidbody == null)
            {
                miniRovRigidbody = miniRovObject.GetComponent<Rigidbody>();
            }

            if (miniRovModule == null)
            {
                miniRovModule = miniRovObject.GetComponent<MIMISKMiniROVModule>();
            }

            if (miniRovMission == null)
            {
                miniRovMission = miniRovObject.GetComponent<MIMISKMiniROVMissionManager>();
            }

            if (rovTetherAnchor == null)
            {
                rovTetherAnchor = FindDeepChild(miniRovObject.transform, "ROV_TetherAnchor");
            }

            if (rovTetherAnchor == null)
            {
                rovTetherAnchor = FindDeepChild(miniRovObject.transform, "MiniROV_TetherPoint");
            }

            if (rovTetherAnchor == null)
            {
                rovTetherAnchor = FindDeepChild(miniRovObject.transform, "TetherPoint");
            }
        }
    }

    [ContextMenu("Refresh State")]
    public void RefreshState()
    {
        if (droneObject == null || miniRovObject == null)
        {
            AutoFindReferences();
        }

        RefreshDroneState();
        RefreshTetherState();
        RefreshMiniRovState();
        RefreshRecoveryState();
        DeriveSystemState();
    }

    private void RefreshDroneState()
    {
        droneMissionState =
            droneMission != null ? droneMission.missionState.ToString() : "missing";

        droneFlightModeState =
            droneFlightMode != null ? droneFlightMode.flightMode.ToString() : "missing";

        bool explicitMissionReady =
            droneMission != null &&
            (
                droneMission.missionState == MIMISKDroneCoreMissionManager.MissionState.ReadyForTetherDeployment ||
                droneMission.missionState == MIMISKDroneCoreMissionManager.MissionState.Completed
            );

        droneMissionActive =
            droneMission != null && droneMission.missionActive;

        droneMissionRunning =
            droneMissionActive && !explicitMissionReady;

        bool flightModeSurface =
            droneFlightModeState == "SurfaceStable" ||
            droneFlightModeState == "SurfaceHold";

        droneMeasuredSurfaceContact =
            HasMeasuredDroneSurfaceContact();

        droneSurfaceReady =
            flightModeSurface ||
            (
                allowDeploymentFromPhysicalSurfaceContact &&
                droneMeasuredSurfaceContact
            );

        bool alreadyOnSurfaceWithoutRunningMission =
            allowDeploymentFromPhysicalSurfaceContact &&
            droneSurfaceReady &&
            !droneMissionRunning;

        droneMissionReady =
            explicitMissionReady ||
            alreadyOnSurfaceWithoutRunningMission;

        if (blockDeploymentWhileDroneMissionActive && droneMissionRunning)
        {
            droneMissionReady = false;
        }

        if (explicitMissionReady)
        {
            droneSurfaceReadinessSource = "mission_ready";
        }
        else if (flightModeSurface)
        {
            droneSurfaceReadinessSource = "flight_mode_surface";
        }
        else if (droneMeasuredSurfaceContact)
        {
            droneSurfaceReadinessSource = "measured_surface_contact";
        }
        else
        {
            droneSurfaceReadinessSource = "not_surface_ready";
        }
    }

    private void RefreshTetherState()
    {
        if (tetherManager == null)
        {
            tetherState = "missing";
            tetherBusy = false;
            tetherDeploying = false;
            tetherRecovering = false;
            tetherAtMinimum = false;
            tetherAwayFromMinimum = false;
            tetherDeployedHolding = false;
            tetherLengthM = 0.0f;
            tetherTargetLengthM = 0.0f;
            tetherWinchRateMS = 0.0f;
            return;
        }

        MIMISKDroneCoreTetherManager.TetherState ts =
            tetherManager.tetherState;

        tetherState = ts.ToString();

        tetherLengthM =
            tetherManager.deployedLengthM;

        tetherTargetLengthM =
            tetherManager.targetLengthM;

        tetherWinchRateMS =
            tetherManager.winchCommandRateMS;

        float minLength =
            tetherManager.minimumLengthM;

        bool physicallyAtMinimum =
            tetherManager.deployedLengthM <=
            minLength + tetherAtMinimumToleranceM;

        bool targetIsDeployment =
            tetherManager.targetLengthM >
            minLength + tetherAtMinimumToleranceM;

        bool physicallyAtTarget =
            targetIsDeployment &&
            tetherManager.deployedLengthM >=
            tetherManager.targetLengthM - tetherDeployedToleranceM;

        tetherAtMinimum =
            physicallyAtMinimum;

        tetherAwayFromMinimum =
            !physicallyAtMinimum;

        // IMPORTANT:
        // Use exact enum states. Do not use string Contains().
        // "Recovered" contains "recover", and "HoldingDeployed" contains "deploy",
        // which caused false busy states after recovery/deployment.
        tetherDeploying =
            ts == MIMISKDroneCoreTetherManager.TetherState.Deploying &&
            !physicallyAtTarget;

        tetherRecovering =
            ts == MIMISKDroneCoreTetherManager.TetherState.Recovering &&
            !physicallyAtMinimum;

        tetherBusy =
            tetherDeploying ||
            tetherRecovering;

        bool explicitHolding =
            ts == MIMISKDroneCoreTetherManager.TetherState.HoldingDeployed;

        bool staleDeployButActuallyAtTarget =
            ts == MIMISKDroneCoreTetherManager.TetherState.Deploying &&
            physicallyAtTarget;

        bool staleRecoveredButActuallyAway =
            ts == MIMISKDroneCoreTetherManager.TetherState.Recovered &&
            tetherAwayFromMinimum;

        tetherDeployedHolding =
            tetherAwayFromMinimum &&
            !tetherBusy &&
            (
                explicitHolding ||
                staleDeployButActuallyAtTarget ||
                physicallyAtTarget ||
                staleRecoveredButActuallyAway
            );

        // Soft synchronization only for bridge predicates:
        // If the tether state is Recovering/Recovered but the measured length is
        // physically back at minimum, the bridge treats the tether as recovered.
        // We do not create a new tether behavior here.
        if (physicallyAtMinimum &&
            (
                ts == MIMISKDroneCoreTetherManager.TetherState.Recovering ||
                ts == MIMISKDroneCoreTetherManager.TetherState.Recovered ||
                ts == MIMISKDroneCoreTetherManager.TetherState.Locked ||
                ts == MIMISKDroneCoreTetherManager.TetherState.Ready ||
                ts == MIMISKDroneCoreTetherManager.TetherState.Idle
            ))
        {
            tetherBusy = false;
            tetherDeploying = false;
            tetherRecovering = false;
            tetherAwayFromMinimum = false;
            tetherDeployedHolding = false;
        }
    }

    private void RefreshMiniRovState()
    {
        if (miniRovModule == null)
        {
            miniRovState = "missing";
            miniRovPassiveOrCableOwned = false;
            miniRovControlActive = false;
            miniRovFault = false;
            miniRovBackendReady = false;
            miniRovBackendStatus = "missing_minirov_module";
            return;
        }

        miniRovState =
            miniRovModule.state.ToString();

        miniRovPassiveOrCableOwned =
            miniRovModule.state == MIMISKMiniROVModule.MiniROVState.PassiveKinematic ||
            miniRovModule.state == MIMISKMiniROVModule.MiniROVState.CableAttachedKinematic ||
            miniRovModule.state == MIMISKMiniROVModule.MiniROVState.FreeDynamicPassive ||
            miniRovModule.state == MIMISKMiniROVModule.MiniROVState.RecoveryKinematic;

        miniRovControlActive =
            miniRovModule.state == MIMISKMiniROVModule.MiniROVState.ExternalControlActive;

        miniRovFault =
            miniRovModule.state == MIMISKMiniROVModule.MiniROVState.Fault;


        if (miniRovMission != null)
        {
            miniRovMissionState = miniRovMission.missionState.ToString();
            miniRovMissionActive = miniRovMission.missionActive;
            miniRovMissionRecoveryReady = miniRovMission.recoveryReady;
        }
        else
        {
            miniRovMissionState = "missing";
            miniRovMissionActive = false;
            miniRovMissionRecoveryReady = false;
        }


        if (requireMiniRovBackendReadyForControl)
        {
            miniRovBackendReady =
                miniRovModule.CheckExternalStackReady();

            miniRovBackendStatus =
                miniRovModule.externalStackStatus;
        }
        else
        {
            miniRovBackendReady = true;
            miniRovBackendStatus = "backend_check_disabled";
        }
    }

    private void RefreshRecoveryState()
    {
        if (recoveryPointCaptured && rovTetherAnchor != null)
        {
            rovDistanceToRecoveryPointM =
                Vector3.Distance(
                    rovTetherAnchor.position,
                    recoveryPointWorld
                );

            rovRecoveryReady =
                rovDistanceToRecoveryPointM <= recoveryRadiusM;
        }
        else
        {
            rovDistanceToRecoveryPointM = 999.0f;
            rovRecoveryReady = false;
        }
    }

    private void DeriveSystemState()
    {
        if (miniRovFault)
        {
            systemState = BridgeSystemState.Fault;
            return;
        }

        if (tetherRecovering)
        {
            systemState = BridgeSystemState.Recovering;
            return;
        }

        if ((miniRovControlActive && rovRecoveryReady) || miniRovMissionRecoveryReady)
        {
            systemState = BridgeSystemState.ROVRecoveryReady;
            return;
        }

        if (miniRovControlActive)
        {
            systemState = BridgeSystemState.ROVControlActive;
            return;
        }

        if (tetherDeployedHolding)
        {
            systemState = BridgeSystemState.ROVLoweredControlInactive;
            return;
        }

        if (tetherDeploying)
        {
            systemState = BridgeSystemState.TetherDeploying;
            return;
        }

        if (droneMissionRunning)
        {
            systemState = BridgeSystemState.DroneMissionRunning;
            return;
        }

        // Fully recovered / safe surface condition:
        // tether measured at minimum, no winch motion, MiniROV passive/cable-owned.
        bool tetherSafelyRecovered =
            tetherAtMinimum &&
            !tetherBusy &&
            miniRovPassiveOrCableOwned &&
            !miniRovControlActive;

        if (tetherSafelyRecovered)
        {
            if (droneSurfaceReady && droneMissionReady)
            {
                systemState = BridgeSystemState.DroneReadyForDeployment;
            }
            else
            {
                systemState = BridgeSystemState.IdleSurface;
            }

            return;
        }

        if (droneSurfaceReady && droneMissionReady)
        {
            systemState = BridgeSystemState.DroneReadyForDeployment;
            return;
        }

        systemState = BridgeSystemState.Unknown;
    }

    private void OnBusCommand(MIMISKCommandMessage command)
    {
        if (command == null)
        {
            return;
        }

        if (command.source == "MIMISKCommunicationBridge")
        {
            return;
        }

        if (command.target != MIMISKSubsystem.Mission &&
            command.target != MIMISKSubsystem.Bridge &&
            command.target != MIMISKSubsystem.Drone &&
            command.target != MIMISKSubsystem.Tether &&
            command.target != MIMISKSubsystem.MiniROV)
        {
            return;
        }

        BridgeCommand bridgeCommand;

        if (!TryConvertCommand(command.verb, out bridgeCommand))
        {
            return;
        }

        RequestCommand(
            bridgeCommand,
            "bus:" + command.source
        );
    }

    private bool TryConvertCommand(
        MIMISKCommandVerb verb,
        out BridgeCommand bridgeCommand)
    {
        bridgeCommand = BridgeCommand.ResetBridge;

        if (verb == MIMISKCommandVerb.StartMission)
        {
            bridgeCommand = BridgeCommand.StartDroneMission;
            return true;
        }

        if (verb == MIMISKCommandVerb.DeployTether)
        {
            bridgeCommand = BridgeCommand.DeployTether;
            return true;
        }

        if (verb == MIMISKCommandVerb.EnableMiniROVControl)
        {
            bridgeCommand = BridgeCommand.EnableMiniROVControl;
            return true;
        }

        if (verb == MIMISKCommandVerb.StartMiniROVMission)
        {
            bridgeCommand = BridgeCommand.StartMiniROVMission;
            return true;
        }

        if (verb == MIMISKCommandVerb.RecoverMiniROV)
        {
            bridgeCommand = BridgeCommand.RecoverMiniROV;
            return true;
        }

        if (verb == MIMISKCommandVerb.HoldTether)
        {
            bridgeCommand = BridgeCommand.HoldTether;
            return true;
        }

        if (verb == MIMISKCommandVerb.ResetMission ||
            verb == MIMISKCommandVerb.ResetFault)
        {
            bridgeCommand = BridgeCommand.ResetBridge;
            return true;
        }

        return false;
    }

    public void RequestCommand(
        BridgeCommand command,
        string source)
    {
        commandsReceived++;
        lastCommand = source + " / " + command;

        RefreshState();

        string rejectReason;

        if (!IsCommandAllowed(command, out rejectReason))
        {
            RejectCommand(command, rejectReason);
            return;
        }

        ExecuteCommand(command);
    }

    private bool IsCommandAllowed(
        BridgeCommand command,
        out string reason)
    {
        reason = "allowed";

        if (command == BridgeCommand.StartDroneMission)
        {
            return CanStartDroneMission(out reason);
        }

        if (command == BridgeCommand.DeployTether)
        {
            return CanDeployTether(out reason);
        }

        if (command == BridgeCommand.EnableMiniROVControl)
        {
            return CanEnableMiniROVControl(out reason);
        }

        if (command == BridgeCommand.StartMiniROVMission)
        {
            return CanStartMiniROVMission(out reason);
        }

        if (command == BridgeCommand.RecoverMiniROV)
        {
            return CanRecoverMiniROV(out reason);
        }

        if (command == BridgeCommand.HoldTether)
        {
            return CanHoldTether(out reason);
        }

        if (command == BridgeCommand.ResetBridge)
        {
            reason = "reset_allowed";
            return true;
        }

        reason = "unknown_command";
        return false;
    }

    private bool CanStartDroneMission(out string reason)
    {
        if (droneMissionRunning)
        {
            reason = "drone_mission_already_running";
            return false;
        }

        if (tetherBusy || tetherAwayFromMinimum)
        {
            reason = "cannot_start_drone_mission_tether_is_deployed_or_busy";
            return false;
        }

        if (miniRovControlActive)
        {
            reason = "cannot_start_drone_mission_minirov_control_active";
            return false;
        }

        if (!miniRovPassiveOrCableOwned)
        {
            reason = "cannot_start_drone_mission_minirov_not_passive_" + miniRovState;
            return false;
        }

        reason = "start_drone_mission_allowed";
        return true;
    }

    private bool CanDeployTether(out string reason)
    {
        if (droneMissionRunning)
        {
            reason = "deploy_rejected_drone_mission_running";
            return false;
        }

        if (!droneSurfaceReady || !droneMissionReady)
        {
            reason =
                "deploy_rejected_drone_not_surface_ready_source_" +
                droneSurfaceReadinessSource;

            return false;
        }

        if (tetherBusy)
        {
            reason = "deploy_rejected_tether_busy_" + tetherState;
            return false;
        }

        if (tetherAwayFromMinimum)
        {
            reason = "deploy_rejected_tether_already_deployed";
            return false;
        }

        if (miniRovControlActive)
        {
            reason = "deploy_rejected_minirov_control_active";
            return false;
        }

        if (!miniRovPassiveOrCableOwned)
        {
            reason = "deploy_rejected_minirov_not_passive_or_cable_owned_" + miniRovState;
            return false;
        }

        reason = "deploy_allowed";
        return true;
    }

    private bool CanEnableMiniROVControl(out string reason)
    {
        if (!droneSurfaceReady)
        {
            reason = "rov_control_rejected_drone_not_surface_ready";
            return false;
        }

        if (tetherBusy)
        {
            reason = "rov_control_rejected_tether_busy_" + tetherState;
            return false;
        }

        if (!tetherDeployedHolding)
        {
            reason = "rov_control_rejected_tether_not_deployed_holding";
            return false;
        }

        if (miniRovControlActive)
        {
            reason = "rov_control_rejected_already_active";
            return false;
        }

        if (!miniRovBackendReady)
        {
            reason = "rov_control_rejected_backend_not_ready_" + miniRovBackendStatus;
            return false;
        }

        reason = "rov_control_allowed";
        return true;
    }


    private bool CanStartMiniROVMission(out string reason)
    {
        if (!droneSurfaceReady)
        {
            reason = "minirov_mission_rejected_drone_not_surface_ready";
            return false;
        }

        if (tetherBusy)
        {
            reason = "minirov_mission_rejected_tether_busy_" + tetherState;
            return false;
        }

        if (!tetherDeployedHolding)
        {
            reason = "minirov_mission_rejected_tether_not_deployed_holding";
            return false;
        }

        if (miniRovControlActive)
        {
            reason = "minirov_mission_rejected_manual_control_active";
            return false;
        }

        if (miniRovMissionActive)
        {
            reason = "minirov_mission_rejected_already_running";
            return false;
        }

        if (miniRovMission == null)
        {
            reason = "minirov_mission_rejected_missing_mission_manager";
            return false;
        }

        if (!miniRovMission.CanStartMission())
        {
            reason = "minirov_mission_rejected_" + miniRovMission.lastEvent;
            return false;
        }

        reason = "minirov_mission_allowed";
        return true;
    }

    private bool CanRecoverMiniROV(out string reason)
    {
        if (droneMissionRunning)
        {
            reason = "recovery_rejected_drone_mission_running";
            return false;
        }

        if (miniRovMissionActive && !miniRovMissionRecoveryReady)
        {
            reason = "recovery_rejected_minirov_mission_active_not_recovery_ready";
            return false;
        }

        bool somethingDeployed =
            tetherAwayFromMinimum ||
            tetherBusy ||
            miniRovControlActive ||
            miniRovMissionRecoveryReady ||
            systemState == BridgeSystemState.ROVLoweredControlInactive;

        if (!somethingDeployed)
        {
            reason = "recovery_rejected_nothing_deployed";
            return false;
        }

        if (miniRovControlActive &&
            requireRovNearRecoveryPointWhenControlActive &&
            !allowEmergencyRecoveryOverride)
        {
            if (!recoveryPointCaptured)
            {
                reason = "recovery_rejected_no_recovery_point_captured";
                return false;
            }

            if (!rovRecoveryReady)
            {
                reason =
                    "recovery_rejected_rov_not_near_recovery_point_distance_" +
                    rovDistanceToRecoveryPointM.ToString("F2") +
                    "_m";

                return false;
            }
        }

        reason = "recovery_allowed";
        return true;
    }

    private bool CanHoldTether(out string reason)
    {
        if (tetherBusy || tetherAwayFromMinimum)
        {
            reason = "hold_tether_allowed";
            return true;
        }

        reason = "hold_rejected_tether_locked_or_not_deployed";
        return false;
    }

    private void ExecuteCommand(BridgeCommand command)
    {
        commandsApproved++;
        lastDecision = "approved";
        lastRejectReason = "none";

        if (command == BridgeCommand.StartDroneMission)
        {
            ExecuteStartDroneMission();
        }
        else if (command == BridgeCommand.DeployTether)
        {
            ExecuteDeployTether();
        }
        else if (command == BridgeCommand.EnableMiniROVControl)
        {
            ExecuteEnableMiniROVControl();
        }
        else if (command == BridgeCommand.StartMiniROVMission)
        {
            ExecuteStartMiniROVMission();
        }
        else if (command == BridgeCommand.RecoverMiniROV)
        {
            ExecuteRecoverMiniROV();
        }
        else if (command == BridgeCommand.HoldTether)
        {
            ExecuteHoldTether();
        }
        else if (command == BridgeCommand.ResetBridge)
        {
            ExecuteResetBridge();
        }

        PublishBridgeState();
    }

    private void ExecuteStartDroneMission()
    {
        if (droneMission == null)
        {
            lastExecutionEvent = "start_failed_missing_drone_mission";
            Debug.LogWarning("[MIMISK Bridge] Start failed: missing drone mission.");
            return;
        }

        droneMission.StartMission();
        lastExecutionEvent = "start_drone_mission_called_existing_droneMission_StartMission";
    }

    private void ExecuteDeployTether()
    {
        if (finalPlanner != null)
        {
            finalPlanner.RequestTetherDeployment();
            lastExecutionEvent = "deploy_called_existing_finalPlanner_RequestTetherDeployment";
            return;
        }

        if (tetherManager != null)
        {
            tetherManager.StartDeployment();
            lastExecutionEvent = "deploy_called_existing_tetherManager_StartDeployment";
            return;
        }

        lastExecutionEvent = "deploy_failed_missing_finalPlanner_and_tetherManager";
    }

    private void ExecuteEnableMiniROVControl()
    {
        CaptureRecoveryPoint();

        if (finalPlanner != null)
        {
            finalPlanner.RequestMiniRovControlHandoff();
            lastExecutionEvent = "rov_control_called_existing_finalPlanner_RequestMiniRovControlHandoff";
            return;
        }

        if (miniRovModule != null)
        {
            miniRovModule.EnableExternalControl();
            lastExecutionEvent = "rov_control_called_existing_miniRovModule_EnableExternalControl";
            return;
        }

        lastExecutionEvent = "rov_control_failed_missing_finalPlanner_and_miniRovModule";
    }


    private void ExecuteStartMiniROVMission()
    {
        if (miniRovMission == null)
        {
            lastExecutionEvent = "minirov_mission_failed_missing_mission_manager";
            return;
        }

        CaptureRecoveryPoint();

        miniRovMission.StartMissionFromBridge(recoveryPointWorld);

        lastExecutionEvent = "minirov_mission_started_existing_miniRovMission_StartMissionFromBridge";
    }

    private void ExecuteRecoverMiniROV()
    {
        if (miniRovMission != null && miniRovMission.missionState == MIMISKMiniROVMissionManager.MiniROVMissionState.RecoveryReady)
        {
            miniRovMission.PrepareForRecovery();
        }

        if (finalPlanner != null)
        {
            finalPlanner.RequestRecovery();
            lastExecutionEvent = "recovery_called_existing_finalPlanner_RequestRecovery";
            return;
        }

        if (tetherManager != null)
        {
            if (miniRovModule != null)
            {
                miniRovModule.SetPassiveKinematic();
            }

            tetherManager.StartRecovery();
            lastExecutionEvent = "recovery_called_existing_tetherManager_StartRecovery";
            return;
        }

        lastExecutionEvent = "recovery_failed_missing_finalPlanner_and_tetherManager";
    }

    private void ExecuteHoldTether()
    {
        if (finalPlanner != null)
        {
            finalPlanner.HoldTether();
            lastExecutionEvent = "hold_called_existing_finalPlanner_HoldTether";
            return;
        }

        if (tetherManager != null)
        {
            tetherManager.StopWinch();
            lastExecutionEvent = "hold_called_existing_tetherManager_StopWinch";
            return;
        }

        lastExecutionEvent = "hold_failed_missing_tether";
    }

    private void ExecuteResetBridge()
    {
        recoveryPointCaptured = false;
        recoveryPointWorld = Vector3.zero;
        rovDistanceToRecoveryPointM = 999.0f;
        rovRecoveryReady = false;

        if (finalPlanner != null)
        {
            finalPlanner.ResetPlanner();
        }

        lastExecutionEvent = "bridge_reset_called_existing_finalPlanner_if_available";
    }

    private void RejectCommand(
        BridgeCommand command,
        string reason)
    {
        commandsRejected++;
        lastDecision = "rejected";
        lastRejectReason = reason;

        Debug.LogWarning(
            "[MIMISK Bridge] Rejected " +
            command +
            ": " +
            reason
        );

        PublishBridgeState();
    }

    private void CaptureRecoveryPoint()
    {
        if (rovTetherAnchor != null)
        {
            recoveryPointWorld = rovTetherAnchor.position;
        }
        else if (miniRovObject != null)
        {
            recoveryPointWorld = miniRovObject.transform.position;
        }
        else
        {
            recoveryPointWorld = Vector3.zero;
        }

        recoveryPointCaptured = true;
        rovDistanceToRecoveryPointM = 0.0f;
        rovRecoveryReady = true;
    }

    private bool HasMeasuredDroneSurfaceContact()
    {
        if (cachedDroneSurfaceComponents == null ||
            cachedDroneSurfaceComponents.Length == 0)
        {
            if (droneObject != null)
            {
                cachedDroneSurfaceComponents =
                    droneObject.GetComponentsInChildren<Component>(true);
            }
        }

        if (cachedDroneSurfaceComponents == null)
        {
            return false;
        }

        for (int i = 0; i < cachedDroneSurfaceComponents.Length; i++)
        {
            Component c = cachedDroneSurfaceComponents[i];

            if (c == null)
            {
                continue;
            }

            string typeName =
                c.GetType().Name.ToLowerInvariant();

            if (!typeName.Contains("surfacebuoyancy") &&
                !typeName.Contains("watercontact"))
            {
                continue;
            }

            bool b;
            int n;

            if (TryReadBool(c, "isInWater", out b) && b) return true;
            if (TryReadBool(c, "isTouchingWater", out b) && b) return true;
            if (TryReadBool(c, "waterContact", out b) && b) return true;
            if (TryReadBool(c, "contact", out b) && b) return true;
            if (TryReadBool(c, "hasContact", out b) && b) return true;

            if (TryReadInt(c, "activePointCount", out n) && n > 0) return true;
            if (TryReadInt(c, "activePoints", out n) && n > 0) return true;
            if (TryReadInt(c, "buoyancyActivePoints", out n) && n > 0) return true;
            if (TryReadInt(c, "waterContactCount", out n) && n > 0) return true;
        }

        return false;
    }

    private bool TryReadBool(
        Component component,
        string memberName,
        out bool value)
    {
        value = false;

        if (component == null)
        {
            return false;
        }

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        FieldInfo field =
            component.GetType().GetField(memberName, flags);

        if (field != null && field.FieldType == typeof(bool))
        {
            value = (bool)field.GetValue(component);
            return true;
        }

        PropertyInfo prop =
            component.GetType().GetProperty(memberName, flags);

        if (prop != null &&
            prop.PropertyType == typeof(bool) &&
            prop.CanRead)
        {
            value = (bool)prop.GetValue(component, null);
            return true;
        }

        return false;
    }

    private bool TryReadInt(
        Component component,
        string memberName,
        out int value)
    {
        value = 0;

        if (component == null)
        {
            return false;
        }

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        FieldInfo field =
            component.GetType().GetField(memberName, flags);

        if (field != null && field.FieldType == typeof(int))
        {
            value = (int)field.GetValue(component);
            return true;
        }

        PropertyInfo prop =
            component.GetType().GetProperty(memberName, flags);

        if (prop != null &&
            prop.PropertyType == typeof(int) &&
            prop.CanRead)
        {
            value = (int)prop.GetValue(component, null);
            return true;
        }

        return false;
    }

    private void PublishBridgeState()
    {
        if (bus == null)
        {
            return;
        }

        MIMISKStateMessage msg =
            new MIMISKStateMessage();

        msg.subsystem = MIMISKSubsystem.Bridge;
        msg.moduleName = "MIMISKCommunicationBridge";
        msg.mode = systemState.ToString();
        msg.health =
            systemState == BridgeSystemState.Fault
                ? MIMISKHealth.Fault
                : MIMISKHealth.OK;

        msg.ready =
            systemState == BridgeSystemState.IdleSurface ||
            systemState == BridgeSystemState.DroneReadyForDeployment ||
            systemState == BridgeSystemState.ROVLoweredControlInactive ||
            systemState == BridgeSystemState.ROVRecoveryReady;

        msg.active =
            systemState != BridgeSystemState.Unknown &&
            systemState != BridgeSystemState.Fault;

        msg.fault =
            systemState == BridgeSystemState.Fault;

        msg.position =
            droneObject != null ? droneObject.transform.position : Vector3.zero;

        msg.attitude =
            droneObject != null ? droneObject.transform.rotation : Quaternion.identity;

        msg.scalarA = tetherLengthM;
        msg.scalarB = rovDistanceToRecoveryPointM;
        msg.scalarC = commandsRejected;

        msg.eventText =
            "decision=" + lastDecision +
            "; reject=" + lastRejectReason +
            "; exec=" + lastExecutionEvent +
            "; minirovMission=" + miniRovMissionState;

        bus.PublishState(msg);
    }

    private Transform FindDeepChild(
        Transform root,
        string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found =
                FindDeepChild(root.GetChild(i), childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
