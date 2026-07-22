using UnityEngine;

public class MIMISKDroneLocalizationProfileManager : MonoBehaviour
{
    public enum LocalizationProfile
    {
        AquaLocRawM10Baseline,
        AquaLocLowCostSmoothM10,
        AquaLocPaperEquivalentGPS,
        AquaLocRtkHighAccuracy,
        Disabled
    }

    [Header("Profile")]
    public LocalizationProfile activeProfile = LocalizationProfile.AquaLocPaperEquivalentGPS;
    public bool applyOnStart = false;

    [Header("References")]
    public MIMISKDroneAquaLocEstimator aquaLoc;
    public MIMISKDroneAquaLocLogger aquaLocLogger;
    public MIMISKDroneGnssDevice gnss;

    [Header("Debug")]
    public string appliedProfileName;
    public string profileDescription;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        if (applyOnStart)
        {
            ApplyActiveProfile();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (aquaLoc == null)
        {
            aquaLoc = GetComponent<MIMISKDroneAquaLocEstimator>();
        }

        if (aquaLocLogger == null)
        {
            aquaLocLogger = GetComponent<MIMISKDroneAquaLocLogger>();
        }

        if (gnss == null)
        {
            gnss = GetComponentInChildren<MIMISKDroneGnssDevice>();
        }
    }

    [ContextMenu("Apply Active Localization Profile")]
    public void ApplyActiveProfile()
    {
        AutoFindReferences();

        if (aquaLoc == null)
        {
            aquaLoc = gameObject.AddComponent<MIMISKDroneAquaLocEstimator>();
        }

        if (aquaLocLogger == null)
        {
            aquaLocLogger = gameObject.AddComponent<MIMISKDroneAquaLocLogger>();
        }

        if (activeProfile == LocalizationProfile.Disabled)
        {
            aquaLoc.estimatorEnabled = false;
            aquaLocLogger.enableLogging = false;
            appliedProfileName = "Disabled";
            profileDescription = "AquaLoc disabled.";
            Debug.Log("[MIMISK] Localization profile disabled.");
            return;
        }

        ApplyCommonAquaLocSettings();

        switch (activeProfile)
        {
            case LocalizationProfile.AquaLocRawM10Baseline:
                ApplyRawM10Baseline();
                break;

            case LocalizationProfile.AquaLocLowCostSmoothM10:
                ApplyLowCostSmoothM10();
                break;

            case LocalizationProfile.AquaLocPaperEquivalentGPS:
                ApplyPaperEquivalentGPS();
                break;

            case LocalizationProfile.AquaLocRtkHighAccuracy:
                ApplyRtkHighAccuracy();
                break;
        }

        aquaLoc.estimatorEnabled = true;
        aquaLoc.resetOnStart = true;
        aquaLocLogger.estimator = aquaLoc;
        aquaLocLogger.enableLogging = true;
        aquaLocLogger.logHz = 50.0f;

        Debug.Log("[MIMISK] Applied localization profile: " + appliedProfileName);
    }

    [ContextMenu("Apply Raw M10 Baseline")]
    public void ApplyRawM10BaselineFromMenu()
    {
        activeProfile = LocalizationProfile.AquaLocRawM10Baseline;
        ApplyActiveProfile();
    }

    [ContextMenu("Apply Low-Cost Smooth M10")]
    public void ApplyLowCostSmoothM10FromMenu()
    {
        activeProfile = LocalizationProfile.AquaLocLowCostSmoothM10;
        ApplyActiveProfile();
    }

    [ContextMenu("Apply Paper-Equivalent GPS")]
    public void ApplyPaperEquivalentGPSFromMenu()
    {
        activeProfile = LocalizationProfile.AquaLocPaperEquivalentGPS;
        ApplyActiveProfile();
    }

    [ContextMenu("Apply RTK High Accuracy")]
    public void ApplyRtkHighAccuracyFromMenu()
    {
        activeProfile = LocalizationProfile.AquaLocRtkHighAccuracy;
        ApplyActiveProfile();
    }

