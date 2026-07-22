using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMiniROVPlantBasedControllerSetup
{
    [MenuItem("MIMISK/MiniROV/Setup Plant-Based Controller")]
    public static void SetupPlantBasedController()
    {
        GameObject miniRov =
            GameObject.Find("MiniROV");

        if (miniRov == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV.");
            return;
        }

        Rigidbody rb =
            miniRov.GetComponent<Rigidbody>();

        if (rb == null)
        {
            rb = miniRov.AddComponent<Rigidbody>();
        }

        ControlManager control =
            miniRov.GetComponent<ControlManager>();

        if (control == null)
        {
            control = miniRov.AddComponent<ControlManager>();
        }

        UnityVirtualESP32 esp32 =
            miniRov.GetComponent<UnityVirtualESP32>();

        MIMISKMiniROVPlantBasedController controller =
            miniRov.GetComponent<MIMISKMiniROVPlantBasedController>();

        if (controller == null)
        {
            controller =
                miniRov.AddComponent<MIMISKMiniROVPlantBasedController>();
        }

        controller.rb = rb;
        controller.controlManager = control;
        controller.unityVirtualESP32 = esp32;

        controller.controllerEnabled = false;
        controller.controlMode =
            MIMISKMiniROVPlantBasedController.ControlMode.Disabled;

        controller.waterLevelY = 0.0f;
        controller.depthPositiveDown = true;

        // Robust nominal gains from Python validation.
        controller.surgeSpeedKp = 2.5f;
        controller.surgeSpeedKi = 0.25f;
        controller.surgeSpeedKd = 0.0f;
        controller.surgeCmdLimit = 0.95f;
        controller.surgeIntegralLimit = 1.0f;

        controller.yawAngleKp = 1.0f;
        controller.yawRateKd = 0.6f;
        controller.yawCmdLimit = 0.08f;

        controller.depthKp = 5.0f;
        controller.depthKi = 0.05f;
        controller.depthKd = 25.0f;
        controller.depthCmdLimit = 1.0f;
        controller.depthIntegralLimit = 1.0f;

        controller.waypointDistanceKp = 0.35f;
        controller.waypointMaxSpeedMS = 0.10f;
        controller.losLookaheadM = 0.25f;
        controller.yawAlignmentSlowdownDeg = 45.0f;
        controller.minSpeedAlignmentScale = 0.05f;

        controller.yawReferenceMode =
            MIMISKMiniROVPlantBasedController.YawReferenceMode.TravelHeading;
        controller.allowIndependentYawWhileMoving = false;
        controller.fixedYawDeg = 0.0f;
        controller.hasFinalYawReference = false;
        controller.finalYawDeg = 0.0f;
        controller.activeTravelYawDeg = 0.0f;
        controller.activeControlYawDeg = 0.0f;

        controller.goToPointStopAtTarget = true;
        controller.goToPointArrivalRadiusM = 0.15f;
        controller.goToPointStopSpeedMS = 0.03f;
        controller.goToPointMaxSpeedMS = 0.08f;
        controller.goToPointRotateInPlaceWhenYawLarge = true;
        controller.goToPointRotateInPlaceYawErrorDeg = 65.0f;

        controller.maxThrusterPwm = 255;
        controller.maxBallastPwm = 255;

        controller.invertSurgeOutput = false;
        controller.invertYawOutput = false;
        controller.invertBallastOutput = false;
        controller.swapLeftRight = false;
        controller.invertLeftThruster = false;
        controller.invertRightThruster = false;
        controller.invertBothThrusters = false;

        controller.targetDepthM = Mathf.Max(
            0.1f,
            controller.waterLevelY - miniRov.transform.position.y
        );

        controller.targetPointWorld =
            miniRov.transform.position +
            miniRov.transform.forward * 1.0f;

        controller.targetPointDepthM =
            controller.targetDepthM;

        controller.circleCenterWorld =
            miniRov.transform.position;

        controller.circleDepthM =
            controller.targetDepthM;

        controller.circleRadiusM = 0.8f;

        controller.polylineStopAtEnd = true;
        controller.polylineArrivalRadiusM = 0.15f;
        controller.completionHoldCurrentPose = true;

        controller.goToPointStopAtTarget = true;
        controller.goToPointArrivalRadiusM = 0.15f;
        controller.goToPointStopSpeedMS = 0.03f;

        controller.circleStopAfterCompletedLaps = true;
        controller.circleTargetLaps = 1;
        controller.circleMinRadiusForPhaseM = 0.10f;
        controller.circleStartWithTangentForward = true;
        controller.circleClockwise = true;
        controller.circleStopAfterCompletedLaps = true;
        controller.circleTargetLaps = 1;

        controller.zeroSurgeOnAxisHoldStart = true;
        controller.autoStationHoldWhenNoMode = true;
        controller.stationHoldDeadbandM = 0.12f;
        controller.stationHoldReturnRadiusM = 0.22f;
        controller.stationHoldMaxReturnSpeedMS = 0.04f;
        controller.stationHoldLockYaw = true;


        controller.zeroSurgeOnAxisHoldStart = true;

        controller.AutoFindReferences();

        EditorUtility.SetDirty(controller);

        control.enabled = true;
        control.autoOpenOnStart = false;

        if (rb != null)
        {
            control.rb = rb;
        }

        if (control.leftThruster == null)
        {
            control.leftThruster =
                FindDeepChild(miniRov.transform, "propulseur_gauche");
        }

        if (control.rightThruster == null)
        {
            control.rightThruster =
                FindDeepChild(miniRov.transform, "propulseur_droite");
        }

        EditorUtility.SetDirty(control);

        if (esp32 != null)
        {
            esp32.autoOpenOnStart = false;
            esp32.rb = rb;
            esp32.controlManager = control;
            EditorUtility.SetDirty(esp32);
        }

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] MiniROV plant-based controller configured. Use component context menu to run Hold, Go-To-Point, Line, Square, or Circle tests.");
    }

    private static Transform FindDeepChild(
        Transform root,
        string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found =
                FindDeepChild(
                    root.GetChild(i),
                    childName
                );

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
