using UnityEngine;

public class MIMISKDroneMagnetometerDevice : MonoBehaviour
{
    public enum MagnetometerModel
    {
        IST8310,
        LIS3MDL
    }

    [Header("Device")]
    public MagnetometerModel magnetometerModel = MagnetometerModel.IST8310;
    public string deviceName = "IST8310";
    public Transform sensorFrame;

    [Header("Sampling")]
    public float sampleRateHz = 50.0f;
    public bool enableNoise = true;
    public bool enableBias = true;
    public bool enableSoftIron = true;

    [Header("Earth Magnetic Field Model")]
    [Tooltip("Approximate total magnetic field magnitude in microtesla.")]
    public float fieldMagnitudeUT = 48.0f;

    [Tooltip("Magnetic inclination. Positive means field points downward into Earth.")]
    public float inclinationDeg = 60.0f;

    [Tooltip("Magnetic declination: magnetic north east of true north.")]
    public float declinationDeg = 4.0f;

    [Header("Noise / Distortion")]
    public float noiseStdUT = 0.12f;
    public Vector3 hardIronBiasUT = new Vector3(0.8f, -0.4f, 0.3f);
    public Vector3 softIronScale = new Vector3(1.03f, 0.97f, 1.01f);
    public float biasRandomWalkUTPerSqrtS = 0.002f;

    [Header("Limits")]
    public float saturationUT = 1600.0f;

    [Header("Outputs")]
    public bool hasNewSample;
    public float lastSampleTime;

    public Vector3 magneticFieldBodyUT;
    public float magneticHeadingDeg;
    public float trueHeadingDeg;

    [Header("Truth Debug")]
    public Vector3 earthFieldWorldUT;
    public Vector3 trueMagneticFieldBodyUT;

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
        ComputeEarthFieldWorld();
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
    }

    [ContextMenu("Configure Model Defaults")]
    public void ConfigureModelDefaults()
    {
        if (magnetometerModel == MagnetometerModel.IST8310)
        {
            deviceName = "IST8310";
            noiseStdUT = 0.12f;
        }
        else
        {
            deviceName = "LIS3MDL";
            noiseStdUT = 0.18f;
        }
    }

    [ContextMenu("Compute Earth Field")]
    public void ComputeEarthFieldWorld()
    {
        float inc = inclinationDeg * Mathf.Deg2Rad;
        float dec = declinationDeg * Mathf.Deg2Rad;

        float horizontal = fieldMagnitudeUT * Mathf.Cos(inc);
        float down = fieldMagnitudeUT * Mathf.Sin(inc);

        float north = horizontal * Mathf.Cos(dec);
        float east = horizontal * Mathf.Sin(dec);

        // Unity convention used in this project:
        // X = east/right, Y = up, Z = north/forward.
        earthFieldWorldUT = new Vector3(east, -down, north);
    }

    private void Sample(float dt)
    {
        if (sensorFrame == null)
        {
            return;
        }

        ComputeEarthFieldWorld();

        trueMagneticFieldBodyUT =
            sensorFrame.InverseTransformDirection(earthFieldWorldUT);

        Vector3 measured = trueMagneticFieldBodyUT;

        if (enableSoftIron)
        {
            measured = new Vector3(
                measured.x * softIronScale.x,
                measured.y * softIronScale.y,
                measured.z * softIronScale.z
            );
        }

        if (enableBias)
        {
            hardIronBiasUT += GaussianVector(biasRandomWalkUTPerSqrtS * Mathf.Sqrt(Mathf.Max(dt, 0.0001f)));
            measured += hardIronBiasUT;
        }

        if (enableNoise)
        {
            measured += GaussianVector(noiseStdUT);
        }

        magneticFieldBodyUT = Saturate(measured, saturationUT);

        trueHeadingDeg = GetTrueHeadingDeg();
        magneticHeadingDeg = Wrap360(trueHeadingDeg - declinationDeg);

        if (enableNoise)
        {
            magneticHeadingDeg = Wrap360(magneticHeadingDeg + Gaussian(0.0f, 0.8f));
        }

        lastSampleTime = Time.time;
        hasNewSample = true;
    }

    private float GetTrueHeadingDeg()
    {
        Vector3 forward = sensorFrame.forward;
        forward.y = 0.0f;

        if (forward.sqrMagnitude < 0.0001f)
        {
            return 0.0f;
        }

        forward.Normalize();

        float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        return Wrap360(yaw);
    }

    private Vector3 Saturate(Vector3 value, float limit)
    {
        return new Vector3(
            Mathf.Clamp(value.x, -limit, limit),
            Mathf.Clamp(value.y, -limit, limit),
            Mathf.Clamp(value.z, -limit, limit)
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

    private float Wrap360(float angle)
    {
        while (angle >= 360.0f) angle -= 360.0f;
        while (angle < 0.0f) angle += 360.0f;
        return angle;
    }
}
