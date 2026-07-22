using UnityEngine;

[DefaultExecutionOrder(1800)]
[DisallowMultipleComponent]
public class MIMISKTetherSmartWinchController : MonoBehaviour
{
    public enum SmartWinchMode
    {
        Disabled,
        DistanceSlackPD,
        HybridFeedforward
    }

    [Header("References")]
    public MIMISKUnifiedTetherManager unifiedTether;
    public MIMISKDroneCoreTetherManager tetherManager;
    public Rigidbody droneRigidbody;
    public Rigidbody miniRovRigidbody;
    public Transform miniRovTetherPoint;

    [Header("Controller")]
    public bool controllerEnabled = true;
    public SmartWinchMode mode = SmartWinchMode.HybridFeedforward;

    [Tooltip("Only control the winch when UnifiedTether is in RovControlActive.")]
    public bool activeOnlyWhenRovControlActive = true;

    [Tooltip("When ON, disables the simple adaptiveSlackManagement flag in UnifiedTether to avoid two scripts writing targetLengthM.")]
    public bool takeOwnershipFromUnifiedSimpleSlack = true;

    [Header("Semi-Taut Objective")]
    public float desiredSlackM = 0.10f;
    public float slackDeadbandM = 0.03f;

    [Tooltip("If stretch exceeds this value, immediately command payout.")]
    public float stretchEmergencyThresholdM = 0.03f;

    public float emergencyPayoutSlackM = 0.20f;

    [Header("PD + Feedforward")]
    public float kpLength = 0.85f;
    public float kdLength = 0.18f;

    [Tooltip("Feedforward from ROV velocity projected away from the fairlead.")]
    public float rovVelocityFeedforward = 0.65f;

    public float minCommandSpeedMS = 0.015f;
    public float maxPayoutSpeedMS = 0.25f;
    public float maxRecoverySpeedMS = 0.25f;

    [Header("Runtime")]
    public bool smartTmsActive;
    public float requiredLengthM;
    public float desiredLengthM;
    public float deployedLengthM;
    public float lengthErrorM;
    public float lengthErrorRateMS;
    public float projectedRovSpeedMS;
    public float winchSpeedCommandMS;
    public float commandedTargetLengthM;
    public string tmsDecision = "idle";
    public string lastEvent = "idle";

    private float previousLengthErrorM;
    private bool previousInitialized;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void FixedUpdate()
    {
        if (!controllerEnabled || mode == SmartWinchMode.Disabled)
        {
            smartTmsActive = false;
            return;
        }

        AutoFindReferences();

        if (tetherManager == null)
        {
            smartTmsActive = false;
            lastEvent = "missing_tether_manager";
            return;
        }

        if (activeOnlyWhenRovControlActive &&
            !IsRovControlActive())
        {
            smartTmsActive = false;
            previousInitialized = false;
            return;
        }

        if (takeOwnershipFromUnifiedSimpleSlack &&
            unifiedTether != null)
        {
            unifiedTether.adaptiveSlackManagement = false;
        }

        UpdateSmartWinch(Time.fixedDeltaTime);
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

        if (droneRigidbody == null)
        {
            droneRigidbody =
                GetComponent<Rigidbody>();
        }

        if (unifiedTether != null)
        {
            if (miniRovRigidbody == null)
            {
                miniRovRigidbody =
                    unifiedTether.miniRovRigidbody;
            }

            if (miniRovTetherPoint == null)
            {
                miniRovTetherPoint =
                    unifiedTether.miniRovTetherAnchor;
            }
        }

        if (miniRovTetherPoint == null && tetherManager != null)
        {
            miniRovTetherPoint =
                tetherManager.miniRovTetherPoint;
        }

        if (miniRovRigidbody == null && tetherManager != null)
        {
            miniRovRigidbody =
                tetherManager.miniRovRigidbody;
        }
    }

    [ContextMenu("Enable Smart TMS")]
    public void EnableSmartTms()
    {
        controllerEnabled = true;
        mode = SmartWinchMode.HybridFeedforward;
        lastEvent = "smart_tms_enabled";
    }

    [ContextMenu("Disable Smart TMS")]
    public void DisableSmartTms()
    {
        controllerEnabled = false;
        smartTmsActive = false;
        lastEvent = "smart_tms_disabled";
    }

    private bool IsRovControlActive()
    {
        return
            unifiedTether != null &&
            unifiedTether.tetherState ==
                MIMISKUnifiedTetherManager.TetherMissionState.RovControlActive;
    }

