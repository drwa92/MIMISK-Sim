using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneAquaHoldV42TransitProfile
{
    [MenuItem("MIMISK/Drone/Autonomy/Apply AquaHold v4.2 Fast-Transit Precision-Hold Profile")]
    public static void ApplyProfile()
    {
        GameObject drone = Selection.activeGameObject;

        if (drone == null || drone.GetComponent<Rigidbody>() == null)
        {
            drone = GameObject.Find("Drone");
        }

        if (drone == null)
        {
            Debug.LogError("[MIMISK] Could not find Drone root.");
            return;
        }

        MIMISKDroneAquaLocPositionHold hold =
            drone.GetComponent<MIMISKDroneAquaLocPositionHold>();

        if (hold == null)
        {
            hold = drone.AddComponent<MIMISKDroneAquaLocPositionHold>();
        }

        hold.aquaLoc = drone.GetComponent<MIMISKDroneAquaLocEstimator>();
        hold.controller = drone.GetComponent<MIMISKDroneModelController>();
        hold.udpReceiver = drone.GetComponent<MIMISKDroneUdpGamepadReceiver>();

        hold.enableGuidance = false;
        hold.guidanceMode = MIMISKDroneAquaLocPositionHold.GuidanceMode.Disabled;
        hold.holdState = MIMISKDroneAquaLocPositionHold.HoldState.Inactive;
        hold.takeControlOfDrone = true;
        hold.suppressManualUdpInputWhenActive = false;

        hold.autoEnableWhenModeIsNotDisabled = false;
        hold.initializeHoldTargetAutomatically = true;
        hold.allowManualStickOverride = false;

        hold.enableFeedbackSmoothing = true;
        hold.feedbackPositionResponse = 8.0f;
        hold.feedbackVelocityResponse = 10.0f;
        hold.controlDeadbandM = 0.035f;

        hold.enableBumplessCapture = true;
        hold.captureVelocityLeadSeconds = 0.25f;
        hold.maxCaptureLeadM = 0.25f;
        hold.captureReturnSeconds = 1.6f;
        hold.captureToPrecisionRadiusM = 0.30f;
        hold.captureToPrecisionSpeedMS = 0.18f;

        // Base controller. Transit authority is scheduled below.
        hold.horizontalKp = 0.95f;
        hold.horizontalKd = 1.05f;
        hold.horizontalKi = 0.015f;
        hold.horizontalIntegralLimitM = 0.45f;

        hold.useNonlinearPositionResponse = true;
        hold.nonlinearErrorScaleM = 0.85f;

        // Capture stage: moderate braking.
        hold.captureKpScale = 0.80f;
        hold.captureKdScale = 1.15f;
        hold.captureSpeedScale = 0.80f;
        hold.captureCommandScale = 0.80f;

        // Precision stage: tight final hold, not too aggressive.
        hold.precisionKpScale = 0.65f;
        hold.precisionKdScale = 1.65f;
        hold.precisionSpeedScale = 0.50f;
        hold.precisionCommandScale = 0.62f;

        // GoTo stage: stronger transit authority for corridor following.
        hold.goToKpScale = 1.35f;
        hold.goToKdScale = 1.05f;
        hold.goToSpeedScale = 1.55f;
        hold.goToCommandScale = 1.35f;

        hold.adaptiveCloseRangeDamping = true;
        hold.closeRangeRadiusM = 0.75f;
        hold.closeRangeDampingMultiplier = 1.55f;

        hold.maxHorizontalSpeedMS = 0.95f;
        hold.maxCommandMagnitude = 0.82f;

        hold.enableAntiWindup = true;
        hold.integralActivationRadiusM = 0.70f;
        hold.integratorLeakRate = 0.16f;

        hold.enableDisturbanceObserver = true;
        hold.disturbanceLearningRadiusM = 0.65f;
        hold.disturbanceLearningSpeedMS = 0.18f;
        hold.disturbanceObserverResponse = 0.14f;
        hold.disturbanceCompensationGain = 0.30f;
        hold.maxDisturbanceVelocityMS = 0.16f;

        hold.positionArrivalRadiusM = 0.20f;
        hold.velocityArrivalThresholdMS = 0.09f;
        hold.stableRequiredSeconds = 1.00f;

        hold.commandResponse = 6.0f;
        hold.maxCommandSlewPerSecond = 2.8f;

        hold.minTargetAltitudeM = 0.6f;
        hold.maxTargetAltitudeM = 12.0f;
        hold.useAquaLocYawForBodyFrame = true;

        EditorUtility.SetDirty(hold);

        MIMISKDroneAquaLocPositionHoldLogger logger =
            drone.GetComponent<MIMISKDroneAquaLocPositionHoldLogger>();

        if (logger != null)
        {
            logger.hold = hold;
            logger.aquaLoc = drone.GetComponent<MIMISKDroneAquaLocEstimator>();
            logger.enableLogging = true;
            logger.logHz = 50.0f;
            EditorUtility.SetDirty(logger);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Applied AquaHold v4.2 fast-transit / precision-hold profile.");
    }
}
