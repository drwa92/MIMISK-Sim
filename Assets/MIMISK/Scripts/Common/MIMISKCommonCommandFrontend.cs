using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MIMISKCommonCommandFrontend : MonoBehaviour
{
    [Header("References")]
    public MIMISKCommonBus bus;

    [Header("Command Frontend")]
    public bool frontendEnabled = true;

    [Tooltip("Use 1/2/3/4/5/F8 for Phase 2A so we do not conflict with the working P/U/I/R keys.")]
    public bool usePhase2TestKeys = true;

    [Header("Phase 2A Test Keys")]
    public Key startMissionKey = Key.Digit1;
    public Key deployTetherKey = Key.Digit2;
    public Key enableMiniRovControlKey = Key.Digit3;
    public Key recoverMiniRovKey = Key.Digit4;
    public Key holdTetherKey = Key.Digit5;
    public Key resetMissionKey = Key.F8;

    [Header("Runtime")]
    public int commandsSent;
    public string lastCommand = "none";

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Update()
    {
        if (!frontendEnabled || !usePhase2TestKeys)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[startMissionKey].wasPressedThisFrame)
        {
            SendStartMission();
        }

        if (Keyboard.current[deployTetherKey].wasPressedThisFrame)
        {
            SendDeployTether();
        }

        if (Keyboard.current[enableMiniRovControlKey].wasPressedThisFrame)
        {
            SendEnableMiniROVControl();
        }

        if (Keyboard.current[recoverMiniRovKey].wasPressedThisFrame)
        {
            SendRecoverMiniROV();
        }

        if (Keyboard.current[holdTetherKey].wasPressedThisFrame)
        {
            SendHoldTether();
        }

        if (Keyboard.current[resetMissionKey].wasPressedThisFrame)
        {
            SendResetMission();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (bus == null)
        {
            bus = MIMISKCommonBus.GetOrCreate();
        }
    }

    [ContextMenu("Send Start Mission")]
    public void SendStartMission()
    {
        Send(
            MIMISKSubsystem.Mission,
            MIMISKCommandVerb.StartMission,
            "phase2a_start_mission"
        );
    }

    [ContextMenu("Send Deploy Tether")]
    public void SendDeployTether()
    {
        Send(
            MIMISKSubsystem.Mission,
            MIMISKCommandVerb.DeployTether,
            "phase2a_deploy_tether"
        );
    }

    [ContextMenu("Send Enable MiniROV Control")]
    public void SendEnableMiniROVControl()
    {
        Send(
            MIMISKSubsystem.Mission,
            MIMISKCommandVerb.EnableMiniROVControl,
            "phase2a_enable_minirov_control"
        );
    }

    [ContextMenu("Send Recover MiniROV")]
    public void SendRecoverMiniROV()
    {
        Send(
            MIMISKSubsystem.Mission,
            MIMISKCommandVerb.RecoverMiniROV,
            "phase2a_recover_minirov"
        );
    }

    [ContextMenu("Send Hold Tether")]
    public void SendHoldTether()
    {
        Send(
            MIMISKSubsystem.Mission,
            MIMISKCommandVerb.HoldTether,
            "phase2a_hold_tether"
        );
    }

    [ContextMenu("Send Reset Mission")]
    public void SendResetMission()
    {
        Send(
            MIMISKSubsystem.Mission,
            MIMISKCommandVerb.ResetMission,
            "phase2a_reset_mission"
        );
    }

    private void Send(
        MIMISKSubsystem target,
        MIMISKCommandVerb verb,
        string text)
    {
        AutoFindReferences();

        if (bus == null)
        {
            Debug.LogWarning("[MIMISK Common Command Frontend] Missing bus.");
            return;
        }

        MIMISKCommandMessage command =
            new MIMISKCommandMessage();

        command.source = "MIMISKCommonCommandFrontend";
        command.target = target;
        command.verb = verb;
        command.text = text;

        bus.SendCommand(command);

        commandsSent++;
        lastCommand = target + " / " + verb + " / " + text;
    }
}
