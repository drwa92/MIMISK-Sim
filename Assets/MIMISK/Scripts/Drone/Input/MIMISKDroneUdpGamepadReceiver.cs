using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

[RequireComponent(typeof(MIMISKDroneModelController))]
public class MIMISKDroneUdpGamepadReceiver : MonoBehaviour
{
    public enum AxisSource
    {
        None,
        Lx,
        Ly,
        Rx,
        Ry,
        Lt,
        Rt,
        HatX,
        HatY
    }

    public enum AltitudeMode
    {
        RtMinusLt,
        RtCentered,
        Ry,
        HatY,
        None
    }

    [Header("References")]
    public MIMISKDroneModelController controller;

    [Header("UDP")]
    public bool enableReceiver = true;
    public int listenPort = 54331;
    public float commandTimeoutSeconds = 0.35f;

    [Header("Fallback Axis Mapping From Old Payload")]
    public AxisSource forwardAxis = AxisSource.Ly;
    public float forwardSign = -1.0f;

    public AxisSource rightAxis = AxisSource.Lx;
    public float rightSign = 1.0f;

    public AxisSource yawAxis = AxisSource.Rx;
    public float yawSign = 1.0f;

    public AltitudeMode altitudeMode = AltitudeMode.Ry;
    public float altitudeSign = -1.0f;

    [Header("Fallback Mode Buttons, Linux BTN_* Names")]
    public string armButtons = "BTN_SOUTH";
    public string takeoffButtons = "BTN_NORTH";
    public string altitudeHoldButtons = "BTN_WEST";
    public string manualModeButtons = "BTN_TL,BTN_TR";
    public string landButtons = "BTN_EAST";
    public string disarmButtons = "BTN_SELECT,BTN_BACK";
    public string failsafeButtons = "BTN_START";

    [Header("Behavior")]
    public bool disableKeyboardInputOnStart = true;
    public bool autoEnterManualMode = true;
    public bool sendZeroOnTimeout = true;
    public float deadzone = 0.05f;
    public float commandSmoothness = 10.0f;

    [Header("Command Arbitration")]
    [Tooltip("If true, the receiver still reads gamepad/UDP input but does not send movement commands to the flight controller.")]
    public bool suppressCommandOutput = false;

    [Tooltip("If true, mode buttons are still accepted while command output is suppressed.")]
    public bool allowModeButtonsWhileSuppressed = true;

    [Header("Debug")]
    public bool connected;
    public float lastPacketAge;
    public string lastJson = "";
    public string lastButtonEvent = "none";

    public float lx;
    public float ly;
    public float rx;
    public float ry;
    public float lt;
    public float rt;
    public int hatx;
    public int haty;

    public bool usingDirectDronePayload;
    public Vector4 rawCommandForwardRightYawAlt;
    public Vector4 smoothedCommandForwardRightYawAlt;

    private UdpClient udp;
    private Thread thread;
    private volatile bool running;
    private readonly object lockObj = new object();
    private string latestJson = "";
    private float lastPacketTime = -999.0f;

