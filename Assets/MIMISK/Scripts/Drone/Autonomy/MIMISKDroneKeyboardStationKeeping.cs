using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MIMISKDroneKeyboardStationKeeping : MonoBehaviour
{
    [Header("References")]
    public MIMISKDroneAquaLocEstimator aquaLoc;
    public MIMISKDroneAquaLocPositionHold aquaHold;
    public MIMISKDroneModelController controller;
    public MIMISKDroneUdpGamepadReceiver udpReceiver;

    [Header("Keyboard Commands")]
    public Key toggleHoldKey = Key.H;
    public Key disableHoldKey = Key.P;
    public Key setTargetForwardKey = Key.T;
    public Key landAtTargetKey = Key.L;

    [Header("Control Arbitration")]
    [Tooltip("Normal mode: UDP/gamepad receiver is ON. Station keeping mode: UDP/gamepad receiver is OFF.")]
    public bool disableUdpReceiverWhileHolding = true;

    [Tooltip("When hold is disabled, the UDP/gamepad receiver is forced ON again.")]
    public bool forceUdpReceiverOnWhenReleased = true;

    [Tooltip("When released, the drone returns to ManualAttitude mode.")]
    public bool forceManualModeOnRelease = true;

    [Header("Station Keeping")]
    public bool stationKeepingActive;
    public bool captureYawOnHold = true;

    [Header("Runtime Debug")]
    public string activeMode = "Manual Gamepad";
    public string lastKeyboardEvent = "none";
    public Vector3 holdTargetWorld;
    public float holdTargetYawDeg;
    public bool aquaLocReady;
    public bool udpReceiverEnabled;

    private bool cachedUdpState;
    private bool udpWasEnabledBeforeHold;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void OnEnable()
    {
        AutoFindReferences();
    }

    private void Update()
    {
        AutoFindReferencesIfNeeded();

        aquaLocReady = aquaLoc != null && aquaLoc.estimatorReady;
        udpReceiverEnabled = udpReceiver != null && udpReceiver.enabled;

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[toggleHoldKey].wasPressedThisFrame)
        {
            ToggleStationKeeping();
        }

        if (Keyboard.current[disableHoldKey].wasPressedThisFrame)
        {
            DisableStationKeeping();
        }

        if (Keyboard.current[setTargetForwardKey].wasPressedThisFrame)
        {
            SetTargetThreeMetersForward();
        }

        if (Keyboard.current[landAtTargetKey].wasPressedThisFrame)
        {
            LandOnWaterAtCurrentTarget();
        }

        UpdateDebugState();
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (aquaLoc == null)
        {
            aquaLoc = GetComponent<MIMISKDroneAquaLocEstimator>();
        }

        if (aquaHold == null)
        {
            aquaHold = GetComponent<MIMISKDroneAquaLocPositionHold>();
        }

        if (controller == null)
        {
            controller = GetComponent<MIMISKDroneModelController>();
        }

        if (udpReceiver == null)
        {
            udpReceiver = GetComponent<MIMISKDroneUdpGamepadReceiver>();
        }
    }

    private void AutoFindReferencesIfNeeded()
    {
        if (aquaLoc == null || aquaHold == null || controller == null || udpReceiver == null)
        {
            AutoFindReferences();
        }
    }

    public void ToggleStationKeeping()
    {
        if (stationKeepingActive)
        {
            DisableStationKeeping();
        }
        else
        {
            EnableStationKeepingAtCurrentPosition();
        }
    }

    [ContextMenu("Enable Station Keeping At Current AquaLoc Position")]
    public void EnableStationKeepingAtCurrentPosition()
    {
        AutoFindReferences();

        if (aquaLoc == null || !aquaLoc.estimatorReady)
        {
            lastKeyboardEvent = "H rejected: AquaLoc not ready";
            Debug.LogWarning("[MIMISK] Station keeping rejected: AquaLoc is not ready.");
            return;
        }

        if (aquaHold == null)
        {
            Debug.LogError("[MIMISK] AquaHold is missing. Run MIMISK/Drone/Autonomy/Setup Keyboard Station Keeping.");
            return;
        }

        if (controller == null)
        {
            Debug.LogError("[MIMISK] MIMISKDroneModelController is missing.");
            return;
        }

        aquaHold.aquaLoc = aquaLoc;
        aquaHold.controller = controller;
        aquaHold.udpReceiver = udpReceiver;

        // Important:
        // KeyboardStationKeeping owns gamepad arbitration.
        // AquaHold must not disable/enable the UDP receiver internally.
        aquaHold.suppressManualUdpInputWhenActive = false;

        aquaHold.targetPositionWorld = aquaLoc.estimatedPositionWorld;
        aquaHold.targetYawDeg = captureYawOnHold
            ? aquaLoc.estimatedYawDeg
            : aquaHold.targetYawDeg;

        aquaHold.SetTargetToCurrentAquaLocEstimate();

        aquaHold.enableGuidance = true;
        aquaHold.guidanceMode = MIMISKDroneAquaLocPositionHold.GuidanceMode.HoldTarget;
        aquaHold.takeControlOfDrone = true;

        stationKeepingActive = true;

        DisableGamepadReceiverForHold();

        controller.SetMode(MIMISKDroneModelController.DroneMode.ManualAttitude);

        holdTargetWorld = aquaHold.targetPositionWorld;
        holdTargetYawDeg = aquaHold.targetYawDeg;

        activeMode = "AquaLoc Station Keeping";
        lastKeyboardEvent = "H: station keeping enabled";

        Debug.Log("[MIMISK] Station keeping enabled. Gamepad command receiver disabled.");
    }

    [ContextMenu("Disable Station Keeping And Restore Gamepad")]
    public void DisableStationKeeping()
    {
        AutoFindReferences();

        stationKeepingActive = false;

        if (aquaHold != null)
        {
            aquaHold.enableGuidance = false;
            aquaHold.guidanceMode = MIMISKDroneAquaLocPositionHold.GuidanceMode.Disabled;
            aquaHold.DisableAquaHold();
        }

        if (controller != null)
        {
            controller.ClearExternalCommand();

            if (forceManualModeOnRelease)
            {
                controller.SetMode(MIMISKDroneModelController.DroneMode.ManualAttitude);
            }
        }

        RestoreGamepadReceiver();

        activeMode = "Manual Gamepad";
        lastKeyboardEvent = "H/P: station keeping disabled, gamepad restored";

        Debug.Log("[MIMISK] Station keeping disabled. Gamepad command receiver restored.");
    }

    [ContextMenu("Set Target 3m Forward")]
    public void SetTargetThreeMetersForward()
    {
        if (!stationKeepingActive)
        {
            EnableStationKeepingAtCurrentPosition();
        }

        if (aquaLoc == null || !aquaLoc.estimatorReady || aquaHold == null)
        {
            return;
        }

        float yaw = aquaLoc.estimatedYawDeg;
        Vector3 forward = Quaternion.Euler(0.0f, yaw, 0.0f) * Vector3.forward;

        aquaHold.targetPositionWorld = aquaLoc.estimatedPositionWorld + forward * 3.0f;
        aquaHold.targetYawDeg = yaw;

        aquaHold.enableGuidance = true;
        aquaHold.guidanceMode = MIMISKDroneAquaLocPositionHold.GuidanceMode.GoToTarget;

        stationKeepingActive = true;
        DisableGamepadReceiverForHold();

        holdTargetWorld = aquaHold.targetPositionWorld;
        holdTargetYawDeg = aquaHold.targetYawDeg;

        activeMode = "AquaLoc GoToTarget";
        lastKeyboardEvent = "T: target set 3 m forward";

        Debug.Log("[MIMISK] AquaHold target set 3 m forward. Gamepad receiver disabled.");
    }

    [ContextMenu("Land On Water At Current Target")]
    public void LandOnWaterAtCurrentTarget()
    {
        if (!stationKeepingActive)
        {
            EnableStationKeepingAtCurrentPosition();
        }

        if (aquaHold == null)
        {
            return;
        }

        aquaHold.enableGuidance = true;
        aquaHold.guidanceMode = MIMISKDroneAquaLocPositionHold.GuidanceMode.LandOnWaterAtTarget;

        stationKeepingActive = true;
        DisableGamepadReceiverForHold();

        activeMode = "AquaLoc LandOnWaterAtTarget";
        lastKeyboardEvent = "L: land on water at target";

        Debug.Log("[MIMISK] Land-on-water at AquaHold target requested.");
    }


    private void SetUdpSuppressCommandOutput(bool value)
    {
        if (udpReceiver == null)
        {
            return;
        }

        var field = udpReceiver.GetType().GetField(
            "suppressCommandOutput",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
        );

        if (field != null)
        {
            field.SetValue(udpReceiver, value);
        }
    }

    private void DisableGamepadReceiverForHold()
    {
        if (!disableUdpReceiverWhileHolding || udpReceiver == null)
        {
            return;
        }

        if (!cachedUdpState)
        {
            udpWasEnabledBeforeHold = udpReceiver.enabled;
            cachedUdpState = true;
        }

        // Station keeping ignores gamepad by disabling the receiver.
        // Make sure no previous suppression flag remains stuck.
        SetUdpSuppressCommandOutput(false);
        udpReceiver.enabled = false;
    }

    private void RestoreGamepadReceiver()
    {
        if (udpReceiver == null)
        {
            return;
        }

        SetUdpSuppressCommandOutput(false);

        if (forceUdpReceiverOnWhenReleased)
        {
            udpReceiver.enabled = true;
        }
        else if (cachedUdpState)
        {
            udpReceiver.enabled = udpWasEnabledBeforeHold;
        }

        cachedUdpState = false;
    }

    private void UpdateDebugState()
    {
        if (aquaHold != null)
        {
            holdTargetWorld = aquaHold.targetPositionWorld;
            holdTargetYawDeg = aquaHold.targetYawDeg;
        }

        if (!stationKeepingActive)
        {
            activeMode = "Manual Gamepad";
        }
    }

    private void OnDisable()
    {
        RestoreGamepadReceiver();
    }

    private void OnApplicationQuit()
    {
        RestoreGamepadReceiver();
    }
}
