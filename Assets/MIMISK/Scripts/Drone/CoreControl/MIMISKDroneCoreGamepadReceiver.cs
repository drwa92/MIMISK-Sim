using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKDroneCoreGamepadReceiver : MonoBehaviour
{
    [Header("UDP")]
    public int listenPort = 54331;
    public bool receiverEnabled = true;
    public bool printPayload = false;

    [Header("Axis Mapping")]
    public bool preferSemanticFields = true;

    public float forwardSign = 1.0f;
    public float rightSign = 1.0f;
    public float yawSign = 1.0f;
    public float altitudeSign = 1.0f;

    [Tooltip("Fallback raw mapping: forward = -ly, right = lx, yaw = rx, altitude = -ry.")]
    public bool invertLeftYForForward = true;

    public bool invertRightYForAltitude = true;
    public float deadzone = 0.05f;

    [Header("Runtime Command")]
    public Vector4 commandForwardRightYawAlt;

    public float lx;
    public float ly;
    public float rx;
    public float ry;
    public float lt;
    public float rt;
    public int hatx;
    public int haty;

    [Header("Buttons - Current")]
    public bool buttonA;
    public bool buttonB;
    public bool buttonX;
    public bool buttonY;
    public bool buttonLB;
    public bool buttonRB;
    public bool buttonBack;
    public bool buttonStart;

    [Header("Buttons - Rising Edge")]
    public bool buttonADown;
    public bool buttonBDown;
    public bool buttonXDown;
    public bool buttonYDown;
    public bool buttonLBDown;
    public bool buttonRBDown;
    public bool buttonBackDown;
    public bool buttonStartDown;

    [Header("Semantic Button Edges")]
    public bool armDown;
    public bool idleDown;
    public bool takeoffDown;
    public bool holdDown;
    public bool manualDown;
    public bool landDown;
    public bool pathDown;
    public bool disarmDown;
    public bool failsafeDown;
    public bool abortDown;

    [Header("Debug")]
    public string lastPayload;
    public float lastPacketTime;
    public int packetCount;

    private UdpClient udp;
    private IPEndPoint remoteEndPoint;
    private bool prevA;
    private bool prevB;
    private bool prevX;
    private bool prevY;
    private bool prevLB;
    private bool prevRB;
    private bool prevBack;
    private bool prevStart;

    private void OnEnable()
    {
        OpenSocket();
    }

    private void Update()
    {
        buttonADown = false;
        buttonBDown = false;
        buttonXDown = false;
        buttonYDown = false;
        buttonLBDown = false;
        buttonRBDown = false;
        buttonBackDown = false;
        buttonStartDown = false;

        armDown = false;
        idleDown = false;
        takeoffDown = false;
        holdDown = false;
        manualDown = false;
        landDown = false;
        pathDown = false;
        disarmDown = false;
        failsafeDown = false;
        abortDown = false;

        if (!receiverEnabled)
        {
            return;
        }

        if (udp == null)
        {
            OpenSocket();
        }

        if (udp == null)
        {
            return;
        }

        try
        {
            while (udp.Available > 0)
            {
                byte[] data = udp.Receive(ref remoteEndPoint);
                string json = Encoding.UTF8.GetString(data);

                ParsePayload(json);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[MIMISKCoreGamepadReceiver] UDP read error: " + ex.Message);
        }
    }

    private void OpenSocket()
    {
        CloseSocket();

        try
        {
            remoteEndPoint = new IPEndPoint(IPAddress.Any, listenPort);
            udp = new UdpClient(listenPort);
            udp.Client.Blocking = false;

            Debug.Log("[MIMISKCoreGamepadReceiver] Listening on UDP port " + listenPort);
        }
        catch (Exception ex)
        {
            udp = null;
            Debug.LogError("[MIMISKCoreGamepadReceiver] Could not open UDP port " + listenPort + ": " + ex.Message);
        }
    }

    private void ParsePayload(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        lastPayload = json;
        lastPacketTime = Time.time;
        packetCount++;

        lx = GetFloat(json, "lx", lx);
        ly = GetFloat(json, "ly", ly);
        rx = GetFloat(json, "rx", rx);
        ry = GetFloat(json, "ry", ry);
        lt = GetFloat(json, "lt", lt);
        rt = GetFloat(json, "rt", rt);
        hatx = Mathf.RoundToInt(GetFloat(json, "hatx", hatx));
        haty = Mathf.RoundToInt(GetFloat(json, "haty", haty));

        bool hasSemantic =
            HasField(json, "forward") ||
            HasField(json, "right") ||
            HasField(json, "yaw") ||
            HasField(json, "altitude");

        float forward;
        float right;
        float yaw;
        float altitude;

        if (preferSemanticFields && hasSemantic)
        {
            forward = GetFloat(json, "forward", commandForwardRightYawAlt.x);
            right = GetFloat(json, "right", commandForwardRightYawAlt.y);
            yaw = GetFloat(json, "yaw", commandForwardRightYawAlt.z);
            altitude = GetFloat(json, "altitude", commandForwardRightYawAlt.w);
        }
        else
        {
            forward = invertLeftYForForward ? -ly : ly;
            right = lx;
            yaw = rx;
            altitude = invertRightYForAltitude ? -ry : ry;
        }

        forward = ApplyDeadzone(forward) * forwardSign;
        right = ApplyDeadzone(right) * rightSign;
        yaw = ApplyDeadzone(yaw) * yawSign;
        altitude = ApplyDeadzone(altitude) * altitudeSign;

        commandForwardRightYawAlt = new Vector4(
            Mathf.Clamp(forward, -1.0f, 1.0f),
            Mathf.Clamp(right, -1.0f, 1.0f),
            Mathf.Clamp(yaw, -1.0f, 1.0f),
            Mathf.Clamp(altitude, -1.0f, 1.0f)
        );

        bool newA =
            ButtonPressed(json, "BTN_SOUTH") ||
            ButtonPressed(json, "BTN_A") ||
            ButtonPressed(json, "A") ||
            GetBool(json, "arm");

        bool newB =
            ButtonPressed(json, "BTN_EAST") ||
            ButtonPressed(json, "BTN_B") ||
            ButtonPressed(json, "B") ||
            GetBool(json, "manual") ||
            GetBool(json, "disarm");

        bool newX =
            ButtonPressed(json, "BTN_WEST") ||
            ButtonPressed(json, "BTN_X") ||
            ButtonPressed(json, "X") ||
            GetBool(json, "hold");

        bool newY =
            ButtonPressed(json, "BTN_NORTH") ||
            ButtonPressed(json, "BTN_Y") ||
            ButtonPressed(json, "Y") ||
            GetBool(json, "takeoff");

        bool newLB =
            ButtonPressed(json, "BTN_TL") ||
            ButtonPressed(json, "LB") ||
            GetBool(json, "land");

        bool newRB =
            ButtonPressed(json, "BTN_TR") ||
            ButtonPressed(json, "RB") ||
            GetBool(json, "path");

        bool newBack =
            ButtonPressed(json, "BTN_SELECT") ||
            ButtonPressed(json, "BACK") ||
            GetBool(json, "failsafe");

        bool newStart =
            ButtonPressed(json, "BTN_START") ||
            ButtonPressed(json, "START") ||
            GetBool(json, "mission");

        buttonADown = newA && !prevA;
        buttonBDown = newB && !prevB;
        buttonXDown = newX && !prevX;
        buttonYDown = newY && !prevY;
        buttonLBDown = newLB && !prevLB;
        buttonRBDown = newRB && !prevRB;
        buttonBackDown = newBack && !prevBack;
        buttonStartDown = newStart && !prevStart;

        buttonA = newA;
        buttonB = newB;
        buttonX = newX;
        buttonY = newY;
        buttonLB = newLB;
        buttonRB = newRB;
        buttonBack = newBack;
        buttonStart = newStart;

        prevA = newA;
        prevB = newB;
        prevX = newX;
        prevY = newY;
        prevLB = newLB;
        prevRB = newRB;
        prevBack = newBack;
        prevStart = newStart;

        // Old MIMISK model-controller semantics:
        // A / BTN_SOUTH      = Arm / TakeoffIdle
        // Y / BTN_NORTH      = Takeoff
        // X / BTN_WEST       = Hold
        // LB or RB           = Manual
        // B / BTN_EAST       = LandingOnWater
        // Back / Select      = Disarm
        // Start              = Failsafe
        armDown = buttonADown || GetBoolDownApprox(json, "arm");
        idleDown = armDown;

        takeoffDown = buttonYDown || GetBoolDownApprox(json, "takeoff");
        holdDown = buttonXDown || GetBoolDownApprox(json, "hold");

        manualDown =
            buttonLBDown ||
            buttonRBDown ||
            GetBoolDownApprox(json, "manual");

        landDown =
            buttonBDown ||
            GetBoolDownApprox(json, "land");

        disarmDown =
            buttonBackDown ||
            GetBoolDownApprox(json, "disarm");

        failsafeDown =
            buttonStartDown ||
            GetBoolDownApprox(json, "failsafe");

        abortDown = disarmDown || failsafeDown;

        // Path tracking is intentionally not mapped to START anymore,
        // because START was Failsafe in the old controller.
        // Use keyboard N for path tracking, or add a dedicated mission button later.
        pathDown = GetBoolDownApprox(json, "path") || GetBoolDownApprox(json, "mission");

        if (printPayload)
        {
            Debug.Log("[MIMISKCoreGamepadReceiver] " + json);
        }
    }

    private float ApplyDeadzone(float value)
    {
        return Mathf.Abs(value) < deadzone ? 0.0f : value;
    }

    private bool HasField(string json, string name)
    {
        return Regex.IsMatch(json, "\\\"" + Regex.Escape(name) + "\\\"\\s*:");
    }

    private float GetFloat(string json, string name, float defaultValue)
    {
        Match m =
            Regex.Match(
                json,
                "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)",
                RegexOptions.IgnoreCase
            );

        if (!m.Success)
        {
            return defaultValue;
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

        return defaultValue;
    }

    private bool GetBool(string json, string name)
    {
        Match m =
            Regex.Match(
                json,
                "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*(1|true)",
                RegexOptions.IgnoreCase
            );

        return m.Success;
    }

    private bool GetBoolDownApprox(string json, string name)
    {
        return GetBool(json, name);
    }

    private bool ButtonPressed(string json, string buttonName)
    {
        Match m =
            Regex.Match(
                json,
                "\\\"" + Regex.Escape(buttonName) + "\\\"\\s*:\\s*(1|true)",
                RegexOptions.IgnoreCase
            );

        return m.Success;
    }

    private void CloseSocket()
    {
        if (udp == null)
        {
            return;
        }

        try
        {
            udp.Close();
        }
        catch
        {
        }

        udp = null;
    }

    private void OnDisable()
    {
        CloseSocket();
    }

    private void OnApplicationQuit()
    {
        CloseSocket();
    }
}
