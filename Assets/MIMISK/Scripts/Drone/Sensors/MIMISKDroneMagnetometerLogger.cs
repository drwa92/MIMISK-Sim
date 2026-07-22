using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class MIMISKDroneMagnetometerLogger : MonoBehaviour
{
    public MIMISKDroneMagnetometerDevice magnetometer;

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
        if (magnetometer == null)
        {
            magnetometer = GetComponent<MIMISKDroneMagnetometerDevice>();
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
        if (!enableLogging || magnetometer == null)
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

        string fileName = "drone_magnetometer_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        currentLogPath = Path.Combine(logDir, fileName);

        writer = new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,device," +
            "mag_x_ut,mag_y_ut,mag_z_ut," +
            "truth_mag_x_ut,truth_mag_y_ut,truth_mag_z_ut," +
            "earth_x_ut,earth_y_ut,earth_z_ut," +
            "true_heading_deg,magnetic_heading_deg," +
            "bias_x_ut,bias_y_ut,bias_z_ut"
        );

        writer.Flush();

        Debug.Log("[MIMISKDroneMagnetometerLogger] Logging to: " + currentLogPath);
    }

    private void WriteLine()
    {
        string line = string.Join(",",
            F(Time.time),
            magnetometer.deviceName,

            F(magnetometer.magneticFieldBodyUT.x),
            F(magnetometer.magneticFieldBodyUT.y),
            F(magnetometer.magneticFieldBodyUT.z),

            F(magnetometer.trueMagneticFieldBodyUT.x),
            F(magnetometer.trueMagneticFieldBodyUT.y),
            F(magnetometer.trueMagneticFieldBodyUT.z),

            F(magnetometer.earthFieldWorldUT.x),
            F(magnetometer.earthFieldWorldUT.y),
            F(magnetometer.earthFieldWorldUT.z),

            F(magnetometer.trueHeadingDeg),
            F(magnetometer.magneticHeadingDeg),

            F(magnetometer.hardIronBiasUT.x),
            F(magnetometer.hardIronBiasUT.y),
            F(magnetometer.hardIronBiasUT.z)
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

        Debug.Log("[MIMISKDroneMagnetometerLogger] Closed log: " + currentLogPath);
    }
}
