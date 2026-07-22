using UnityEngine;

public class MIMISKDroneBarometerDevice : MonoBehaviour
{
    public enum BarometerModel
    {
        MS5611,
        BMP388
    }

    [Header("Device")]
    public BarometerModel barometerModel = BarometerModel.MS5611;
    public string deviceName = "MS5611";
    public Transform sensorFrame;
    public Rigidbody droneRigidbody;

    [Header("Sampling")]
    public float sampleRateHz = 50.0f;
    public bool enableNoise = true;
    public bool enableBias = true;

    [Header("Reference")]
    public bool setHomeOnStart = true;
    public float homeWorldY;
    public float referencePressurePa = 101325.0f;
    public float referenceTemperatureC = 20.0f;

    [Header("Noise / Bias")]
    public float pressureNoisePa = 1.5f;
    public float altitudeNoiseM = 0.04f;
    public float temperatureNoiseC = 0.05f;

    public float pressureBiasPa = 0.0f;
    public float pressureBiasRandomWalkPaPerSqrtS = 0.02f;

    [Header("Outputs")]
    public bool hasNewSample;
    public float lastSampleTime;

    public float pressurePa;
    public float altitudeM;
    public float relativeAltitudeM;
    public float verticalSpeedMS;
    public float temperatureC;

    [Header("Truth Debug")]
    public float trueRelativeAltitudeM;
    public float truePressurePa;
    public float trueVerticalSpeedMS;

    private float sampleTimer;

    private void Reset()
    {
        AutoFindReferences();
        ConfigureModelDefaults();
    }

    private void Awake()
    {
        AutoFindReferences();
        ConfigureModelDefaults();
    }

    private void Start()
    {
        if (setHomeOnStart && sensorFrame != null)
        {
            homeWorldY = sensorFrame.position.y;
        }

        Sample(0.0f);
    }

    private void FixedUpdate()
    {
        hasNewSample = false;

        sampleTimer += Time.fixedDeltaTime;

        float period = 1.0f / Mathf.Max(sampleRateHz, 1.0f);

        if (sampleTimer >= period)
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
        if (barometerModel == BarometerModel.MS5611)
        {
            deviceName = "MS5611";
            pressureNoisePa = 1.5f;
            altitudeNoiseM = 0.04f;
        }
        else
        {
            deviceName = "BMP388";
            pressureNoisePa = 2.0f;
            altitudeNoiseM = 0.05f;
        }
    }

    private void Sample(float dt)
    {
        if (sensorFrame == null)
        {
            return;
        }

        trueRelativeAltitudeM = sensorFrame.position.y - homeWorldY;

        truePressurePa = AltitudeToPressurePa(trueRelativeAltitudeM);

        trueVerticalSpeedMS = droneRigidbody != null
            ? droneRigidbody.linearVelocity.y
            : 0.0f;

        if (enableBias && dt > 0.0f)
        {
            pressureBiasPa += Gaussian(0.0f, pressureBiasRandomWalkPaPerSqrtS * Mathf.Sqrt(dt));
        }

        float measuredPressure = truePressurePa;

        if (enableBias)
        {
            measuredPressure += pressureBiasPa;
        }

        if (enableNoise)
        {
            measuredPressure += Gaussian(0.0f, pressureNoisePa);
        }

        pressurePa = measuredPressure;

        altitudeM = PressureToAltitudeM(pressurePa);

        if (enableNoise)
        {
            altitudeM += Gaussian(0.0f, altitudeNoiseM);
        }

        relativeAltitudeM = altitudeM;
        verticalSpeedMS = trueVerticalSpeedMS;

        temperatureC = referenceTemperatureC - 0.0065f * trueRelativeAltitudeM;

        if (enableNoise)
        {
            temperatureC += Gaussian(0.0f, temperatureNoiseC);
        }

        lastSampleTime = Time.time;
        hasNewSample = true;
    }

    private float AltitudeToPressurePa(float altitude)
    {
        float ratio = 1.0f - altitude / 44330.0f;
        ratio = Mathf.Max(0.01f, ratio);

        return referencePressurePa * Mathf.Pow(ratio, 5.255f);
    }

    private float PressureToAltitudeM(float pressure)
    {
        pressure = Mathf.Max(1.0f, pressure);

        return 44330.0f * (1.0f - Mathf.Pow(pressure / referencePressurePa, 1.0f / 5.255f));
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
