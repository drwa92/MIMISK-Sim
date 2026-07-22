using UnityEngine;

/// <summary>
/// Safety monitor for the physical tether model.
///
/// Default action mode is MonitorOnly, so adding this component cannot disturb
/// the existing mission stack. Switch to ActiveWinchProtection after the
/// physical tether has been tuned in the scene.
/// </summary>
[DefaultExecutionOrder(1700)]
[DisallowMultipleComponent]
public class MIMISKPhysicalTetherSafetyGuard : MonoBehaviour
{
    public enum GuardActionMode
    {
        MonitorOnly,
        WinchHoldOnly,
        ActiveWinchProtection
    }

    [Header("References")]
    public bool autoFindReferences = true;
    public MIMISKPhysicalTetherModel physicalTether;
    public MIMISKUnifiedTetherManager unifiedTether;
    public MIMISKDroneCoreTetherManager tetherManager;

    [Header("Guard")]
    public bool guardEnabled = true;
    public GuardActionMode actionMode = GuardActionMode.MonitorOnly;

    [Tooltip("Warn above this tension, but do not necessarily command the winch.")]
    public float warningTensionN = 20.0f;

    [Tooltip("Critical tension threshold. Active modes hold or pay out the winch.")]
    public float criticalTensionN = 28.0f;

    [Tooltip("Minimum slack target in normal operations.")]
    public float minimumSlackM = 0.05f;

    [Tooltip("Maximum allowed slack before recovery/hold warning.")]
    public float maximumSlackM = 2.0f;

    [Tooltip("If length is shorter than straight distance plus this margin, the tether is too short.")]
    public float minimumLengthMarginM = 0.04f;

    [Tooltip("When active protection sees a too-short tether, it commands this added slack.")]
    public float emergencyPayoutSlackM = 0.25f;

    [Tooltip("Do not command payout above this speed even during protection.")]
    public float emergencyPayoutSpeedMS = 0.28f;

    [Tooltip("Stop recovery if recovery is pulling too hard.")]
    public bool stopRecoveryOnHighTension = true;

    [Tooltip("Command payout when the cable is geometrically too short.")]
    public bool payoutWhenTooShort = true;

    [Header("Runtime Flags")]
    public bool warningTension;
    public bool criticalTension;
    public bool tooShort;
    public bool tooSlack;
    public bool solverFault;
    public bool recoveryUnsafe;
    public bool deploymentUnsafe;
    public bool rovControlUnsafe;
    public bool winchCommandModified;
    public string lastAction = "idle";

    private void Awake()
    {
        if (autoFindReferences)
        {
            AutoFindReferences();
        }
    }

    private void FixedUpdate()
    {
        if (!guardEnabled)
        {
            return;
        }

        if (autoFindReferences)
        {
            AutoFindReferencesIfMissing();
        }

        if (physicalTether == null)
        {
            lastAction = "missing_physical_tether";
            return;
        }

        EvaluateSafety();
        ApplyGuardActionIfEnabled();
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (physicalTether == null)
        {
            physicalTether = GetComponent<MIMISKPhysicalTetherModel>();
        }

        if (unifiedTether == null)
        {
            unifiedTether = GetComponent<MIMISKUnifiedTetherManager>();
        }

        if (tetherManager == null)
        {
            tetherManager = GetComponent<MIMISKDroneCoreTetherManager>();
        }
    }

    private void AutoFindReferencesIfMissing()
    {
        if (physicalTether == null || unifiedTether == null || tetherManager == null)
        {
            AutoFindReferences();
        }
    }

    [ContextMenu("Set Monitor Only")]
    public void SetMonitorOnly()
    {
        actionMode = GuardActionMode.MonitorOnly;
        lastAction = "monitor_only";
    }

    [ContextMenu("Set Active Winch Protection")]
    public void SetActiveWinchProtection()
    {
        actionMode = GuardActionMode.ActiveWinchProtection;
        lastAction = "active_winch_protection";
    }

    private void EvaluateSafety()
    {
        warningTension = physicalTether.maxTensionN >= warningTensionN;
        criticalTension = physicalTether.maxTensionN >= criticalTensionN;
        solverFault = physicalTether.physicalState == MIMISKPhysicalTetherModel.PhysicalTetherState.SolverFault;

        tooShort = physicalTether.deployedLengthM <
            physicalTether.straightDistanceM + minimumLengthMarginM;

        tooSlack = physicalTether.slackM > maximumSlackM;

        bool recovering = tetherManager != null &&
            tetherManager.tetherState == MIMISKDroneCoreTetherManager.TetherState.Recovering;

        bool deploying = tetherManager != null &&
            tetherManager.tetherState == MIMISKDroneCoreTetherManager.TetherState.Deploying;

        recoveryUnsafe = solverFault || criticalTension || (recovering && warningTension);
        deploymentUnsafe = solverFault || tooSlack;
        rovControlUnsafe = solverFault || criticalTension || tooShort;

        if (solverFault)
        {
            lastAction = "solver_fault_monitor";
        }
        else if (criticalTension)
        {
            lastAction = "critical_tension_monitor";
        }
        else if (warningTension)
        {
            lastAction = "warning_tension_monitor";
        }
        else if (tooShort)
        {
            lastAction = "too_short_monitor";
        }
        else if (tooSlack)
        {
            lastAction = "too_slack_monitor";
        }
        else if (deploying)
        {
            lastAction = "deploying_safe_monitor";
        }
        else
        {
            lastAction = "safe_monitor";
        }
    }

    private void ApplyGuardActionIfEnabled()
    {
        winchCommandModified = false;

        if (actionMode == GuardActionMode.MonitorOnly || tetherManager == null)
        {
            return;
        }

        if (solverFault)
        {
            HoldWinch("guard_hold_solver_fault");
            return;
        }

        if (criticalTension)
        {
            if (actionMode == GuardActionMode.ActiveWinchProtection && payoutWhenTooShort)
            {
                EmergencyPayout("guard_emergency_payout_critical_tension");
            }
            else
            {
                HoldWinch("guard_hold_critical_tension");
            }

            return;
        }

        if (stopRecoveryOnHighTension && warningTension &&
            tetherManager.tetherState == MIMISKDroneCoreTetherManager.TetherState.Recovering)
        {
            HoldWinch("guard_hold_recovery_tension_warning");
            return;
        }

        if (actionMode == GuardActionMode.ActiveWinchProtection && tooShort && payoutWhenTooShort)
        {
            EmergencyPayout("guard_payout_tether_too_short");
        }
    }

    private void HoldWinch(string action)
    {
        tetherManager.targetLengthM = tetherManager.deployedLengthM;
        tetherManager.winchCommandRateMS = 0.0f;
        tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.HoldingDeployed;
        tetherManager.lastEvent = action;
        winchCommandModified = true;
        lastAction = action;
    }

    private void EmergencyPayout(string action)
    {
        float target = Mathf.Clamp(
            physicalTether.straightDistanceM + emergencyPayoutSlackM,
            tetherManager.minimumLengthM,
            tetherManager.maximumLengthM
        );

        tetherManager.targetLengthM = target;
        tetherManager.payoutSpeedMS = Mathf.Min(
            Mathf.Max(0.01f, tetherManager.payoutSpeedMS),
            Mathf.Max(0.01f, emergencyPayoutSpeedMS)
        );
        tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.Deploying;
        tetherManager.lastEvent = action;
        winchCommandModified = true;
        lastAction = action;
    }
}
