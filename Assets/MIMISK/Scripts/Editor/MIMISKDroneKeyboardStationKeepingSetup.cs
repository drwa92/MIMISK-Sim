using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneKeyboardStationKeepingSetup
{
    [MenuItem("MIMISK/Drone/Autonomy/Setup Keyboard Station Keeping")]
    public static void SetupKeyboardStationKeeping()
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

        MIMISKDroneAquaLocEstimator aquaLoc =
            drone.GetComponent<MIMISKDroneAquaLocEstimator>();

        if (aquaLoc == null)
        {
            Debug.LogError("[MIMISK] AquaLoc estimator missing. Set up localization first.");
            return;
        }

        MIMISKDroneModelController controller =
            drone.GetComponent<MIMISKDroneModelController>();

        if (controller == null)
        {
            Debug.LogError("[MIMISK] MIMISKDroneModelController missing.");
            return;
        }

        MIMISKDroneUdpGamepadReceiver udp =
            drone.GetComponent<MIMISKDroneUdpGamepadReceiver>();

        if (udp == null)
        {
            Debug.LogError("[MIMISK] MIMISKDroneUdpGamepadReceiver missing.");
            return;
        }

        MIMISKDroneAquaLocPositionHold hold =
            drone.GetComponent<MIMISKDroneAquaLocPositionHold>();

        if (hold == null)
        {
            hold = drone.AddComponent<MIMISKDroneAquaLocPositionHold>();
        }

        hold.aquaLoc = aquaLoc;
        hold.controller = controller;
        hold.udpReceiver = udp;

        hold.enableGuidance = false;
        hold.guidanceMode = MIMISKDroneAquaLocPositionHold.GuidanceMode.Disabled;
        hold.takeControlOfDrone = true;

        // KeyboardStationKeeping handles gamepad enable/disable.
        hold.suppressManualUdpInputWhenActive = false;

        hold.horizontalKp = 0.75f;
        hold.horizontalKd = 0.55f;
        hold.horizontalKi = 0.06f;
        hold.horizontalIntegralLimitM = 1.5f;

        hold.maxHorizontalSpeedMS = 0.85f;
        hold.maxCommandMagnitude = 0.85f;

        hold.enableDisturbanceObserver = true;
        hold.disturbanceLearningRadiusM = 1.5f;
        hold.disturbanceObserverResponse = 0.7f;
        hold.disturbanceCompensationGain = 0.85f;
        hold.maxDisturbanceVelocityMS = 0.6f;

        hold.positionArrivalRadiusM = 0.20f;
        hold.velocityArrivalThresholdMS = 0.10f;
        hold.commandResponse = 6.0f;

        hold.minTargetAltitudeM = 0.6f;
        hold.maxTargetAltitudeM = 12.0f;
        hold.useAquaLocYawForBodyFrame = true;

        // Disable optional auto/manual-override fields if they exist from previous patches.
        SerializedObject holdSO = new SerializedObject(hold);

        SetBoolIfExists(holdSO, "autoEnableWhenModeIsNotDisabled", false);
        SetBoolIfExists(holdSO, "allowManualStickOverride", false);
        SetBoolIfExists(holdSO, "initializeHoldTargetAutomatically", true);

        holdSO.ApplyModifiedProperties();
        EditorUtility.SetDirty(hold);

        MIMISKDroneKeyboardStationKeeping commander =
            drone.GetComponent<MIMISKDroneKeyboardStationKeeping>();

        if (commander == null)
        {
            commander = drone.AddComponent<MIMISKDroneKeyboardStationKeeping>();
        }

        commander.aquaLoc = aquaLoc;
        commander.aquaHold = hold;
        commander.controller = controller;
        commander.udpReceiver = udp;

        commander.disableUdpReceiverWhileHolding = true;
        commander.forceUdpReceiverOnWhenReleased = true;
        commander.forceManualModeOnRelease = true;
        commander.captureYawOnHold = true;
        commander.stationKeepingActive = false;

        EditorUtility.SetDirty(commander);

        // Normal mode must always start with gamepad enabled.
        udp.enabled = true;

        SerializedObject udpSO = new SerializedObject(udp);

        SetBoolIfExists(udpSO, "suppressCommandOutput", false);
        SetBoolIfExists(udpSO, "allowModeButtonsWhileSuppressed", false);

        udpSO.ApplyModifiedProperties();
        EditorUtility.SetDirty(udp);

        MIMISKDroneAquaLocPositionHoldLogger logger =
            drone.GetComponent<MIMISKDroneAquaLocPositionHoldLogger>();

        if (logger == null)
        {
            logger = drone.AddComponent<MIMISKDroneAquaLocPositionHoldLogger>();
        }

        logger.hold = hold;
        logger.aquaLoc = aquaLoc;
        logger.enableLogging = true;
        logger.logHz = 50.0f;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(logger);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Keyboard station keeping configured: gamepad manual mode, H for hold, H/P for release.");
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
