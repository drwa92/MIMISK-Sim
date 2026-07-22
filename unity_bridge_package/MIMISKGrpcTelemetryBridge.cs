using System;
using System.Reflection;
using UnityEngine;

using MimiskDroneState = MIMISK.Grpc.DroneState;
using MimiskHeader = MIMISK.Grpc.Header;
using MimiskMiniROVState = MIMISK.Grpc.MiniROVState;
using MimiskTelemetryAck = MIMISK.Grpc.TelemetryAck;
using MimiskTelemetryFrame = MIMISK.Grpc.MIMISKTelemetryFrame;
using MimiskTetherState = MIMISK.Grpc.TetherState;
using MimiskVec3 = MIMISK.Grpc.Vec3;

[DefaultExecutionOrder(2500)]
[DisallowMultipleComponent]
public class MIMISKGrpcTelemetryBridge : MonoBehaviour
{
    [Header("Connection")]
    public MIMISKGrpcConnection connection;
    public bool telemetryEnabled = true;
    public float publishHz = 10.0f;

    [Header("Drone References")]
    public Rigidbody droneRigidbody;
    public Transform droneRoot;
    public MonoBehaviour droneCoreController;
    public MonoBehaviour droneFlightModeManager;
    public MonoBehaviour droneMissionManager;

    [Header("MiniROV References")]
    public Rigidbody miniRovRigidbody;
    public Transform miniRovRoot;
    public MonoBehaviour miniRovMissionManager;
    public MonoBehaviour miniRovPathPlanner;
    public MonoBehaviour miniRovController;

    [Header("Tether References")]
    public MonoBehaviour lowLevelTetherManager;
    public MonoBehaviour unifiedTetherManager;
    public MonoBehaviour smartWinchController;

    [Header("Runtime")]
    public bool autoFindReferences = true;
    public ulong sequence;
    public string lastStatus = "idle";
    public bool sendInProgress;
    public int framesSent;
    public int framesFailed;

    private float timerS;

    private void Awake()
    {
        if (autoFindReferences)
        {
            AutoFindReferences();
        }
    }

    private void Start()
    {
        if (connection == null)
        {
            connection =
                GetComponent<MIMISKGrpcConnection>();
        }

        if (connection == null)
        {
            connection =
                FindFirstObjectByType<MIMISKGrpcConnection>();
        }

        if (autoFindReferences)
        {
            AutoFindReferences();
        }
    }

