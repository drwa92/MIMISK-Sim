using UnityEngine;

public class MIMISKDroneGnssDevice : MonoBehaviour
{
    public enum GnssModel
    {
        UBloxM10,
        ZedF9pRtkFloat,
        ZedF9pRtkFixed
    }

    public enum FixType
    {
        NoFix = 0,
        Fix2D = 2,
        Fix3D = 3,
        RtkFloat = 4,
        RtkFixed = 5
    }

    [Header("Device")]
    public GnssModel gnssModel = GnssModel.UBloxM10;
    public string deviceName = "u-blox M10 GNSS";
    public Transform antennaFrame;
    public Rigidbody droneRigidbody;

    [Header("Sampling")]
    public float sampleRateHz = 10.0f;
    public bool enableNoise = true;
    public bool enableDropout = false;
    [Range(0.0f, 1.0f)] public float dropoutProbability = 0.0f;

    [Header("Home Geodetic Reference")]
    public bool setHomeOnStart = true;
    public Vector3 homeWorldPosition;
    public double homeLatitudeDeg = 45.800000;
    public double homeLongitudeDeg = 15.970000;
    public double homeAltitudeM = 0.0;

    [Header("Noise / Accuracy")]
    public float horizontalPositionNoiseM = 0.8f;
    public float verticalPositionNoiseM = 1.2f;
    public float velocityNoiseMS = 0.08f;

    public float horizontalAccuracyM = 1.5f;
    public float verticalAccuracyM = 2.5f;
    public float velocityAccuracyMS = 0.15f;

    [Header("GNSS Quality")]
    public FixType fixType = FixType.Fix3D;
    public int satelliteCount = 14;
    public float hdop = 0.8f;
    public float vdop = 1.2f;

    [Header("Outputs")]
    public bool hasNewSample;
    public float lastSampleTime;
    public float gpsAgeSeconds;

    public Vector3 localPositionENU_M;
    public Vector3 velocityENU_MS;

    public double latitudeDeg;
    public double longitudeDeg;
    public double altitudeM;

    public float groundSpeedMS;
    public float courseOverGroundDeg;

    [Header("Truth Debug")]
    public Vector3 trueLocalPositionENU_M;
    public Vector3 trueVelocityENU_MS;
    public double trueLatitudeDeg;
    public double trueLongitudeDeg;
    public double trueAltitudeM;

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
        if (setHomeOnStart && antennaFrame != null)
        {
            homeWorldPosition = antennaFrame.position;
        }

