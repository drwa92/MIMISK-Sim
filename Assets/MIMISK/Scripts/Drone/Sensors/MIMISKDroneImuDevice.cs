using UnityEngine;

public class MIMISKDroneImuDevice : MonoBehaviour
{
    public enum ImuModel
    {
        ICM42688P,
        BMI088
    }

    [Header("Device")]
    public ImuModel imuModel = ImuModel.ICM42688P;
    public string deviceName = "ICM-42688-P";
    public Transform sensorFrame;
    public Rigidbody droneRigidbody;

    [Header("Sampling")]
    public float sampleRateHz = 200.0f;
    public bool enableNoise = true;
    public bool enableBias = true;
    public bool enableQuantization = false;

    [Header("Noise Density")]
    [Tooltip("Gyro noise density in rad/s/sqrt(Hz). ICM-42688-P low-noise default approx 2.8 mdps/sqrt(Hz).")]
    public float gyroNoiseDensityRadSqrtHz = 4.8869e-5f;

    [Tooltip("Accelerometer noise density in m/s^2/sqrt(Hz). ICM-42688-P default approx 70 ug/sqrt(Hz).")]
    public float accelNoiseDensityMS2SqrtHz = 0.000686f;

    [Header("Bias")]
    public Vector3 gyroBiasRadS = Vector3.zero;
    public Vector3 accelBiasMS2 = Vector3.zero;

    public float gyroBiasRandomWalkRadSPerSqrtS = 2.0e-5f;
    public float accelBiasRandomWalkMS2PerSqrtS = 2.0e-4f;

    [Header("Limits")]
    public float gyroSaturationRadS = 34.9f;      // about 2000 deg/s
    public float accelSaturationMS2 = 156.9f;     // about 16 g

    [Header("Temperature")]
    public float nominalTemperatureC = 35.0f;
    public float temperatureC;
    public float temperatureDriftCPerSecond = 0.005f;

    [Header("Outputs")]
    public bool hasNewSample;
    public float lastSampleTime;
    public Vector3 accelerometerBodyMS2;
    public Vector3 gyroscopeBodyRadS;

    [Header("Truth Debug")]
    public Vector3 trueSpecificForceBodyMS2;
    public Vector3 trueAngularRateBodyRadS;
    public Vector3 trueAccelerationWorldMS2;
    public Vector3 trueVelocityWorldMS;

    private float sampleTimer;
    private Vector3 previousVelocityWorld;
    private bool hasPreviousVelocity;

    private void Reset()
    {
        AutoFindReferences();
        ConfigureModelDefaults();
    }

    private void Awake()
    {
        AutoFindReferences();
        ConfigureModelDefaults();
        previousVelocityWorld = droneRigidbody != null ? droneRigidbody.linearVelocity : Vector3.zero;
        hasPreviousVelocity = true;
        temperatureC = nominalTemperatureC;
    }

    private void FixedUpdate()
    {
        hasNewSample = false;

        if (droneRigidbody == null)
        {
            return;
        }

        sampleTimer += Time.fixedDeltaTime;

        float period = 1.0f / Mathf.Max(sampleRateHz, 1.0f);

        while (sampleTimer >= period)
        {
            sampleTimer -= period;
            Sample(period);
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (sensorFrame == null)
        {
            sensorFrame = transform;
        }

        if (droneRigidbody == null)
        {
            droneRigidbody = GetComponentInParent<Rigidbody>();
        }
    }

    [ContextMenu("Configure Model Defaults")]
    public void ConfigureModelDefaults()
    {
        if (imuModel == ImuModel.ICM42688P)
        {
            deviceName = "ICM-42688-P";
            gyroNoiseDensityRadSqrtHz = 4.8869e-5f;
            accelNoiseDensityMS2SqrtHz = 0.000686f;
        }
        else if (imuModel == ImuModel.BMI088)
        {
            deviceName = "BMI088";
            gyroNoiseDensityRadSqrtHz = 1.8e-4f;
            accelNoiseDensityMS2SqrtHz = 0.0012f;
        }
    }

    private void Sample(float dt)
    {
        Vector3 velocity = droneRigidbody.linearVelocity;

        if (hasPreviousVelocity)
        {
            trueAccelerationWorldMS2 =
                (velocity - previousVelocityWorld) /
                Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        }
        else
        {
            trueAccelerationWorldMS2 = Vector3.zero;
            hasPreviousVelocity = true;
        }

        previousVelocityWorld = velocity;
        trueVelocityWorldMS = velocity;

        Vector3 specificForceWorld = trueAccelerationWorldMS2 - Physics.gravity;

        trueSpecificForceBodyMS2 =
            sensorFrame.InverseTransformDirection(specificForceWorld);

        trueAngularRateBodyRadS =
            sensorFrame.InverseTransformDirection(droneRigidbody.angularVelocity);

        UpdateBias(dt);
        UpdateTemperature(dt);

        Vector3 accel = trueSpecificForceBodyMS2;
        Vector3 gyro = trueAngularRateBodyRadS;

        if (enableBias)
        {
            accel += accelBiasMS2;
            gyro += gyroBiasRadS;
        }

        if (enableNoise)
        {
            float accelStd = accelNoiseDensityMS2SqrtHz * Mathf.Sqrt(sampleRateHz * 0.5f);
            float gyroStd = gyroNoiseDensityRadSqrtHz * Mathf.Sqrt(sampleRateHz * 0.5f);

            accel += GaussianVector(accelStd);
            gyro += GaussianVector(gyroStd);
        }

        accel = Saturate(accel, accelSaturationMS2);
        gyro = Saturate(gyro, gyroSaturationRadS);

        if (enableQuantization)
        {
            accel = Quantize(accel, 0.00025f);
            gyro = Quantize(gyro, 0.00001f);
        }

        accelerometerBodyMS2 = accel;
        gyroscopeBodyRadS = gyro;

        lastSampleTime = Time.time;
        hasNewSample = true;
    }

    private void UpdateBias(float dt)
    {
        if (!enableBias)
        {
            return;
        }

        gyroBiasRadS += GaussianVector(gyroBiasRandomWalkRadSPerSqrtS * Mathf.Sqrt(dt));
        accelBiasMS2 += GaussianVector(accelBiasRandomWalkMS2PerSqrtS * Mathf.Sqrt(dt));
    }

    private void UpdateTemperature(float dt)
    {
        temperatureC += temperatureDriftCPerSecond * dt;
    }

    private Vector3 Saturate(Vector3 value, float limit)
    {
        return new Vector3(
            Mathf.Clamp(value.x, -limit, limit),
            Mathf.Clamp(value.y, -limit, limit),
            Mathf.Clamp(value.z, -limit, limit)
        );
    }

    private Vector3 Quantize(Vector3 value, float step)
    {
        return new Vector3(
            Mathf.Round(value.x / step) * step,
            Mathf.Round(value.y / step) * step,
            Mathf.Round(value.z / step) * step
        );
    }

    private Vector3 GaussianVector(float std)
    {
        return new Vector3(
            Gaussian(0.0f, std),
            Gaussian(0.0f, std),
            Gaussian(0.0f, std)
        );
    }

    private float Gaussian(float mean, float std)
    {
        float u1 = Mathf.Clamp(Random.value, 1e-6f, 1.0f);
        float u2 = Mathf.Clamp(Random.value, 1e-6f, 1.0f);

        float n =
            Mathf.Sqrt(-2.0f * Mathf.Log(u1)) *
            Mathf.Sin(2.0f * Mathf.PI * u2);

        return mean + std * n;
    }
}
