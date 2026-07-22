using System;
using System.Reflection;
using UnityEngine;

using MimiskManualBatch = MIMISK.Grpc.ManualControlBatch;
using MimiskManualCommand = MIMISK.Grpc.ManualControlCommand;
using MimiskManualRequest = MIMISK.Grpc.ManualControlRequest;

[DefaultExecutionOrder(2575)]
[DisallowMultipleComponent]
public class MIMISKGrpcManualControlBridge : MonoBehaviour
{
    [Header("Connection")]
    public MIMISKGrpcConnection connection;
    public bool manualBridgeEnabled = true;
    public float pollHz = 50.0f;
    public string simId = "mimisk_unity_v2";

    [Header("Safety / Mode Gates")]
    public bool applyManualCommands = true;

    [Tooltip("Drone movement axes are applied only when FlightMode=Gamepad or ControlMode=ManualGamepad.")]
    public bool requireDroneGamepadMode = true;

    [Tooltip("Button edges may still request Gamepad/Hold/Takeoff/Land mode through the existing drone manager.")]
    public bool allowDroneButtonsOutsideGamepad = true;

    [Tooltip("MiniROV movement axes are applied only when MissionState=ManualDirect or RunningGamepadTest.")]
    public bool requireMiniRovManualMode = true;

    [Tooltip("MiniROV buttons are also blocked unless MiniROV is in manual mode.")]
    public bool allowMiniRovButtonsOutsideManual = false;

    [Header("Drone")]
    public MonoBehaviour droneCoreGamepadReceiver;
    public MonoBehaviour droneFlightModeManager;
    public MonoBehaviour droneCoreController;

    [Header("MiniROV")]
    public MonoBehaviour miniRovMissionManager;
    public MonoBehaviour miniRovDirectBypassInput;
    public MonoBehaviour miniRovUdpGamepadReceiver;
    public MonoBehaviour miniRovCoreController;

    [Header("Runtime")]
    public bool autoFindReferences = true;
    public bool pollInProgress;
    public int manualFramesReceived;
    public int manualFramesApplied;
    public int manualFramesBlocked;
    public string lastStatus = "idle";
    public string lastDroneManual = "none";
    public string lastMiniRovManual = "none";

    private float timerS;

    private ButtonLatch droneLatch = new ButtonLatch();
    private ButtonLatch rovLatch = new ButtonLatch();

    private class ButtonLatch
    {
        public bool a;
        public bool b;
        public bool x;
        public bool y;
        public bool lb;
        public bool rb;
        public bool back;
        public bool start;

        public bool aDown;
        public bool bDown;
        public bool xDown;
        public bool yDown;
        public bool lbDown;
        public bool rbDown;
        public bool backDown;
        public bool startDown;