        Sample(0.0f);
    }

    private void FixedUpdate()
    {
        hasNewSample = false;

        sampleTimer += Time.fixedDeltaTime;

        float period = 1.0f / Mathf.Max(sampleRateHz, 0.5f);

        if (sampleTimer >= period)
        {
            sampleTimer -= period;
            Sample(period);
        }

        gpsAgeSeconds += Time.fixedDeltaTime;
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (antennaFrame == null)
        {
            antennaFrame = transform;
        }

        if (droneRigidbody == null)
        {
            droneRigidbody = GetComponentInParent<Rigidbody>();
        }
    }

    [ContextMenu("Configure Model Defaults")]
    public void ConfigureModelDefaults()
    {
        switch (gnssModel)
        {
            case GnssModel.UBloxM10:
                deviceName = "u-blox M10 GNSS";
                sampleRateHz = 10.0f;
                horizontalPositionNoiseM = 0.8f;
                verticalPositionNoiseM = 1.2f;
                velocityNoiseMS = 0.08f;
                horizontalAccuracyM = 1.5f;
                verticalAccuracyM = 2.5f;
                velocityAccuracyMS = 0.15f;
                fixType = FixType.Fix3D;
                satelliteCount = 14;
                hdop = 0.8f;
                vdop = 1.2f;
                break;

            case GnssModel.ZedF9pRtkFloat:
                deviceName = "u-blox ZED-F9P RTK Float";
                sampleRateHz = 10.0f;
                horizontalPositionNoiseM = 0.15f;
                verticalPositionNoiseM = 0.25f;
                velocityNoiseMS = 0.03f;
                horizontalAccuracyM = 0.25f;
                verticalAccuracyM = 0.40f;
                velocityAccuracyMS = 0.05f;
                fixType = FixType.RtkFloat;
                satelliteCount = 20;
                hdop = 0.45f;
                vdop = 0.70f;
                break;

            case GnssModel.ZedF9pRtkFixed:
                deviceName = "u-blox ZED-F9P RTK Fixed";
                sampleRateHz = 10.0f;
                horizontalPositionNoiseM = 0.03f;
                verticalPositionNoiseM = 0.06f;
                velocityNoiseMS = 0.015f;
                horizontalAccuracyM = 0.05f;
                verticalAccuracyM = 0.10f;
                velocityAccuracyMS = 0.03f;
                fixType = FixType.RtkFixed;
                satelliteCount = 22;
                hdop = 0.35f;
                vdop = 0.55f;
                break;
        }
    }

    private void Sample(float dt)
    {
        if (antennaFrame == null)
        {
            return;
        }

        if (enableDropout && Random.value < dropoutProbability)
        {
            fixType = FixType.NoFix;
            hasNewSample = false;
            return;
        }

        ConfigureFixQualityIfNeeded();

        trueLocalPositionENU_M = antennaFrame.position - homeWorldPosition;
        trueVelocityENU_MS = droneRigidbody != null ? droneRigidbody.linearVelocity : Vector3.zero;

        Vector3 measuredPosition = trueLocalPositionENU_M;
        Vector3 measuredVelocity = trueVelocityENU_MS;

        if (enableNoise)
        {
            measuredPosition.x += Gaussian(0.0f, horizontalPositionNoiseM);
            measuredPosition.z += Gaussian(0.0f, horizontalPositionNoiseM);
            measuredPosition.y += Gaussian(0.0f, verticalPositionNoiseM);

            measuredVelocity += GaussianVector(velocityNoiseMS);
        }

        localPositionENU_M = measuredPosition;
        velocityENU_MS = measuredVelocity;

        LocalEnuToGeodetic(trueLocalPositionENU_M, out trueLatitudeDeg, out trueLongitudeDeg, out trueAltitudeM);
        LocalEnuToGeodetic(localPositionENU_M, out latitudeDeg, out longitudeDeg, out altitudeM);

        Vector3 horizontalVelocity = new Vector3(velocityENU_MS.x, 0.0f, velocityENU_MS.z);
        groundSpeedMS = horizontalVelocity.magnitude;

        if (horizontalVelocity.sqrMagnitude > 0.0001f)
        {
            courseOverGroundDeg =
                Mathf.Atan2(horizontalVelocity.x, horizontalVelocity.z) * Mathf.Rad2Deg;

            if (courseOverGroundDeg < 0.0f)
            {
                courseOverGroundDeg += 360.0f;
            }
        }

        lastSampleTime = Time.time;
        gpsAgeSeconds = 0.0f;
        hasNewSample = true;
    }

    private void ConfigureFixQualityIfNeeded()
    {
        if (fixType == FixType.NoFix)
        {
            switch (gnssModel)
            {
                case GnssModel.UBloxM10:
                    fixType = FixType.Fix3D;
                    break;

                case GnssModel.ZedF9pRtkFloat:
                    fixType = FixType.RtkFloat;
                    break;

                case GnssModel.ZedF9pRtkFixed:
                    fixType = FixType.RtkFixed;
                    break;
            }
        }
    }

    private void LocalEnuToGeodetic(Vector3 enu, out double lat, out double lon, out double alt)
    {
        double metersPerDegLat = 111111.0;
        double metersPerDegLon = 111111.0 * System.Math.Cos(homeLatitudeDeg * System.Math.PI / 180.0);

        lat = homeLatitudeDeg + enu.z / metersPerDegLat;
        lon = homeLongitudeDeg + enu.x / metersPerDegLon;
        alt = homeAltitudeM + enu.y;
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
