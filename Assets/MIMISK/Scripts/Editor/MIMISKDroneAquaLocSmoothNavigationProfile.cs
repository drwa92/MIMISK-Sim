using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneAquaLocSmoothNavigationProfile
{
    [MenuItem("MIMISK/Drone/Localization/Apply Profile/Paper GPS v7.3 Smooth Navigation Output")]
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

        MIMISKDroneAquaLocEstimator estimator =
            drone.GetComponent<MIMISKDroneAquaLocEstimator>();

        if (estimator == null)
        {
            estimator = drone.AddComponent<MIMISKDroneAquaLocEstimator>();
        }

        MIMISKDroneGnssDevice gnss =
            drone.GetComponentInChildren<MIMISKDroneGnssDevice>();

        if (gnss != null)
        {
            gnss.gnssModel = MIMISKDroneGnssDevice.GnssModel.UBloxM10;
            gnss.ConfigureModelDefaults();

            // Paper-equivalent GPS class.
            gnss.horizontalPositionNoiseM = 0.50f;
            gnss.verticalPositionNoiseM = 1.00f;
            gnss.velocityNoiseMS = 0.10f;

            gnss.horizontalAccuracyM = 0.50f;
            gnss.verticalAccuracyM = 1.00f;
            gnss.velocityAccuracyMS = 0.10f;

            gnss.sampleRateHz = 10.0f;
            gnss.enableNoise = true;
            gnss.enableDropout = false;

            EditorUtility.SetDirty(gnss);
        }

        SerializedObject so = new SerializedObject(estimator);

        SetObject(so, "imu", drone.GetComponentInChildren<MIMISKDroneImuDevice>());
        SetObject(so, "barometer", drone.GetComponentInChildren<MIMISKDroneBarometerDevice>());
        SetObject(so, "magnetometer", drone.GetComponentInChildren<MIMISKDroneMagnetometerDevice>());
        SetObject(so, "gnss", gnss);
        SetObject(so, "rangefinder", drone.GetComponentInChildren<MIMISKDroneRangefinderDevice>());
        SetObject(so, "surfaceBuoyancy", drone.GetComponent<MIMISKDroneSurfaceBuoyancy>());
        SetObject(so, "batteryPower", drone.GetComponentInChildren<MIMISKDroneBatteryPowerDevice>());

        SetBool(so, "estimatorEnabled", true);
        SetBool(so, "resetOnStart", true);

        SetBool(so, "initializeFromUnityPose", true);
        SetFloat(so, "initializationDelaySeconds", 0.25f);
        SetBool(so, "useGnssLocalWorldAnchor", true);
        SetBool(so, "calibrateGnssOriginAtStartup", true);
        SetBool(so, "requireGnssOriginReadyForFusion", true);
        SetFloat(so, "gnssOriginCalibrationSeconds", 5.0f);
        SetInt(so, "gnssOriginMinimumSamples", 30);
        SetBool(so, "freezeOutputDuringGnssOriginCalibration", true);

        SetFloat(so, "gyroNoiseQ", 1.0e-6f);
        SetFloat(so, "gyroNoiseR", 2.5e-5f);
        SetFloat(so, "accelNoiseQ", 2.0e-4f);
        SetFloat(so, "accelNoiseR", 2.0e-3f);
        SetFloat(so, "magYawNoiseQ", 0.08f);
        SetFloat(so, "magYawNoiseR", 2.0f);

        SetBool(so, "enableGnssPrefilter", true);
        SetFloat(so, "gnssPositionFilterQ", 0.006f);
        SetFloat(so, "gnssVelocityFilterQ", 0.006f);

        SetBool(so, "enableAltitudePrefilter", true);
        SetFloat(so, "barometerFilterQ", 0.01f);
        SetFloat(so, "rangefinderFilterQ", 0.006f);

        SetFloat(so, "rollPitchGyroAlpha", 0.985f);
        SetFloat(so, "yawCorrectionGain", 6.0f);
        SetFloat(so, "yawGyroSign", 1.0f);
        SetFloat(so, "pitchGyroSign", 1.0f);
        SetFloat(so, "rollGyroSign", 1.0f);
        SetFloat(so, "magYawOffsetDeg", 0.0f);
        SetFloat(so, "accelTiltGateMS2", 2.5f);

        SetFloat(so, "magnetometerYawSigmaDeg", 12.0f);
        SetBool(so, "reduceMagTrustDuringHighCurrent", true);
        SetFloat(so, "highCurrentThresholdA", 15.0f);
        SetFloat(so, "highCurrentMagTrustScale", 0.85f);
        SetBool(so, "useMagneticFieldNormQuality", true);
        SetFloat(so, "magneticFieldNormTolerance", 0.20f);
        SetFloat(so, "minimumMagneticFieldTrust", 0.25f);

        SetFloat(so, "horizontalAccelPredictionScale", 0.14f);
        SetFloat(so, "verticalAccelPredictionScale", 0.05f);

        SetFloat(so, "qHorizontalPosition", 0.006f);
        SetFloat(so, "qHorizontalVelocity", 0.024f);
        SetFloat(so, "qVerticalPosition", 0.006f);
        SetFloat(so, "qVerticalVelocity", 0.025f);

        SetFloat(so, "gnssHorizontalSigmaM", 0.65f);
        SetFloat(so, "gnssVerticalSigmaM", 1.10f);
        SetFloat(so, "gnssVelocitySigmaMS", 0.12f);
        SetFloat(so, "barometerSigmaM", 0.35f);
        SetFloat(so, "rangefinderSigmaM", 0.12f);

        SetBool(so, "enableRobustGating", true);
        SetFloat(so, "softGate", 3.0f);
        SetFloat(so, "hardGate", 9.0f);

        SetBool(so, "enableSurfaceConstraint", true);
        SetFloat(so, "waterSurfaceY", 0.0f);
        SetFloat(so, "surfaceFloatRootHeightM", 0.0f);
        SetBool(so, "disableOpticalRangefinderWhenWet", true);
        SetFloat(so, "rangefinderMaxFusionRangeM", 6.0f);

        // v7.3 difference:
        // Causal smooth output for navigation and paper-quality trajectory display.
        // Raw/internal estimates remain logged separately.
        SetBool(so, "enableOutputSmoothing", true);
        SetFloat(so, "outputPositionResponseHz", 5.5f);
        SetFloat(so, "outputVelocityResponseHz", 8.0f);
        SetFloat(so, "outputYawResponseHz", 12.0f);

        SetBool(so, "enableOutputLeadCompensation", true);
        SetFloat(so, "horizontalOutputLeadSeconds", 0.65f);
        SetFloat(so, "verticalOutputLeadSeconds", 0.02f);
        SetFloat(so, "maxOutputLeadCorrectionM", 0.95f);

        SetBool(so, "enableAdaptiveHorizontalLead", true);
        SetFloat(so, "adaptiveHorizontalLeadSeconds", 0.65f);
        SetFloat(so, "minAdaptiveHorizontalLeadSeconds", 0.45f);
        SetFloat(so, "maxAdaptiveHorizontalLeadSeconds", 0.80f);
        SetFloat(so, "adaptiveLeadLearningRate", 0.03f);
        SetFloat(so, "adaptiveLeadMinSpeedMS", 0.12f);
        SetFloat(so, "adaptiveLeadMaxInnovationM", 1.75f);

        so.ApplyModifiedProperties();

        estimator.enabled = true;
        EditorUtility.SetDirty(estimator);

        MIMISKDroneAquaLocLogger logger =
            drone.GetComponent<MIMISKDroneAquaLocLogger>();

        if (logger != null)
        {
            logger.estimator = estimator;
            logger.enableLogging = true;
            logger.logHz = 50.0f;
            EditorUtility.SetDirty(logger);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Applied AquaLoc v7.3 Smooth Navigation Output profile.");
    }

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.floatValue = value;
        else Debug.LogWarning("[AquaLoc v7.3] Missing float: " + name);
    }

    private static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.intValue = value;
        else Debug.LogWarning("[AquaLoc v7.3] Missing int: " + name);
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.boolValue = value;
        else Debug.LogWarning("[AquaLoc v7.3] Missing bool: " + name);
    }

    private static void SetObject(SerializedObject so, string name, Object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning("[AquaLoc v7.3] Missing object: " + name);
    }
}