        public void Update(MimiskManualCommand cmd)
        {
            bool na = cmd.ButtonA;
            bool nb = cmd.ButtonB;
            bool nx = cmd.ButtonX;
            bool ny = cmd.ButtonY;
            bool nlb = cmd.ButtonLb;
            bool nrb = cmd.ButtonRb;
            bool nback = cmd.ButtonBack;
            bool nstart = cmd.ButtonStart;

            aDown = na && !a;
            bDown = nb && !b;
            xDown = nx && !x;
            yDown = ny && !y;
            lbDown = nlb && !lb;
            rbDown = nrb && !rb;
            backDown = nback && !back;
            startDown = nstart && !start;

            a = na;
            b = nb;
            x = nx;
            y = ny;
            lb = nlb;
            rb = nrb;
            back = nback;
            start = nstart;
        }
    }

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
            connection = GetComponent<MIMISKGrpcConnection>();
        }

        if (connection == null)
        {
            connection = FindFirstObjectByType<MIMISKGrpcConnection>();
        }

        if (autoFindReferences)
        {
            AutoFindReferences();
        }
    }

    private void Update()
    {
        if (!manualBridgeEnabled)
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
            1.0f / Mathf.Max(1.0f, pollHz);

        if (timerS >= period)
        {
            timerS -= period;
            PollManualControls();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (connection == null)
        {
            connection = GetComponent<MIMISKGrpcConnection>();

            if (connection == null)
            {
                connection = FindFirstObjectByType<MIMISKGrpcConnection>();
            }
        }

        if (droneCoreGamepadReceiver == null)
        {
            droneCoreGamepadReceiver = FindBehaviourByTypeName("MIMISKDroneCoreGamepadReceiver");
        }

        if (droneFlightModeManager == null)
        {
            droneFlightModeManager = FindBehaviourByTypeName("MIMISKDroneCoreFlightModeManager");
        }

        if (droneCoreController == null)
        {
            droneCoreController = FindBehaviourByTypeName("MIMISKDroneCoreRotorController");
        }

        if (miniRovMissionManager == null)
        {
            miniRovMissionManager = FindBehaviourByTypeName("MIMISKMiniROVMissionManager");
        }

        if (miniRovDirectBypassInput == null)
        {
            miniRovDirectBypassInput = FindBehaviourByTypeName("MIMISKMiniROVDirectRaspberryBypassInput");
        }

        if (miniRovUdpGamepadReceiver == null)
        {
            miniRovUdpGamepadReceiver = FindBehaviourByTypeName("MIMISKMiniROVUDPGamepadReceiver");
        }

        if (miniRovCoreController == null)
        {
            miniRovCoreController = FindBehaviourByTypeName("MIMISKMiniROVCoreController");
        }
    }

    [ContextMenu("Poll Manual Controls Once")]
    public async void PollManualControls()
    {
        if (pollInProgress)
        {
            return;
        }

        if (connection == null ||
            connection.Client == null ||
            !connection.isConnected)
        {
            lastStatus = "not_connected";
            return;
        }

        pollInProgress = true;

        try
        {
            MimiskManualBatch batch =
                await connection.Client.GetManualControlsAsync(
                    new MimiskManualRequest
                    {
                        SimId = simId
                    }
                );

            if (batch == null || batch.Commands == null)
            {
                return;
            }

            manualFramesReceived += batch.Commands.Count;

            for (int i = 0; i < batch.Commands.Count; i++)
            {
                ApplyManualCommand(batch.Commands[i]);
            }
        }
        catch (Exception ex)
        {
            lastStatus =
                ex.GetType().Name + ": " + ex.Message;

            Debug.LogWarning(
                "[MIMISK gRPC] Manual poll failed: " +
                lastStatus
            );

            if (connection != null)
            {
                connection.isConnected = false;
            }
        }
        finally
        {
            pollInProgress = false;
        }
    }

    private void ApplyManualCommand(MimiskManualCommand cmd)
    {
        if (cmd == null || !cmd.Enabled)
        {
            return;
        }

        if (!applyManualCommands)
        {
            lastStatus = "manual_received_apply_disabled";
            return;
        }

        if (autoFindReferences)
        {
            AutoFindReferences();
        }

        string target = Normalize(cmd.TargetAgent);

        if (target == "drone")
        {
            ApplyDroneManual(cmd);
            return;
        }

        if (target == "minirov" || target == "rov")
        {
            ApplyMiniRovManual(cmd);
            return;
        }
    }

    private void ApplyDroneManual(MimiskManualCommand cmd)
    {
        droneLatch.Update(cmd);

        bool inManual =
            IsDroneInGamepadMode();

        if (allowDroneButtonsOutsideGamepad || inManual)
        {
            ApplyDroneButtonEdges(droneLatch);
            ApplyDroneButtonFields(cmd, droneLatch);
        }

        Vector4 movement =
            Vector4.zero;

        if (!requireDroneGamepadMode || inManual)
        {
            movement =
                new Vector4(
                    Clamp((float)cmd.Ly),  // forward
                    Clamp((float)cmd.Lx),  // right
                    Clamp((float)cmd.Rx),  // yaw
                    Clamp((float)cmd.Ry)   // altitude
                );

            manualFramesApplied++;
        }
        else
        {
            manualFramesBlocked++;
            lastStatus = "drone_axes_blocked_not_gamepad_mode";
        }

        ApplyDroneMovement(movement);

        lastDroneManual =
            "mode=" + ReadString(droneFlightModeManager, "flightMode", "missing") +
            " fwd=" + movement.x.ToString("F2") +
            " right=" + movement.y.ToString("F2") +
            " yaw=" + movement.z.ToString("F2") +
            " alt=" + movement.w.ToString("F2");
    }

    private void ApplyDroneMovement(Vector4 movement)
    {
        if (droneCoreGamepadReceiver == null)
        {
            return;
        }

        SetFloatField(droneCoreGamepadReceiver, "lx", movement.y);
        SetFloatField(droneCoreGamepadReceiver, "ly", movement.x);
        SetFloatField(droneCoreGamepadReceiver, "rx", movement.z);
        SetFloatField(droneCoreGamepadReceiver, "ry", movement.w);

        SetVector4Field(droneCoreGamepadReceiver, "commandForwardRightYawAlt", movement);
        SetStringField(droneCoreGamepadReceiver, "lastPayload", "ros2_grpc_manual");
        SetFloatField(droneCoreGamepadReceiver, "lastPacketTime", Time.time);
        IncrementIntField(droneCoreGamepadReceiver, "packetCount");
    }

    private void ApplyDroneButtonEdges(ButtonLatch latch)
    {
        if (latch.lbDown || latch.rbDown)
        {
            CallFirst(droneFlightModeManager, "EnterGamepadMode", "StartGamepad", "SetGamepadMode");
        }

        if (latch.xDown)
        {
            CallFirst(droneFlightModeManager, "CapturePositionHold");
        }

        if (latch.yDown)
        {
            CallFirst(droneFlightModeManager, "StartTakeoff");
        }

        if (latch.bDown)
        {
            CallFirst(droneFlightModeManager, "StartLandingOnSurface");
        }

        if (latch.backDown || latch.startDown)
        {
            CallFirst(droneFlightModeManager, "EnterFailsafe");
        }

        if (latch.aDown)
        {
            CallFirst(droneFlightModeManager, "EnterTakeoffIdle");
        }
    }

    private void ApplyDroneButtonFields(MimiskManualCommand cmd, ButtonLatch latch)
    {
        if (droneCoreGamepadReceiver == null)
        {
            return;
        }

        SetBoolField(droneCoreGamepadReceiver, "buttonA", cmd.ButtonA);
        SetBoolField(droneCoreGamepadReceiver, "buttonB", cmd.ButtonB);
        SetBoolField(droneCoreGamepadReceiver, "buttonX", cmd.ButtonX);
        SetBoolField(droneCoreGamepadReceiver, "buttonY", cmd.ButtonY);
        SetBoolField(droneCoreGamepadReceiver, "buttonLB", cmd.ButtonLb);
        SetBoolField(droneCoreGamepadReceiver, "buttonRB", cmd.ButtonRb);
        SetBoolField(droneCoreGamepadReceiver, "buttonBack", cmd.ButtonBack);
        SetBoolField(droneCoreGamepadReceiver, "buttonStart", cmd.ButtonStart);

        SetBoolField(droneCoreGamepadReceiver, "buttonADown", latch.aDown);
        SetBoolField(droneCoreGamepadReceiver, "buttonBDown", latch.bDown);
        SetBoolField(droneCoreGamepadReceiver, "buttonXDown", latch.xDown);
        SetBoolField(droneCoreGamepadReceiver, "buttonYDown", latch.yDown);
        SetBoolField(droneCoreGamepadReceiver, "buttonLBDown", latch.lbDown);
        SetBoolField(droneCoreGamepadReceiver, "buttonRBDown", latch.rbDown);
        SetBoolField(droneCoreGamepadReceiver, "buttonBackDown", latch.backDown);
        SetBoolField(droneCoreGamepadReceiver, "buttonStartDown", latch.startDown);

        SetBoolField(droneCoreGamepadReceiver, "armDown", latch.aDown);
        SetBoolField(droneCoreGamepadReceiver, "idleDown", latch.aDown);
        SetBoolField(droneCoreGamepadReceiver, "takeoffDown", latch.yDown);
        SetBoolField(droneCoreGamepadReceiver, "holdDown", latch.xDown);
        SetBoolField(droneCoreGamepadReceiver, "manualDown", latch.lbDown || latch.rbDown);
        SetBoolField(droneCoreGamepadReceiver, "landDown", latch.bDown);
        SetBoolField(droneCoreGamepadReceiver, "disarmDown", latch.backDown);
        SetBoolField(droneCoreGamepadReceiver, "failsafeDown", latch.startDown);
        SetBoolField(droneCoreGamepadReceiver, "abortDown", latch.backDown || latch.startDown);
    }

    private void ApplyMiniRovManual(MimiskManualCommand cmd)
    {
        rovLatch.Update(cmd);

        bool inManual =
            IsMiniRovInManualMode();

        if (allowMiniRovButtonsOutsideManual || inManual)
        {
            ApplyMiniRovButtonEdges(rovLatch);
        }

        if (requireMiniRovManualMode && !inManual)
        {
            manualFramesBlocked++;
            lastStatus = "minirov_axes_blocked_not_manual_direct_mode";
            lastMiniRovManual = "blocked missionState=" + ReadString(miniRovMissionManager, "missionState", "missing");
            return;
        }

        float surge =
            Clamp((float)cmd.Ly);

        float yaw =
            Clamp((float)cmd.Rx);

        float depth =
            Clamp((float)(cmd.Rt - cmd.Lt));

        bool hold =
            cmd.ButtonA;

        if (hold)
        {
            surge = 0.0f;
            yaw = 0.0f;
            depth = 0.0f;
        }

        ApplyMiniRovDirectBypass(surge, yaw, depth, hold);
        ApplyMiniRovUdpReceiverMirror(surge, yaw, depth, hold, cmd.ButtonY, cmd.ButtonB);

        CallMethod3Float(
            miniRovCoreController,
            "SetManualCommand",
            surge,
            yaw,
            depth
        );

        manualFramesApplied++;

        lastMiniRovManual =
            "state=" + ReadString(miniRovMissionManager, "missionState", "missing") +
            " surge=" + surge.ToString("F2") +
            " yaw=" + yaw.ToString("F2") +
            " depth=" + depth.ToString("F2") +
            " hold=" + hold;
    }

    private void ApplyMiniRovButtonEdges(ButtonLatch latch)
    {
        if (latch.yDown)
        {
            CallFirst(miniRovMissionManager, "RequestReturnToRecovery", "ReturnHome");
        }

        if (latch.bDown)
        {
            CallFirst(miniRovMissionManager, "StopMissionAndSetRecoveryReady", "PrepareForRecovery");
        }
    }

    private void ApplyMiniRovDirectBypass(float surge, float yaw, float depth, bool hold)
    {
        if (miniRovDirectBypassInput == null)
        {
            return;
        }

        Behaviour b =
            miniRovDirectBypassInput as Behaviour;

        if (b != null && !b.enabled)
        {
            b.enabled = true;
        }

        SetBoolField(miniRovDirectBypassInput, "receiverEnabled", true);
        SetBoolField(miniRovDirectBypassInput, "connected", true);
        SetBoolField(miniRovDirectBypassInput, "packetFresh", true);
        SetBoolField(miniRovDirectBypassInput, "hasReceivedPacket", true);
        SetFloatField(miniRovDirectBypassInput, "lastPacketTime", Time.time);
        SetFloatField(miniRovDirectBypassInput, "lastPacketAgeS", 0.0f);
        IncrementIntField(miniRovDirectBypassInput, "packetsReceived");

        // Feed the same semantic values used by the validated MiniROV sender.
        SetFloatField(miniRovDirectBypassInput, "ly", surge);
        SetFloatField(miniRovDirectBypassInput, "lx", yaw);
        SetFloatField(miniRovDirectBypassInput, "rx", yaw);

        if (depth >= 0.0f)
        {
            SetFloatField(miniRovDirectBypassInput, "rt", depth);
            SetFloatField(miniRovDirectBypassInput, "lt", 0.0f);
        }
        else
        {
            SetFloatField(miniRovDirectBypassInput, "rt", 0.0f);
            SetFloatField(miniRovDirectBypassInput, "lt", -depth);
        }

        SetFloatField(miniRovDirectBypassInput, "throttle", surge);
        SetFloatField(miniRovDirectBypassInput, "yaw", yaw);
        SetFloatField(miniRovDirectBypassInput, "verticalDc", depth);
        SetBoolField(miniRovDirectBypassInput, "holdCommand", hold);
        SetStringField(miniRovDirectBypassInput, "lastJson", "ros2_grpc_manual");
        SetStringField(miniRovDirectBypassInput, "lastEvent", "ros2_grpc_manual_applied");
    }

    private void ApplyMiniRovUdpReceiverMirror(
        float surge,
        float yaw,
        float depth,
        bool hold,
        bool returnHome,
        bool stop)
    {
        if (miniRovUdpGamepadReceiver == null)
        {
            return;
        }

        Behaviour b =
            miniRovUdpGamepadReceiver as Behaviour;

        if (b != null && !b.enabled)
        {
            b.enabled = true;
        }

        SetBoolField(miniRovUdpGamepadReceiver, "receiverEnabled", true);
        SetBoolField(miniRovUdpGamepadReceiver, "connected", true);
        SetBoolField(miniRovUdpGamepadReceiver, "packetFresh", true);
        SetFloatField(miniRovUdpGamepadReceiver, "lastPacketTime", Time.time);
        SetFloatField(miniRovUdpGamepadReceiver, "lastPacketAge", 0.0f);
        IncrementIntField(miniRovUdpGamepadReceiver, "packetsReceived");

        SetFloatField(miniRovUdpGamepadReceiver, "surge", surge);
        SetFloatField(miniRovUdpGamepadReceiver, "yaw", yaw);
        SetFloatField(miniRovUdpGamepadReceiver, "depth", depth);

        SetBoolField(miniRovUdpGamepadReceiver, "hold", hold);
        SetBoolField(miniRovUdpGamepadReceiver, "returnHome", returnHome);
        SetBoolField(miniRovUdpGamepadReceiver, "stop", stop);
        SetBoolField(miniRovUdpGamepadReceiver, "sendingToCore", true);
        SetStringField(miniRovUdpGamepadReceiver, "lastJson", "ros2_grpc_manual");
        SetStringField(miniRovUdpGamepadReceiver, "lastEvent", "ros2_grpc_manual_applied");
    }

    private bool IsDroneInGamepadMode()
    {
        string flight =
            ReadString(droneFlightModeManager, "flightMode", "");

        string control =
            ReadString(droneCoreController, "controlMode", "");

        return
            string.Equals(flight, "Gamepad", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(control, "ManualGamepad", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsMiniRovInManualMode()
    {
        string state =
            ReadString(miniRovMissionManager, "missionState", "");

        bool manualState =
            string.Equals(state, "ManualDirect", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "RunningGamepadTest", StringComparison.OrdinalIgnoreCase);

        bool bypassEnabled =
            ReadBool(miniRovDirectBypassInput, "receiverEnabled", false);

        Behaviour b =
            miniRovDirectBypassInput as Behaviour;

        bool behaviourEnabled =
            b != null && b.enabled;

        return
            manualState ||
            (bypassEnabled && behaviourEnabled);
    }

    private float Clamp(float v)
    {
        return Mathf.Clamp(v, -1.0f, 1.0f);
    }

    private MonoBehaviour FindBehaviourByTypeName(string typeName)
    {
        MonoBehaviour[] behaviours =
            Resources.FindObjectsOfTypeAll<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour b = behaviours[i];

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

    private bool CallFirst(object target, params string[] methodNames)
    {
        if (target == null)
        {
            return false;
        }

        Type type = target.GetType();

        for (int i = 0; i < methodNames.Length; i++)
        {
            MethodInfo method =
                type.GetMethod(
                    methodNames[i],
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );

            if (method == null || method.GetParameters().Length != 0)
            {
                continue;
            }

            try
            {
                method.Invoke(target, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private bool CallMethod3Float(object target, string methodName, float a, float b, float c)
    {
        if (target == null)
        {
            return false;
        }

        MethodInfo method =
            target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

        if (method == null || method.GetParameters().Length != 3)
        {
            return false;
        }

        try
        {
            method.Invoke(target, new object[] { a, b, c });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string ReadString(object target, string name, string fallback)
    {
        object value =
            GetMember(target, name);

        return value != null ? value.ToString() : fallback;
    }

    private bool ReadBool(object target, string name, bool fallback)
    {
        object value =
            GetMember(target, name);

        if (value is bool)
        {
            return (bool)value;
        }

        return fallback;
    }

    private object GetMember(object target, string name)
    {
        if (target == null || string.IsNullOrEmpty(name))
        {
            return null;
        }

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        FieldInfo field =
            target.GetType().GetField(name, flags);

        if (field != null)
        {
            return field.GetValue(target);
        }

        PropertyInfo prop =
            target.GetType().GetProperty(name, flags);

        if (prop != null && prop.CanRead)
        {
            return prop.GetValue(target, null);
        }

        return null;
    }

    private void SetFloatField(object target, string name, float value)
    {
        SetMember(target, name, value, typeof(float));
    }

    private void SetIntField(object target, string name, int value)
    {
        SetMember(target, name, value, typeof(int));
    }

    private void SetBoolField(object target, string name, bool value)
    {
        SetMember(target, name, value, typeof(bool));
    }

    private void SetStringField(object target, string name, string value)
    {
        SetMember(target, name, value, typeof(string));
    }

    private void SetVector4Field(object target, string name, Vector4 value)
    {
        SetMember(target, name, value, typeof(Vector4));
    }

    private void IncrementIntField(object target, string name)
    {
        object current =
            GetMember(target, name);

        int value =
            current is int ? (int)current : 0;

        SetIntField(target, name, value + 1);
    }

    private void SetMember(object target, string name, object value, Type expectedType)
    {
        if (target == null || string.IsNullOrEmpty(name))
        {
            return;
        }

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        FieldInfo field =
            target.GetType().GetField(name, flags);

        if (field != null && field.FieldType == expectedType)
        {
            field.SetValue(target, value);
            return;
        }

        PropertyInfo prop =
            target.GetType().GetProperty(name, flags);

        if (prop != null && prop.CanWrite && prop.PropertyType == expectedType)
        {
            prop.SetValue(target, value, null);
        }
    }

    private string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return
            value.Trim()
                 .Replace("-", "_")
                 .Replace(" ", "_")
                 .ToLowerInvariant();
    }
}