    private void UpdateSmartWinch(float dt)
    {
        deployedLengthM =
            tetherManager.deployedLengthM;

        Vector3 start =
            tetherManager.tetherStartWorld;

        Vector3 end =
            tetherManager.tetherEndWorld;

        if (miniRovTetherPoint != null)
        {
            end =
                miniRovTetherPoint.position;
        }

        requiredLengthM =
            Mathf.Max(
                0.001f,
                Vector3.Distance(start, end)
            );

        desiredLengthM =
            Mathf.Clamp(
                requiredLengthM + desiredSlackM,
                tetherManager.minimumLengthM,
                tetherManager.maximumLengthM
            );

        lengthErrorM =
            desiredLengthM - deployedLengthM;

        if (!previousInitialized)
        {
            previousLengthErrorM = lengthErrorM;
            previousInitialized = true;
        }

        lengthErrorRateMS =
            (lengthErrorM - previousLengthErrorM) /
            Mathf.Max(0.001f, dt);

        previousLengthErrorM =
            lengthErrorM;

        projectedRovSpeedMS =
            ComputeProjectedRovSpeed(start, end);

        float pd =
            kpLength * lengthErrorM +
            kdLength * lengthErrorRateMS;

        float ff =
            mode == SmartWinchMode.HybridFeedforward
                ? rovVelocityFeedforward * projectedRovSpeedMS
                : 0.0f;

        winchSpeedCommandMS =
            pd + ff;

        if (tetherManager.stretchM > stretchEmergencyThresholdM)
        {
            desiredLengthM =
                Mathf.Clamp(
                    requiredLengthM + emergencyPayoutSlackM,
                    tetherManager.minimumLengthM,
                    tetherManager.maximumLengthM
                );

            winchSpeedCommandMS =
                maxPayoutSpeedMS;

            tmsDecision =
                "emergency_payout_stretch";
        }

        if (Mathf.Abs(lengthErrorM) <= slackDeadbandM &&
            tetherManager.stretchM <= stretchEmergencyThresholdM)
        {
            winchSpeedCommandMS = 0.0f;
            commandedTargetLengthM = tetherManager.deployedLengthM;
            tetherManager.targetLengthM = commandedTargetLengthM;
            tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.HoldingDeployed;
            tetherManager.lastEvent = "smart_tms_hold_semi_taut";
            tmsDecision = "hold";
            smartTmsActive = true;
            lastEvent = "smart_tms_hold";
            return;
        }

        if (winchSpeedCommandMS > 0.0f)
        {
            winchSpeedCommandMS =
                Mathf.Clamp(
                    winchSpeedCommandMS,
                    minCommandSpeedMS,
                    maxPayoutSpeedMS
                );

            tetherManager.payoutSpeedMS =
                winchSpeedCommandMS;

            commandedTargetLengthM =
                desiredLengthM;

            tetherManager.targetLengthM =
                commandedTargetLengthM;

            tetherManager.tetherState =
                MIMISKDroneCoreTetherManager.TetherState.Deploying;

            tetherManager.lastEvent =
                "smart_tms_payout";

            tmsDecision =
                "payout";
        }
        else
        {
            winchSpeedCommandMS =
                -Mathf.Clamp(
                    Mathf.Abs(winchSpeedCommandMS),
                    minCommandSpeedMS,
                    maxRecoverySpeedMS
                );

            tetherManager.recoverySpeedMS =
                Mathf.Abs(winchSpeedCommandMS);

            commandedTargetLengthM =
                desiredLengthM;

            tetherManager.targetLengthM =
                commandedTargetLengthM;

            tetherManager.tetherState =
                MIMISKDroneCoreTetherManager.TetherState.Recovering;

            tetherManager.lastEvent =
                "smart_tms_recover_slack";

            tmsDecision =
                "recover";
        }

        smartTmsActive = true;
        lastEvent = "smart_tms_" + tmsDecision;
    }

    private float ComputeProjectedRovSpeed(Vector3 start, Vector3 end)
    {
        if (miniRovRigidbody == null)
        {
            return 0.0f;
        }

        Vector3 direction =
            end - start;

        float distance =
            direction.magnitude;

        if (distance < 0.001f)
        {
            return 0.0f;
        }

        direction /=
            distance;

        Vector3 rovVelocity =
            miniRovRigidbody.linearVelocity;

        Vector3 surfaceVelocity =
            droneRigidbody != null
                ? droneRigidbody.linearVelocity
                : Vector3.zero;

        return
            Vector3.Dot(
                rovVelocity - surfaceVelocity,
                direction
            );
    }
}
