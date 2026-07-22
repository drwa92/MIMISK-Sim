using UnityEngine;

public class MIMISKDroneFusedLocalization : MonoBehaviour
{
    [Header("Sensor References")]
    public MIMISKDroneImuDevice imu;
    public MIMISKDroneBarometerDevice barometer;
    public MIMISKDroneMagnetometerDevice magnetometer;
    public MIMISKDroneGnssDevice gnss;
    public MIMISKDroneRangefinderDevice rangefinder;

    [Header("Estimator Enable")]
    public bool estimatorEnabled = true;
    public bool resetOnStart = true;

    [Header("Fusion Gains")]
    public float accelTiltCorrectionGain = 2.5f;
    public float magnetometerYawGain = 2.0f;

    public float gnssPositionGain = 3.0f;
    public float gnssVelocityGain = 4.0f;

    public float barometerAltitudeGain = 4.0f;
    public float rangefinderAltitudeGain = 8.0f;
    public float rangefinderMaxFusionRangeM = 6.0f;

    [Header("Estimated State")]
    public bool estimatorReady;
    public Vector3 estimatedPositionWorld;
    public Vector3 estimatedVelocityWorld;
    public Quaternion estimatedRotationWorld;
    public Vector3 estimatedEulerDeg;
    public Vector3 estimatedBodyRatesRadS;
    public float estimatedYawDeg;
    public float estimatedAltitudeAboveHomeM;
    public float estimatedHeightAboveWaterM;

    [Header("Sensor Use Debug")]
    public bool usingGnss;
    public bool usingBarometer;
    public bool usingRangefinder;
    public bool usingMagnetometer;
    public bool usingImu;

    [Header("Innovation Debug")]
    public Vector3 gnssPositionInnovationM;
    public Vector3 gnssVelocityInnovationMS;
    public float barometerInnovationM;
    public float rangefinderInnovationM;
    public float yawInnovationDeg;

