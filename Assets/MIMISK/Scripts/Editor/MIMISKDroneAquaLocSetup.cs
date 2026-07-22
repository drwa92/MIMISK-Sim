using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneAquaLocSetup
{
    [MenuItem("MIMISK/Drone/Localization/Setup MIMISK-AquaLoc Robust Estimator")]
    public static void SetupAquaLoc()
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

        MIMISKDroneAquaLocEstimator estimator =
            drone.GetComponent<MIMISKDroneAquaLocEstimator>();

        if (estimator == null)
        {
            estimator = drone.AddComponent<MIMISKDroneAquaLocEstimator>();
        }

        SerializedObject so = new SerializedObject(estimator);

        SetObjectIfExists(so, "imu", drone.GetComponentInChildren<MIMISKDroneImuDevice>());
        SetObjectIfExists(so, "barometer", drone.GetComponentInChildren<MIMISKDroneBarometerDevice>());
        SetObjectIfExists(so, "magnetometer", drone.GetComponentInChildren<MIMISKDroneMagnetometerDevice>());
        SetObjectIfExists(so, "gnss", drone.GetComponentInChildren<MIMISKDroneGnssDevice>());
        SetObjectIfExists(so, "rangefinder", drone.GetComponentInChildren<MIMISKDroneRangefinderDevice>());
        SetObjectIfExists(so, "surfaceBuoyancy", drone.GetComponent<MIMISKDroneSurfaceBuoyancy>());
        SetObjectIfExists(so, "batteryPower", drone.GetComponentInChildren<MIMISKDroneBatteryPowerDevice>());

        SetBoolIfExists(so, "estimatorEnabled", true);
        SetBoolIfExists(so, "resetOnStart", true);

        SetBoolIfExists(so, "initializeFromUnityPose", true);
        SetFloatIfExists(so, "initializationDelaySeconds", 0.25f);
        SetBoolIfExists(so, "useGnssLocalWorldAnchor", true);
        SetBoolIfExists(so, "calibrateGnssOriginAtStartup", true);
        SetBoolIfExists(so, "requireGnssOriginReadyForFusion", true);
        SetFloatIfExists(so, "gnssOriginCalibrationSeconds", 2.0f);
        SetBoolIfExists(so, "freezeOutputDuringGnssOriginCalibration", true);

        SetFloatIfExists(so, "landingApproachRangeM", 3.0f);
        SetIntIfExists(so, "waterContactMinBuoyancyPoints", 1);
        SetIntIfExists(so, "surfaceFloatMinBuoyancyPoints", 3);

        SetFloatIfExists(so, "gyroNoiseQ", 1.0e-6f);
        SetFloatIfExists(so, "gyroNoiseR", 2.5e-5f);
        SetFloatIfExists(so, "accelNoiseQ", 2.0e-4f);
        SetFloatIfExists(so, "accelNoiseR", 2.0e-3f);
        SetFloatIfExists(so, "magYawNoiseQ", 0.08f);
        SetFloatIfExists(so, "magYawNoiseR", 2.0f);

        SetBoolIfExists(so, "enableGnssPrefilter", true);
        SetFloatIfExists(so, "gnssPositionFilterQ", 0.02f);
        SetFloatIfExists(so, "gnssVelocityFilterQ", 0.02f);

        SetBoolIfExists(so, "enableAltitudePrefilter", true);
        SetFloatIfExists(so, "barometerFilterQ", 0.01f);
        SetFloatIfExists(so, "rangefinderFilterQ", 0.006f);

        SetFloatIfExists(so, "rollPitchGyroAlpha", 0.985f);
        SetFloatIfExists(so, "yawCorrectionGain", 6.0f);
        SetFloatIfExists(so, "yawGyroSign", 1.0f);
        SetFloatIfExists(so, "pitchGyroSign", 1.0f);
        SetFloatIfExists(so, "rollGyroSign", 1.0f);
        SetFloatIfExists(so, "magYawOffsetDeg", 0.0f);
        SetFloatIfExists(so, "accelTiltGateMS2", 2.5f);

        SetFloatIfExists(so, "magnetometerYawSigmaDeg", 12.0f);
        SetBoolIfExists(so, "reduceMagTrustDuringHighCurrent", true);
        SetFloatIfExists(so, "highCurrentThresholdA", 15.0f);
        SetFloatIfExists(so, "highCurrentMagTrustScale", 0.85f);
        SetBoolIfExists(so, "useMagneticFieldNormQuality", true);
        SetFloatIfExists(so, "magneticFieldNormTolerance", 0.20f);
        SetFloatIfExists(so, "minimumMagneticFieldTrust", 0.25f);

        SetFloatIfExists(so, "horizontalAccelPredictionScale", 0.12f);
        SetFloatIfExists(so, "verticalAccelPredictionScale", 0.05f);

        SetFloatIfExists(so, "qHorizontalPosition", 0.010f);
        SetFloatIfExists(so, "qHorizontalVelocity", 0.030f);
        SetFloatIfExists(so, "qVerticalPosition", 0.006f);
        SetFloatIfExists(so, "qVerticalVelocity", 0.025f);

        SetFloatIfExists(so, "gnssHorizontalSigmaM", 1.5f);
        SetFloatIfExists(so, "gnssVerticalSigmaM", 2.5f);
        SetFloatIfExists(so, "gnssVelocitySigmaMS", 0.25f);
        SetFloatIfExists(so, "barometerSigmaM", 0.35f);
        SetFloatIfExists(so, "rangefinderSigmaM", 0.12f);

        SetBoolIfExists(so, "enableRobustGating", true);
        SetFloatIfExists(so, "softGate", 3.0f);
        SetFloatIfExists(so, "hardGate", 9.0f);

        SetBoolIfExists(so, "enableSurfaceConstraint", true);
        SetFloatIfExists(so, "waterSurfaceY", 0.0f);
        SetFloatIfExists(so, "surfaceFloatRootHeightM", 0.0f);
        SetBoolIfExists(so, "disableOpticalRangefinderWhenWet", true);
        SetFloatIfExists(so, "rangefinderMaxFusionRangeM", 6.0f);

        SetBoolIfExists(so, "enableOutputSmoothing", true);
        SetFloatIfExists(so, "outputPositionResponseHz", 8.0f);
        SetFloatIfExists(so, "outputVelocityResponseHz", 10.0f);
        SetFloatIfExists(so, "outputYawResponseHz", 10.0f);

        so.ApplyModifiedProperties();

        estimator.enabled = true;
        EditorUtility.SetDirty(estimator);

        MIMISKDroneAquaLocLogger logger =
            drone.GetComponent<MIMISKDroneAquaLocLogger>();

        if (logger == null)
        {
            logger = drone.AddComponent<MIMISKDroneAquaLocLogger>();
        }

        logger.estimator = estimator;
        logger.enableLogging = true;
        logger.logHz = 50.0f;
        logger.flushEveryLine = false;
        logger.enabled = true;

        EditorUtility.SetDirty(logger);

        MIMISKDroneFusedLocalization oldEstimator =
            drone.GetComponent<MIMISKDroneFusedLocalization>();

        if (oldEstimator != null)
        {
            oldEstimator.enabled = false;
            EditorUtility.SetDirty(oldEstimator);
        }

        MIMISKDroneLocalizationLogger oldLogger =
            drone.GetComponent<MIMISKDroneLocalizationLogger>();

        if (oldLogger != null)
        {
            oldLogger.enabled = false;
            EditorUtility.SetDirty(oldLogger);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] MIMISK-AquaLoc v6 synchronized estimator configured.");
    }

    private static void SetFloatIfExists(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.floatValue = value;
        else Debug.LogWarning("[AquaLoc Setup] Missing float: " + name);
    }

    private static void SetIntIfExists(SerializedObject so, string name, int value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.intValue = value;
        else Debug.LogWarning("[AquaLoc Setup] Missing int: " + name);
    }

    private static void SetBoolIfExists(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.boolValue = value;
        else Debug.LogWarning("[AquaLoc Setup] Missing bool: " + name);
    }

    private static void SetObjectIfExists(SerializedObject so, string name, Object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning("[AquaLoc Setup] Missing object: " + name);
    }
}
