using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneAquaHoldSetup
{
    [MenuItem("MIMISK/Drone/Autonomy/Setup AquaLoc Position Hold")]
    public static void SetupAquaLocPositionHold()
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
        hold.takeControlOfDrone = true;
        hold.suppressManualUdpInputWhenActive = false;

        hold.autoEnableWhenModeIsNotDisabled = false;
        hold.initializeHoldTargetAutomatically = true;
        hold.allowManualStickOverride = false;

        hold.enableBumplessCapture = true;
        hold.captureVelocityLeadSeconds = 0.35f;
        hold.maxCaptureLeadM = 0.45f;
        hold.captureReturnSeconds = 1.8f;

        hold.horizontalKp = 1.10f;
        hold.horizontalKd = 0.90f;
        hold.horizontalKi = 0.035f;
        hold.horizontalIntegralLimitM = 1.0f;

        hold.useNonlinearPositionResponse = true;
        hold.nonlinearErrorScaleM = 0.70f;

        hold.adaptiveCloseRangeDamping = true;
        hold.closeRangeRadiusM = 0.65f;
        hold.closeRangeDampingMultiplier = 1.35f;

        hold.maxHorizontalSpeedMS = 0.95f;
        hold.maxCommandMagnitude = 0.90f;

        hold.enableAntiWindup = true;
        hold.integralActivationRadiusM = 1.25f;
        hold.integratorLeakRate = 0.06f;

        hold.enableDisturbanceObserver = true;
        hold.disturbanceLearningRadiusM = 1.25f;
        hold.disturbanceObserverResponse = 0.55f;
        hold.disturbanceCompensationGain = 0.75f;
        hold.maxDisturbanceVelocityMS = 0.45f;

        hold.positionArrivalRadiusM = 0.18f;
        hold.velocityArrivalThresholdMS = 0.09f;

        hold.commandResponse = 8.0f;

        hold.minTargetAltitudeM = 0.6f;
        hold.maxTargetAltitudeM = 12.0f;
        hold.useAquaLocYawForBodyFrame = true;

        EditorUtility.SetDirty(hold);

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

        Debug.Log("[MIMISK] AquaHold v2 configured. Keyboard H should still be used for station keeping.");
    }
}