    private readonly Dictionary<string, bool> previousButtonStates =
        new Dictionary<string, bool>();

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<MIMISKDroneModelController>();
        }

        if (controller != null && disableKeyboardInputOnStart)
        {
            controller.enableKeyboardInput = false;
        }
    }

    private void OnEnable()
    {
        StartReceiver();
    }

    private void OnDisable()
    {
        StopReceiver();

        if (controller != null)
        {
            controller.ClearExternalCommand();
        }
    }

    private void OnApplicationQuit()
    {
        StopReceiver();
    }

    private void Update()
    {
        if (!enableReceiver || controller == null)
        {
            return;
        }

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

            ParseMiniRovStyleDebugFields(json);
            HandleButtons(json);
            UpdateCommandFromPayload(json);
        }

        lastPacketAge = Time.time - lastPacketTime;

        if (sendZeroOnTimeout && lastPacketAge > commandTimeoutSeconds)
        {
            connected = false;
            rawCommandForwardRightYawAlt = Vector4.zero;

            smoothedCommandForwardRightYawAlt = Vector4.Lerp(
                smoothedCommandForwardRightYawAlt,
                Vector4.zero,
                commandSmoothness * Time.deltaTime
            );

            if (!suppressCommandOutput)
            {
                controller.SetExternalCommand(
                    smoothedCommandForwardRightYawAlt.x,
                    smoothedCommandForwardRightYawAlt.y,
                    smoothedCommandForwardRightYawAlt.z,
                    smoothedCommandForwardRightYawAlt.w
                );
            }
        }
    }

    private void StartReceiver()
    {
        if (!enableReceiver || running)
        {
            return;
        }

        try
        {
            udp = new UdpClient(listenPort);
            running = true;

            thread = new Thread(ReceiveLoop);
            thread.IsBackground = true;
            thread.Start();

            Debug.Log("[MIMISKDroneUdpGamepadReceiver] Listening on UDP port " + listenPort);
        }
        catch (Exception e)
        {
            Debug.LogError("[MIMISKDroneUdpGamepadReceiver] Failed to bind UDP port " + listenPort + ": " + e.Message);
        }
    }

    private void StopReceiver()
    {
        running = false;

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
    }

    private void ParseMiniRovStyleDebugFields(string json)
    {
        lx = ExtractFloat(json, "lx", lx);
        ly = ExtractFloat(json, "ly", ly);
        rx = ExtractFloat(json, "rx", rx);
        ry = ExtractFloat(json, "ry", ry);
        lt = ExtractFloat(json, "lt", lt);
        rt = ExtractFloat(json, "rt", rt);

        hatx = Mathf.RoundToInt(ExtractFloat(json, "hatx", hatx));
        haty = Mathf.RoundToInt(ExtractFloat(json, "haty", haty));
    }

    private void HandleButtons(string json)
    {
        if (suppressCommandOutput && !allowModeButtonsWhileSuppressed)
        {
            return;
        }
        if (WasPressed(json, "arm", armButtons))
        {
            controller.SetMode(MIMISKDroneModelController.DroneMode.ArmedIdle);
            lastButtonEvent = "Arm";
        }

        if (WasPressed(json, "takeoff", takeoffButtons))
        {
            controller.SetMode(MIMISKDroneModelController.DroneMode.Takeoff);
            lastButtonEvent = "Takeoff";
        }

        if (WasPressed(json, "hold", altitudeHoldButtons))
        {
            controller.SetMode(MIMISKDroneModelController.DroneMode.AltitudeHold);
            lastButtonEvent = "AltitudeHold";
        }

        if (WasPressed(json, "manual", manualModeButtons))
        {
            controller.SetMode(MIMISKDroneModelController.DroneMode.ManualAttitude);
            lastButtonEvent = "ManualAttitude";
        }

        if (WasPressed(json, "land", landButtons))
        {
            controller.SetMode(MIMISKDroneModelController.DroneMode.LandingOnWater);
            lastButtonEvent = "LandingOnWater";
        }

        if (WasPressed(json, "disarm", disarmButtons))
        {
            controller.SetMode(MIMISKDroneModelController.DroneMode.Disarmed);
            lastButtonEvent = "Disarmed";
        }

        if (WasPressed(json, "failsafe", failsafeButtons))
        {
            controller.SetMode(MIMISKDroneModelController.DroneMode.Failsafe);
            lastButtonEvent = "Failsafe";
        }
    }

    private void UpdateCommandFromPayload(string json)
    {
        float forward;
        float right;
        float yaw;
        float altitude;

        bool hasDirectForward = TryExtractFloat(json, "forward", out forward);
        bool hasDirectRight = TryExtractFloat(json, "right", out right);
        bool hasDirectYaw = TryExtractFloat(json, "yaw", out yaw);
        bool hasDirectAltitude = TryExtractFloat(json, "altitude", out altitude);

        usingDirectDronePayload =
            hasDirectForward || hasDirectRight || hasDirectYaw || hasDirectAltitude;

        if (!usingDirectDronePayload)
        {
            forward = ApplyDeadzone(GetAxis(forwardAxis) * forwardSign);
            right = ApplyDeadzone(GetAxis(rightAxis) * rightSign);
            yaw = ApplyDeadzone(GetAxis(yawAxis) * yawSign);
            altitude = ApplyDeadzone(GetAltitudeCommand() * altitudeSign);
        }
        else
        {
            if (!hasDirectForward) forward = 0.0f;
            if (!hasDirectRight) right = 0.0f;
            if (!hasDirectYaw) yaw = 0.0f;
            if (!hasDirectAltitude) altitude = 0.0f;

            forward = ApplyDeadzone(forward);
            right = ApplyDeadzone(right);
            yaw = ApplyDeadzone(yaw);
            altitude = ApplyDeadzone(altitude);
        }

        rawCommandForwardRightYawAlt =
            new Vector4(forward, right, yaw, altitude);

        smoothedCommandForwardRightYawAlt = Vector4.Lerp(
            smoothedCommandForwardRightYawAlt,
            rawCommandForwardRightYawAlt,
            commandSmoothness * Time.deltaTime
        );

        bool manualInputActive =
            Mathf.Abs(smoothedCommandForwardRightYawAlt.x) > 0.02f ||
            Mathf.Abs(smoothedCommandForwardRightYawAlt.y) > 0.02f ||
            Mathf.Abs(smoothedCommandForwardRightYawAlt.z) > 0.02f ||
            Mathf.Abs(smoothedCommandForwardRightYawAlt.w) > 0.02f;

        if (!suppressCommandOutput &&
            autoEnterManualMode &&
            manualInputActive &&
            (controller.mode == MIMISKDroneModelController.DroneMode.Takeoff ||
             controller.mode == MIMISKDroneModelController.DroneMode.AltitudeHold))
        {
            controller.SetMode(MIMISKDroneModelController.DroneMode.ManualAttitude);
        }

        if (!suppressCommandOutput)
        {
            controller.SetExternalCommand(
                smoothedCommandForwardRightYawAlt.x,
                smoothedCommandForwardRightYawAlt.y,
                smoothedCommandForwardRightYawAlt.z,
                smoothedCommandForwardRightYawAlt.w
            );
        }
    }

    private float GetAxis(AxisSource source)
    {
        switch (source)
        {
            case AxisSource.Lx: return lx;
            case AxisSource.Ly: return ly;
            case AxisSource.Rx: return rx;
            case AxisSource.Ry: return ry;
            case AxisSource.Lt: return lt;
            case AxisSource.Rt: return rt;
            case AxisSource.HatX: return hatx;
            case AxisSource.HatY: return haty;
            default: return 0.0f;
        }
    }

    private float GetAltitudeCommand()
    {
        switch (altitudeMode)
        {
            case AltitudeMode.RtMinusLt:
                return rt - lt;

            case AltitudeMode.RtCentered:
                return rt * 2.0f - 1.0f;

            case AltitudeMode.Ry:
                return ry;

            case AltitudeMode.HatY:
                return haty;

            default:
                return 0.0f;
        }
    }

    private float ApplyDeadzone(float value)
    {
        if (Mathf.Abs(value) < deadzone)
        {
            return 0.0f;
        }

        return Mathf.Clamp(value, -1.0f, 1.0f);
    }

    private float ExtractFloat(string json, string key, float fallback)
    {
        if (TryExtractFloat(json, key, out float value))
        {
            return value;
        }

        return fallback;
    }

    private bool TryExtractFloat(string json, string key, out float value)
    {
        value = 0.0f;

        Match m = Regex.Match(
            json,
            "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(-?(?:[0-9]+(?:\\.[0-9]*)?|\\.[0-9]+)(?:[eE][-+]?[0-9]+)?)"
        );

        if (!m.Success)
        {
            return false;
        }

        return float.TryParse(
            m.Groups[1].Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out value
        );
    }

    private bool WasPressed(string json, string directKey, string fallbackButtonNames)
    {
        bool now = ExtractBool(json, directKey, false) || IsRawButtonPressed(json, fallbackButtonNames);

        if (!previousButtonStates.TryGetValue(directKey, out bool previous))
        {
            previous = false;
        }

        previousButtonStates[directKey] = now;

        return now && !previous;
    }

    private bool ExtractBool(string json, string key, bool fallback)
    {
        Match m = Regex.Match(
            json,
            "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(true|false|0|1)",
            RegexOptions.IgnoreCase
        );

        if (!m.Success)
        {
            return fallback;
        }

        string v = m.Groups[1].Value.ToLowerInvariant();

        return v == "true" || v == "1";
    }

    private bool IsRawButtonPressed(string json, string commaSeparatedButtonNames)
    {
        if (string.IsNullOrWhiteSpace(commaSeparatedButtonNames))
        {
            return false;
        }

        string[] names = commaSeparatedButtonNames.Split(',');

        foreach (string raw in names)
        {
            string name = raw.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            string pattern =
                "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*1";

            if (Regex.IsMatch(json, pattern))
            {
                return true;
            }
        }

        return false;
    }
}
