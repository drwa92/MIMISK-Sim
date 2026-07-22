using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class MIMISKDroneBarometerLogger : MonoBehaviour
{
    public MIMISKDroneBarometerDevice barometer;

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
        if (barometer == null)
        {
            barometer = GetComponent<MIMISKDroneBarometerDevice>();
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
        if (!enableLogging || barometer == null)
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

        string fileName = "drone_barometer_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        currentLogPath = Path.Combine(logDir, fileName);

        writer = new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,device," +
            "pressure_pa,altitude_m,relative_altitude_m,vertical_speed_ms,temp_c," +
            "truth_relative_altitude_m,true_pressure_pa,true_vertical_speed_ms," +
            "pressure_bias_pa"
        );

        writer.Flush();

        Debug.Log("[MIMISKDroneBarometerLogger] Logging to: " + currentLogPath);
    }

    private void WriteLine()
    {
        string line = string.Join(",",
            F(Time.time),
            barometer.deviceName,

            F(barometer.pressurePa),
            F(barometer.altitudeM),
            F(barometer.relativeAltitudeM),
            F(barometer.verticalSpeedMS),
            F(barometer.temperatureC),

            F(barometer.trueRelativeAltitudeM),
            F(barometer.truePressurePa),
            F(barometer.trueVerticalSpeedMS),

            F(barometer.pressureBiasPa)
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

        Debug.Log("[MIMISKDroneBarometerLogger] Closed log: " + currentLogPath);
    }
}
