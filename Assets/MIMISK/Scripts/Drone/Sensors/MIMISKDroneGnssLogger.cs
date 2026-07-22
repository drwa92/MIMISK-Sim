using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class MIMISKDroneGnssLogger : MonoBehaviour
{
    public MIMISKDroneGnssDevice gnss;

    [Header("Logging")]
    public bool enableLogging = true;
    public float logHz = 10.0f;
    public bool flushEveryLine = false;

    [Header("Runtime")]
    public string currentLogPath;
    public int linesWritten;
    public float actualLogHz;

    private StreamWriter writer;
    private float logTimer;
    private float lastLogTime;

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private void Awake()
    {
        if (gnss == null)
        {
            gnss = GetComponent<MIMISKDroneGnssDevice>();
        }
    }

    private void OnEnable()
    {
        if (enableLogging)
        {
            OpenLog();
        }
    }

    private void FixedUpdate()
    {
        if (!enableLogging || gnss == null)
        {
            return;
        }

        if (writer == null)
        {
            OpenLog();
        }

        logTimer += Time.fixedDeltaTime;

        float period = 1.0f / Mathf.Max(1.0f, logHz);

        if (logTimer >= period)
        {
            logTimer -= period;
            WriteLine();

            if (lastLogTime > 0.0f)
            {
                actualLogHz = 1.0f / Mathf.Max(0.0001f, Time.time - lastLogTime);
            }

            lastLogTime = Time.time;
        }
    }

    private void OnDisable()
    {
        CloseLog();
    }

    private void OnApplicationQuit()
    {
        CloseLog();
    }

    private void OpenLog()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string logDir = Path.Combine(projectRoot, "Logs", "MIMISKDrone");

        Directory.CreateDirectory(logDir);

        string fileName = "drone_gnss_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        currentLogPath = Path.Combine(logDir, fileName);

        writer = new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,device,fix_type,satellites,hdop,vdop," +
            "lat_deg,lon_deg,alt_m," +
            "local_e,local_u,local_n," +
            "vel_e,vel_u,vel_n,ground_speed,cog_deg," +
            "true_lat_deg,true_lon_deg,true_alt_m," +
            "true_e,true_u,true_n," +
            "true_vel_e,true_vel_u,true_vel_n," +
            "h_acc_m,v_acc_m,vel_acc_ms,gps_age"
        );

        writer.Flush();

        Debug.Log("[MIMISKDroneGnssLogger] Logging to: " + currentLogPath);
    }

    private void WriteLine()
    {
        string line = string.Join(",",
            F(Time.time),
            gnss.deviceName,
            ((int)gnss.fixType).ToString(Culture),
            gnss.satelliteCount.ToString(Culture),
            F(gnss.hdop),
            F(gnss.vdop),

            D(gnss.latitudeDeg),
            D(gnss.longitudeDeg),
            D(gnss.altitudeM),

            F(gnss.localPositionENU_M.x),
            F(gnss.localPositionENU_M.y),
            F(gnss.localPositionENU_M.z),

            F(gnss.velocityENU_MS.x),
            F(gnss.velocityENU_MS.y),
            F(gnss.velocityENU_MS.z),
            F(gnss.groundSpeedMS),
            F(gnss.courseOverGroundDeg),

            D(gnss.trueLatitudeDeg),
            D(gnss.trueLongitudeDeg),
            D(gnss.trueAltitudeM),

            F(gnss.trueLocalPositionENU_M.x),
            F(gnss.trueLocalPositionENU_M.y),
            F(gnss.trueLocalPositionENU_M.z),

            F(gnss.trueVelocityENU_MS.x),
            F(gnss.trueVelocityENU_MS.y),
            F(gnss.trueVelocityENU_MS.z),

            F(gnss.horizontalAccuracyM),
            F(gnss.verticalAccuracyM),
            F(gnss.velocityAccuracyMS),
            F(gnss.gpsAgeSeconds)
        );

        writer.WriteLine(line);
        linesWritten++;

        if (flushEveryLine)
        {
            writer.Flush();
        }
    }

    private string F(float value)
    {
        return value.ToString("G9", Culture);
    }

    private string D(double value)
    {
        return value.ToString("G17", Culture);
    }

    private void CloseLog()
    {
        if (writer == null)
        {
            return;
        }

        writer.Flush();
        writer.Close();
        writer.Dispose();
        writer = null;

        Debug.Log("[MIMISKDroneGnssLogger] Closed log: " + currentLogPath);
    }
}
