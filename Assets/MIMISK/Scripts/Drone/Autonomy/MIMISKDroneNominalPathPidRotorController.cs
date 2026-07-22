using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class MIMISKDroneNominalPathPidRotorController : MonoBehaviour
{
    public enum StateSource
    {
        GroundTruth,
        AquaLocRTK
    }

    public enum PathKind
    {
        Circle,
        Square,
        Spiral
    }

    [Header("References")]
    public Rigidbody rb;
    public MIMISKDroneAquaLocEstimator aquaLoc;
    public MIMISKDroneUdpGamepadReceiver udpReceiver;
    public MIMISKDroneModelController modelControllerToDisable;

    [Header("Keyboard")]
    public Key startKey = Key.N;
    public Key abortKey = Key.B;

    [Header("Mission")]
    public bool missionActive;
    public StateSource stateSource = StateSource.GroundTruth;
    public PathKind pathKind = PathKind.Circle;

    [Tooltip("Mission duration is used for circle and spiral. Square duration is computed from side/speed.")]
    public float missionDurationS = 40.0f;

    public bool restoreGamepadOnFinish = true;
    public bool disableOldModelControllerDuringMission = true;

    [Header("Path Parameters")]
    public float circleRadiusM = 1.4f;
    public float circleOmegaRadS = 0.23f;

    public float squareSideM = 2.4f;
    public float squareSpeedMS = 0.32f;

    public float spiralOmegaRadS = 0.28f;
    public float spiralInitialRadiusM = 0.25f;
    public float spiralFinalRadiusM = 1.25f;
    public float spiralDurationS = 36.0f;
    public float spiralAltitudeRiseM = 0.35f;

    [Header("Outer Path PID - Python Report Values")]
    public float kpXZ = 1.25f;
    public float kdXZ = 2.15f;
    public float kiXZ = 0.04f;

    public float kpY = 1.8f;
    public float kdY = 1.7f;
    public float kiY = 0.15f;

    public Vector3 integralLimitXYZ = new Vector3(0.8f, 0.6f, 0.8f);
    public float maxTiltDeg = 22.0f;

    [Header("Inner Attitude Controller")]
    [Tooltip("Torque gain about body X, Y, Z in Nm/rad.")]
    public Vector3 attitudeKpNmPerRad = new Vector3(8.0f, 1.2f, 8.0f);

    [Tooltip("Rate damping about body X, Y, Z in Nm/(rad/s).")]
    public Vector3 rateKdNmPerRadS = new Vector3(4.6f, 0.8f, 4.6f);

    public Vector3 torqueLimitNm = new Vector3(8.0f, 0.36f, 8.0f);

    [Header("Rotor / Drone Parameters")]
    public bool useRigidbodyMass = true;
    public float massKg = 4.0f;
    public float gravity = 9.80665f;

    public float armX_M = 0.58f;
    public float armZ_M = 0.50f;

    public float maxThrustPerRotorN = 18.0f;
    public float motorTimeConstantS = 0.12f;

    [Tooltip("Yaw reaction torque coefficient in Nm per Newton of rotor thrust. For the current 4 kg Unity drone, 0.18 Nm/output / 18 N/output = 0.01.")]
    public float yawTorqueCoeffNmPerN = 0.010f;

    [Header("Rotor Spin Signs: FL, FR, RL, RR")]
    public Vector4 rotorSpinSigns = new Vector4(1.0f, -1.0f, -1.0f, 1.0f);

    [Header("Rigidbody Setup")]
    public bool configureRigidbodyOnStart = true;
    public bool forceGravityOn = true;
    public bool forceNonKinematic = true;

    [Header("Runtime State")]
    public Vector3 missionStartWorld;
    public float missionStartYawDeg;
    public float missionTimerS;

    public Vector3 estimatedPositionWorld;
    public Vector3 estimatedVelocityWorld;
    public float estimatedYawDeg;

    public Vector3 referencePositionWorld;
    public Vector3 referenceVelocityWorld;
    public Vector3 referenceAccelerationWorld;

    public Vector3 positionErrorWorld;
    public Vector3 velocityErrorWorld;
    public Vector3 integralErrorWorld;

    public Vector3 commandedAccelerationWorld;
    public Quaternion desiredAttitudeWorld;
    public Vector3 attitudeErrorBodyRad;
    public Vector3 angularVelocityBodyRadS;
    public Vector3 torqueCommandBodyNm;

    public float totalThrustCommandN;

    public Vector4 motorThrustCommandN;
    public Vector4 motorThrustActualN;

    public float trackingErrorM;
    public string lastEvent = "idle";

    [Header("Logging")]
    public bool enableLogging = true;
    public float logHz = 50.0f;
    public bool flushEveryLine = false;
    public string currentLogPath;
    public int logLines;

    private StreamWriter writer;
    private float logTimer;
    private bool oldModelControllerWasEnabled;
    private bool udpWasEnabled;
    private bool cachedOldStates;
    private Matrix4x4 allocation;
    private Matrix4x4 allocationInv;

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private void Awake()
    {
        AutoFindReferences();
        BuildAllocationMatrix();
    }

    private void Start()
    {
        if (configureRigidbodyOnStart && rb != null)
        {
            if (forceGravityOn)
            {
                rb.useGravity = true;
            }

            if (forceNonKinematic)
            {
                rb.isKinematic = false;
            }
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[startKey].wasPressedThisFrame)
        {
            StartMission();
        }

        if (Keyboard.current[abortKey].wasPressedThisFrame)
        {
            AbortMission();
        }
    }

    private void FixedUpdate()
    {
        if (!missionActive)
        {
            return;
        }

        if (!UpdateState())
        {
            return;
        }

        float dt = Time.fixedDeltaTime;
        missionTimerS += dt;

        EvaluateReference(missionTimerS, out referencePositionWorld, out referenceVelocityWorld, out referenceAccelerationWorld);

        ComputeOuterPathPid(dt);
        ComputeDesiredAttitudeAndThrust();
        ComputeAttitudeTorque();
        AllocateAndApplyRotorForces(dt);

        trackingErrorM = Vector3.Distance(estimatedPositionWorld, referencePositionWorld);

        WriteLogIfDue(dt);

        if (missionTimerS >= GetMissionDuration())
        {
            CompleteMission();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (aquaLoc == null)
        {
            aquaLoc = GetComponent<MIMISKDroneAquaLocEstimator>();
        }

        if (udpReceiver == null)
        {
            udpReceiver = GetComponent<MIMISKDroneUdpGamepadReceiver>();
        }

        if (modelControllerToDisable == null)
        {
            modelControllerToDisable = GetComponent<MIMISKDroneModelController>();
        }
    }

    [ContextMenu("Start Mission")]
    public void StartMission()
    {
        AutoFindReferences();
        BuildAllocationMatrix();

        if (rb == null)
        {
            Debug.LogError("[MIMISK] Nominal path PID cannot start: missing Rigidbody.");
            return;
        }

        if (stateSource == StateSource.AquaLocRTK &&
            (aquaLoc == null || !aquaLoc.estimatorReady))
        {
            Debug.LogWarning("[MIMISK] Nominal path PID rejected: AquaLoc RTK is not ready.");
            return;
        }

        if (!cachedOldStates)
        {
            oldModelControllerWasEnabled =
                modelControllerToDisable != null && modelControllerToDisable.enabled;

            udpWasEnabled =
                udpReceiver != null && udpReceiver.enabled;

            cachedOldStates = true;
        }

        if (disableOldModelControllerDuringMission && modelControllerToDisable != null)
        {
            modelControllerToDisable.ClearExternalCommand();
            modelControllerToDisable.enabled = false;
        }

        if (udpReceiver != null)
        {
            udpReceiver.enabled = false;
        }

        rb.useGravity = true;
        rb.isKinematic = false;

        UpdateState();

        missionStartWorld = estimatedPositionWorld;
        missionStartYawDeg = estimatedYawDeg;

        missionTimerS = 0.0f;
        integralErrorWorld = Vector3.zero;
        motorThrustCommandN = Vector4.zero;
        motorThrustActualN = Vector4.zero;

        missionActive = true;
        lastEvent = "mission_started_" + pathKind.ToString();

        OpenLog();

        Debug.Log("[MIMISK] Nominal path PID rotor mission started: " + pathKind + " using " + stateSource);
    }

    [ContextMenu("Abort Mission")]
    public void AbortMission()
    {
        missionActive = false;
        lastEvent = "mission_aborted";

        CloseLog();
        RestoreManualControl();

        Debug.Log("[MIMISK] Nominal path PID mission aborted.");
    }

    private void CompleteMission()
    {
        missionActive = false;
        lastEvent = "mission_completed";

        CloseLog();
        RestoreManualControl();

        Debug.Log("[MIMISK] Nominal path PID mission completed.");
    }

    private void RestoreManualControl()
    {
        if (modelControllerToDisable != null)
        {
            modelControllerToDisable.enabled = oldModelControllerWasEnabled;
            modelControllerToDisable.ClearExternalCommand();
        }

        if (restoreGamepadOnFinish && udpReceiver != null)
        {
            udpReceiver.enabled = udpWasEnabled;
        }

        cachedOldStates = false;
    }

    private bool UpdateState()
    {
        if (stateSource == StateSource.AquaLocRTK &&
            aquaLoc != null &&
            aquaLoc.estimatorReady)
        {
            estimatedPositionWorld = aquaLoc.estimatedPositionWorld;
            estimatedVelocityWorld = aquaLoc.estimatedVelocityWorld;
            estimatedYawDeg = aquaLoc.estimatedYawDeg;
            return true;
        }

        if (rb == null)
        {
            return false;
        }

        estimatedPositionWorld = rb.position;
        estimatedVelocityWorld = rb.linearVelocity;
        estimatedYawDeg = rb.rotation.eulerAngles.y;
        return true;
    }

    private float GetMissionDuration()
    {
        if (pathKind == PathKind.Square)
        {
            float sideTime = squareSideM / Mathf.Max(0.05f, squareSpeedMS);
            return sideTime * 4.0f;
        }

        if (pathKind == PathKind.Spiral)
        {
            return Mathf.Max(missionDurationS, spiralDurationS);
        }

        return missionDurationS;
    }

    private void EvaluateReference(
        float t,
        out Vector3 p,
        out Vector3 v,
        out Vector3 a)
    {
        if (pathKind == PathKind.Circle)
        {
            float R = circleRadiusM;
            float w = circleOmegaRadS;
            float wt = w * t;

            p = missionStartWorld + new Vector3(
                R * (Mathf.Cos(wt) - 1.0f),
                0.0f,
                R * Mathf.Sin(wt)
            );

            v = new Vector3(
                -R * w * Mathf.Sin(wt),
                0.0f,
                R * w * Mathf.Cos(wt)
            );

            a = new Vector3(
                -R * w * w * Mathf.Cos(wt),
                0.0f,
                -R * w * w * Mathf.Sin(wt)
            );

            return;
        }

        if (pathKind == PathKind.Spiral)
        {
            float T = Mathf.Max(0.1f, spiralDurationS);
            float s = Mathf.Clamp01(t / T);
            float r = Mathf.Lerp(spiralInitialRadiusM, spiralFinalRadiusM, s);
            float rDot = t <= T ? (spiralFinalRadiusM - spiralInitialRadiusM) / T : 0.0f;
            float w = spiralOmegaRadS;
            float th = w * t;

            p = missionStartWorld + new Vector3(
                r * Mathf.Cos(th) - spiralInitialRadiusM,
                spiralAltitudeRiseM * s,
                r * Mathf.Sin(th)
            );

            v = new Vector3(
                rDot * Mathf.Cos(th) - r * w * Mathf.Sin(th),
                spiralAltitudeRiseM / T,
                rDot * Mathf.Sin(th) + r * w * Mathf.Cos(th)
            );

            a = new Vector3(
                -2.0f * rDot * w * Mathf.Sin(th) - r * w * w * Mathf.Cos(th),
                0.0f,
                2.0f * rDot * w * Mathf.Cos(th) - r * w * w * Mathf.Sin(th)
            );

            return;
        }

        EvaluateSquareReference(t, out p, out v, out a);
    }

    private void EvaluateSquareReference(float t, out Vector3 p, out Vector3 v, out Vector3 a)
    {
        float side = Mathf.Max(0.1f, squareSideM);
        float speed = Mathf.Max(0.05f, squareSpeedMS);
        float segTime = side / speed;
        float totalTime = 4.0f * segTime;
        float tau = Mathf.Repeat(t, totalTime);

        Vector3 p0 = missionStartWorld;
        Vector3 p1 = missionStartWorld + new Vector3(side, 0.0f, 0.0f);
        Vector3 p2 = missionStartWorld + new Vector3(side, 0.0f, side);
        Vector3 p3 = missionStartWorld + new Vector3(0.0f, 0.0f, side);

        Vector3 a0;
        Vector3 b0;

        if (tau < segTime)
        {
            a0 = p0;
            b0 = p1;
        }
        else if (tau < 2.0f * segTime)
        {
            a0 = p1;
            b0 = p2;
            tau -= segTime;
        }
        else if (tau < 3.0f * segTime)
        {
            a0 = p2;
            b0 = p3;
            tau -= 2.0f * segTime;
        }
        else
        {
            a0 = p3;
            b0 = p0;
            tau -= 3.0f * segTime;
        }

        float u = Mathf.Clamp01(tau / segTime);
        p = Vector3.Lerp(a0, b0, u);
        v = (b0 - a0).normalized * speed;
        a = Vector3.zero;
    }

    private void ComputeOuterPathPid(float dt)
    {
        positionErrorWorld = referencePositionWorld - estimatedPositionWorld;
        velocityErrorWorld = referenceVelocityWorld - estimatedVelocityWorld;

        integralErrorWorld += positionErrorWorld * dt;

        integralErrorWorld.x = Mathf.Clamp(integralErrorWorld.x, -integralLimitXYZ.x, integralLimitXYZ.x);
        integralErrorWorld.y = Mathf.Clamp(integralErrorWorld.y, -integralLimitXYZ.y, integralLimitXYZ.y);
        integralErrorWorld.z = Mathf.Clamp(integralErrorWorld.z, -integralLimitXYZ.z, integralLimitXYZ.z);

        commandedAccelerationWorld = new Vector3(
            referenceAccelerationWorld.x + kpXZ * positionErrorWorld.x + kdXZ * velocityErrorWorld.x + kiXZ * integralErrorWorld.x,
            referenceAccelerationWorld.y + kpY  * positionErrorWorld.y + kdY  * velocityErrorWorld.y + kiY  * integralErrorWorld.y,
            referenceAccelerationWorld.z + kpXZ * positionErrorWorld.z + kdXZ * velocityErrorWorld.z + kiXZ * integralErrorWorld.z
        );

        float maxHorizontalAccel =
            gravity * Mathf.Tan(maxTiltDeg * Mathf.Deg2Rad);

        Vector2 horizontalAccel = new Vector2(
            commandedAccelerationWorld.x,
            commandedAccelerationWorld.z
        );

        if (horizontalAccel.magnitude > maxHorizontalAccel)
        {
            horizontalAccel = horizontalAccel.normalized * maxHorizontalAccel;
            commandedAccelerationWorld.x = horizontalAccel.x;
            commandedAccelerationWorld.z = horizontalAccel.y;
        }
    }

    private void ComputeDesiredAttitudeAndThrust()
    {
        float m = useRigidbodyMass && rb != null ? rb.mass : massKg;

        Vector3 requiredSpecificForce = new Vector3(
            commandedAccelerationWorld.x,
            gravity + commandedAccelerationWorld.y,
            commandedAccelerationWorld.z
        );

        if (requiredSpecificForce.y < gravity * 0.25f)
        {
            requiredSpecificForce.y = gravity * 0.25f;
        }

        totalThrustCommandN =
            Mathf.Clamp(
                m * requiredSpecificForce.magnitude,
                0.0f,
                4.0f * maxThrustPerRotorN
            );

        Vector3 bodyYDesired =
            requiredSpecificForce.normalized;

        Vector3 headingForward =
            Quaternion.Euler(0.0f, missionStartYawDeg, 0.0f) * Vector3.forward;

        Vector3 bodyZDesired =
            Vector3.ProjectOnPlane(headingForward, bodyYDesired);

        if (bodyZDesired.sqrMagnitude < 1e-6f)
        {
            bodyZDesired =
                Vector3.ProjectOnPlane(Vector3.forward, bodyYDesired);
        }

        bodyZDesired.Normalize();

        Vector3 bodyXDesired =
            Vector3.Cross(bodyYDesired, bodyZDesired).normalized;

        bodyZDesired =
            Vector3.Cross(bodyXDesired, bodyYDesired).normalized;

        desiredAttitudeWorld =
            Quaternion.LookRotation(bodyZDesired, bodyYDesired);
    }

    private void ComputeAttitudeTorque()
    {
        Quaternion qErr =
            desiredAttitudeWorld * Quaternion.Inverse(rb.rotation);

        if (qErr.w < 0.0f)
        {
            qErr.x = -qErr.x;
            qErr.y = -qErr.y;
            qErr.z = -qErr.z;
            qErr.w = -qErr.w;
        }

        qErr.ToAngleAxis(out float angleDeg, out Vector3 axisWorld);

        if (angleDeg > 180.0f)
        {
            angleDeg -= 360.0f;
        }

        if (float.IsNaN(axisWorld.x) || axisWorld.sqrMagnitude < 1e-8f)
        {
            axisWorld = Vector3.zero;
            angleDeg = 0.0f;
        }

        Vector3 axisBody =
            rb.transform.InverseTransformDirection(axisWorld.normalized);

        attitudeErrorBodyRad =
            axisBody * (angleDeg * Mathf.Deg2Rad);

        angularVelocityBodyRadS =
            rb.transform.InverseTransformDirection(rb.angularVelocity);

        torqueCommandBodyNm = new Vector3(
            attitudeKpNmPerRad.x * attitudeErrorBodyRad.x - rateKdNmPerRadS.x * angularVelocityBodyRadS.x,
            attitudeKpNmPerRad.y * attitudeErrorBodyRad.y - rateKdNmPerRadS.y * angularVelocityBodyRadS.y,
            attitudeKpNmPerRad.z * attitudeErrorBodyRad.z - rateKdNmPerRadS.z * angularVelocityBodyRadS.z
        );

        torqueCommandBodyNm.x =
            Mathf.Clamp(torqueCommandBodyNm.x, -torqueLimitNm.x, torqueLimitNm.x);

        torqueCommandBodyNm.y =
            Mathf.Clamp(torqueCommandBodyNm.y, -torqueLimitNm.y, torqueLimitNm.y);

        torqueCommandBodyNm.z =
            Mathf.Clamp(torqueCommandBodyNm.z, -torqueLimitNm.z, torqueLimitNm.z);
    }

    private void BuildAllocationMatrix()
    {
        // Motor order: FL, FR, RL, RR.
        // Local rotor positions:
        // FL = (-x, 0, -z), FR = (+x, 0, -z), RL = (-x, 0, +z), RR = (+x, 0, +z).
        Vector3[] r = new Vector3[]
        {
            new Vector3(-armX_M, 0.0f, -armZ_M),
            new Vector3( armX_M, 0.0f, -armZ_M),
            new Vector3(-armX_M, 0.0f,  armZ_M),
            new Vector3( armX_M, 0.0f,  armZ_M)
        };

        float[] spin = new float[]
        {
            rotorSpinSigns.x,
            rotorSpinSigns.y,
            rotorSpinSigns.z,
            rotorSpinSigns.w
        };

        allocation = Matrix4x4.zero;

        for (int i = 0; i < 4; i++)
        {
            allocation[0, i] = 1.0f;
            allocation[1, i] = -r[i].z;
            allocation[2, i] = spin[i] * yawTorqueCoeffNmPerN;
            allocation[3, i] = r[i].x;
        }

        allocationInv = allocation.inverse;
    }

    private void AllocateAndApplyRotorForces(float dt)
    {
        Vector4 wrench = new Vector4(
            totalThrustCommandN,
            torqueCommandBodyNm.x,
            torqueCommandBodyNm.y,
            torqueCommandBodyNm.z
        );

        motorThrustCommandN = allocationInv * wrench;

        motorThrustCommandN.x = Mathf.Clamp(motorThrustCommandN.x, 0.0f, maxThrustPerRotorN);
        motorThrustCommandN.y = Mathf.Clamp(motorThrustCommandN.y, 0.0f, maxThrustPerRotorN);
        motorThrustCommandN.z = Mathf.Clamp(motorThrustCommandN.z, 0.0f, maxThrustPerRotorN);
        motorThrustCommandN.w = Mathf.Clamp(motorThrustCommandN.w, 0.0f, maxThrustPerRotorN);

        float alpha =
            1.0f - Mathf.Exp(-dt / Mathf.Max(0.001f, motorTimeConstantS));

        motorThrustActualN =
            Vector4.Lerp(motorThrustActualN, motorThrustCommandN, alpha);

        ApplyRotorForce(0, new Vector3(-armX_M, 0.0f, -armZ_M), motorThrustActualN.x, rotorSpinSigns.x);
        ApplyRotorForce(1, new Vector3( armX_M, 0.0f, -armZ_M), motorThrustActualN.y, rotorSpinSigns.y);
        ApplyRotorForce(2, new Vector3(-armX_M, 0.0f,  armZ_M), motorThrustActualN.z, rotorSpinSigns.z);
        ApplyRotorForce(3, new Vector3( armX_M, 0.0f,  armZ_M), motorThrustActualN.w, rotorSpinSigns.w);
    }

    private void ApplyRotorForce(int index, Vector3 localPosition, float thrustN, float spinSign)
    {
        Vector3 worldPosition =
            rb.transform.TransformPoint(localPosition);

        Vector3 forceWorld =
            rb.transform.up * thrustN;

        rb.AddForceAtPosition(forceWorld, worldPosition, ForceMode.Force);

        Vector3 yawTorqueWorld =
            rb.transform.up * (spinSign * yawTorqueCoeffNmPerN * thrustN);

        rb.AddTorque(yawTorqueWorld, ForceMode.Force);
    }

    private void OpenLog()
    {
        if (!enableLogging)
        {
            return;
        }

        CloseLog();

        string projectRoot =
            Directory.GetParent(Application.dataPath).FullName;

        string logDir =
            Path.Combine(projectRoot, "Logs", "MIMISKDrone");

        Directory.CreateDirectory(logDir);

        string fileName =
            "drone_nominalpathpid_" +
            pathKind.ToString().ToLowerInvariant() + "_" +
            stateSource.ToString().ToLowerInvariant() + "_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss") +
            ".csv";

        currentLogPath =
            Path.Combine(logDir, fileName);

        writer = new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,active,path,state_source,event," +
            "ref_x,ref_y,ref_z,ref_vx,ref_vy,ref_vz,ref_ax,ref_ay,ref_az," +
            "est_x,est_y,est_z,est_vx,est_vy,est_vz,est_yaw," +
            "true_x,true_y,true_z,true_vx,true_vy,true_vz,true_yaw," +
            "err_x,err_y,err_z,err_norm," +
            "cmd_ax,cmd_ay,cmd_az,total_thrust," +
            "att_err_x,att_err_y,att_err_z,omega_x,omega_y,omega_z," +
            "tau_x,tau_y,tau_z," +
            "m_FL_cmd,m_FR_cmd,m_RL_cmd,m_RR_cmd," +
            "m_FL,m_FR,m_RL,m_RR"
        );

        writer.Flush();
        logLines = 0;

        Debug.Log("[MIMISK] Nominal path PID logging to: " + currentLogPath);
    }

    private void WriteLogIfDue(float dt)
    {
        if (writer == null)
        {
            return;
        }

        logTimer += dt;

        float period = 1.0f / Mathf.Max(1.0f, logHz);

        if (logTimer < period)
        {
            return;
        }

        logTimer -= period;

        Vector3 trueP = rb != null ? rb.position : Vector3.zero;
        Vector3 trueV = rb != null ? rb.linearVelocity : Vector3.zero;
        float trueYaw = rb != null ? rb.rotation.eulerAngles.y : 0.0f;

        string line = string.Join(",",
            F(Time.time),
            missionActive ? "1" : "0",
            pathKind.ToString(),
            stateSource.ToString(),
            lastEvent,

            F(referencePositionWorld.x), F(referencePositionWorld.y), F(referencePositionWorld.z),
            F(referenceVelocityWorld.x), F(referenceVelocityWorld.y), F(referenceVelocityWorld.z),
            F(referenceAccelerationWorld.x), F(referenceAccelerationWorld.y), F(referenceAccelerationWorld.z),

            F(estimatedPositionWorld.x), F(estimatedPositionWorld.y), F(estimatedPositionWorld.z),
            F(estimatedVelocityWorld.x), F(estimatedVelocityWorld.y), F(estimatedVelocityWorld.z),
            F(estimatedYawDeg),

            F(trueP.x), F(trueP.y), F(trueP.z),
            F(trueV.x), F(trueV.y), F(trueV.z),
            F(trueYaw),

            F(positionErrorWorld.x), F(positionErrorWorld.y), F(positionErrorWorld.z),
            F(trackingErrorM),

            F(commandedAccelerationWorld.x), F(commandedAccelerationWorld.y), F(commandedAccelerationWorld.z),
            F(totalThrustCommandN),

            F(attitudeErrorBodyRad.x), F(attitudeErrorBodyRad.y), F(attitudeErrorBodyRad.z),
            F(angularVelocityBodyRadS.x), F(angularVelocityBodyRadS.y), F(angularVelocityBodyRadS.z),

            F(torqueCommandBodyNm.x), F(torqueCommandBodyNm.y), F(torqueCommandBodyNm.z),

            F(motorThrustCommandN.x), F(motorThrustCommandN.y), F(motorThrustCommandN.z), F(motorThrustCommandN.w),
            F(motorThrustActualN.x), F(motorThrustActualN.y), F(motorThrustActualN.z), F(motorThrustActualN.w)
        );

        writer.WriteLine(line);
        logLines++;

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

        Debug.Log("[MIMISK] Nominal path PID log closed: " + currentLogPath);
    }

    private void OnDisable()
    {
        if (missionActive)
        {
            AbortMission();
        }

        CloseLog();
    }

    private void OnApplicationQuit()
    {
        CloseLog();
    }
}
