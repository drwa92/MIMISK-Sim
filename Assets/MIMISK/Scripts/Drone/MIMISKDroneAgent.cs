using System;
using MIMISK.Common;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKDroneAgent : MonoBehaviour, IMIMISKAgent
{
    [Header("Agent")]
    public string agentName = "Drone";
    public bool agentEnabled = true;
    public bool autoFindOnAwake = true;

    [Header("Tether / MiniROV Safety Interlock")]
    public bool blockDroneMotionWhenMiniRovDeployed = true;
    public MIMISKMiniROVRealisticDeploymentManager miniRovDeployment;
    public MIMISKUnifiedTetherManager unifiedTetherManager;
    public MIMISKDroneTetherHandoffMission tetherHandoff;

    [Tooltip("If ON, Agent state reports ReadyForTetherDeployment whenever the drone is SurfaceStable/SurfaceHold.")]
    public bool surfaceStableImpliesReadyForTether = true;

    [Header("Validated Drone Stack References")]
    public MIMISKDroneCoreMissionManager missionManager;
    public MIMISKDroneCoreFlightModeManager flightManager;
    public MIMISKDroneCoreRotorController core;
    public MIMISKDroneCoreTrajectoryPlanner trajectoryPlanner;
    public Rigidbody rb;

    [Header("Runtime")]
    public string lastCommand = "none";
    public string lastResult = "idle";
    public string lastRejectReason = "";

    public string AgentName
    {
        get { return agentName; }
    }

    public MIMISKAgentKind AgentKind
    {
        get { return MIMISKAgentKind.Drone; }
    }

    public event Action<MIMISKEvent> OnEvent;

    private void Awake()
    {
        if (autoFindOnAwake)
        {
            AutoFindReferences();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (missionManager == null)
        {
            missionManager =
                GetComponent<MIMISKDroneCoreMissionManager>();
        }

        if (flightManager == null)
        {
            flightManager =
                GetComponent<MIMISKDroneCoreFlightModeManager>();
        }

        if (core == null)
        {
            core =
                GetComponent<MIMISKDroneCoreRotorController>();
        }

        if (trajectoryPlanner == null)
        {
            trajectoryPlanner =
                GetComponent<MIMISKDroneCoreTrajectoryPlanner>();
        }

        if (rb == null)
        {
            rb =
                GetComponent<Rigidbody>();
        }

        if (miniRovDeployment == null)
        {
            miniRovDeployment =
                GetComponent<MIMISKMiniROVRealisticDeploymentManager>();

        if (unifiedTetherManager == null)
        {
            unifiedTetherManager =
                GetComponent<MIMISKUnifiedTetherManager>();
        }
        }

        if (tetherHandoff == null)
        {
            tetherHandoff =
                GetComponent<MIMISKDroneTetherHandoffMission>();
        }
    }

    public MIMISKState GetState()
    {
        AutoFindReferences();

        MIMISKState s =
            new MIMISKState();

        s.agentName = agentName;
        s.agentKind = MIMISKAgentKind.Drone;

        s.available =
            agentEnabled &&
            missionManager != null &&
            flightManager != null &&
            core != null;

        s.positionWorld =
            core != null
                ? core.estimatedPositionWorld
                : (rb != null ? rb.position : transform.position);

        s.depthM =
            float.NaN;

        s.yawDeg =
            core != null
                ? core.estimatedYawDeg
                : transform.eulerAngles.y;

        s.speedMS =
            core != null
                ? core.estimatedVelocityWorld.magnitude
                : (rb != null ? rb.linearVelocity.magnitude : 0.0f);

        s.active =
            IsActiveDroneMission();

        s.recoveryReady =
            IsReadyForTetherDeployment();

        s.fault =
            missionManager != null &&
            (missionManager.missionState ==
                MIMISKDroneCoreMissionManager.MissionState.Failsafe ||
             missionManager.missionState ==
                MIMISKDroneCoreMissionManager.MissionState.Aborted);

        s.mode =
            MapMode();

        s.missionState =
            missionManager != null
                ? missionManager.missionState.ToString()
                : "missing";

        s.controllerMode =
            core != null
                ? core.controlMode.ToString()
                : "missing";

        s.selectedPathType =
            trajectoryPlanner != null
                ? trajectoryPlanner.trajectoryType.ToString()
                : "missing";

        s.selectedYawPolicy =
            trajectoryPlanner != null && trajectoryPlanner.yawAlongPath
                ? "YawAlongPath"
                : "FixedMissionYaw";

        s.distanceToHomeM =
            float.NaN;

        s.distanceToTargetM =
            core != null
                ? core.trackingErrorM
                : float.NaN;

        s.lastEvent =
            BuildLastEventString();

        return s;
    }

    public bool CanAccept(MIMISKCommand command, out string reason)
    {
        AutoFindReferences();

        reason = "ok";

        if (!agentEnabled)
        {
            reason = "agent_disabled";
            return false;
        }

        if (command == null)
        {
            reason = "null_command";
            return false;
        }

        if (missionManager == null &&
            RequiresMissionManager(command.verb))
        {
            reason = "missing_mission_manager";
            return false;
        }

        if (flightManager == null &&
            RequiresFlightManager(command.verb))
        {
            reason = "missing_flight_manager";
            return false;
        }

        if (core == null &&
            RequiresCore(command.verb))
        {
            reason = "missing_core_controller";
            return false;
        }

        if (!Application.isPlaying &&
            RequiresPlayMode(command.verb))
        {
            reason = "requires_play_mode";
            return false;
        }

        if (IsStartMissionCommand(command.verb) &&
            missionManager != null &&
            missionManager.missionActive)
        {
            reason = "drone_mission_already_active";
            return false;
        }

        if (blockDroneMotionWhenMiniRovDeployed &&
            IsDroneMotionCommand(command.verb) &&
            IsMiniRovDeployedOrRecovering())
        {
            reason = "MiniROV/tether is deployed or recovering. Recover and attach MiniROV before drone motion.";
            return false;
        }

        return true;
    }

    public MIMISKCommandResult Execute(MIMISKCommand command)
    {
        AutoFindReferences();

        string reason;

        if (!CanAccept(command, out reason))
        {
            lastRejectReason = reason;
            EmitEvent("command_rejected", reason);
            return MIMISKCommandResult.Rejected(reason, GetState());
        }

        try
        {
            lastCommand =
                command.verb.ToString();

            switch (command.verb)
            {
                case MIMISKAgentCommandVerb.AutoFindReferences:
                    AutoFindReferences();
                    lastResult = "auto_find_completed";
                    break;

                case MIMISKAgentCommandVerb.StartSelectedMission:
                case MIMISKAgentCommandVerb.StartDroneMission:
                    missionManager.StartMission();
                    lastResult = "drone_mission_started";
                    break;

                case MIMISKAgentCommandVerb.AbortDroneMission:
                    missionManager.AbortMission();
                    lastResult = "drone_mission_aborted";
                    break;

                case MIMISKAgentCommandVerb.EnterTakeoffIdle:
                    flightManager.EnterTakeoffIdle();
                    lastResult = "takeoff_idle_requested";
                    break;

                case MIMISKAgentCommandVerb.Takeoff:
                    flightManager.StartTakeoff();
                    lastResult = "takeoff_requested";
                    break;

                case MIMISKAgentCommandVerb.Hold:
                    flightManager.CapturePositionHold();
                    lastResult = "position_hold_requested";
                    break;

                case MIMISKAgentCommandVerb.StartPath:
                    ExecuteStartPath(command);
                    break;

                case MIMISKAgentCommandVerb.ManualMode:
                    flightManager.EnterGamepadMode();
                    lastResult = "manual_gamepad_requested";
                    break;

                case MIMISKAgentCommandVerb.LandOnSurface:
                    flightManager.StartLandingOnSurface();
                    lastResult = "landing_on_surface_requested";
                    break;

                case MIMISKAgentCommandVerb.Failsafe:
                    flightManager.EnterFailsafe();
                    lastResult = "failsafe_requested";
                    break;

                case MIMISKAgentCommandVerb.AbortToHold:
                    if (missionManager != null)
                    {
                        missionManager.AbortMission();
                    }

                    if (flightManager != null)
                    {
                        flightManager.CapturePositionHold();
                    }

                    lastResult = "abort_to_position_hold_requested";
                    break;

                case MIMISKAgentCommandVerb.Stop:
                case MIMISKAgentCommandVerb.Disarm:
                    if (missionManager != null && missionManager.missionActive)
                    {
                        missionManager.AbortMission();
                    }

                    flightManager.EnterDisarmed();
                    lastResult = "disarm_requested";
                    break;

                case MIMISKAgentCommandVerb.CutMotors:
                    if (core != null)
                    {
                        core.CutMotors();
                    }

                    lastResult = "cut_motors_requested";
                    break;

                default:
                    reason = "unsupported_drone_command_" + command.verb;
                    lastRejectReason = reason;
                    EmitEvent("command_rejected", reason);
                    return MIMISKCommandResult.Rejected(reason, GetState());
            }

            EmitEvent("command_accepted", lastResult);
            return MIMISKCommandResult.Accepted(lastResult, GetState());
        }
        catch (Exception ex)
        {
            reason =
                "exception_" +
                ex.GetType().Name +
                "_" +
                ex.Message;

            lastResult =
                reason;

            EmitEvent("command_failed", reason);

            return MIMISKCommandResult.Failed(reason, GetState());
        }
    }

    private void ExecuteStartPath(MIMISKCommand command)
    {
        if (flightManager == null)
        {
            throw new InvalidOperationException("missing_flight_manager");
        }

        if (trajectoryPlanner != null &&
            !string.IsNullOrEmpty(command.pathTypeName))
        {
            MIMISKDroneCoreTrajectoryPlanner.TrajectoryType trajectoryType;

            if (Enum.TryParse(
                    command.pathTypeName,
                    true,
                    out trajectoryType))
            {
                trajectoryPlanner.trajectoryType =
                    trajectoryType;

                flightManager.useTrajectoryPlanner =
                    true;

                flightManager.trajectoryPlanner =
                    trajectoryPlanner;

                flightManager.StartPathTracking(
                    MIMISKDroneCoreFlightModeManager.PathKind.Circle
                );

                lastResult =
                    "trajectory_path_requested_" +
                    trajectoryType.ToString();

                return;
            }
        }

        MIMISKDroneCoreFlightModeManager.PathKind pathKind =
            flightManager.pathKind;

        if (!string.IsNullOrEmpty(command.pathTypeName))
        {
            MIMISKDroneCoreFlightModeManager.PathKind parsedPathKind;

            if (Enum.TryParse(
                    command.pathTypeName,
                    true,
                    out parsedPathKind))
            {
                pathKind =
                    parsedPathKind;
            }
        }

        flightManager.StartPathTracking(pathKind);

        lastResult =
            "flight_mode_path_requested_" +
            pathKind.ToString();
    }

    private bool IsSurfaceStableForTether()
    {
        return
            flightManager != null &&
            (flightManager.flightMode ==
                MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceStable ||
             flightManager.flightMode ==
                MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceHold);
    }

    private bool IsReadyForTetherDeployment()
    {
        if (missionManager != null &&
            missionManager.IsReadyForTetherDeployment())
        {
            return true;
        }

        return
            surfaceStableImpliesReadyForTether &&
            IsSurfaceStableForTether();
    }

    private MIMISKAgentMode MapMode()
    {
        if (IsReadyForTetherDeployment())
        {
            return MIMISKAgentMode.ReadyForTetherDeployment;
        }

        if (missionManager != null &&
            missionManager.missionState ==
                MIMISKDroneCoreMissionManager.MissionState.Failsafe)
        {
            return MIMISKAgentMode.Fault;
        }

        if (missionManager != null &&
            missionManager.missionState ==
                MIMISKDroneCoreMissionManager.MissionState.Aborted)
        {
            return MIMISKAgentMode.Fault;
        }

        if (flightManager == null)
        {
            return MIMISKAgentMode.Unknown;
        }

        switch (flightManager.flightMode)
        {
            case MIMISKDroneCoreFlightModeManager.FlightMode.Disabled:
                return MIMISKAgentMode.Idle;

            case MIMISKDroneCoreFlightModeManager.FlightMode.Disarmed:
                return MIMISKAgentMode.Disarmed;

            case MIMISKDroneCoreFlightModeManager.FlightMode.TakeoffIdle:
                return MIMISKAgentMode.TakeoffIdle;

            case MIMISKDroneCoreFlightModeManager.FlightMode.Takeoff:
                return MIMISKAgentMode.Takeoff;

            case MIMISKDroneCoreFlightModeManager.FlightMode.Gamepad:
                return MIMISKAgentMode.Manual;

            case MIMISKDroneCoreFlightModeManager.FlightMode.PositionHold:
                return MIMISKAgentMode.PositionHold;

            case MIMISKDroneCoreFlightModeManager.FlightMode.PathTracking:
            case MIMISKDroneCoreFlightModeManager.FlightMode.PathBraking:
                return MIMISKAgentMode.PathTracking;

            case MIMISKDroneCoreFlightModeManager.FlightMode.LandingOnSurface:
                return MIMISKAgentMode.LandingOnSurface;

            case MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceStable:
                return MIMISKAgentMode.SurfaceStable;

            case MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceHold:
                return MIMISKAgentMode.SurfaceHold;

            case MIMISKDroneCoreFlightModeManager.FlightMode.Failsafe:
                return MIMISKAgentMode.Fault;

            default:
                return MIMISKAgentMode.Unknown;
        }
    }

    private bool IsActiveDroneMission()
    {
        if (missionManager != null &&
            missionManager.missionActive)
        {
            return true;
        }

        if (flightManager == null)
        {
            return false;
        }

        return
            flightManager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.Takeoff ||
            flightManager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.PathTracking ||
            flightManager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.PathBraking ||
            flightManager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.LandingOnSurface;
    }

    private bool IsDroneMotionCommand(MIMISKAgentCommandVerb verb)
    {
        return
            verb == MIMISKAgentCommandVerb.StartSelectedMission ||
            verb == MIMISKAgentCommandVerb.StartDroneMission ||
            verb == MIMISKAgentCommandVerb.StartPath ||
            verb == MIMISKAgentCommandVerb.Takeoff ||
            verb == MIMISKAgentCommandVerb.EnterTakeoffIdle ||
            verb == MIMISKAgentCommandVerb.ManualMode;
    }

    private bool IsMiniRovDeployedOrRecovering()
    {
        if (unifiedTetherManager != null &&
            !unifiedTetherManager.droneMotionAllowed)
        {
            return true;
        }

        if (miniRovDeployment == null)
        {
            return false;
        }

        switch (miniRovDeployment.deploymentState)
        {
            case MIMISKMiniROVRealisticDeploymentManager.DeploymentState.NotConfigured:
            case MIMISKMiniROVRealisticDeploymentManager.DeploymentState.CableAttachedIdle:
            case MIMISKMiniROVRealisticDeploymentManager.DeploymentState.ReadyToDeploy:
            case MIMISKMiniROVRealisticDeploymentManager.DeploymentState.RecoveredAttached:
                return false;

            default:
                return true;
        }
    }

    private bool RequiresMissionManager(MIMISKAgentCommandVerb verb)
    {
        return
            verb == MIMISKAgentCommandVerb.StartSelectedMission ||
            verb == MIMISKAgentCommandVerb.StartDroneMission ||
            verb == MIMISKAgentCommandVerb.AbortDroneMission;
    }

    private bool RequiresFlightManager(MIMISKAgentCommandVerb verb)
    {
        return
            verb == MIMISKAgentCommandVerb.EnterTakeoffIdle ||
            verb == MIMISKAgentCommandVerb.Takeoff ||
            verb == MIMISKAgentCommandVerb.Hold ||
            verb == MIMISKAgentCommandVerb.StartPath ||
            verb == MIMISKAgentCommandVerb.ManualMode ||
            verb == MIMISKAgentCommandVerb.LandOnSurface ||
            verb == MIMISKAgentCommandVerb.Failsafe ||
            verb == MIMISKAgentCommandVerb.AbortToHold ||
            verb == MIMISKAgentCommandVerb.Stop ||
            verb == MIMISKAgentCommandVerb.Disarm;
    }

    private bool RequiresCore(MIMISKAgentCommandVerb verb)
    {
        return
            verb == MIMISKAgentCommandVerb.CutMotors;
    }

    private bool RequiresPlayMode(MIMISKAgentCommandVerb verb)
    {
        return
            verb != MIMISKAgentCommandVerb.None &&
            verb != MIMISKAgentCommandVerb.AutoFindReferences;
    }

    private bool IsStartMissionCommand(MIMISKAgentCommandVerb verb)
    {
        return
            verb == MIMISKAgentCommandVerb.StartSelectedMission ||
            verb == MIMISKAgentCommandVerb.StartDroneMission;
    }

    private string BuildLastEventString()
    {
        string mission =
            missionManager != null
                ? missionManager.lastMissionEvent
                : "mission_missing";

        string flight =
            flightManager != null
                ? flightManager.lastModeEvent
                : "flight_missing";

        string c =
            core != null
                ? core.lastEvent
                : "core_missing";

        return
            "mission=" + mission +
            "; flight=" + flight +
            "; core=" + c +
            "; agent=" + lastResult;
    }

    private void EmitEvent(string eventType, string message)
    {
        MIMISKEvent e =
            new MIMISKEvent
            {
                timeUtc = DateTime.UtcNow.ToString("o"),
                agentName = agentName,
                agentKind = MIMISKAgentKind.Drone,
                eventType = eventType,
                message = message,
                state = GetState()
            };

        if (OnEvent != null)
        {
            OnEvent.Invoke(e);
        }
    }

    [ContextMenu("Agent / Enter Takeoff Idle")]
    public void AgentEnterTakeoffIdle()
    {
        Execute(new MIMISKCommand
        {
            verb = MIMISKAgentCommandVerb.EnterTakeoffIdle,
            source = "inspector"
        });
    }

    [ContextMenu("Agent / Takeoff")]
    public void AgentTakeoff()
    {
        Execute(new MIMISKCommand
        {
            verb = MIMISKAgentCommandVerb.Takeoff,
            source = "inspector"
        });
    }

    [ContextMenu("Agent / Hold")]
    public void AgentHold()
    {
        Execute(new MIMISKCommand
        {
            verb = MIMISKAgentCommandVerb.Hold,
            source = "inspector"
        });
    }

    [ContextMenu("Agent / Start Selected Drone Mission")]
    public void AgentStartDroneMission()
    {
        Execute(new MIMISKCommand
        {
            verb = MIMISKAgentCommandVerb.StartDroneMission,
            source = "inspector"
        });
    }

    [ContextMenu("Agent / Start Current Path")]
    public void AgentStartCurrentPath()
    {
        Execute(new MIMISKCommand
        {
            verb = MIMISKAgentCommandVerb.StartPath,
            pathTypeName =
                trajectoryPlanner != null
                    ? trajectoryPlanner.trajectoryType.ToString()
                    : "",
            source = "inspector"
        });
    }

    [ContextMenu("Agent / Land On Surface")]
    public void AgentLandOnSurface()
    {
        Execute(new MIMISKCommand
        {
            verb = MIMISKAgentCommandVerb.LandOnSurface,
            source = "inspector"
        });
    }

    [ContextMenu("Agent / Manual Mode")]
    public void AgentManualMode()
    {
        Execute(new MIMISKCommand
        {
            verb = MIMISKAgentCommandVerb.ManualMode,
            source = "inspector"
        });
    }

    [ContextMenu("Agent / Failsafe")]
    public void AgentFailsafe()
    {
        Execute(new MIMISKCommand
        {
            verb = MIMISKAgentCommandVerb.Failsafe,
            source = "inspector"
        });
    }

    [ContextMenu("Agent / Disarm")]
    public void AgentDisarm()
    {
        Execute(new MIMISKCommand
        {
            verb = MIMISKAgentCommandVerb.Disarm,
            source = "inspector"
        });
    }
}
