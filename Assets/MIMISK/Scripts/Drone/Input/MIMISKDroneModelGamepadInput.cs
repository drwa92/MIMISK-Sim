using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[RequireComponent(typeof(MIMISKDroneModelController))]
public class MIMISKDroneModelGamepadInput : MonoBehaviour
{
    [Header("References")]
    public MIMISKDroneModelController controller;

    [Header("Enable")]
    public bool enableGamepadInput = true;
    public bool disableKeyboardInputOnStart = true;
    public bool autoEnterManualMode = true;

    [Header("Device Selection")]
    public bool useFirstAvailableGamepad = true;
    public int selectedGamepadIndex = 0;
    public bool allowJoystickFallback = true;

    [Header("Input Tuning")]
    [Range(0.0f, 0.5f)] public float stickDeadzone = 0.10f;
    [Range(0.0f, 0.5f)] public float triggerDeadzone = 0.05f;
    public float commandSmoothness = 10.0f;

    [Header("Axis Inversion")]
    public bool invertForward = false;
    public bool invertRight = false;
    public bool invertYaw = false;
    public bool invertAltitude = false;

    [Header("Use Calibrated Generic Mapping")]
    public bool forceGenericAxisMapping = true;

    [Header("Calibrated Axis Paths")]
    public string forwardAxisNames = "leftStick/y,y,axis1";
    public float forwardAxisSign = 1.0f;

    public string rightAxisNames = "leftStick/x,x,axis0";
    public float rightAxisSign = 1.0f;

    public string yawAxisNames = "rightStick/x,rx,z,twist,axis2,axis3";
    public float yawAxisSign = 1.0f;

    public string altitudeUpAxisNames = "rightTrigger,rz,slider,axis5,axis6";
    public float altitudeUpAxisSign = 1.0f;

    public string altitudeDownAxisNames = "leftTrigger,axis4,axis3";
    public float altitudeDownAxisSign = 1.0f;

    [Header("Calibrated Button Paths")]
    public string armButtonNames = "buttonSouth,button0,a,cross";
    public string takeoffButtonNames = "buttonNorth,button3,y,triangle";
    public string altitudeHoldButtonNames = "buttonWest,button2,x,square";
    public string manualModeButtonNames = "leftShoulder,button4,button5,lb,l1";
    public string landButtonNames = "buttonEast,button1,b,circle";
    public string disarmButtonNames = "select,back,button6,button8,view";
    public string failsafeButtonNames = "start,button7,button9,menu";

    [Header("Debug")]
    public bool gamepadConnected;
    public bool joystickFallbackActive;
    public int gamepadCount;
    public string activeDeviceName = "none";
    public string activeDeviceLayout = "none";
    public string lastButtonEvent = "none";
    public string lastPressedButton = "none";
    public string pressedControlsDebug = "";
    public string activeAxesDebug = "";

    public Vector4 rawCommandForwardRightYawAlt;
    public Vector4 smoothedCommandForwardRightYawAlt;
    public bool manualInputActive;

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

    private void OnDisable()
    {
        if (controller != null)
        {
            controller.ClearExternalCommand();
        }
    }

    private void Update()
    {
        if (!enableGamepadInput || controller == null)
        {
            return;
        }

        gamepadCount = Gamepad.all.Count;

        InputDevice device = null;
        Gamepad gamepad = GetSelectedGamepad();

        if (gamepad != null)
        {
            device = gamepad;
            gamepadConnected = true;
            joystickFallbackActive = false;
        }
        else if (allowJoystickFallback && Joystick.current != null)
        {
            device = Joystick.current;
            gamepadConnected = false;
            joystickFallbackActive = true;
        }

        if (device == null)
        {
            gamepadConnected = false;
            joystickFallbackActive = false;
            activeDeviceName = "none";
            activeDeviceLayout = "none";
            pressedControlsDebug = "";
            activeAxesDebug = "";
            SendCommands(0f, 0f, 0f, 0f);
            return;
        }

        activeDeviceName = device.displayName;
        activeDeviceLayout = device.layout;

        UpdateDebugStrings(device);
        HandleButtons(device);
        HandleAxes(device, gamepad);
    }

