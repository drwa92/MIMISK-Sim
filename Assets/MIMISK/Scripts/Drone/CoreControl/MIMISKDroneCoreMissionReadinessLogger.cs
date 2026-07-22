using System;
using System.Globalization;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKDroneCoreMissionReadinessLogger : MonoBehaviour
{
    [Header("References")]
    public MIMISKDroneCoreRotorController core;
    public MIMISKDroneCoreFlightModeManager manager;
    public MIMISKDroneCoreTrajectoryPlanner trajectoryPlanner;
    public MIMISKDroneCoreMissionManager missionManager;
    public MIMISKDroneCorePropellerAnimationBridge propellerBridge;
    public MIMISKDronePropellerAnimator propellerAnimator;
    public MIMISKDroneSurfaceBuoyancy surfaceBuoyancy;
    public Rigidbody rb;

    [Header("Logging")]
    public bool enableLogging = true;
    public float logHz = 50.0f;
    public bool flushEveryLine = false;

    public string currentLogPath;
    public int linesWritten;

    private StreamWriter writer;
    private float timer;
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private void Awake()
    {
        AutoFindReferences();
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
        if (!enableLogging)
        {
            return;
        }

        if (writer == null)
        {
            OpenLog();
        }

        timer += Time.fixedDeltaTime;

        float period =
            1.0f / Mathf.Max(1.0f, logHz);

        if (timer >= period)
        {
            timer -= period;
            WriteLine();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (core == null)
        {
            core = GetComponent<MIMISKDroneCoreRotorController>();
        }

        if (manager == null)
        {
            manager = GetComponent<MIMISKDroneCoreFlightModeManager>();
        }

        if (trajectoryPlanner == null)
        {
            trajectoryPlanner = GetComponent<MIMISKDroneCoreTrajectoryPlanner>();
        }

        if (missionManager == null)
        {
            missionManager = GetComponent<MIMISKDroneCoreMissionManager>();
        }

        if (propellerBridge == null)
        {
            propellerBridge = GetComponent<MIMISKDroneCorePropellerAnimationBridge>();
        }

        if (propellerAnimator == null)
        {
            propellerAnimator = GetComponent<MIMISKDronePropellerAnimator>();
        }

        if (surfaceBuoyancy == null)
        {
            surfaceBuoyancy = GetComponent<MIMISKDroneSurfaceBuoyancy>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }

    private void OpenLog()
    {
        AutoFindReferences();

        string projectRoot =
            Directory.GetParent(Application.dataPath).FullName;

        string logDir =
            Path.Combine(projectRoot, "Logs", "MIMISKDrone");

        Directory.CreateDirectory(logDir);

        string fileName =
            "drone_core_mission_readiness_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss") +
            ".csv";

        currentLogPath =
            Path.Combine(logDir, fileName);

        writer =
            new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time," +
            "core_mode,flight_mode,manager_event,mission_state,mission_event," +
            "trajectory_type,trajectory_time,path_ramp,sequence_state," +
            "propeller_state,propeller_rpm,propeller_bound_count," +
            "surface_contact,buoyancy_active_points," +
            "ref_x,ref_y,ref_z,ref_vx,ref_vy,ref_vz,ref_ax,ref_ay,ref_az,ref_yaw," +
            "est_x,est_y,est_z,est_vx,est_vy,est_vz,est_yaw," +
            "true_x,true_y,true_z,true_vx,true_vy,true_vz,true_yaw," +
            "err_x,err_y,err_z,err_norm,yaw_error_deg," +
            "motor_fl,motor_fr,motor_rl,motor_rr,total_thrust"
        );

        writer.Flush();
    }

    private void WriteLine()
    {
        if (core == null)
        {
            return;
        }

        Vector3 refP = core.referencePositionWorld;
        Vector3 refV = core.referenceVelocityWorld;
        Vector3 refA = core.referenceAccelerationWorld;

        Vector3 estP = core.estimatedPositionWorld;
        Vector3 estV = core.estimatedVelocityWorld;

        Vector3 trueP = rb != null ? rb.position : estP;
        Vector3 trueV = rb != null ? rb.linearVelocity : estV;
        float trueYaw = rb != null ? rb.rotation.eulerAngles.y : core.estimatedYawDeg;

        Vector3 err = refP - estP;
        float yawErr =
            Mathf.DeltaAngle(core.referenceYawDeg, core.estimatedYawDeg);

        string coreMode = core.controlMode.ToString();

        string flightMode =
            manager != null ? manager.flightMode.ToString() : "none";

        string managerEvent =
            manager != null ? manager.lastModeEvent : "none";

        string missionStateText =
            missionManager != null ? missionManager.missionState.ToString() : "none";

        string missionEventText =
            missionManager != null ? missionManager.lastMissionEvent : "none";

        string trajType =
            trajectoryPlanner != null ? trajectoryPlanner.trajectoryType.ToString() : "none";

        float trajTime =
            trajectoryPlanner != null ? trajectoryPlanner.lastPathTimeS : 0.0f;

        float pathRamp =
            manager != null ? manager.pathRamp01 : 0.0f;

        string sequenceState =
            GetSequenceStateText();

        string propState =
            propellerBridge != null
                ? propellerBridge.currentVisualState
                : (propellerAnimator != null ? propellerAnimator.rotorState.ToString() : "none");

        float propRpm =
            propellerAnimator != null ? propellerAnimator.targetRpm : 0.0f;

        int boundCount =
            propellerBridge != null ? propellerBridge.boundRotorCount : 0;

        int activeBuoyancy =
            surfaceBuoyancy != null ? surfaceBuoyancy.activePointCount : 0;

        bool surfaceContact =
            activeBuoyancy > 0 || HasWaterContactBySensors();

        Vector4 motors = core.motorThrustActualN;

        string line = string.Join(",",
            F(Time.time),

            coreMode,
            flightMode,
            Safe(managerEvent),
            Safe(missionStateText),
            Safe(missionEventText),

            trajType,
            F(trajTime),
            F(pathRamp),
            Safe(sequenceState),

            Safe(propState),
            F(propRpm),
            boundCount.ToString(Culture),

            surfaceContact ? "1" : "0",
            activeBuoyancy.ToString(Culture),

            F(refP.x), F(refP.y), F(refP.z),
            F(refV.x), F(refV.y), F(refV.z),
            F(refA.x), F(refA.y), F(refA.z),
            F(core.referenceYawDeg),

            F(estP.x), F(estP.y), F(estP.z),
            F(estV.x), F(estV.y), F(estV.z),
            F(core.estimatedYawDeg),

            F(trueP.x), F(trueP.y), F(trueP.z),
            F(trueV.x), F(trueV.y), F(trueV.z),
            F(trueYaw),

            F(err.x), F(err.y), F(err.z),
            F(err.magnitude),
            F(yawErr),

            F(motors.x), F(motors.y), F(motors.z), F(motors.w),
            F(core.totalThrustCommandN)
        );

        writer.WriteLine(line);
        linesWritten++;

        if (flushEveryLine)
        {
            writer.Flush();
        }
    }

    private string GetSequenceStateText()
    {
        // Keep this logger independent from the optional validation sequence script.
        // This prevents compile failure if the validation sequence component is not present.
        Component[] components = GetComponents<Component>();

        if (components == null)
        {
            return "none";
        }

        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];

            if (c == null)
            {
                continue;
            }

            System.Type t = c.GetType();

            if (t == null || t.Name != "MIMISKDroneCoreValidationSequence")
            {
                continue;
            }

            System.Reflection.FieldInfo field =
                t.GetField(
                    "sequenceState",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic
                );

            if (field != null)
            {
                object value = field.GetValue(c);
                return value != null ? value.ToString() : "none";
            }

            System.Reflection.PropertyInfo prop =
                t.GetProperty(
                    "sequenceState",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic
                );

            if (prop != null)
            {
                object value = prop.GetValue(c, null);
                return value != null ? value.ToString() : "none";
            }
        }

        return "none";
    }

    private bool HasWaterContactBySensors()
    {
        if (manager == null ||
            manager.waterContactSensors == null)
        {
            return false;
        }

        for (int i = 0; i < manager.waterContactSensors.Length; i++)
        {
            Transform sensor = manager.waterContactSensors[i];

            if (sensor == null)
            {
                continue;
            }

            if (sensor.position.y <= manager.waterSurfaceY + manager.waterContactToleranceM)
            {
                return true;
            }
        }

        return false;
    }

    private string F(float value)
    {
        return value.ToString("G9", Culture);
    }

    private string Safe(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "none";
        }

        return value.Replace(",", "_");
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
    }

    private void OnDisable()
    {
        CloseLog();
    }

    private void OnApplicationQuit()
    {
        CloseLog();
    }
}
