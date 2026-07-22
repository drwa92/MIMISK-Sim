using System;
using MIMISK.Common;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKTetherAgent : MonoBehaviour, IMIMISKAgent
{
    [Header("Agent")]
    public string agentName = "Tether";
    public bool agentEnabled = true;
    public bool autoFindOnAwake = true;

    [Header("References")]
    public MIMISKUnifiedTetherManager unifiedTether;
    public MIMISKDroneCoreTetherManager tetherManager;
    public MIMISKTetherSmartWinchController smartWinch;
    public MIMISKUnifiedTetherResearchLogger researchLogger;
    public MIMISKSingleYellowTetherVisualAuthority visualAuthority;

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
        get { return MIMISKAgentKind.Tether; }
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
        if (unifiedTether == null)
        {
            unifiedTether =
                GetComponent<MIMISKUnifiedTetherManager>();
        }

        if (tetherManager == null)
        {
            tetherManager =
                GetComponent<MIMISKDroneCoreTetherManager>();
        }

        if (smartWinch == null)
        {
            smartWinch =
                GetComponent<MIMISKTetherSmartWinchController>();
        }

        if (researchLogger == null)
        {
            researchLogger =
                GetComponent<MIMISKUnifiedTetherResearchLogger>();
        }

        if (visualAuthority == null)
        {
            visualAuthority =
                GetComponent<MIMISKSingleYellowTetherVisualAuthority>();
        }
    }

    public MIMISKState GetState()
    {
        AutoFindReferences();

        MIMISKState s =
            new MIMISKState();

        s.agentName = agentName;
        s.agentKind = MIMISKAgentKind.Tether;
        s.available =
            agentEnabled &&
            unifiedTether != null &&
            tetherManager != null;

        s.active =
            unifiedTether != null &&
            unifiedTether.tetherState != MIMISKUnifiedTetherManager.TetherMissionState.Disabled &&
            unifiedTether.tetherState != MIMISKUnifiedTetherManager.TetherMissionState.Fault;

        s.fault =
            unifiedTether != null &&
            unifiedTether.tetherState == MIMISKUnifiedTetherManager.TetherMissionState.Fault;

        s.recoveryReady =
            unifiedTether != null &&
            unifiedTether.tetherState == MIMISKUnifiedTetherManager.TetherMissionState.RecoveredAttached;

        s.mode =
            MapMode();

        s.positionWorld =
            tetherManager != null
                ? tetherManager.tetherEndWorld
                : transform.position;

        s.depthM =
            unifiedTether != null && unifiedTether.miniRovRoot != null
                ? Mathf.Max(0.0f, unifiedTether.waterSurfaceY - unifiedTether.miniRovRoot.position.y)
                : float.NaN;

        s.speedMS =
            tetherManager != null
                ? Mathf.Abs(tetherManager.winchCommandRateMS)
                : 0.0f;

        s.yawDeg = 0.0f;

        s.missionState =
            unifiedTether != null
                ? unifiedTether.tetherState.ToString()
                : "missing";

        s.controllerMode =
            tetherManager != null
                ? tetherManager.tetherState.ToString()
                : "missing";

        s.selectedPathType =
            "tether";

        s.selectedYawPolicy =
            "none";

        s.distanceToTargetM =
            tetherManager != null
                ? tetherManager.straightDistanceM
                : float.NaN;

        s.distanceToHomeM =
            unifiedTether != null
                ? unifiedTether.distanceToDeploymentHomeM
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

        if (unifiedTether == null)
        {
            reason = "missing_unified_tether_manager";
            return false;
        }

        if (RequiresPlayMode(command.verb) && !Application.isPlaying)
        {
            reason = "requires_play_mode";
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

                case MIMISKAgentCommandVerb.DeployRov:
                    unifiedTether.StartDeployment();
                    lastResult = "deploy_rov_requested";
                    break;

                case MIMISKAgentCommandVerb.ActivateRovControl:
                    unifiedTether.ActivateRovControl();
                    lastResult = "activate_rov_control_requested";
                    break;

                case MIMISKAgentCommandVerb.RecoverRov:
                    unifiedTether.RequestRecovery();
                    lastResult = "recover_rov_requested";
                    break;

                case MIMISKAgentCommandVerb.HoldWinch:
                    unifiedTether.HoldWinch();
                    lastResult = "hold_winch_requested";
                    break;

                case MIMISKAgentCommandVerb.AttachRovToCableEnd:
                    unifiedTether.AttachRovToCableEnd();
                    lastResult = "attach_rov_to_cable_end_requested";
                    break;

                case MIMISKAgentCommandVerb.ResetTetherFault:
                    unifiedTether.ResetFault();
                    lastResult = "reset_tether_fault_requested";
                    break;

                case MIMISKAgentCommandVerb.StartSmartTms:
                    if (smartWinch != null)
                    {
                        smartWinch.EnableSmartTms();
                        lastResult = "smart_tms_enabled";
                    }
                    else
                    {
                        lastResult = "smart_tms_missing";
                    }
                    break;

                case MIMISKAgentCommandVerb.StopSmartTms:
                    if (smartWinch != null)
                    {
                        smartWinch.DisableSmartTms();
                        lastResult = "smart_tms_disabled";
                    }
                    else
                    {
                        lastResult = "smart_tms_missing";
                    }
                    break;

                default:
                    reason = "unsupported_tether_command_" + command.verb;
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

    private MIMISKAgentMode MapMode()
    {
        if (unifiedTether == null)
        {
            return MIMISKAgentMode.Unknown;
        }

        switch (unifiedTether.tetherState)
        {
            case MIMISKUnifiedTetherManager.TetherMissionState.WaitingForDroneSurfaceReady:
                return MIMISKAgentMode.TetherWaiting;

            case MIMISKUnifiedTetherManager.TetherMissionState.ReadyForDeploy:
            case MIMISKUnifiedTetherManager.TetherMissionState.CableAttachedIdle:
                return MIMISKAgentMode.TetherReadyForDeploy;

            case MIMISKUnifiedTetherManager.TetherMissionState.Deploying:
                return MIMISKAgentMode.TetherDeploying;

            case MIMISKUnifiedTetherManager.TetherMissionState.DynamicStabilizing:
                return MIMISKAgentMode.TetherDynamicStabilizing;

            case MIMISKUnifiedTetherManager.TetherMissionState.ReadyForRovControl:
                return MIMISKAgentMode.TetherRovControlReady;

            case MIMISKUnifiedTetherManager.TetherMissionState.RovControlActive:
                return MIMISKAgentMode.TetherRovControlActive;

            case MIMISKUnifiedTetherManager.TetherMissionState.WaitingForRovRecoveryReady:
            case MIMISKUnifiedTetherManager.TetherMissionState.Recovering:
                return MIMISKAgentMode.TetherRecovering;

            case MIMISKUnifiedTetherManager.TetherMissionState.RecoveredAttached:
                return MIMISKAgentMode.TetherRecoveredAttached;

            case MIMISKUnifiedTetherManager.TetherMissionState.Fault:
                return MIMISKAgentMode.Fault;

            default:
                return MIMISKAgentMode.Unknown;
        }
    }

    private bool RequiresPlayMode(MIMISKAgentCommandVerb verb)
    {
        return
            verb != MIMISKAgentCommandVerb.None &&
            verb != MIMISKAgentCommandVerb.AutoFindReferences;
    }

    private string BuildLastEventString()
    {
        string unified =
            unifiedTether != null
                ? unifiedTether.lastEvent
                : "unified_missing";

        string tether =
            tetherManager != null
                ? tetherManager.lastEvent
                : "tether_missing";

        string smart =
            smartWinch != null
                ? smartWinch.lastEvent
                : "smart_missing";

        return
            "unified=" + unified +
            "; tether=" + tether +
            "; smart=" + smart +
            "; agent=" + lastResult;
    }

    private void EmitEvent(string eventType, string message)
    {
        MIMISKEvent e =
            new MIMISKEvent
            {
                timeUtc = DateTime.UtcNow.ToString("o"),
                agentName = agentName,
                agentKind = MIMISKAgentKind.Tether,
                eventType = eventType,
                message = message,
                state = GetState()
            };

        if (OnEvent != null)
        {
            OnEvent.Invoke(e);
        }
    }

    [ContextMenu("Agent / Deploy ROV")]
    public void AgentDeployRov()
    {
        Execute(new MIMISKCommand
        {
            verb = MIMISKAgentCommandVerb.DeployRov,
            source = "inspector"
        });
    }

    [ContextMenu("Agent / Activate ROV Control")]
    public void AgentActivateRovControl()
    {
        Execute(new MIMISKCommand
        {
            verb = MIMISKAgentCommandVerb.ActivateRovControl,
            source = "inspector"
        });
    }

    [ContextMenu("Agent / Recover ROV")]
    public void AgentRecoverRov()
    {
        Execute(new MIMISKCommand
        {
            verb = MIMISKAgentCommandVerb.RecoverRov,
            source = "inspector"
        });
    }

    [ContextMenu("Agent / Hold Winch")]
    public void AgentHoldWinch()
    {
        Execute(new MIMISKCommand
        {
            verb = MIMISKAgentCommandVerb.HoldWinch,
            source = "inspector"
        });
    }

    [ContextMenu("Agent / Attach ROV To Cable End")]
    public void AgentAttachRovToCableEnd()
    {
        Execute(new MIMISKCommand
        {
            verb = MIMISKAgentCommandVerb.AttachRovToCableEnd,
            source = "inspector"
        });
    }

    [ContextMenu("Agent / Enable Smart TMS")]
    public void AgentEnableSmartTms()
    {
        Execute(new MIMISKCommand
        {
            verb = MIMISKAgentCommandVerb.StartSmartTms,
            source = "inspector"
        });
    }

    [ContextMenu("Agent / Disable Smart TMS")]
    public void AgentDisableSmartTms()
    {
        Execute(new MIMISKCommand
        {
            verb = MIMISKAgentCommandVerb.StopSmartTms,
            source = "inspector"
        });
    }
}
