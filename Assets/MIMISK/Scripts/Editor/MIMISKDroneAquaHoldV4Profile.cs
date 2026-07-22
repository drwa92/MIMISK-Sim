using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneAquaHoldV4Profile
{
    [MenuItem("MIMISK/Drone/Autonomy/Apply AquaHold v4 Adaptive Capture Profile")]
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
        hold.feedbackPositionResponse = 9.0f;
        hold.feedbackVelocityResponse = 11.0f;
        hold.controlDeadbandM = 0.055f;

        hold.enableBumplessCapture = true;
        hold.captureVelocityLeadSeconds = 0.30f;
        hold.maxCaptureLeadM = 0.32f;
        hold.captureReturnSeconds = 2.2f;
        hold.captureToPrecisionRadiusM = 0.32f;
        hold.captureToPrecisionSpeedMS = 0.16f;

        hold.horizontalKp = 0.82f;
        hold.horizontalKd = 1.15f;
        hold.horizontalKi = 0.018f;
        hold.horizontalIntegralLimitM = 0.55f;

        hold.useNonlinearPositionResponse = true;
        hold.nonlinearErrorScaleM = 1.00f;

        hold.captureKpScale = 0.72f;
        hold.captureKdScale = 1.15f;
        hold.captureSpeedScale = 0.65f;
        hold.captureCommandScale = 0.70f;

        hold.precisionKpScale = 0.62f;
        hold.precisionKdScale = 1.70f;
        hold.precisionSpeedScale = 0.45f;
        hold.precisionCommandScale = 0.58f;

        hold.goToKpScale = 1.00f;
        hold.goToKdScale = 1.00f;
        hold.goToSpeedScale = 1.00f;
        hold.goToCommandScale = 1.00f;

        hold.adaptiveCloseRangeDamping = true;
        hold.closeRangeRadiusM = 0.85f;
        hold.closeRangeDampingMultiplier = 1.85f;

        hold.maxHorizontalSpeedMS = 0.62f;
        hold.maxCommandMagnitude = 0.62f;

        hold.enableAntiWindup = true;
        hold.integralActivationRadiusM = 0.80f;
        hold.integratorLeakRate = 0.14f;

        hold.enableDisturbanceObserver = true;
        hold.disturbanceLearningRadiusM = 0.65f;
        hold.disturbanceLearningSpeedMS = 0.18f;
        hold.disturbanceObserverResponse = 0.18f;
        hold.disturbanceCompensationGain = 0.35f;
        hold.maxDisturbanceVelocityMS = 0.18f;

        hold.positionArrivalRadiusM = 0.22f;
        hold.velocityArrivalThresholdMS = 0.08f;
        hold.stableRequiredSeconds = 1.25f;

        hold.commandResponse = 4.2f;
        hold.maxCommandSlewPerSecond = 1.6f;

        hold.minTargetAltitudeM = 0.6f;
        hold.maxTargetAltitudeM = 12.0f;
        hold.useAquaLocYawForBodyFrame = true;

        EditorUtility.SetDirty(hold);

        MIMISKDroneKeyboardStationKeeping keyboard =
            drone.GetComponent<MIMISKDroneKeyboardStationKeeping>();

        if (keyboard != null)
        {
            keyboard.disableUdpReceiverWhileHolding = true;
            keyboard.forceUdpReceiverOnWhenReleased = true;
            keyboard.forceManualModeOnRelease = true;
            keyboard.captureYawOnHold = true;
            keyboard.stationKeepingActive = false;

            EditorUtility.SetDirty(keyboard);
        }

        MIMISKDroneUdpGamepadReceiver udp =
            drone.GetComponent<MIMISKDroneUdpGamepadReceiver>();

        if (udp != null)
        {
            udp.enabled = true;

            SerializedObject udpSO = new SerializedObject(udp);
            SetBoolIfExists(udpSO, "suppressCommandOutput", false);
            SetBoolIfExists(udpSO, "allowModeButtonsWhileSuppressed", false);
            udpSO.ApplyModifiedProperties();

            EditorUtility.SetDirty(udp);
        }

        MIMISKDroneAquaLocPositionHoldLogger logger =
            drone.GetComponent<MIMISKDroneAquaLocPositionHoldLogger>();

        if (logger == null)
        {
            logger = drone.AddComponent<MIMISKDroneAquaLocPositionHoldLogger>();
        }

        logger.hold = hold;
        logger.aquaLoc = drone.GetComponent<MIMISKDroneAquaLocEstimator>();
        logger.enableLogging = true;
        logger.logHz = 50.0f;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(logger);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Applied AquaHold v4 Adaptive Capture profile.");
    }

    private static void SetBoolIfExists(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);

        if (p != null)
        {
            p.boolValue = value;
        }
    }
}
