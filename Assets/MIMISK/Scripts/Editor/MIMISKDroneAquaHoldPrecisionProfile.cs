using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneAquaHoldPrecisionProfile
{
    [MenuItem("MIMISK/Drone/Autonomy/Apply AquaHold v3 Precision Station Keeping Profile")]
    public static void ApplyPrecisionProfile()
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

        // Keep hold disabled by default. Keyboard H activates it.
        hold.enableGuidance = false;
        hold.guidanceMode = MIMISKDroneAquaLocPositionHold.GuidanceMode.Disabled;
        hold.takeControlOfDrone = true;

        // KeyboardStationKeeping owns gamepad enable/disable.
        hold.suppressManualUdpInputWhenActive = false;

        // Safety / compatibility.
        SetBoolIfExists(hold, "autoEnableWhenModeIsNotDisabled", false);
        SetBoolIfExists(hold, "allowManualStickOverride", false);
        SetBoolIfExists(hold, "initializeHoldTargetAutomatically", true);

        // Bumpless capture: prevents aggressive correction when H is pressed while the drone is still moving.
        SetBoolIfExists(hold, "enableBumplessCapture", true);
        SetFloatIfExists(hold, "captureVelocityLeadSeconds", 0.28f);
        SetFloatIfExists(hold, "maxCaptureLeadM", 0.28f);
        SetFloatIfExists(hold, "captureReturnSeconds", 2.40f);

        // Main precision-hold gains.
        // Less aggressive than the previous test; more damping and less saturation.
        SetFloatIfExists(hold, "horizontalKp", 0.78f);
        SetFloatIfExists(hold, "horizontalKd", 1.18f);
        SetFloatIfExists(hold, "horizontalKi", 0.020f);
        SetFloatIfExists(hold, "horizontalIntegralLimitM", 0.65f);

        SetBoolIfExists(hold, "useNonlinearPositionResponse", true);
        SetFloatIfExists(hold, "nonlinearErrorScaleM", 0.95f);

        SetBoolIfExists(hold, "adaptiveCloseRangeDamping", true);
        SetFloatIfExists(hold, "closeRangeRadiusM", 0.85f);
        SetFloatIfExists(hold, "closeRangeDampingMultiplier", 1.85f);

        // Reduce maximum hold velocity. This should reduce orbiting/overshoot around the target.
        SetFloatIfExists(hold, "maxHorizontalSpeedMS", 0.55f);
        SetFloatIfExists(hold, "maxCommandMagnitude", 0.58f);

        // Anti-windup.
        SetBoolIfExists(hold, "enableAntiWindup", true);
        SetFloatIfExists(hold, "integralActivationRadiusM", 0.85f);
        SetFloatIfExists(hold, "integratorLeakRate", 0.12f);

        // Slower disturbance observer. Previous version learned too aggressively.
        SetBoolIfExists(hold, "enableDisturbanceObserver", true);
        SetFloatIfExists(hold, "disturbanceLearningRadiusM", 0.95f);
        SetFloatIfExists(hold, "disturbanceObserverResponse", 0.25f);
        SetFloatIfExists(hold, "disturbanceCompensationGain", 0.45f);
        SetFloatIfExists(hold, "maxDisturbanceVelocityMS", 0.25f);

        // Arrival thresholds.
        SetFloatIfExists(hold, "positionArrivalRadiusM", 0.22f);
        SetFloatIfExists(hold, "velocityArrivalThresholdMS", 0.08f);

        // Command smoothing.
        SetFloatIfExists(hold, "commandResponse", 4.5f);

        // Altitude/yaw.
        SetFloatIfExists(hold, "minTargetAltitudeM", 0.6f);
        SetFloatIfExists(hold, "maxTargetAltitudeM", 12.0f);
        SetBoolIfExists(hold, "useAquaLocYawForBodyFrame", true);

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

        Debug.Log("[MIMISK] Applied AquaHold v3 Precision Station Keeping profile.");
    }

    private static void SetFloatIfExists(Object obj, string name, float value)
    {
        SerializedObject so = new SerializedObject(obj);
        SerializedProperty p = so.FindProperty(name);

        if (p != null)
        {
            p.floatValue = value;
            so.ApplyModifiedProperties();
        }
    }

    private static void SetBoolIfExists(Object obj, string name, bool value)
    {
        SerializedObject so = new SerializedObject(obj);
        SerializedProperty p = so.FindProperty(name);

        if (p != null)
        {
            p.boolValue = value;
            so.ApplyModifiedProperties();
        }
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