    private void ApplyCommonAquaLocSettings()
    {
        aquaLoc.imu = GetComponentInChildren<MIMISKDroneImuDevice>();
        aquaLoc.barometer = GetComponentInChildren<MIMISKDroneBarometerDevice>();
        aquaLoc.magnetometer = GetComponentInChildren<MIMISKDroneMagnetometerDevice>();
        aquaLoc.gnss = gnss;
        aquaLoc.rangefinder = GetComponentInChildren<MIMISKDroneRangefinderDevice>();
        aquaLoc.surfaceBuoyancy = GetComponent<MIMISKDroneSurfaceBuoyancy>();
        aquaLoc.batteryPower = GetComponentInChildren<MIMISKDroneBatteryPowerDevice>();

        aquaLoc.initializeFromUnityPose = true;
        aquaLoc.initializationDelaySeconds = 0.25f;
        aquaLoc.useGnssLocalWorldAnchor = true;
        aquaLoc.calibrateGnssOriginAtStartup = true;
        aquaLoc.requireGnssOriginReadyForFusion = true;
        aquaLoc.gnssOriginCalibrationSeconds = 2.0f;
        aquaLoc.freezeOutputDuringGnssOriginCalibration = true;

        aquaLoc.landingApproachRangeM = 3.0f;
        aquaLoc.waterContactMinBuoyancyPoints = 1;
        aquaLoc.surfaceFloatMinBuoyancyPoints = 3;

        aquaLoc.gyroNoiseQ = 1.0e-6f;
        aquaLoc.gyroNoiseR = 2.5e-5f;
        aquaLoc.accelNoiseQ = 2.0e-4f;
        aquaLoc.accelNoiseR = 2.0e-3f;
        aquaLoc.magYawNoiseQ = 0.08f;
        aquaLoc.magYawNoiseR = 2.0f;

        aquaLoc.enableGnssPrefilter = true;
        aquaLoc.enableAltitudePrefilter = true;

        aquaLoc.barometerFilterQ = 0.01f;
        aquaLoc.rangefinderFilterQ = 0.006f;

        aquaLoc.rollPitchGyroAlpha = 0.985f;
        aquaLoc.yawCorrectionGain = 6.0f;
        aquaLoc.yawGyroSign = 1.0f;
        aquaLoc.pitchGyroSign = 1.0f;
        aquaLoc.rollGyroSign = 1.0f;
        aquaLoc.magYawOffsetDeg = 0.0f;
        aquaLoc.accelTiltGateMS2 = 2.5f;

        aquaLoc.magnetometerYawSigmaDeg = 12.0f;
        aquaLoc.reduceMagTrustDuringHighCurrent = true;
        aquaLoc.highCurrentThresholdA = 15.0f;
        aquaLoc.highCurrentMagTrustScale = 0.85f;

        aquaLoc.useMagneticFieldNormQuality = true;
        aquaLoc.magneticFieldNormTolerance = 0.20f;
        aquaLoc.minimumMagneticFieldTrust = 0.25f;

        aquaLoc.horizontalAccelPredictionScale = 0.12f;
        aquaLoc.verticalAccelPredictionScale = 0.05f;

        aquaLoc.qVerticalPosition = 0.006f;
        aquaLoc.qVerticalVelocity = 0.025f;

        aquaLoc.enableRobustGating = true;
        aquaLoc.softGate = 3.0f;
        aquaLoc.hardGate = 9.0f;

        aquaLoc.enableSurfaceConstraint = true;
        aquaLoc.waterSurfaceY = 0.0f;
        aquaLoc.surfaceFloatRootHeightM = 0.0f;
        aquaLoc.disableOpticalRangefinderWhenWet = true;
        aquaLoc.rangefinderMaxFusionRangeM = 6.0f;

        aquaLoc.barometerSigmaM = 0.35f;
        aquaLoc.rangefinderSigmaM = 0.12f;

        aquaLoc.enableOutputSmoothing = true;
        aquaLoc.outputYawResponseHz = 10.0f;

        aquaLoc.enableOutputLeadCompensation = true;
        aquaLoc.horizontalOutputLeadSeconds = 0.12f;
        aquaLoc.verticalOutputLeadSeconds = 0.02f;
        aquaLoc.maxOutputLeadCorrectionM = 0.35f;
    }

    private void ApplyRawM10Baseline()
    {
        ConfigureGnssM10(0.8f, 1.2f, 0.08f, 1.5f, 2.5f, 0.15f);

        aquaLoc.gnssHorizontalSigmaM = 1.5f;
        aquaLoc.gnssVerticalSigmaM = 2.5f;
        aquaLoc.gnssVelocitySigmaMS = 0.25f;

        aquaLoc.gnssPositionFilterQ = 0.02f;
        aquaLoc.gnssVelocityFilterQ = 0.02f;

        aquaLoc.qHorizontalPosition = 0.010f;
        aquaLoc.qHorizontalVelocity = 0.030f;

        aquaLoc.outputPositionResponseHz = 8.0f;
        aquaLoc.outputVelocityResponseHz = 10.0f;

        appliedProfileName = "AquaLoc Raw M10 Baseline";
        profileDescription = "Meter-class low-cost GNSS baseline with moderate smoothing.";
    }