    private void Update()
    {
        if (!telemetryEnabled)
        {
            return;
        }

        if (connection == null ||
            connection.Client == null ||
            !connection.isConnected)
        {
            return;
        }

        timerS += Time.deltaTime;

        float period =
            1.0f / Mathf.Max(1.0f, publishHz);

        if (timerS >= period)
        {
            timerS -= period;
            SendTelemetry();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (connection == null)
        {
            connection =
                GetComponent<MIMISKGrpcConnection>();

            if (connection == null)
            {
                connection =
                    FindFirstObjectByType<MIMISKGrpcConnection>();
            }
        }

        if (droneRoot == null)
        {
            GameObject drone =
                GameObject.Find("Drone");

            if (drone != null)
            {
                droneRoot =
                    drone.transform;
            }
        }

        if (droneRigidbody == null && droneRoot != null)
        {
            droneRigidbody =
                droneRoot.GetComponent<Rigidbody>();
        }

        if (miniRovRoot == null)
        {
            GameObject rov =
                GameObject.Find("MiniROV");

            if (rov != null)
            {
                miniRovRoot =
                    rov.transform;
            }
        }

        if (miniRovRigidbody == null && miniRovRoot != null)
        {
            miniRovRigidbody =
                miniRovRoot.GetComponent<Rigidbody>();
        }

        if (droneCoreController == null)
        {
            droneCoreController =
                FindBehaviourByTypeName("MIMISKDroneCoreRotorController");
        }

        if (droneFlightModeManager == null)
        {
            droneFlightModeManager =
                FindBehaviourByTypeName("MIMISKDroneCoreFlightModeManager");
        }

        if (droneMissionManager == null)
        {
            droneMissionManager =
                FindBehaviourByTypeName("MIMISKDroneCoreMissionManager");
        }

        if (miniRovMissionManager == null)
        {
            miniRovMissionManager =
                FindBehaviourByTypeName("MIMISKMiniROVMissionManager");
        }

        if (miniRovPathPlanner == null)
        {
            miniRovPathPlanner =
                FindBehaviourByTypeName("MIMISKMiniROVPathPlanner");
        }

        if (miniRovController == null)
        {
            miniRovController =
                FindBehaviourByTypeName("MIMISKMiniROVPlantBasedController");
        }

        if (lowLevelTetherManager == null)
        {
            lowLevelTetherManager =
                FindBehaviourByTypeName("MIMISKDroneCoreTetherManager");
        }

        if (unifiedTetherManager == null)
        {
            unifiedTetherManager =
                FindBehaviourByTypeName("MIMISKUnifiedTetherManager");
        }

        if (smartWinchController == null)
        {
            smartWinchController =
                FindBehaviourByTypeName("MIMISKTetherSmartWinchController");
        }
    }

    [ContextMenu("Send One Telemetry Frame")]
    public async void SendTelemetry()
    {
        if (sendInProgress)
        {
            return;
        }

        if (connection == null ||
            connection.Client == null ||
            !connection.isConnected)
        {
            lastStatus =
                "not_connected";

            return;
        }

        sendInProgress =
            true;

        try
        {
            if (autoFindReferences &&
                (droneRoot == null || miniRovRoot == null))
            {
                AutoFindReferences();
            }

            MimiskTelemetryFrame frame =
                BuildTelemetryFrame();

            MimiskTelemetryAck ack =
                await connection.Client.SendTelemetryAsync(frame);

            if (ack.Accepted)
            {
                framesSent++;
                lastStatus =
                    ack.Message;
            }
            else
            {
                framesFailed++;
                lastStatus =
                    "rejected: " + ack.Message;
            }
        }
        catch (Exception ex)
        {
            framesFailed++;

            if (connection != null)
            {
                connection.isConnected =
                    false;
            }

            lastStatus =
                ex.GetType().Name + ": " + ex.Message;

            Debug.LogWarning(
                "[MIMISK gRPC] Telemetry send failed: " +
                lastStatus
            );
        }
        finally
        {
            sendInProgress =
                false;
        }
    }

    private MimiskTelemetryFrame BuildTelemetryFrame()
    {
        double simTime =
            Time.time;

        MimiskHeader header =
            new MimiskHeader
            {
                SimTimeSec = simTime,
                Seq = sequence++,
                FrameId = "mimisk_world"
            };

        return
            new MimiskTelemetryFrame
            {
                Header = header,
                Drone = BuildDroneState(header),
                Minirov = BuildMiniROVState(header),
                Tether = BuildTetherState(header)
            };
    }

    private MimiskDroneState BuildDroneState(MimiskHeader header)
    {
        Vector3 position =
            droneRigidbody != null
                ? droneRigidbody.position
                : (droneRoot != null ? droneRoot.position : Vector3.zero);

        Vector3 velocity =
            GetRigidbodyLinearVelocity(droneRigidbody);

        float yawDeg =
            droneRoot != null
                ? droneRoot.eulerAngles.y
                : 0.0f;

        return
            new MimiskDroneState
            {
                Header = header,
                Position = ToGrpcVec(position),
                Velocity = ToGrpcVec(velocity),
                YawDeg = yawDeg,
                FlightMode = ReadString(droneFlightModeManager, "flightMode", "missing"),
                MissionState = ReadString(droneMissionManager, "missionState", "missing"),
                ControlMode = ReadString(droneCoreController, "controlMode", "missing")
            };
    }

    private MimiskMiniROVState BuildMiniROVState(MimiskHeader header)
    {
        Vector3 position =
            miniRovRigidbody != null
                ? miniRovRigidbody.position
                : (miniRovRoot != null ? miniRovRoot.position : Vector3.zero);

        Vector3 velocity =
            GetRigidbodyLinearVelocity(miniRovRigidbody);

        float yawDeg =
            miniRovRoot != null
                ? miniRovRoot.eulerAngles.y
                : 0.0f;

        float waterY =
            ReadFloat(unifiedTetherManager, "waterSurfaceY", 0.0f);

        if (Mathf.Approximately(waterY, 0.0f))
        {
            waterY =
                ReadFloat(miniRovController, "waterLevelY", 0.0f);
        }

        float depthM =
            Mathf.Max(0.0f, waterY - position.y);

        return
            new MimiskMiniROVState
            {
                Header = header,
                Position = ToGrpcVec(position),
                Velocity = ToGrpcVec(velocity),
                YawDeg = yawDeg,
                DepthM = depthM,
                MissionState = ReadString(miniRovMissionManager, "missionState", "missing"),
                PathType = ReadString(miniRovPathPlanner, "selectedPathType", "missing"),
                ControlMode = ReadString(miniRovController, "controlMode", "missing"),
                DistanceToHomeM = ReadFloat(miniRovMissionManager, "distanceToHomeM", float.NaN)
            };
    }

    private MimiskTetherState BuildTetherState(MimiskHeader header)
    {
        return
            new MimiskTetherState
            {
                Header = header,

                UnifiedState = ReadString(unifiedTetherManager, "tetherState", "missing"),
                LowLevelState = ReadString(lowLevelTetherManager, "tetherState", "missing"),

                DeployedLengthM = ReadFloat(lowLevelTetherManager, "deployedLengthM", float.NaN),
                TargetLengthM = ReadFloat(lowLevelTetherManager, "targetLengthM", float.NaN),
                WinchRateMS = ReadFloat(lowLevelTetherManager, "winchCommandRateMS", float.NaN),
                StraightDistanceM = ReadFloat(lowLevelTetherManager, "straightDistanceM", float.NaN),
                SlackM = ReadFloat(lowLevelTetherManager, "slackM", float.NaN),
                StretchM = ReadFloat(lowLevelTetherManager, "stretchM", float.NaN),
                TensionN = ReadFloat(lowLevelTetherManager, "tensionN", float.NaN),

                TetherStart = ToGrpcVec(ReadVector3(lowLevelTetherManager, "tetherStartWorld", Vector3.zero)),
                TetherEnd = ToGrpcVec(ReadVector3(lowLevelTetherManager, "tetherEndWorld", Vector3.zero)),

                DroneMotionAllowed = ReadBool(unifiedTetherManager, "droneMotionAllowed", true),
                SafeToDeploy = ReadBool(unifiedTetherManager, "safeToDeploy", false)
            };
    }

    private MimiskVec3 ToGrpcVec(Vector3 v)
    {
        return
            new MimiskVec3
            {
                X = v.x,
                Y = v.y,
                Z = v.z
            };
    }

    private Vector3 GetRigidbodyLinearVelocity(Rigidbody rb)
    {
        if (rb == null)
        {
            return Vector3.zero;
        }

#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    private MonoBehaviour FindBehaviourByTypeName(string typeName)
    {
        MonoBehaviour[] behaviours =
            Resources.FindObjectsOfTypeAll<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour b =
                behaviours[i];

            if (b == null ||
                b.gameObject == null ||
                !b.gameObject.scene.IsValid())
            {
                continue;
            }

            if (b.GetType().Name == typeName)
            {
                return b;
            }
        }

        return null;
    }

    private object GetMember(object target, string name)
    {
        if (target == null || string.IsNullOrEmpty(name))
        {
            return null;
        }

        Type type =
            target.GetType();

        FieldInfo field =
            type.GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

        if (field != null)
        {
            return field.GetValue(target);
        }

        PropertyInfo prop =
            type.GetProperty(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

        if (prop != null && prop.CanRead)
        {
            return prop.GetValue(target, null);
        }

        return null;
    }

    private string ReadString(object target, string member, string fallback)
    {
        object value =
            GetMember(target, member);

        return
            value != null
                ? value.ToString()
                : fallback;
    }

    private float ReadFloat(object target, string member, float fallback)
    {
        object value =
            GetMember(target, member);

        if (value is float)
        {
            return (float)value;
        }

        if (value is double)
        {
            return (float)(double)value;
        }

        if (value is int)
        {
            return (int)value;
        }

        return fallback;
    }

    private bool ReadBool(object target, string member, bool fallback)
    {
        object value =
            GetMember(target, member);

        if (value is bool)
        {
            return (bool)value;
        }

        return fallback;
    }

    private Vector3 ReadVector3(object target, string member, Vector3 fallback)
    {
        object value =
            GetMember(target, member);

        if (value is Vector3)
        {
            return (Vector3)value;
        }

        return fallback;
    }
}