    private Gamepad GetSelectedGamepad()
    {
        if (Gamepad.all.Count == 0)
        {
            return null;
        }

        if (useFirstAvailableGamepad)
        {
            return Gamepad.all[0];
        }

        int index = Mathf.Clamp(selectedGamepadIndex, 0, Gamepad.all.Count - 1);
        return Gamepad.all[index];
    }

    private void HandleButtons(InputDevice device)
    {
        foreach (InputControl control in device.allControls)
        {
            ButtonControl button = control as ButtonControl;

            if (button == null || !button.wasPressedThisFrame)
            {
                continue;
            }

            lastPressedButton = control.path;

            if (Matches(control, armButtonNames))
            {
                controller.SetMode(MIMISKDroneModelController.DroneMode.ArmedIdle);
                lastButtonEvent = control.path + ": ArmedIdle";
            }
            else if (Matches(control, takeoffButtonNames))
            {
                controller.SetMode(MIMISKDroneModelController.DroneMode.Takeoff);
                lastButtonEvent = control.path + ": Takeoff";
            }
            else if (Matches(control, altitudeHoldButtonNames))
            {
                controller.SetMode(MIMISKDroneModelController.DroneMode.AltitudeHold);
                lastButtonEvent = control.path + ": AltitudeHold";
            }
            else if (Matches(control, manualModeButtonNames))
            {
                controller.SetMode(MIMISKDroneModelController.DroneMode.ManualAttitude);
                lastButtonEvent = control.path + ": ManualAttitude";
            }
            else if (Matches(control, landButtonNames))
            {
                controller.SetMode(MIMISKDroneModelController.DroneMode.LandingOnWater);
                lastButtonEvent = control.path + ": LandingOnWater";
            }
            else if (Matches(control, disarmButtonNames))
            {
                controller.SetMode(MIMISKDroneModelController.DroneMode.Disarmed);
                lastButtonEvent = control.path + ": Disarmed";
            }
            else if (Matches(control, failsafeButtonNames))
            {
                controller.SetMode(MIMISKDroneModelController.DroneMode.Failsafe);
                lastButtonEvent = control.path + ": Failsafe";
            }
            else
            {
                lastButtonEvent = "Unmapped: " + control.path;
            }
        }
    }

    private void HandleAxes(InputDevice device, Gamepad gamepad)
    {
        float forward;
        float right;
        float yaw;
        float altitudeUp;
        float altitudeDown;

        if (gamepad != null && !forceGenericAxisMapping)
        {
            Vector2 leftStick = gamepad.leftStick.ReadValue();
            Vector2 rightStick = gamepad.rightStick.ReadValue();

            forward = leftStick.y;
            right = leftStick.x;
            yaw = rightStick.x;
            altitudeUp = gamepad.rightTrigger.ReadValue();
            altitudeDown = gamepad.leftTrigger.ReadValue();
        }
        else
        {
            forward = ReadFirstNamedAxis(device, forwardAxisNames) * forwardAxisSign;
            right = ReadFirstNamedAxis(device, rightAxisNames) * rightAxisSign;
            yaw = ReadFirstNamedAxis(device, yawAxisNames) * yawAxisSign;
            altitudeUp = ReadFirstNamedAxis(device, altitudeUpAxisNames) * altitudeUpAxisSign;
            altitudeDown = ReadFirstNamedAxis(device, altitudeDownAxisNames) * altitudeDownAxisSign;
        }

        forward = ApplyDeadzone(forward, stickDeadzone);
        right = ApplyDeadzone(right, stickDeadzone);
        yaw = ApplyDeadzone(yaw, stickDeadzone);

        altitudeUp = Mathf.Max(0.0f, ApplyDeadzone(altitudeUp, triggerDeadzone));
        altitudeDown = Mathf.Max(0.0f, ApplyDeadzone(altitudeDown, triggerDeadzone));

        float altitude = altitudeUp - altitudeDown;

        SendCommands(forward, right, yaw, altitude);
    }

