using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class MIMISKDroneLocalizationLogger : MonoBehaviour
{
    public MIMISKDroneFusedLocalization localization;

    [Header("Logging")]
    public bool enableLogging = true;
    public float logHz = 50.0f;
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
        if (localization == null)
        {
            localization = GetComponent<MIMISKDroneFusedLocalization>();
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
        if (!enableLogging || localization == null)
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

        string fileName = "drone_fused_localization_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        currentLogPath = Path.Combine(logDir, fileName);

        writer = new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time," +
            "true_x,true_y,true_z,true_vx,true_vy,true_vz,true_pitch,true_yaw,true_roll," +
            "est_x,est_y,est_z,est_vx,est_vy,est_vz,est_pitch,est_yaw,est_roll," +
            "pos_err_x,pos_err_y,pos_err_z,pos_err_norm,yaw_err_deg," +
            "using_imu,using_gnss,using_baro,using_range,using_mag," +
            "gnss_innov_x,gnss_innov_y,gnss_innov_z," +
            "baro_innov,range_innov,yaw_innov"
        );

        writer.Flush();

        Debug.Log("[MIMISKDroneLocalizationLogger] Logging to: " + currentLogPath);
    }

    private void WriteLine()
    {
        Vector3 tp = localization.truePositionWorld;
        Vector3 tv = localization.trueVelocityWorld;
        Vector3 te = localization.trueEulerDeg;

        Vector3 ep = localization.estimatedPositionWorld;
        Vector3 ev = localization.estimatedVelocityWorld;
        Vector3 ee = localization.estimatedEulerDeg;

        Vector3 posErr = ep - tp;
        float yawErr = Mathf.DeltaAngle(te.y, ee.y);

        string line = string.Join(",",
            F(Time.time),

            F(tp.x), F(tp.y), F(tp.z),
            F(tv.x), F(tv.y), F(tv.z),
            F(te.x), F(te.y), F(te.z),

            F(ep.x), F(ep.y), F(ep.z),
            F(ev.x), F(ev.y), F(ev.z),
            F(ee.x), F(ee.y), F(ee.z),

            F(posErr.x), F(posErr.y), F(posErr.z), F(posErr.magnitude),
            F(yawErr),

            localization.usingImu ? "1" : "0",
            localization.usingGnss ? "1" : "0",
            localization.usingBarometer ? "1" : "0",
            localization.usingRangefinder ? "1" : "0",
            localization.usingMagnetometer ? "1" : "0",

            F(localization.gnssPositionInnovationM.x),
            F(localization.gnssPositionInnovationM.y),
            F(localization.gnssPositionInnovationM.z),

            F(localization.barometerInnovationM),
            F(localization.rangefinderInnovationM),
            F(localization.yawInnovationDeg)
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

        Debug.Log("[MIMISKDroneLocalizationLogger] Closed log: " + currentLogPath);
    }
}
