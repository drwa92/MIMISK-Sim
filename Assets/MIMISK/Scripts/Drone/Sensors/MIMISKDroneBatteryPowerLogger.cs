using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class MIMISKDroneBatteryPowerLogger : MonoBehaviour
{
    public MIMISKDroneBatteryPowerDevice power;

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
        if (power == null)
        {
            power = GetComponent<MIMISKDroneBatteryPowerDevice>();
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
        if (!enableLogging || power == null)
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

        string fileName = "drone_battery_power_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        currentLogPath = Path.Combine(logDir, fileName);

        writer = new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,device," +
            "soc,consumed_mah,consumed_wh," +
            "pack_ocv,pack_voltage,cell_voltage,current_a,power_w,remaining_min," +
            "motor_fl_current,motor_fr_current,motor_rl_current,motor_rr_current," +
            "motor_fl_frac,motor_fr_frac,motor_rl_frac,motor_rr_frac," +
            "low_battery,critical_battery"
        );

        writer.Flush();

        Debug.Log("[MIMISKDroneBatteryPowerLogger] Logging to: " + currentLogPath);
    }

    private void WriteLine()
    {
        Vector4 mf = power.motorFractionsFL_FR_RL_RR;

        string line = string.Join(",",
            F(Time.time),
            power.deviceName,

            F(power.stateOfCharge),
            F(power.consumedMah),
            F(power.consumedWh),

            F(power.packOpenCircuitVoltage),
            F(power.packVoltage),
            F(power.cellVoltage),
            F(power.currentA),
            F(power.powerW),
            F(power.estimatedRemainingMinutes),

            F(power.motorFLCurrentA),
            F(power.motorFRCurrentA),
            F(power.motorRLCurrentA),
            F(power.motorRRCurrentA),

            F(mf.x),
            F(mf.y),
            F(mf.z),
            F(mf.w),

            power.lowBattery ? "1" : "0",
            power.criticalBattery ? "1" : "0"
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

        Debug.Log("[MIMISKDroneBatteryPowerLogger] Closed log: " + currentLogPath);
    }
}
