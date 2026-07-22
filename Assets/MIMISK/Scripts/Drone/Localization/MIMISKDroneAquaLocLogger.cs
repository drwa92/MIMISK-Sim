using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class MIMISKDroneAquaLocLogger : MonoBehaviour
{
    public MIMISKDroneAquaLocEstimator estimator;

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
        if (estimator == null)
        {
            estimator = GetComponent<MIMISKDroneAquaLocEstimator>();
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
        if (!enableLogging || estimator == null)
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

        string fileName = "drone_aqualoc_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        currentLogPath = Path.Combine(logDir, fileName);

        writer = new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,mode," +
            "true_x,true_y,true_z,true_vx,true_vy,true_vz,true_pitch,true_yaw,true_roll," +
            "est_x,est_y,est_z,est_vx,est_vy,est_vz,est_pitch,est_yaw,est_roll," +
            "raw_est_x,raw_est_y,raw_est_z,raw_est_vx,raw_est_vy,raw_est_vz,raw_est_yaw," +
            "pos_err_x,pos_err_y,pos_err_z,pos_err_norm,yaw_err_deg," +
            "used_imu,used_gnss,used_baro,used_range,used_mag,used_surface," +
            "rej_gnss,rej_baro,rej_range,rej_mag," +
            "w_gnss_pos,w_gnss_vel,w_baro,w_range,w_mag,w_surface," +
            "gnss_innov_x,gnss_innov_y,gnss_innov_z," +
            "baro_innov,range_innov,surface_innov,yaw_innov," +
            "adaptive_lead,observed_lead,active_lead,lead_x,lead_y,lead_z," +
            "gnss_origin_ready,gnss_origin_samples," +
            "imu_updates,gnss_updates,baro_updates,range_updates,mag_updates"
        );

        writer.Flush();

        Debug.Log("[MIMISKDroneAquaLocLogger] Logging to: " + currentLogPath);
    }

    private void WriteLine()
    {
        Vector3 tp = estimator.truePositionWorld;
        Vector3 tv = estimator.trueVelocityWorld;
        Vector3 te = estimator.trueEulerDeg;

        Vector3 ep = estimator.estimatedPositionWorld;
        Vector3 ev = estimator.estimatedVelocityWorld;
        Vector3 ee = estimator.estimatedEulerDeg;

        Vector3 rawP = GetVector3Field("rawEstimatedPositionWorld", ep);
        Vector3 rawV = GetVector3Field("rawEstimatedVelocityWorld", ev);
        float rawYaw = GetFloatField("rawEstimatedYawDeg", estimator.estimatedYawDeg);

        Vector3 lead = GetVector3Field("outputLeadCorrectionM", Vector3.zero);

        Vector3 posErr = ep - tp;
        float yawErr = Mathf.DeltaAngle(te.y, ee.y);

        string line = string.Join(",",
            F(Time.time),
            estimator.currentMode.ToString(),

            F(tp.x), F(tp.y), F(tp.z),
            F(tv.x), F(tv.y), F(tv.z),
            F(te.x), F(te.y), F(te.z),

            F(ep.x), F(ep.y), F(ep.z),
            F(ev.x), F(ev.y), F(ev.z),
            F(ee.x), F(ee.y), F(ee.z),

            F(rawP.x), F(rawP.y), F(rawP.z),
            F(rawV.x), F(rawV.y), F(rawV.z),
            F(rawYaw),

            F(posErr.x), F(posErr.y), F(posErr.z), F(posErr.magnitude),
            F(yawErr),

            estimator.usedImu ? "1" : "0",
            estimator.usedGnss ? "1" : "0",
            estimator.usedBarometer ? "1" : "0",
            estimator.usedRangefinder ? "1" : "0",
            estimator.usedMagnetometer ? "1" : "0",
            estimator.usedSurfaceConstraint ? "1" : "0",

            estimator.rejectedGnss ? "1" : "0",
            estimator.rejectedBarometer ? "1" : "0",
            estimator.rejectedRangefinder ? "1" : "0",
            estimator.rejectedMagnetometer ? "1" : "0",

            F(estimator.gnssPositionWeight),
            F(estimator.gnssVelocityWeight),
            F(estimator.barometerWeight),
            F(estimator.rangefinderWeight),
            F(estimator.magnetometerWeight),
            F(estimator.surfaceConstraintWeight),

            F(estimator.gnssPositionInnovationM.x),
            F(estimator.gnssPositionInnovationM.y),
            F(estimator.gnssPositionInnovationM.z),

            F(estimator.barometerInnovationM),
            F(estimator.rangefinderInnovationM),
            F(estimator.surfaceConstraintInnovationM),
            F(estimator.yawInnovationDeg),

            F(GetFloatField("adaptiveHorizontalLeadSeconds", 0.0f)),
            F(GetFloatField("observedHorizontalLeadSeconds", 0.0f)),
            F(GetFloatField("activeHorizontalLeadSeconds", 0.0f)),
            F(lead.x), F(lead.y), F(lead.z),

            GetBoolField("gnssOriginReady", false) ? "1" : "0",
            GetIntField("gnssOriginSampleCount", 0).ToString(Culture),

            GetIntField("imuUpdateCount", 0).ToString(Culture),
            GetIntField("gnssUpdateCount", 0).ToString(Culture),
            GetIntField("barometerUpdateCount", 0).ToString(Culture),
            GetIntField("rangefinderUpdateCount", 0).ToString(Culture),
            GetIntField("magnetometerUpdateCount", 0).ToString(Culture)
        );

        writer.WriteLine(line);
        linesWritten++;

        if (flushEveryLine)
        {
            writer.Flush();
        }
    }

    private float GetFloatField(string fieldName, float defaultValue)
    {
        if (estimator == null)
        {
            return defaultValue;
        }

        var field = estimator.GetType().GetField(fieldName);

        if (field == null)
        {
            return defaultValue;
        }

        object value = field.GetValue(estimator);

        if (value is float f)
        {
            return f;
        }

        return defaultValue;
    }

    private int GetIntField(string fieldName, int defaultValue)
    {
        if (estimator == null)
        {
            return defaultValue;
        }

        var field = estimator.GetType().GetField(fieldName);

        if (field == null)
        {
            return defaultValue;
        }

        object value = field.GetValue(estimator);

        if (value is int i)
        {
            return i;
        }

        return defaultValue;
    }

    private bool GetBoolField(string fieldName, bool defaultValue)
    {
        if (estimator == null)
        {
            return defaultValue;
        }

        var field = estimator.GetType().GetField(fieldName);

        if (field == null)
        {
            return defaultValue;
        }

        object value = field.GetValue(estimator);

        if (value is bool b)
        {
            return b;
        }

        return defaultValue;
    }

    private Vector3 GetVector3Field(string fieldName, Vector3 defaultValue)
    {
        if (estimator == null)
        {
            return defaultValue;
        }

        var field = estimator.GetType().GetField(fieldName);

        if (field == null)
        {
            return defaultValue;
        }

        object value = field.GetValue(estimator);

        if (value is Vector3 v)
        {
            return v;
        }

        return defaultValue;
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

        Debug.Log("[MIMISKDroneAquaLocLogger] Closed log: " + currentLogPath);
    }
}