    [Header("Truth Debug")]
    public Vector3 truePositionWorld;
    public Vector3 trueVelocityWorld;
    public Vector3 trueEulerDeg;
    public float trueYawDeg;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        AutoFindSensors();
    }

    private void Start()
    {
        if (resetOnStart)
        {
            ResetEstimator();
        }
    }

    private void FixedUpdate()
    {
        if (!estimatorEnabled)
        {
            return;
        }

        if (!estimatorReady)
        {
            ResetEstimator();
            return;
        }

        UpdateTruthDebug();

        float dt = Time.fixedDeltaTime;

        PropagateWithImu(dt);
        CorrectWithGnss(dt);
        CorrectWithBarometer(dt);
        CorrectWithRangefinder(dt);
        CorrectWithMagnetometer(dt);

        UpdateOutputs();
    }

    [ContextMenu("Auto Find Sensors")]
    public void AutoFindSensors()
    {
        if (imu == null)
        {
            imu = GetComponentInChildren<MIMISKDroneImuDevice>();
        }

        if (barometer == null)
        {
            barometer = GetComponentInChildren<MIMISKDroneBarometerDevice>();
        }

        if (magnetometer == null)
        {
            magnetometer = GetComponentInChildren<MIMISKDroneMagnetometerDevice>();
        }

        if (gnss == null)
        {
            gnss = GetComponentInChildren<MIMISKDroneGnssDevice>();
        }

        if (rangefinder == null)
        {
            rangefinder = GetComponentInChildren<MIMISKDroneRangefinderDevice>();
        }
    }

    [ContextMenu("Reset Estimator")]
    public void ResetEstimator()
    {
        AutoFindSensors();

        estimatedPositionWorld = transform.position;

        if (gnss != null && gnss.fixType != MIMISKDroneGnssDevice.FixType.NoFix)
        {
            estimatedPositionWorld = gnss.homeWorldPosition + gnss.localPositionENU_M;
        }

        estimatedVelocityWorld = rb != null ? rb.linearVelocity : Vector3.zero;
        estimatedRotationWorld = transform.rotation;

        if (magnetometer != null)
        {
            estimatedYawDeg = Wrap360(magnetometer.magneticHeadingDeg + magnetometer.declinationDeg);
            Vector3 e = estimatedRotationWorld.eulerAngles;
            e.y = estimatedYawDeg;
            estimatedRotationWorld = Quaternion.Euler(e);
        }
        else
        {
            estimatedYawDeg = GetYawDeg(estimatedRotationWorld);
        }

        UpdateOutputs();

        estimatorReady = true;
    }

    private void PropagateWithImu(float dt)
    {
        usingImu = imu != null;

        if (!usingImu)
        {
            return;
        }

        Vector3 gyroBody = imu.gyroscopeBodyRadS;
        estimatedBodyRatesRadS = gyroBody;

        Quaternion deltaRotation = Quaternion.Euler(
            gyroBody.x * Mathf.Rad2Deg * dt,
            gyroBody.y * Mathf.Rad2Deg * dt,
            gyroBody.z * Mathf.Rad2Deg * dt
        );

        estimatedRotationWorld = estimatedRotationWorld * deltaRotation;

        Vector3 measuredUpBody = imu.accelerometerBodyMS2.normalized;

        if (measuredUpBody.sqrMagnitude > 0.5f)
        {
            Vector3 predictedUpBody =
                Quaternion.Inverse(estimatedRotationWorld) * Vector3.up;

            Vector3 errorBody = Vector3.Cross(predictedUpBody, measuredUpBody);

            Vector3 correctionDeg =
                errorBody * accelTiltCorrectionGain * Mathf.Rad2Deg * dt;

            estimatedRotationWorld =
                estimatedRotationWorld *
                Quaternion.Euler(correctionDeg.x, correctionDeg.y, correctionDeg.z);
        }

        Vector3 accelWorld =
            estimatedRotationWorld * imu.accelerometerBodyMS2 + Physics.gravity;

        estimatedVelocityWorld += accelWorld * dt;
        estimatedPositionWorld += estimatedVelocityWorld * dt;
    }

    private void CorrectWithGnss(float dt)
    {
        usingGnss =
            gnss != null &&
            gnss.fixType != MIMISKDroneGnssDevice.FixType.NoFix;

        if (!usingGnss)
        {
            return;
        }

        Vector3 gnssWorld = gnss.homeWorldPosition + gnss.localPositionENU_M;

        gnssPositionInnovationM = gnssWorld - estimatedPositionWorld;
        gnssVelocityInnovationMS = gnss.velocityENU_MS - estimatedVelocityWorld;

        float posAlpha = Alpha(gnssPositionGain, dt);
        float velAlpha = Alpha(gnssVelocityGain, dt);

        estimatedPositionWorld =
            Vector3.Lerp(estimatedPositionWorld, gnssWorld, posAlpha);

        estimatedVelocityWorld =
            Vector3.Lerp(estimatedVelocityWorld, gnss.velocityENU_MS, velAlpha);
    }

    private void CorrectWithBarometer(float dt)
    {
        usingBarometer = barometer != null;

        if (!usingBarometer)
        {
            return;
        }

        float baroWorldY = barometer.homeWorldY + barometer.relativeAltitudeM;
        barometerInnovationM = baroWorldY - estimatedPositionWorld.y;

        float alpha = Alpha(barometerAltitudeGain, dt);
        estimatedPositionWorld.y =
            Mathf.Lerp(estimatedPositionWorld.y, baroWorldY, alpha);

        estimatedVelocityWorld.y =
            Mathf.Lerp(estimatedVelocityWorld.y, barometer.verticalSpeedMS, alpha * 0.5f);
    }

    private void CorrectWithRangefinder(float dt)
    {
        usingRangefinder =
            rangefinder != null &&
            rangefinder.validReturn &&
            rangefinder.verticalDistanceM > 0.0f &&
            rangefinder.verticalDistanceM < rangefinderMaxFusionRangeM;

        if (!usingRangefinder)
        {
            return;
        }

        float sensorOffsetY = 0.0f;

        if (rangefinder.sensorFrame != null)
        {
            sensorOffsetY =
                transform.TransformVector(rangefinder.sensorFrame.localPosition).y;
        }

        float rootYFromRange =
            rangefinder.waterLevelY +
            rangefinder.verticalDistanceM -
            sensorOffsetY;

        rangefinderInnovationM = rootYFromRange - estimatedPositionWorld.y;

        float alpha = Alpha(rangefinderAltitudeGain, dt);

        estimatedPositionWorld.y =
            Mathf.Lerp(estimatedPositionWorld.y, rootYFromRange, alpha);

        estimatedHeightAboveWaterM =
            estimatedPositionWorld.y - rangefinder.waterLevelY;
    }

    private void CorrectWithMagnetometer(float dt)
    {
        usingMagnetometer = magnetometer != null;

        if (!usingMagnetometer)
        {
            return;
        }

        float trueHeadingFromMag =
            Wrap360(magnetometer.magneticHeadingDeg + magnetometer.declinationDeg);

        float currentYaw = GetYawDeg(estimatedRotationWorld);
        yawInnovationDeg = Mathf.DeltaAngle(currentYaw, trueHeadingFromMag);

        float alpha = Alpha(magnetometerYawGain, dt);

        estimatedRotationWorld =
            Quaternion.AngleAxis(yawInnovationDeg * alpha, Vector3.up) *
            estimatedRotationWorld;
    }

    private void UpdateOutputs()
    {
        estimatedEulerDeg = estimatedRotationWorld.eulerAngles;
        estimatedEulerDeg.x = NormalizeAngleDeg(estimatedEulerDeg.x);
        estimatedEulerDeg.y = Wrap360(estimatedEulerDeg.y);
        estimatedEulerDeg.z = NormalizeAngleDeg(estimatedEulerDeg.z);

        estimatedYawDeg = estimatedEulerDeg.y;

        if (barometer != null)
        {
            estimatedAltitudeAboveHomeM =
                estimatedPositionWorld.y - barometer.homeWorldY;
        }

        if (rangefinder != null)
        {
            estimatedHeightAboveWaterM =
                estimatedPositionWorld.y - rangefinder.waterLevelY;
        }
    }

    private void UpdateTruthDebug()
    {
        truePositionWorld = transform.position;
        trueVelocityWorld = rb != null ? rb.linearVelocity : Vector3.zero;
        trueEulerDeg = transform.eulerAngles;
        trueEulerDeg.x = NormalizeAngleDeg(trueEulerDeg.x);
        trueEulerDeg.y = Wrap360(trueEulerDeg.y);
        trueEulerDeg.z = NormalizeAngleDeg(trueEulerDeg.z);
        trueYawDeg = trueEulerDeg.y;
    }

    private float Alpha(float gain, float dt)
    {
        return 1.0f - Mathf.Exp(-Mathf.Max(0.001f, gain) * dt);
    }

    private float GetYawDeg(Quaternion rotation)
    {
        Vector3 forward = rotation * Vector3.forward;
        forward.y = 0.0f;

        if (forward.sqrMagnitude < 0.0001f)
        {
            return 0.0f;
        }

        forward.Normalize();

        return Wrap360(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg);
    }

    private float NormalizeAngleDeg(float angle)
    {
        while (angle > 180.0f) angle -= 360.0f;
        while (angle < -180.0f) angle += 360.0f;
        return angle;
    }

    private float Wrap360(float angle)
    {
        while (angle >= 360.0f) angle -= 360.0f;
        while (angle < 0.0f) angle += 360.0f;
        return angle;
    }
}