    private void SendCommands(float forward, float right, float yaw, float altitude)
    {
        if (invertForward) forward = -forward;
        if (invertRight) right = -right;
        if (invertYaw) yaw = -yaw;
        if (invertAltitude) altitude = -altitude;

        rawCommandForwardRightYawAlt = new Vector4(forward, right, yaw, altitude);

        smoothedCommandForwardRightYawAlt = Vector4.Lerp(
            smoothedCommandForwardRightYawAlt,
            rawCommandForwardRightYawAlt,
            commandSmoothness * Time.deltaTime
        );

        manualInputActive =
            Mathf.Abs(smoothedCommandForwardRightYawAlt.x) > 0.02f ||
            Mathf.Abs(smoothedCommandForwardRightYawAlt.y) > 0.02f ||
            Mathf.Abs(smoothedCommandForwardRightYawAlt.z) > 0.02f ||
            Mathf.Abs(smoothedCommandForwardRightYawAlt.w) > 0.02f;

        if (autoEnterManualMode &&
            manualInputActive &&
            (controller.mode == MIMISKDroneModelController.DroneMode.Takeoff ||
             controller.mode == MIMISKDroneModelController.DroneMode.AltitudeHold))
        {
            controller.SetMode(MIMISKDroneModelController.DroneMode.ManualAttitude);
        }

        controller.SetExternalCommand(
            smoothedCommandForwardRightYawAlt.x,
            smoothedCommandForwardRightYawAlt.y,
            smoothedCommandForwardRightYawAlt.z,
            smoothedCommandForwardRightYawAlt.w
        );
    }

    private float ApplyDeadzone(float value, float deadzone)
    {
        float abs = Mathf.Abs(value);

        if (abs < deadzone)
        {
            return 0.0f;
        }

        float sign = Mathf.Sign(value);
        float scaled = (abs - deadzone) / Mathf.Max(0.0001f, 1.0f - deadzone);

        return Mathf.Clamp(sign * scaled, -1.0f, 1.0f);
    }

    private float ReadFirstNamedAxis(InputDevice device, string names)
    {
        foreach (InputControl control in device.allControls)
        {
            AxisControl axis = control as AxisControl;

            if (axis == null || control is ButtonControl)
            {
                continue;
            }

            if (Matches(control, names))
            {
                return Mathf.Clamp(axis.ReadValue(), -1.0f, 1.0f);
            }
        }

        return 0.0f;
    }

    private bool Matches(InputControl control, string commaSeparatedNames)
    {
        if (control == null || string.IsNullOrWhiteSpace(commaSeparatedNames))
        {
            return false;
        }

        string[] names = commaSeparatedNames.Split(',');

        foreach (string raw in names)
        {
            string targetRaw = raw.Trim();

            if (string.IsNullOrWhiteSpace(targetRaw))
            {
                continue;
            }

            if (control.path == targetRaw ||
                control.name == targetRaw ||
                control.displayName == targetRaw)
            {
                return true;
            }

            string controlName = Normalize(control.name);
            string displayName = Normalize(control.displayName);
            string path = Normalize(control.path);
            string target = Normalize(targetRaw);

            if (controlName == target ||
                displayName == target ||
                path.EndsWith(target))
            {
                return true;
            }
        }

        return false;
    }

    private string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return value
            .ToLowerInvariant()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .Replace("/", "");
    }

    private void UpdateDebugStrings(InputDevice device)
    {
        StringBuilder pressed = new StringBuilder();
        StringBuilder axes = new StringBuilder();

        foreach (InputControl control in device.allControls)
        {
            ButtonControl button = control as ButtonControl;

            if (button != null && button.isPressed)
            {
                if (pressed.Length > 0)
                {
                    pressed.Append(", ");
                }

                pressed.Append(control.path);
            }

            AxisControl axis = control as AxisControl;

            if (axis != null && !(control is ButtonControl))
            {
                float value = axis.ReadValue();

                if (Mathf.Abs(value) > 0.05f)
                {
                    if (axes.Length > 0)
                    {
                        axes.Append(", ");
                    }

                    axes.Append(control.path);
                    axes.Append("=");
                    axes.Append(value.ToString("F2"));
                }
            }
        }

        pressedControlsDebug = pressed.ToString();
        activeAxesDebug = axes.ToString();
    }
}
