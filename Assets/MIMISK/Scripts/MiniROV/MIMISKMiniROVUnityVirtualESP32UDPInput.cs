using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKMiniROVUnityVirtualESP32UDPInput : MonoBehaviour
{
    public enum GamepadAxisSource
    {
        None,
        LeftStickX,
        LeftStickY,
        RightStickX,
        RightStickY,
        TriggerDifference
    }

    [Header("References")]
    public UnityVirtualESP32 unityVirtualESP32;
    public ControlManager controlManager;
    public MIMISKMiniROVMissionManager missionManager;
    public Rigidbody rb;

    [Header("UDP")]
    public bool receiverEnabled = true;
    public string listenAddress = "0.0.0.0";
    public int listenPort = MIMISKNetworkPorts.MiniROVUnityDirectUdp;
    public float inputTimeoutS = 0.75f;

    [Header("Scene-13 Compatibility")]
    [Tooltip("Keep UnityVirtualESP32 enabled for sensor emulation, but stop its serial bridge so UDP is the motor RX source.")]
    public bool stopUnityVirtualESP32SerialBridge = true;

    [Tooltip("Only allow commands after M enters MiniROV GamepadManualTest.")]
    public bool requireGamepadMissionState = true;

    [Header("Raw pc_sender_new.py Mapping")]
    public GamepadAxisSource rawSurgeAxis = GamepadAxisSource.LeftStickY;
    public GamepadAxisSource rawYawAxis = GamepadAxisSource.RightStickX;
    public GamepadAxisSource rawDepthAxis = GamepadAxisSource.TriggerDifference;

    public bool invertRawSurgeAxis = true;
    public bool invertRawYawAxis = false;
    public bool invertRawDepthAxis = false;

    public float surgeScale = 1.0f;
    public float yawScale = 1.0f;
    public float depthScale = 1.0f;

    [Header("Motor Frame Mapping")]
    [Range(0, 255)] public int maxThrusterPwm = 190;
    [Range(0, 255)] public int maxDcPwm = 160;

    public bool mapDepthToDc = false;

    public bool swapLeftRight = false;
    public bool invertLeftThruster = false;
    public bool invertRightThruster = false;
    public bool invertBothThrusters = false;

    [Header("Runtime Packet")]
    public bool udpThreadRunning;
    public bool connected;
    public bool packetFresh;
    public float lastPacketAgeS;
    public int packetsReceived;
    public int motorFramesInjected;
    public string lastJson = "";
    public string lastEvent = "idle";

    [Header("Raw Axes")]
    public float lx;
    public float ly;
    public float rx;
    public float ry;
    public float lt;
    public float rt;
    public int hatx;
    public int haty;

    [Header("Decoded Commands")]
    public bool allowedByMissionState;
    public float surge;
    public float yaw;
    public float depth;
    public bool hold;
    public bool returnHome;
    public bool stop;

    [Header("Injected Motor Frame")]
    public short leftThruster;
    public short rightThruster;
    public short dcPort;
    public short dcStarboard;
    public bool injectedThisFixedUpdate;

    private UdpClient udp;
    private Thread thread;
    private bool running;

    private readonly object lockObj = new object();
    private string latestJson = "";
    private float lastPacketTime = -999.0f;

    private bool previousReturnHome;
    private bool previousStop;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void OnEnable()
    {
        AutoFindReferences();
        StartReceiver();
    }

    private void OnDisable()
    {
        StopReceiver();
        InjectMotorFrame(0, 0, 0, 0, "receiver_disabled");
    }

    private void OnDestroy()
    {
        StopReceiver();
    }

    private void Update()
    {
        if (!receiverEnabled)
        {
            return;
        }

        AutoFindReferences();

        string json = null;

        lock (lockObj)
        {
            if (!string.IsNullOrEmpty(latestJson))
            {
                json = latestJson;
                latestJson = "";
            }
        }

        if (!string.IsNullOrEmpty(json))
        {
            lastJson = json;
            lastPacketTime = Time.time;
            connected = true;
            packetsReceived++;
            ParsePayload(json);
        }

        lastPacketAgeS = Time.time - lastPacketTime;
        packetFresh = connected && lastPacketAgeS <= inputTimeoutS;

        allowedByMissionState = IsAllowedByMissionState();

        HandleButtons();
    }

    private void FixedUpdate()
    {
        injectedThisFixedUpdate = false;

        if (!receiverEnabled)
        {
            InjectMotorFrame(0, 0, 0, 0, "receiver_disabled");
            return;
        }

        if (!packetFresh)
        {
            InjectMotorFrame(0, 0, 0, 0, "udp_timeout_or_no_packet");
            return;
        }

        if (!allowedByMissionState)
        {
            InjectMotorFrame(0, 0, 0, 0, "blocked_waiting_for_M_GamepadManualTest");
            return;
        }

        PrepareBackend();

        if (hold)
        {
            surge = 0.0f;
            yaw = 0.0f;
            depth = 0.0f;
        }

        float left = surge - yaw;
        float right = surge + yaw;

        left = Mathf.Clamp(left, -1.0f, 1.0f);
        right = Mathf.Clamp(right, -1.0f, 1.0f);

        int leftInt = Mathf.RoundToInt(left * maxThrusterPwm);
        int rightInt = Mathf.RoundToInt(right * maxThrusterPwm);

        if (swapLeftRight)
        {
            int tmp = leftInt;
            leftInt = rightInt;
            rightInt = tmp;
        }

        if (invertBothThrusters)
        {
            leftInt = -leftInt;
            rightInt = -rightInt;
        }

        if (invertLeftThruster)
        {
            leftInt = -leftInt;
        }

        if (invertRightThruster)
        {
            rightInt = -rightInt;
        }

        int dc = mapDepthToDc
            ? Mathf.RoundToInt(depth * maxDcPwm)
            : 0;

        InjectMotorFrame(
            (short)Mathf.Clamp(leftInt, -255, 255),
            (short)Mathf.Clamp(rightInt, -255, 255),
            (short)Mathf.Clamp(dc, -255, 255),
            (short)Mathf.Clamp(dc, -255, 255),
            "udp_to_virtual_esp32_motor_frame"
        );
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (unityVirtualESP32 == null)
        {
            unityVirtualESP32 = GetComponent<UnityVirtualESP32>();
        }

        if (controlManager == null)
        {
            controlManager = GetComponent<ControlManager>();
        }

        if (missionManager == null)
        {
            missionManager = GetComponent<MIMISKMiniROVMissionManager>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }

    private void PrepareBackend()
    {
        if (unityVirtualESP32 != null)
        {
            unityVirtualESP32.enabled = true;
            unityVirtualESP32.autoOpenOnStart = false;

            if (stopUnityVirtualESP32SerialBridge)
            {
                unityVirtualESP32.StopBridge();
            }
        }

        if (controlManager != null)
        {
            controlManager.enabled = true;
            controlManager.autoOpenOnStart = false;

            if (rb != null)
            {
                controlManager.rb = rb;
            }

            if (controlManager.leftThruster == null)
            {
                controlManager.leftThruster = FindDeepChild(transform, "propulseur_gauche");
            }

            if (controlManager.rightThruster == null)
            {
                controlManager.rightThruster = FindDeepChild(transform, "propulseur_droite");
            }
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp();
        }
    }

    private void InjectMotorFrame(short left, short right, short dc1, short dc2, string eventText)
    {
        leftThruster = left;
        rightThruster = right;
        dcPort = dc1;
        dcStarboard = dc2;

        if (controlManager != null)
        {
            controlManager.InjectMotorFrame(left, right, dc1, dc2);
        }

        if (unityVirtualESP32 != null)
        {
            unityVirtualESP32.lastLeftThruster = left;
            unityVirtualESP32.lastRightThruster = right;
            unityVirtualESP32.lastDcPort = dc1;
            unityVirtualESP32.lastDcStarboard = dc2;
            unityVirtualESP32.motorRxConnected = left != 0 || right != 0 || dc1 != 0 || dc2 != 0 || packetFresh;
            unityVirtualESP32.motorRxHz = packetFresh ? Mathf.Max(1.0f, 1.0f / Mathf.Max(Time.fixedDeltaTime, 0.0001f)) : 0.0f;
        }

        injectedThisFixedUpdate = left != 0 || right != 0 || dc1 != 0 || dc2 != 0;

        if (injectedThisFixedUpdate)
        {
            motorFramesInjected++;
        }

        lastEvent =
            eventText +
            "_L_" + left +
            "_R_" + right +
            "_surge_" + surge.ToString("F2") +
            "_yaw_" + yaw.ToString("F2");
    }

    [ContextMenu("Start Receiver")]
    public void StartReceiver()
    {
        if (!receiverEnabled || running)
        {
            return;
        }

        try
        {
            IPAddress ip =
                string.IsNullOrEmpty(listenAddress) ||
                listenAddress == "0.0.0.0"
                    ? IPAddress.Any
                    : IPAddress.Parse(listenAddress);

            udp = new UdpClient(new IPEndPoint(ip, listenPort));

            running = true;

            thread = new Thread(ReceiveLoop);
            thread.IsBackground = true;
            thread.Start();

            udpThreadRunning = true;
            lastEvent = "udp_receiver_started_" + listenAddress + ":" + listenPort;

            Debug.Log("[MIMISK MiniROV ESP32 UDP Input] Listening on " + listenAddress + ":" + listenPort);
        }
        catch (Exception e)
        {
            running = false;
            udpThreadRunning = false;
            lastEvent = "udp_start_failed_" + e.Message;
            Debug.LogError("[MIMISK MiniROV ESP32 UDP Input] " + lastEvent);
        }
    }

    [ContextMenu("Stop Receiver")]
    public void StopReceiver()
    {
        running = false;
        udpThreadRunning = false;

        try
        {
            if (udp != null)
            {
                udp.Close();
                udp = null;
            }
        }
        catch
        {
        }

        try
        {
            if (thread != null && thread.IsAlive)
            {
                thread.Join(100);
            }
        }
        catch
        {
        }

        thread = null;
    }

    private void ReceiveLoop()
    {
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                byte[] data = udp.Receive(ref remote);
                string json = Encoding.UTF8.GetString(data);

                lock (lockObj)
                {
                    latestJson = json;
                }
            }
            catch
            {
                if (!running)
                {
                    break;
                }
            }
        }

        udpThreadRunning = false;
    }

    private void ParsePayload(string json)
    {
        lx = ExtractFloat(json, "lx", lx);
        ly = ExtractFloat(json, "ly", ly);
        rx = ExtractFloat(json, "rx", rx);
        ry = ExtractFloat(json, "ry", ry);
        lt = ExtractFloat(json, "lt", lt);
        rt = ExtractFloat(json, "rt", rt);
        hatx = Mathf.RoundToInt(ExtractFloat(json, "hatx", hatx));
        haty = Mathf.RoundToInt(ExtractFloat(json, "haty", haty));

        surge = ReadRawAxis(rawSurgeAxis);
        yaw = ReadRawAxis(rawYawAxis);
        depth = ReadRawAxis(rawDepthAxis);

        if (invertRawSurgeAxis)
        {
            surge = -surge;
        }

        if (invertRawYawAxis)
        {
            yaw = -yaw;
        }

        if (invertRawDepthAxis)
        {
            depth = -depth;
        }

        surge = Mathf.Clamp(surge * surgeScale, -1.0f, 1.0f);
        yaw = Mathf.Clamp(yaw * yawScale, -1.0f, 1.0f);
        depth = Mathf.Clamp(depth * depthScale, -1.0f, 1.0f);

        hold =
            ExtractButton(json, "btn_south") ||
            ExtractNestedButton(json, "BTN_SOUTH") ||
            ExtractNestedButton(json, "BTN_A");

        returnHome =
            ExtractButton(json, "btn_north") ||
            ExtractNestedButton(json, "BTN_NORTH") ||
            ExtractNestedButton(json, "BTN_Y");

        stop =
            ExtractButton(json, "btn_east") ||
            ExtractNestedButton(json, "BTN_EAST") ||
            ExtractNestedButton(json, "BTN_B");
    }

    private float ReadRawAxis(GamepadAxisSource axis)
    {
        if (axis == GamepadAxisSource.LeftStickX) return lx;
        if (axis == GamepadAxisSource.LeftStickY) return ly;
        if (axis == GamepadAxisSource.RightStickX) return rx;
        if (axis == GamepadAxisSource.RightStickY) return ry;
        if (axis == GamepadAxisSource.TriggerDifference) return rt - lt;
        return 0.0f;
    }

    private bool IsAllowedByMissionState()
    {
        if (!requireGamepadMissionState)
        {
            return true;
        }

        if (missionManager == null)
        {
            return false;
        }

        return
            missionManager.missionActive &&
            missionManager.missionState ==
                MIMISKMiniROVMissionManager.MiniROVMissionState.RunningGamepadTest;
    }

    private void HandleButtons()
    {
        bool returnHomePressed = returnHome;
        bool stopPressed = stop;

        if (returnHomePressed && !previousReturnHome)
        {
            if (missionManager != null)
            {
                missionManager.RequestReturnToRecovery();
                lastEvent = "udp_requested_return_home";
            }
        }

        if (stopPressed && !previousStop)
        {
            if (missionManager != null)
            {
                missionManager.StopMissionAndSetRecoveryReady();
                lastEvent = "udp_requested_stop_recovery_ready";
            }
        }

        previousReturnHome = returnHomePressed;
        previousStop = stopPressed;
    }

    private float ExtractFloat(string json, string name, float fallback)
    {
        Match m = Regex.Match(
            json,
            "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?(?:[eE][-+]?\\d+)?)",
            RegexOptions.IgnoreCase
        );

        if (!m.Success)
        {
            return fallback;
        }

        float value;

        if (float.TryParse(
                m.Groups[1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value))
        {
            return value;
        }

        return fallback;
    }

    private bool ExtractButton(string json, string name)
    {
        return Mathf.Abs(ExtractFloat(json, name, 0.0f)) > 0.5f;
    }

    private bool ExtractNestedButton(string json, string buttonName)
    {
        Match m = Regex.Match(
            json,
            "\\\"" + Regex.Escape(buttonName) + "\\\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)",
            RegexOptions.IgnoreCase
        );

        if (!m.Success)
        {
            return false;
        }

        float value;

        if (float.TryParse(
                m.Groups[1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value))
        {
            return Mathf.Abs(value) > 0.5f;
        }

        return false;
    }

    private Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
