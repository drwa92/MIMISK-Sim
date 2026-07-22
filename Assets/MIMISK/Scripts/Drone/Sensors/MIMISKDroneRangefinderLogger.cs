using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class MIMISKDroneRangefinderLogger : MonoBehaviour
{
    public MIMISKDroneRangefinderDevice rangefinder;

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
        if (rangefinder == null)
        {
            rangefinder = GetComponent<MIMISKDroneRangefinderDevice>();
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
        if (!enableLogging || rangefinder == null)
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

        string fileName = "drone_rangefinder_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        currentLogPath = Path.Combine(logDir, fileName);

        writer = new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,device,valid,source," +
            "range_m,vertical_distance_m,signal_strength," +
            "truth_valid,truth_source,truth_range_m,truth_vertical_distance_m," +
            "origin_x,origin_y,origin_z,dir_x,dir_y,dir_z," +
            "hit_x,hit_y,hit_z,hit_nx,hit_ny,hit_nz," +
            "range_bias_m"
        );

        writer.Flush();

        Debug.Log("[MIMISKDroneRangefinderLogger] Logging to: " + currentLogPath);
    }

    private void WriteLine()
    {
        string line = string.Join(",",
            F(Time.time),
            rangefinder.deviceName,
            rangefinder.validReturn ? "1" : "0",
            rangefinder.returnSource.ToString(),

            F(rangefinder.rangeM),
            F(rangefinder.verticalDistanceM),
            F(rangefinder.signalStrength),

            rangefinder.truthHasReturn ? "1" : "0",
            rangefinder.truthReturnSource.ToString(),
            F(rangefinder.truthRangeM),
            F(rangefinder.truthVerticalDistanceM),

            F(rangefinder.rayOriginWorld.x),
            F(rangefinder.rayOriginWorld.y),
            F(rangefinder.rayOriginWorld.z),

            F(rangefinder.rayDirectionWorld.x),
            F(rangefinder.rayDirectionWorld.y),
            F(rangefinder.rayDirectionWorld.z),

            F(rangefinder.hitPointWorld.x),
            F(rangefinder.hitPointWorld.y),
            F(rangefinder.hitPointWorld.z),

            F(rangefinder.hitNormalWorld.x),
            F(rangefinder.hitNormalWorld.y),
            F(rangefinder.hitNormalWorld.z),

            F(rangefinder.rangeBiasM)
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

        Debug.Log("[MIMISKDroneRangefinderLogger] Closed log: " + currentLogPath);
    }
}