    private void ApplyLowCostSmoothM10()
    {
        ConfigureGnssM10(0.8f, 1.2f, 0.08f, 1.5f, 2.5f, 0.15f);

        aquaLoc.gnssHorizontalSigmaM = 1.8f;
        aquaLoc.gnssVerticalSigmaM = 2.5f;
        aquaLoc.gnssVelocitySigmaMS = 0.25f;

        aquaLoc.gnssPositionFilterQ = 0.004f;
        aquaLoc.gnssVelocityFilterQ = 0.004f;

        aquaLoc.qHorizontalPosition = 0.004f;
        aquaLoc.qHorizontalVelocity = 0.016f;

        aquaLoc.outputPositionResponseHz = 4.5f;
        aquaLoc.outputVelocityResponseHz = 8.0f;

        appliedProfileName = "AquaLoc Low-Cost Smooth M10";
        profileDescription = "Realistic low-cost GNSS with stronger smoothing. Good for affordable GPS comparison.";
    }

    private void ApplyPaperEquivalentGPS()
    {
        // Matches the paper's GPS-class assumption more closely:
        // horizontal accuracy 0.50 m, vertical 1.00 m, velocity 0.10 m/s.
        ConfigureGnssM10(0.50f, 1.00f, 0.10f, 0.50f, 1.00f, 0.10f);

        aquaLoc.gnssHorizontalSigmaM = 0.65f;
        aquaLoc.gnssVerticalSigmaM = 1.10f;
        aquaLoc.gnssVelocitySigmaMS = 0.12f;

        aquaLoc.gnssPositionFilterQ = 0.006f;
        aquaLoc.gnssVelocityFilterQ = 0.006f;

        aquaLoc.qHorizontalPosition = 0.006f;
        aquaLoc.qHorizontalVelocity = 0.024f;

        aquaLoc.outputPositionResponseHz = 8.5f;
        aquaLoc.outputVelocityResponseHz = 11.0f;

        aquaLoc.horizontalOutputLeadSeconds = 0.14f;
        aquaLoc.verticalOutputLeadSeconds = 0.02f;
        aquaLoc.maxOutputLeadCorrectionM = 0.35f;

        appliedProfileName = "AquaLoc Paper-Equivalent GPS";
        profileDescription = "Low-cost GPS profile matched to the paper's stated GPS accuracy values.";
    }

    private void ApplyRtkHighAccuracy()
    {
        if (gnss != null)
        {
            gnss.gnssModel = MIMISKDroneGnssDevice.GnssModel.ZedF9pRtkFixed;
            gnss.ConfigureModelDefaults();

            gnss.horizontalPositionNoiseM = 0.03f;
            gnss.verticalPositionNoiseM = 0.06f;
            gnss.velocityNoiseMS = 0.015f;

            gnss.horizontalAccuracyM = 0.05f;
            gnss.verticalAccuracyM = 0.10f;
            gnss.velocityAccuracyMS = 0.03f;
            gnss.sampleRateHz = 10.0f;
        }

        aquaLoc.gnssHorizontalSigmaM = 0.08f;
        aquaLoc.gnssVerticalSigmaM = 0.15f;
        aquaLoc.gnssVelocitySigmaMS = 0.04f;

        aquaLoc.gnssPositionFilterQ = 0.002f;
        aquaLoc.gnssVelocityFilterQ = 0.002f;

        aquaLoc.qHorizontalPosition = 0.003f;
        aquaLoc.qHorizontalVelocity = 0.012f;

        aquaLoc.outputPositionResponseHz = 10.0f;
        aquaLoc.outputVelocityResponseHz = 12.0f;

        appliedProfileName = "AquaLoc RTK High Accuracy";
        profileDescription = "ZED-F9P RTK-like profile for final high-accuracy autonomous demo.";
    }

    private void ConfigureGnssM10(
        float horizontalNoise,
        float verticalNoise,
        float velocityNoise,
        float horizontalAccuracy,
        float verticalAccuracy,
        float velocityAccuracy)
    {
        if (gnss == null)
        {
            return;
        }

        gnss.gnssModel = MIMISKDroneGnssDevice.GnssModel.UBloxM10;
        gnss.ConfigureModelDefaults();

        gnss.horizontalPositionNoiseM = horizontalNoise;
        gnss.verticalPositionNoiseM = verticalNoise;
        gnss.velocityNoiseMS = velocityNoise;

        gnss.horizontalAccuracyM = horizontalAccuracy;
        gnss.verticalAccuracyM = verticalAccuracy;
        gnss.velocityAccuracyMS = velocityAccuracy;

        gnss.sampleRateHz = 10.0f;
        gnss.enableNoise = true;
        gnss.enableDropout = false;
    }
}
