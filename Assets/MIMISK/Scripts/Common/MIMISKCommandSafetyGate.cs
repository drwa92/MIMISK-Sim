using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKCommandSafetyGate : MonoBehaviour
{
    public enum GateSystemState
    {
        Unknown,
        IdleOnSurface,
        DroneMissionRunning,
        DroneSurfaceReady,
        TetherDeploying,
        ROVLoweredControlInactive,
        ROVControlActive,
        Recovering,
        Recovered,
        Fault
    }

    [Header("Common Interface")]
    public MIMISKCommonBus bus;
    public bool gateEnabled = true;

    [Tooltip("The command bridge should execute only commands re-published by this safety gate.")]
    public string authorizedSourceName = "MIMISKCommandSafetyGate";

    [Header("Existing Module References")]
    public GameObject droneObject;
    public MIMISKDroneCoreMissionManager droneMission;
    public MIMISKDroneCoreFlightModeManager droneFlightMode;
    public MIMISKDroneCoreTetherManager tetherManager;
    public MIMISKFinalMissionPlanner finalPlanner;

    public GameObject miniRovObject;
    public MIMISKMiniROVModule miniRovModule;
    public Rigidbody miniRovRigidbody;

    [Header("Safety Settings")]
    public bool allowDeploymentFromPhysicalSurfaceState = true;
    public bool blockDeploymentWhileDroneMissionActive = true;
    public bool requireExternalMiniRovStackForControl = true;

    public float minimumTetherAwayToleranceM = 0.05f;
    public float deployedLengthToleranceM = 0.04f;

    [Header("Runtime Readiness")]
    public GateSystemState systemState = GateSystemState.Unknown;

    public bool droneSurfaceReady;
    public bool droneMissionReady;
    public bool droneMissionRunning;

    public bool surfaceContactDetected;
    public string surfaceReadinessSource = "unknown";

    public bool tetherBusy;
    public bool tetherDeploying;
    public bool tetherRecovering;
    public bool tetherAtMinimum;
    public bool tetherAway;
    public bool tetherDeployedHolding;
    public string tetherState = "unknown";

    public bool miniRovPassiveOrCableOwned;
    public bool miniRovControlActive;
    public bool miniRovBackendReady;
    public string miniRovState = "unknown";
    public string miniRovBackendStatus = "unknown";

    [Header("Command Runtime")]
    public int receivedCommands;
    public int approvedCommands;
    public int rejectedCommands;
    public string lastDecision = "none";
    public string lastRejectReason = "none";
    public string lastApprovedCommand = "none";

    private void Awake()
    {
        AutoFindReferences();
    }

    private void OnEnable()
    {
        AutoFindReferences();

        if (bus != null)
        {
            bus.OnCommand += OnCommand;
        }
    }

    private void OnDisable()
    {
        if (bus != null)
        {
            bus.OnCommand -= OnCommand;
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
        }

        if (miniRovObject == null)
        {
            miniRovObject = GameObject.Find("MiniROV");
        }

        if (miniRovObject != null)
        {
            if (miniRovModule == null)
            {
                miniRovModule = miniRovObject.GetComponent<MIMISKMiniROVModule>();
            }

            if (miniRovRigidbody == null)
            {
                miniRovRigidbody = miniRovObject.GetComponent<Rigidbody>();
            }
        }
    }

    private void OnCommand(MIMISKCommandMessage command)
    {
        if (!gateEnabled || command == null)
        {
            return;
        }

        // Avoid approving our own approved command again.
        if (command.source == authorizedSourceName)
        {
            return;
        }

        receivedCommands++;

        RefreshReadiness();

        string reason;
        bool allowed = IsAllowed(command, out reason);

        if (allowed)
        {
            PublishApprovedCommand(command);
        }
        else
        {
            RejectCommand(command, reason);
        }
    }

    [ContextMenu("Refresh Readiness")]
    public void RefreshReadiness()
    {
        AutoFindReferences();

        RefreshDroneReadiness();
        RefreshTetherReadiness();
        RefreshMiniROVReadiness();
        RefreshSystemState();
    }

    private void RefreshDroneReadiness()
    {
        bool flightModeSurface = false;

        if (droneFlightMode != null)
        {
            flightModeSurface =
                droneFlightMode.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceStable ||
                droneFlightMode.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceHold;
        }

        surfaceContactDetected =
            HasMeasuredDroneSurfaceContact();

        droneSurfaceReady =
            flightModeSurface ||
            (
                allowDeploymentFromPhysicalSurfaceState &&
                surfaceContactDetected
            );

        bool explicitMissionReady = false;

        if (droneMission != null)
        {
            explicitMissionReady =
                droneMission.missionState == MIMISKDroneCoreMissionManager.MissionState.ReadyForTetherDeployment ||
                droneMission.missionState == MIMISKDroneCoreMissionManager.MissionState.Completed;

            droneMissionRunning =
                droneMission.missionActive && !explicitMissionReady;
        }
        else
        {
            droneMissionRunning = false;
        }

        bool alreadyOnSurfaceNoMission =
            allowDeploymentFromPhysicalSurfaceState &&
            droneSurfaceReady &&
            !droneMissionRunning;

        droneMissionReady =
            explicitMissionReady ||
            alreadyOnSurfaceNoMission;

        if (blockDeploymentWhileDroneMissionActive && droneMissionRunning)
        {
            droneMissionReady = false;
        }

        if (explicitMissionReady)
        {
            surfaceReadinessSource = "mission_ready";
        }
        else if (flightModeSurface)
        {
            surfaceReadinessSource = "flight_mode_surface";
        }
        else if (surfaceContactDetected)
        {
            surfaceReadinessSource = "measured_surface_contact";
        }
        else
        {
            surfaceReadinessSource = "not_surface_ready";
        }
    }

    private void RefreshTetherReadiness()
    {
        if (tetherManager == null)
        {
            tetherState = "missing";
            tetherBusy = false;
            tetherDeploying = false;
            tetherRecovering = false;
            tetherAtMinimum = false;
            tetherAway = false;
            tetherDeployedHolding = false;
            return;
        }

        tetherState = tetherManager.tetherState.ToString();

        tetherDeploying =
            tetherState.ToLowerInvariant().Contains("deploy");

        tetherRecovering =
            tetherState.ToLowerInvariant().Contains("recover");

        tetherBusy =
            tetherDeploying ||
            tetherRecovering;

        tetherAtMinimum =
            tetherManager.deployedLengthM <=
            tetherManager.minimumLengthM + minimumTetherAwayToleranceM;

        tetherAway =
            tetherManager.deployedLengthM >
            tetherManager.minimumLengthM + minimumTetherAwayToleranceM;

        bool holding =
            tetherState.ToLowerInvariant().Contains("holding") ||
            tetherState.ToLowerInvariant().Contains("hold");

        tetherDeployedHolding =
            tetherAway &&
            !tetherBusy &&
            (
                holding ||
                tetherManager.deployedLengthM >= tetherManager.targetLengthM - deployedLengthToleranceM
            );
    }

    private void RefreshMiniROVReadiness()
    {
        if (miniRovModule == null)
        {
            miniRovState = "missing";
            miniRovPassiveOrCableOwned = false;
            miniRovControlActive = false;
            miniRovBackendReady = false;
            miniRovBackendStatus = "missing_minirov_module";
            return;
        }

        miniRovState =
            miniRovModule.state.ToString();

        miniRovPassiveOrCableOwned =
            miniRovModule.state == MIMISKMiniROVModule.MiniROVState.PassiveKinematic ||
            miniRovModule.state == MIMISKMiniROVModule.MiniROVState.CableAttachedKinematic ||
            miniRovModule.state == MIMISKMiniROVModule.MiniROVState.FreeDynamicPassive;

        miniRovControlActive =
            miniRovModule.state == MIMISKMiniROVModule.MiniROVState.ExternalControlActive;

        if (!requireExternalMiniRovStackForControl)
        {
            miniRovBackendReady = true;
            miniRovBackendStatus = "external_stack_check_disabled";
        }
        else
        {
            miniRovBackendReady =
                miniRovModule.CheckExternalStackReady();

            miniRovBackendStatus =
                miniRovModule.externalStackStatus;
        }
    }

    private void RefreshSystemState()
    {
        if (miniRovModule != null &&
            miniRovModule.state == MIMISKMiniROVModule.MiniROVState.Fault)
        {
            systemState = GateSystemState.Fault;
            return;
        }

        if (tetherRecovering)
        {
            systemState = GateSystemState.Recovering;
            return;
        }

        if (miniRovControlActive)
        {
            systemState = GateSystemState.ROVControlActive;
            return;
        }

        if (tetherDeployedHolding && !miniRovControlActive)
        {
            systemState = GateSystemState.ROVLoweredControlInactive;
            return;
        }

        if (tetherDeploying)
        {
            systemState = GateSystemState.TetherDeploying;
            return;
        }

        if (droneMissionRunning)
        {
            systemState = GateSystemState.DroneMissionRunning;
            return;
        }

        if (droneSurfaceReady && droneMissionReady)
        {
            systemState = GateSystemState.DroneSurfaceReady;
            return;
        }

        if (tetherAtMinimum && !tetherBusy)
        {
            systemState = GateSystemState.IdleOnSurface;
            return;
        }

        systemState = GateSystemState.Unknown;
    }

    private bool IsAllowed(MIMISKCommandMessage command, out string reason)
    {
        reason = "allowed";

        switch (command.verb)
        {
            case MIMISKCommandVerb.StartMission:
                return CanStartDroneMission(out reason);

            case MIMISKCommandVerb.DeployTether:
                return CanDeployTether(out reason);

            case MIMISKCommandVerb.EnableMiniROVControl:
                return CanEnableMiniROVControl(out reason);

            case MIMISKCommandVerb.RecoverMiniROV:
                return CanRecoverMiniROV(out reason);

            case MIMISKCommandVerb.HoldTether:
                return CanHoldTether(out reason);

            case MIMISKCommandVerb.ResetMission:
            case MIMISKCommandVerb.ResetFault:
                reason = "reset_always_allowed";
                return true;

            default:
                reason = "unsupported_command_" + command.verb;
                return false;
        }
    }

    private bool CanStartDroneMission(out string reason)
    {
        if (droneMissionRunning)
        {
            reason = "drone_mission_already_running";
            return false;
        }

        if (tetherAway || tetherBusy)
        {
            reason = "cannot_start_drone_mission_tether_is_deployed_or_busy";
            return false;
        }

        if (miniRovControlActive)
        {
            reason = "cannot_start_drone_mission_minirov_control_active";
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
                surfaceReadinessSource;

            return false;
        }

        if (tetherBusy)
        {
            reason = "deploy_rejected_tether_busy_" + tetherState;
            return false;
        }

        if (tetherAway)
        {
            reason = "deploy_rejected_tether_already_deployed";
            return false;
        }

        if (miniRovControlActive)
        {
            reason = "deploy_rejected_minirov_already_active";
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

    private bool CanRecoverMiniROV(out string reason)
    {
        if (droneMissionRunning)
        {
            reason = "recovery_rejected_drone_mission_running";
            return false;
        }

        bool somethingToRecover =
            tetherAway ||
            tetherBusy ||
            miniRovControlActive ||
            systemState == GateSystemState.ROVLoweredControlInactive;

        if (!somethingToRecover)
        {
            reason = "recovery_rejected_nothing_deployed";
            return false;
        }

        reason = "recovery_allowed";
        return true;
    }

    private bool CanHoldTether(out string reason)
    {
        if (tetherBusy || tetherAway)
        {
            reason = "hold_tether_allowed";
            return true;
        }

        reason = "hold_rejected_tether_not_deployed";
        return false;
    }

    private void PublishApprovedCommand(MIMISKCommandMessage original)
    {
        MIMISKCommandMessage approved =
            new MIMISKCommandMessage();

        approved.source = authorizedSourceName;
        approved.target = original.target;
        approved.verb = original.verb;
        approved.value = original.value;
        approved.text =
            "approved:" +
            original.text +
            " state=" +
            systemState;

        approvedCommands++;
        lastDecision = "approved";
        lastApprovedCommand = approved.target + " / " + approved.verb;
        lastRejectReason = "none";

        bus.SendCommand(approved);
    }

    private void RejectCommand(MIMISKCommandMessage command, string reason)
    {
        rejectedCommands++;
        lastDecision = "rejected";
        lastRejectReason = reason;

        Debug.LogWarning(
            "[MIMISK Safety Gate] Rejected " +
            command.verb +
            " from " +
            command.source +
            ": " +
            reason
        );

        MIMISKStateMessage state =
            new MIMISKStateMessage();

        state.subsystem = MIMISKSubsystem.Bridge;
        state.moduleName = "MIMISKCommandSafetyGate";
        state.mode = "CommandRejected";
        state.health = MIMISKHealth.Warning;
        state.ready = false;
        state.active = true;
        state.fault = false;
        state.eventText =
            command.verb +
            " rejected: " +
            reason;

        bus.PublishState(state);
    }

    private bool HasMeasuredDroneSurfaceContact()
    {
        if (droneObject == null)
        {
            return false;
        }

        Component[] components =
            droneObject.GetComponentsInChildren<Component>(true);

        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];

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

            if (TryReadBool(c, "isInWater", out b) && b) return true;
            if (TryReadBool(c, "isTouchingWater", out b) && b) return true;
            if (TryReadBool(c, "waterContact", out b) && b) return true;
            if (TryReadBool(c, "contact", out b) && b) return true;
            if (TryReadBool(c, "hasContact", out b) && b) return true;

            int n;

            if (TryReadInt(c, "activePointCount", out n) && n > 0) return true;
            if (TryReadInt(c, "activePoints", out n) && n > 0) return true;
            if (TryReadInt(c, "buoyancyActivePoints", out n) && n > 0) return true;
            if (TryReadInt(c, "waterContactCount", out n) && n > 0) return true;
        }

        return false;
    }

    private bool TryReadBool(Component component, string memberName, out bool value)
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

    private bool TryReadInt(Component component, string memberName, out int value)
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
}
