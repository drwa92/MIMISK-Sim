using UnityEngine;
using UnityEngine.InputSystem;

public class MIMISKDronePropellerAnimator : MonoBehaviour
{
    public enum DroneRotorState
    {
        Disarmed,
        ArmedIdle,
        Flying,
        WaterSurfaceStable,
        EmergencyStop
    }

    [System.Serializable]
    public class Rotor
    {
        public string name;
        public Transform rotorTransform;

        [Tooltip("Usually Y, Z, or X depending on the FBX rotor orientation.")]
        public Vector3 localSpinAxis = Vector3.up;

        [Tooltip("Use this to make neighboring rotors spin opposite directions.")]
        public bool clockwise = true;

        [Header("Debug")]
        public float currentRpm;
    }

    [Header("Drone State")]
    public DroneRotorState rotorState = DroneRotorState.Disarmed;

    [Header("Rotor References")]
    public Rotor[] rotors;

    [Header("RPM Settings")]
    public float disarmedRpm = 0f;
    public float armedIdleRpm = 600f;
    public float flyingRpm = 4200f;
    public float waterSurfaceStableRpm = 0f;

    [Header("Smoothing")]
    public float spinUpRate = 2500f;
    public float spinDownRate = 1800f;

    [Header("Keyboard Debug")]
    public bool enableKeyboardDebug = true;
    public KeyCode disarmKey = KeyCode.Alpha0;
    public KeyCode idleKey = KeyCode.Alpha1;
    public KeyCode flyingKey = KeyCode.Alpha2;
    public KeyCode surfaceStableKey = KeyCode.Alpha3;
    public KeyCode emergencyStopKey = KeyCode.Alpha9;

    [Header("Debug")]
    public float targetRpm;

    private void Update()
    {
        HandleKeyboardDebug();
        UpdateTargetRpm();
        AnimateRotors();
    }

    private void HandleKeyboardDebug()
    {
        if (!enableKeyboardDebug)
        {
            return;
        }

        if (MIMISKInputWasPressed(disarmKey))
        {
            SetState(DroneRotorState.Disarmed);
        }

        if (MIMISKInputWasPressed(idleKey))
        {
            SetState(DroneRotorState.ArmedIdle);
        }

        if (MIMISKInputWasPressed(flyingKey))
        {
            SetState(DroneRotorState.Flying);
        }

        if (MIMISKInputWasPressed(surfaceStableKey))
        {
            SetState(DroneRotorState.WaterSurfaceStable);
        }

        if (MIMISKInputWasPressed(emergencyStopKey))
        {
            SetState(DroneRotorState.EmergencyStop);
        }
    }

    public void SetState(DroneRotorState newState)
    {
        rotorState = newState;
    }

    public void SetFlyingRpm(float rpm)
    {
        flyingRpm = Mathf.Max(0f, rpm);
    }

    private void UpdateTargetRpm()
    {
        switch (rotorState)
        {
            case DroneRotorState.Disarmed:
                targetRpm = disarmedRpm;
                break;

            case DroneRotorState.ArmedIdle:
                targetRpm = armedIdleRpm;
                break;

            case DroneRotorState.Flying:
                targetRpm = flyingRpm;
                break;

            case DroneRotorState.WaterSurfaceStable:
                targetRpm = waterSurfaceStableRpm;
                break;

            case DroneRotorState.EmergencyStop:
                targetRpm = 0f;
                break;
        }
    }

    private void AnimateRotors()
    {
        if (rotors == null)
        {
            return;
        }

        foreach (Rotor rotor in rotors)
        {
            if (rotor == null || rotor.rotorTransform == null)
            {
                continue;
            }

            float rate = targetRpm > rotor.currentRpm ? spinUpRate : spinDownRate;

            if (rotorState == DroneRotorState.EmergencyStop)
            {
                rate = spinDownRate * 4f;
            }

            rotor.currentRpm = Mathf.MoveTowards(
                rotor.currentRpm,
                targetRpm,
                rate * Time.deltaTime
            );

            float direction = rotor.clockwise ? 1f : -1f;

            float degreesPerSecond = rotor.currentRpm * 360f / 60f;
            float angleThisFrame = degreesPerSecond * Time.deltaTime * direction;

            Vector3 axis = rotor.localSpinAxis.sqrMagnitude > 0.0001f
                ? rotor.localSpinAxis.normalized
                : Vector3.up;

            rotor.rotorTransform.Rotate(axis, angleThisFrame, Space.Self);
        }
    }

    private static bool MIMISKInputWasPressed(KeyCode key)
    {
        var control = MIMISKGetKeyControl(key);
        return control != null && control.wasPressedThisFrame;
    }

    private static bool MIMISKInputIsPressed(KeyCode key)
    {
        var control = MIMISKGetKeyControl(key);
        return control != null && control.isPressed;
    }

    private static UnityEngine.InputSystem.Controls.KeyControl MIMISKGetKeyControl(KeyCode key)
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;

        if (keyboard == null)
        {
            return null;
        }

        switch (key)
        {
            case KeyCode.Alpha0: return keyboard.digit0Key;
            case KeyCode.Alpha1: return keyboard.digit1Key;
            case KeyCode.Alpha2: return keyboard.digit2Key;
            case KeyCode.Alpha3: return keyboard.digit3Key;
            case KeyCode.Alpha4: return keyboard.digit4Key;
            case KeyCode.Alpha5: return keyboard.digit5Key;
            case KeyCode.Alpha6: return keyboard.digit6Key;
            case KeyCode.Alpha7: return keyboard.digit7Key;
            case KeyCode.Alpha8: return keyboard.digit8Key;
            case KeyCode.Alpha9: return keyboard.digit9Key;

            case KeyCode.W: return keyboard.wKey;
            case KeyCode.S: return keyboard.sKey;
            case KeyCode.A: return keyboard.aKey;
            case KeyCode.D: return keyboard.dKey;
            case KeyCode.Q: return keyboard.qKey;
            case KeyCode.E: return keyboard.eKey;
            case KeyCode.R: return keyboard.rKey;
            case KeyCode.F: return keyboard.fKey;

            case KeyCode.Space: return keyboard.spaceKey;
            case KeyCode.Escape: return keyboard.escapeKey;

            default:
                return null;
        }
    }


}
