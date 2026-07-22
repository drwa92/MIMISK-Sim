using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKMiniROVDirectRaspberryBypassInput : MonoBehaviour
{
    [Header("References")]
    public ControlManager controlManager;
    public UnityVirtualESP32 unityVirtualESP32;
    public Rigidbody rb;

    [Header("UDP")]
    public bool receiverEnabled = true;

    [Tooltip("Use 0.0.0.0 for all interfaces, or 127.0.0.1 for local-only.")]
    public string listenAddress = "0.0.0.0";

    [Tooltip("Dedicated direct-Unity MiniROV port. Keep 54321 for Raspberry/my_app.")]
    public int listenPort = 54341;

    [Header("Command Freshness")]
    [Tooltip("OFF mimics Raspberry/my_app: latest gamepad state is held and injected repeatedly.")]
    public bool enableInputTimeout = false;

    public float inputTimeoutS = 2.0f;

    [Header("Backend Ownership")]
    [Tooltip("Stops ControlManager serial reader so this adapter owns motor frames in direct mode.")]
    public bool stopControlManagerSerialReader = true;

    [Tooltip("Stops UnityVirtualESP32 serial bridge so no socat/ESP/my_app process is required.")]
    public bool stopUnityVirtualESP32Bridge = true;

    [Tooltip("Keeps UnityVirtualESP32 enabled for sensor/diagnostic fields, but prevents serial auto-open.")]
    public bool keepUnityVirtualESP32Enabled = true;

    [Header("Exact Raspberry Manual Mapping")]
    public bool useLyForThrottle = true;
    public bool useLxForYaw = true;
    public bool useRtMinusLtForDc = true;

    [Tooltip("Default OFF because Raspberry mapping uses ly directly.")]
    public bool invertThrottle = false;

    public bool invertYaw = false;
    public bool invertDc = false;

    public float throttleScale = 1.0f;
    public float yawScale = 1.0f;
    public float dcScale = 1.0f;

    [Range(0, 255)]
    public int maxThrusterPwm = 255;

    [Range(0, 255)]
    public int maxDcPwm = 255;

    [Header("Motor Frame Options")]
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
    public string lastJson = "";
    public string lastEvent = "idle";

    [Header("Raw Gamepad State")]
    public float lx;
    public float ly;
    public float rx;
    public float ry;
    public float lt;
    public float rt;
    public int hatx;
    public int haty;

    [Header("Decoded Raspberry Commands")]
    public float throttle;
    public float yaw;
    public float verticalDc;
    public bool holdCommand;

    [Header("Injected Motor Frame")]
    public short thrusterPort;
    public short thrusterStarboard;
    public short dcPort;
    public short dcStarboard;

    public bool injectedThisFixedUpdate;
    public int motorFramesInjected;

    private UdpClient udp;
    private Thread receiveThread;
    private bool running;

    private readonly object packetLock = new object();
    private string latestJson = "";
    private float lastPacketTime = -999.0f;
    private bool hasReceivedPacket;

    private MethodInfo controlManagerStopReader;
    private MethodInfo unityVirtualStopBridge;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void OnEnable()
    {
        AutoFindReferences();
        PrepareBackends();
        StartReceiver();
    }

    private void OnDisable()
    {
        InjectMotorFrame(0, 0, 0, 0, "adapter_disabled");
        StopReceiver();
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

        string json = null;

        lock (packetLock)
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
            hasReceivedPacket = true;
            connected = true;
            packetsReceived++;
            ParsePayload(json);
        }

        lastPacketAgeS =
            hasReceivedPacket
                ? Time.time - lastPacketTime
                : 999.0f;

        packetFresh =
            hasReceivedPacket &&
            (
                !enableInputTimeout ||
                lastPacketAgeS <= inputTimeoutS
            );
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
            InjectMotorFrame(0, 0, 0, 0, "no_fresh_packet");
            return;
        }

        PrepareBackends();

        if (holdCommand)
        {
            InjectMotorFrame(0, 0, 0, 0, "hold_button_zero_frame");
            return;
        }

        float left =
            Mathf.Clamp(throttle + yaw, -1.0f, 1.0f);

        float right =
            Mathf.Clamp(throttle - yaw, -1.0f, 1.0f);

        int leftInt =
            Mathf.RoundToInt(left * maxThrusterPwm);

        int rightInt =
            Mathf.RoundToInt(right * maxThrusterPwm);

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

        int dc =
            Mathf.RoundToInt(
                Mathf.Clamp(verticalDc, -1.0f, 1.0f) *
                maxDcPwm
            );

        InjectMotorFrame(
            (short)Mathf.Clamp(leftInt, -255, 255),
            (short)Mathf.Clamp(rightInt, -255, 255),
            (short)Mathf.Clamp(dc, -255, 255),
            (short)Mathf.Clamp(dc, -255, 255),
            "raspberry_bypass_motor_frame"
        );
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (controlManager == null)
        {
            controlManager = GetComponent<ControlManager>();
        }

        if (unityVirtualESP32 == null)
        {
            unityVirtualESP32 = GetComponent<UnityVirtualESP32>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        CacheReflection();
    }

    private void PrepareBackends()
    {
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp();
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
                controlManager.leftThruster =
                    FindDeepChild(transform, "propulseur_gauche");
            }

            if (controlManager.rightThruster == null)
            {
                controlManager.rightThruster =
                    FindDeepChild(transform, "propulseur_droite");
            }

            if (stopControlManagerSerialReader &&
                controlManagerStopReader != null)
            {
                try
                {
                    controlManagerStopReader.Invoke(controlManager, null);
                }
                catch
                {
                }
            }
        }

        if (unityVirtualESP32 != null)
        {
            unityVirtualESP32.autoOpenOnStart = false;
            unityVirtualESP32.enabled = keepUnityVirtualESP32Enabled;

            if (stopUnityVirtualESP32Bridge &&
                unityVirtualStopBridge != null)
            {
                try
                {
                    unityVirtualStopBridge.Invoke(unityVirtualESP32, null);
                }
                catch
                {
                }
            }
        }
    }

    private void CacheReflection()
    {
        if (controlManager != null && controlManagerStopReader == null)
        {
            controlManagerStopReader =
                controlManager.GetType().GetMethod(
                    "StopReader",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );
        }

        if (unityVirtualESP32 != null && unityVirtualStopBridge == null)
        {
            unityVirtualStopBridge =
                unityVirtualESP32.GetType().GetMethod(
                    "StopBridge",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );
        }
    }

    private void InjectMotorFrame(
        short left,
        short right,
        short dc1,
        short dc2,
        string eventText)
    {
        thrusterPort = left;
        thrusterStarboard = right;
        dcPort = dc1;
        dcStarboard = dc2;

        if (controlManager != null)
        {
            controlManager.InjectMotorFrame(left, right, dc1, dc2);
        }

        UpdateUnityVirtualESP32Diagnostics(left, right, dc1, dc2);

        injectedThisFixedUpdate =
            left != 0 ||
            right != 0 ||
            dc1 != 0 ||
            dc2 != 0;

        if (injectedThisFixedUpdate)
        {
            motorFramesInjected++;
        }

        lastEvent =
            eventText +
            " L=" + left +
            " R=" + right +
            " DC=" + dc1 +
            " throttle=" + throttle.ToString("F2") +
            " yaw=" + yaw.ToString("F2");
    }

    private void UpdateUnityVirtualESP32Diagnostics(
        short left,
        short right,
        short dc1,
        short dc2)
    {
        if (unityVirtualESP32 == null)
        {
            return;
        }

        SetFieldOrProperty(unityVirtualESP32, "lastLeftThruster", left);
        SetFieldOrProperty(unityVirtualESP32, "lastRightThruster", right);
        SetFieldOrProperty(unityVirtualESP32, "lastDcPort", dc1);
        SetFieldOrProperty(unityVirtualESP32, "lastDcStarboard", dc2);
        SetFieldOrProperty(unityVirtualESP32, "motorRxConnected", packetFresh);
        SetFieldOrProperty(unityVirtualESP32, "motorRxHz", packetFresh ? 50.0f : 0.0f);
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
            IPAddress address =
                string.IsNullOrEmpty(listenAddress) ||
                listenAddress == "0.0.0.0"
                    ? IPAddress.Any
                    : IPAddress.Parse(listenAddress);

            udp =
                new UdpClient(
                    new IPEndPoint(
                        address,
                        listenPort
                    )
                );

            running = true;
            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            udpThreadRunning = true;
            lastEvent = "udp_receiver_started_" + listenAddress + ":" + listenPort;

            Debug.Log("[MIMISK MiniROV Direct Bypass] Listening on UDP " + listenAddress + ":" + listenPort);
        }
        catch (Exception ex)
        {
            running = false;
            udpThreadRunning = false;
            lastEvent = "udp_start_failed_" + ex.Message;
            Debug.LogError("[MIMISK MiniROV Direct Bypass] " + lastEvent);
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
            if (receiveThread != null &&
                receiveThread.IsAlive)
            {
                receiveThread.Join(100);
            }
        }
        catch
        {
        }

        receiveThread = null;
    }

    private void ReceiveLoop()
    {
        IPEndPoint remote =
            new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                byte[] data =
                    udp.Receive(ref remote);

                string json =
                    Encoding.UTF8.GetString(data);

                lock (packetLock)
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

        throttle =
            useLyForThrottle
                ? ly
                : 0.0f;

        yaw =
            useLxForYaw
                ? lx
                : 0.0f;

        verticalDc =
            useRtMinusLtForDc
                ? rt - lt
                : 0.0f;

        if (invertThrottle)
        {
            throttle = -throttle;
        }

        if (invertYaw)
        {
            yaw = -yaw;
        }

        if (invertDc)
        {
            verticalDc = -verticalDc;
        }

        throttle =
            Mathf.Clamp(throttle * throttleScale, -1.0f, 1.0f);

        yaw =
            Mathf.Clamp(yaw * yawScale, -1.0f, 1.0f);

        verticalDc =
            Mathf.Clamp(verticalDc * dcScale, -1.0f, 1.0f);

        holdCommand =
            ExtractButton(json, "BTN_SOUTH") ||
            ExtractButton(json, "BTN_A") ||
            ExtractButton(json, "btn_south") ||
            ExtractBool(json, "hold");
    }

    private float ExtractFloat(string json, string name, float fallback)
    {
        Match match =
            Regex.Match(
                json,
                "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?(?:[eE][-+]?\\d+)?)",
                RegexOptions.IgnoreCase
            );

        if (!match.Success)
        {
            return fallback;
        }

        float value;

        if (float.TryParse(
                match.Groups[1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value))
        {
            return value;
        }

        return fallback;
    }

    private bool ExtractBool(string json, string name)
    {
        Match match =
            Regex.Match(
                json,
                "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*(true|false|0|1)",
                RegexOptions.IgnoreCase
            );

        if (!match.Success)
        {
            return false;
        }

        string value =
            match.Groups[1].Value.ToLowerInvariant();

        return value == "true" || value == "1";
    }

    private bool ExtractButton(string json, string name)
    {
        return Mathf.Abs(ExtractFloat(json, name, 0.0f)) > 0.5f;
    }

    private void SetFieldOrProperty(object target, string name, object value)
    {
        if (target == null)
        {
            return;
        }

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        FieldInfo field =
            target.GetType().GetField(name, flags);

        if (field != null)
        {
            try
            {
                field.SetValue(target, value);
            }
            catch
            {
            }

            return;
        }

        PropertyInfo prop =
            target.GetType().GetProperty(name, flags);

        if (prop != null && prop.CanWrite)
        {
            try
            {
                prop.SetValue(target, value, null);
            }
            catch
            {
            }
        }
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
            Transform found =
                FindDeepChild(root.GetChild(i), childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
