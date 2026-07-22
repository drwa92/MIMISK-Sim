using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MIMISKMiniROVGamepadReceiver : MonoBehaviour
{
    [Header("References")]
    public MIMISKMiniROVCoreController coreController;
    public MIMISKMiniROVMissionManager missionManager;

    [Header("Receiver")]
    public bool receiverEnabled = true;

    [Tooltip("Only send commands when MiniROV mission action is GamepadManualTest and mission is active.")]
    public bool requireGamepadMissionState = true;

    public bool allowKeyboardFallback = true;

    [Header("Input Mapping")]
    public float gamepadSurgeScale = 1.0f;
    public float gamepadYawScale = 1.0f;
    public float gamepadDepthScale = 1.0f;

    public bool invertGamepadSurge = true;
    public bool invertGamepadYaw = false;

    [Header("Runtime")]
    public float surge;
    public float yaw;
    public float depthNudge;
    public bool sendingToCore;
    public string lastEvent = "idle";

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Update()
    {
        if (!receiverEnabled)
        {
            return;
        }

        AutoFindReferences();

        if (coreController == null)
        {
            return;
        }

        if (requireGamepadMissionState)
        {
            if (missionManager == null ||
                !missionManager.missionActive ||
                missionManager.missionState !=
                    MIMISKMiniROVMissionManager.MiniROVMissionState.RunningGamepadTest)
            {
                sendingToCore = false;
                coreController.SetManualCommand(0.0f, 0.0f, 0.0f);
                return;
            }
        }

        ReadInputs();

        coreController.SetManualCommand(
            surge,
            yaw,
            depthNudge
        );

        sendingToCore = true;

        if (Keyboard.current != null &&
            Keyboard.current.hKey.wasPressedThisFrame &&
            missionManager != null)
        {
            missionManager.RequestReturnToRecovery();
            lastEvent = "requested_return_home";
        }
        else
        {
            lastEvent =
                "manual_surge_" +
                surge.ToString("F2") +
                "_yaw_" +
                yaw.ToString("F2");
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (coreController == null)
        {
            coreController = GetComponent<MIMISKMiniROVCoreController>();
        }

        if (missionManager == null)
        {
            missionManager = GetComponent<MIMISKMiniROVMissionManager>();
        }
    }

    private void ReadInputs()
    {
        surge = 0.0f;
        yaw = 0.0f;
        depthNudge = 0.0f;

        Gamepad pad = Gamepad.current;

        if (pad != null)
        {
            surge =
                pad.leftStick.y.ReadValue() *
                gamepadSurgeScale;

            yaw =
                pad.rightStick.x.ReadValue() *
                gamepadYawScale;

            depthNudge =
                (
                    pad.rightTrigger.ReadValue() -
                    pad.leftTrigger.ReadValue()
                ) *
                gamepadDepthScale;
        }

        if (allowKeyboardFallback && Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed)
            {
                surge += 1.0f;
            }

            if (Keyboard.current.sKey.isPressed)
            {
                surge -= 1.0f;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                yaw += 1.0f;
            }

            if (Keyboard.current.aKey.isPressed)
            {
                yaw -= 1.0f;
            }

            if (Keyboard.current.eKey.isPressed)
            {
                depthNudge += 1.0f;
            }

            if (Keyboard.current.qKey.isPressed)
            {
                depthNudge -= 1.0f;
            }
        }

        if (invertGamepadSurge)
        {
            surge = -surge;
        }

        if (invertGamepadYaw)
        {
            yaw = -yaw;
        }

        surge = Mathf.Clamp(surge, -1.0f, 1.0f);
        yaw = Mathf.Clamp(yaw, -1.0f, 1.0f);
        depthNudge = Mathf.Clamp(depthNudge, -1.0f, 1.0f);
    }
}
