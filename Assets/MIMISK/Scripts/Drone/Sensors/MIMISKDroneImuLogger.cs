using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class MIMISKDroneImuLogger : MonoBehaviour
{
    public MIMISKDroneImuDevice imu;

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
        if (imu == null)
        {
            imu = GetComponent<MIMISKDroneImuDevice>();
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
        if (!enableLogging || imu == null)
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

        string fileName = "drone_imu_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        currentLogPath = Path.Combine(logDir, fileName);

        writer = new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,device,temp_c," +
            "accel_x,accel_y,accel_z," +
            "gyro_x,gyro_y,gyro_z," +
            "truth_accel_x,truth_accel_y,truth_accel_z," +
            "truth_gyro_x,truth_gyro_y,truth_gyro_z," +
            "gyro_bias_x,gyro_bias_y,gyro_bias_z," +
            "accel_bias_x,accel_bias_y,accel_bias_z"
        );

        writer.Flush();

        Debug.Log("[MIMISKDroneImuLogger] Logging to: " + currentLogPath);
    }

    private void WriteLine()
    {
        string line = string.Join(",",
            F(Time.time),
            imu.deviceName,
            F(imu.temperatureC),

            F(imu.accelerometerBodyMS2.x),
            F(imu.accelerometerBodyMS2.y),
            F(imu.accelerometerBodyMS2.z),

            F(imu.gyroscopeBodyRadS.x),
            F(imu.gyroscopeBodyRadS.y),
            F(imu.gyroscopeBodyRadS.z),

            F(imu.trueSpecificForceBodyMS2.x),
            F(imu.trueSpecificForceBodyMS2.y),
            F(imu.trueSpecificForceBodyMS2.z),

            F(imu.trueAngularRateBodyRadS.x),
            F(imu.trueAngularRateBodyRadS.y),
            F(imu.trueAngularRateBodyRadS.z),

            F(imu.gyroBiasRadS.x),
            F(imu.gyroBiasRadS.y),
            F(imu.gyroBiasRadS.z),

            F(imu.accelBiasMS2.x),
            F(imu.accelBiasMS2.y),
            F(imu.accelBiasMS2.z)
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

        Debug.Log("[MIMISKDroneImuLogger] Closed log: " + currentLogPath);
    }
}
