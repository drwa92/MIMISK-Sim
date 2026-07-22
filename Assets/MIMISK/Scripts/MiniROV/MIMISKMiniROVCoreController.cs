using System;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKMiniROVCoreController : MonoBehaviour
{
    public enum BackendMode
    {
        UnityNative,
        ESP_Raspberry_HIL
    }

    public enum ControlMode
    {
        Disabled,
        Hold,
        MissionTracking,
        ReturnHome,
        GamepadManual
    }

    [Header("References")]
    public Rigidbody rb;
    public ControlManager controlManager;

    [Tooltip("Optional depth controller. Accessed by reflection to keep the stack flexible.")]
    public Behaviour ballastDepthController;

    public Behaviour unityVirtualESP32;
    public Behaviour mimiskWaterInteraction;
    public Behaviour simpleRovBuoyancy;
    public Behaviour sensorManager;

    [Header("Backend")]
    public BackendMode backendMode = BackendMode.UnityNative;
    public ControlMode controlMode = ControlMode.Disabled;
    public bool controllerEnabled = true;

    public bool injectMotorFramesToControlManager = true;
    public bool disableESPBridgeInUnityNative = true;
    public bool disableControlManagerSerialReaderInUnityNative = true;

    [Tooltip("For imported models, body-forward is often more reliable than thruster-local forward.")]
    public bool useBodyForwardForThrustersInUnityNative = false;

    [Header("Reference")]
    public bool hasReference;
    public MIMISKMiniROVReference currentReference;
    public Vector3 homePointWorld;

    [Header("Manual Command")]
    [Range(-1, 1)] public float manualSurge;
    [Range(-1, 1)] public float manualYaw;
    [Range(-1, 1)] public float manualDepthNudge;

    [Header("Horizontal Control")]
    public float surgeKp = 0.95f;
    public float surgeKd = 0.85f;
    public float maxSurgeCommand = 0.75f;

    public float yawKp = 0.026f;
    public float yawKd = 0.012f;
    public float maxYawCommand = 0.65f;

    [Range(0, 255)] public int maxThrusterPwm = 190;

    public bool invertSurge = false;
    public bool invertYawDifferential = false;
    public bool swapLeftRight = false;
    public bool invertLeftThruster = false;
    public bool invertRightThruster = false;

    [Header("Bearing Guidance")]
    public bool preferBearingYawToPathYaw = true;
    public float yawErrorStopSurgeDeg = 75.0f;
    public float distanceForFullSurgeM = 1.0f;
    public float minimumSurgeWhileTurning = 0.05f;
    public float commandSmoothingRate = 8.0f;

    [Header("Depth Control")]
    public bool useBallastDepthController = true;
    public float defaultWaterLevel = 0.0f;
    public float manualDepthNudgeRateMPS = 0.10f;
    public float manualDepthTargetMeters = 0.8f;

    [Header("Diagnostics")]
    public int leftThrusterCmd;
    public int rightThrusterCmd;
    public int dcPortCmd;
    public int dcStarboardCmd;

    public int smoothedLeftThrusterCmd;
    public int smoothedRightThrusterCmd;

    public float surgeCommand;
    public float yawCommand;
    public float horizontalDistanceToReferenceM;
    public float distanceToReferenceM;
    public float yawErrorDeg;
    public float targetYawDeg;
    public float surgeAlignmentFactor;
    public bool commandNonZero;
    public bool injectedThisFrame;
    public string lastEvent = "idle";

    private MethodInfo stopReaderMethod;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void FixedUpdate()
    {
        injectedThisFrame = false;

        if (!controllerEnabled || backendMode != BackendMode.UnityNative)
        {
            return;
        }

        if (controlMode == ControlMode.Disabled)
        {
            SendMotorCommand(0, 0, 0, 0);
            return;
        }

        if (controlMode == ControlMode.Hold)
        {
            SendMotorCommand(0, 0, 0, 0);
            lastEvent = "hold_zero_command";
            return;
        }

        if (controlMode == ControlMode.GamepadManual)
        {
            RunManualController(Time.fixedDeltaTime);
            return;
        }

        if ((controlMode == ControlMode.MissionTracking ||
             controlMode == ControlMode.ReturnHome) &&
            hasReference &&
            currentReference.valid)
        {
            RunReferenceController(Time.fixedDeltaTime);
            return;
        }

        SendMotorCommand(0, 0, 0, 0);
        lastEvent = "no_valid_reference_zero_command";
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (controlManager == null)
        {
            controlManager = GetComponent<ControlManager>();
        }

        if (ballastDepthController == null)
        {
            ballastDepthController = FindBehaviourByTypeName("BallastDepthController");
        }

        if (unityVirtualESP32 == null)
        {
            unityVirtualESP32 = FindBehaviourByTypeName("UnityVirtualESP32");
        }

        if (mimiskWaterInteraction == null)
        {
            mimiskWaterInteraction = FindBehaviourByTypeName("MIMISKWaterInteraction");
        }

        if (simpleRovBuoyancy == null)
        {
            simpleRovBuoyancy = FindBehaviourByTypeName("SimpleROVBuoyancy");
        }

        if (sensorManager == null)
        {
            sensorManager = FindBehaviourByTypeName("SensorManager");
        }

        ConfigureControlManagerReferences();
        CachePrivateMethods();
    }

    private void ConfigureControlManagerReferences()
    {
        if (controlManager == null)
        {
            return;
        }

        if (rb != null)
        {
            controlManager.rb = rb;
        }

        if (controlManager.leftThruster == null)
        {
            Transform left =
                FindDeepChild(transform, "propulseur_gauche");

            if (left != null)
            {
                controlManager.leftThruster = left;
            }
        }

        if (controlManager.rightThruster == null)
        {
            Transform right =
                FindDeepChild(transform, "propulseur_droite");

            if (right != null)
            {
                controlManager.rightThruster = right;
            }
        }

        if (useBodyForwardForThrustersInUnityNative)
        {
            controlManager.useThrusterTransformForward = false;
        }

        controlManager.autoOpenOnStart = false;
        controlManager.thrusterMaxForce = Mathf.Max(3.0f, controlManager.thrusterMaxForce);
        controlManager.thrusterDeadzonePwm = Mathf.Min(controlManager.thrusterDeadzonePwm, 8);
    }

    [ContextMenu("Activate Unity Native Backend")]
    public void ActivateUnityNativeBackend()
    {
        AutoFindReferences();

        backendMode = BackendMode.UnityNative;
        controllerEnabled = true;

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
            ConfigureControlManagerReferences();
            InvokeControlManagerStopReaderIfAvailable();
        }

        if (disableESPBridgeInUnityNative && unityVirtualESP32 != null)
        {
            unityVirtualESP32.enabled = false;
        }

        if (mimiskWaterInteraction != null)
        {
            mimiskWaterInteraction.enabled = true;
        }

        if (simpleRovBuoyancy != null)
        {
            simpleRovBuoyancy.enabled = true;
        }

        if (sensorManager != null)
        {
            sensorManager.enabled = false;
        }

        if (ballastDepthController != null && useBallastDepthController)
        {
            ballastDepthController.enabled = true;
            SetBoolMember(ballastDepthController, "enableDepthHold", true);
            SetBoolMember(ballastDepthController, "enableKeyboardTargetControl", false);
            SetFloatMember(ballastDepthController, "waterLevel", defaultWaterLevel);
        }

        lastEvent = "unity_native_backend_active";
    }

    [ContextMenu("Activate ESP Raspberry HIL Backend")]
    public void ActivateESPRaspberryBackend()
    {
        AutoFindReferences();

        backendMode = BackendMode.ESP_Raspberry_HIL;
        controllerEnabled = false;
        controlMode = ControlMode.Disabled;
        hasReference = false;

        if (controlManager != null)
        {
            controlManager.enabled = true;
        }

        if (unityVirtualESP32 != null)
        {
            unityVirtualESP32.enabled = true;
        }

        if (mimiskWaterInteraction != null)
        {
            mimiskWaterInteraction.enabled = true;
        }

        if (simpleRovBuoyancy != null)
        {
            simpleRovBuoyancy.enabled = true;
        }

        if (sensorManager != null)
        {
            sensorManager.enabled = false;
        }

        lastEvent = "esp_raspberry_hil_backend_active";
    }

    public void SetControlMode(ControlMode mode)
    {
        controlMode = mode;

        if (mode == ControlMode.Disabled || mode == ControlMode.Hold)
        {
            ClearReferenceAndStop();
        }

        lastEvent = "control_mode_" + mode;
    }

    public void SetReference(MIMISKMiniROVReference reference)
    {
        currentReference = reference;
        hasReference = reference.valid;

        if (reference.valid && useBallastDepthController)
        {
            SetDepthTarget(reference.depthMeters);
        }
    }

    public void SetManualCommand(float surge, float yaw, float depthNudge)
    {
        manualSurge = Mathf.Clamp(surge, -1.0f, 1.0f);
        manualYaw = Mathf.Clamp(yaw, -1.0f, 1.0f);
        manualDepthNudge = Mathf.Clamp(depthNudge, -1.0f, 1.0f);
    }

    public void ClearReferenceAndStop()
    {
        hasReference = false;
        currentReference = new MIMISKMiniROVReference();
        smoothedLeftThrusterCmd = 0;
        smoothedRightThrusterCmd = 0;
        SendMotorCommand(0, 0, 0, 0);
        lastEvent = "reference_cleared_stop_command";
    }

    public void SetDepthTarget(float depthMeters)
    {
        if (ballastDepthController == null)
        {
            return;
        }

        ballastDepthController.enabled = true;
        SetBoolMember(ballastDepthController, "enableDepthHold", true);
        SetFloatMember(ballastDepthController, "targetDepthMeters", Mathf.Max(0.0f, depthMeters));
        SetFloatMember(ballastDepthController, "waterLevel", defaultWaterLevel);
    }

    private void RunManualController(float dt)
    {
        if (Mathf.Abs(manualDepthNudge) > 0.05f)
        {
            manualDepthTargetMeters =
                Mathf.Max(
                    0.0f,
                    manualDepthTargetMeters + manualDepthNudge * manualDepthNudgeRateMPS * dt
                );

            SetDepthTarget(manualDepthTargetMeters);
        }

        float surge = invertSurge ? -manualSurge : manualSurge;
        float yaw = invertYawDifferential ? -manualYaw : manualYaw;

        surgeCommand = surge;
        yawCommand = yaw;

        float left = surge - yaw;
        float right = surge + yaw;

        SendNormalizedThrusterCommand(left, right, dt);

        lastEvent =
            "gamepad_manual_surge_" +
            surge.ToString("F2") +
            "_yaw_" +
            yaw.ToString("F2");
    }

    private void RunReferenceController(float dt)
    {
        if (rb == null || controlManager == null)
        {
            lastEvent = "missing_rigidbody_or_control_manager";
            return;
        }

        Vector3 errorWorld =
            currentReference.positionWorld - rb.position;

        Vector3 horizontalError =
            new Vector3(errorWorld.x, 0.0f, errorWorld.z);

        horizontalDistanceToReferenceM =
            horizontalError.magnitude;

        distanceToReferenceM =
            errorWorld.magnitude;

        Vector3 localVelocity =
            transform.InverseTransformDirection(rb.linearVelocity);

        float desiredYawDeg;

        if (currentReference.hasYaw && !preferBearingYawToPathYaw)
        {
            desiredYawDeg = currentReference.yawDeg;
        }
        else if (horizontalError.sqrMagnitude > 0.0001f)
        {
            desiredYawDeg =
                Mathf.Atan2(horizontalError.x, horizontalError.z) *
                Mathf.Rad2Deg;
        }
        else
        {
            desiredYawDeg = transform.eulerAngles.y;
        }

        targetYawDeg = desiredYawDeg;

        yawErrorDeg =
            Mathf.DeltaAngle(transform.eulerAngles.y, desiredYawDeg);

        float yawRateDegS =
            rb.angularVelocity.y * Mathf.Rad2Deg;

        float yaw =
            yawKp * yawErrorDeg -
            yawKd * yawRateDegS;

        yaw =
            Mathf.Clamp(
                yaw,
                -Mathf.Abs(maxYawCommand),
                Mathf.Abs(maxYawCommand)
            );

        if (invertYawDifferential)
        {
            yaw = -yaw;
        }

        float distanceScale =
            Mathf.Clamp01(
                horizontalDistanceToReferenceM /
                Mathf.Max(0.05f, distanceForFullSurgeM)
            );

        float alignment =
            1.0f -
            Mathf.Clamp01(
                Mathf.Abs(yawErrorDeg) /
                Mathf.Max(1.0f, yawErrorStopSurgeDeg)
            );

        alignment =
            Mathf.Clamp(
                alignment,
                minimumSurgeWhileTurning,
                1.0f
            );

        surgeAlignmentFactor = alignment;

        float surge =
            surgeKp * distanceScale * alignment -
            surgeKd * Mathf.Max(0.0f, localVelocity.z);

        surge =
            Mathf.Clamp(
                surge,
                -Mathf.Abs(maxSurgeCommand),
                Mathf.Abs(maxSurgeCommand)
            );

        if (invertSurge)
        {
            surge = -surge;
        }

        surgeCommand = surge;
        yawCommand = yaw;

        float left = surge - yaw;
        float right = surge + yaw;

        SendNormalizedThrusterCommand(left, right, dt);

        lastEvent =
            "tracking_distance_" +
            horizontalDistanceToReferenceM.ToString("F2") +
            "_yawerr_" +
            yawErrorDeg.ToString("F1") +
            "_cmdL_" +
            leftThrusterCmd +
            "_cmdR_" +
            rightThrusterCmd;
    }

    private void SendNormalizedThrusterCommand(float left, float right, float dt)
    {
        left = Mathf.Clamp(left, -1.0f, 1.0f);
        right = Mathf.Clamp(right, -1.0f, 1.0f);

        int rawLeft =
            Mathf.RoundToInt(left * maxThrusterPwm);

        int rawRight =
            Mathf.RoundToInt(right * maxThrusterPwm);

        if (swapLeftRight)
        {
            int tmp = rawLeft;
            rawLeft = rawRight;
            rawRight = tmp;
        }

        if (invertLeftThruster)
        {
            rawLeft = -rawLeft;
        }

        if (invertRightThruster)
        {
            rawRight = -rawRight;
        }

        float alpha =
            1.0f -
            Mathf.Exp(
                -Mathf.Max(0.1f, commandSmoothingRate) *
                Mathf.Max(0.001f, dt)
            );

        smoothedLeftThrusterCmd =
            Mathf.RoundToInt(
                Mathf.Lerp(smoothedLeftThrusterCmd, rawLeft, alpha)
            );

        smoothedRightThrusterCmd =
            Mathf.RoundToInt(
                Mathf.Lerp(smoothedRightThrusterCmd, rawRight, alpha)
            );

        SendMotorCommand(
            smoothedLeftThrusterCmd,
            smoothedRightThrusterCmd,
            dcPortCmd,
            dcStarboardCmd
        );
    }

    private void SendMotorCommand(int left, int right, int dcPort, int dcStarboard)
    {
        leftThrusterCmd = Mathf.Clamp(left, -255, 255);
        rightThrusterCmd = Mathf.Clamp(right, -255, 255);
        dcPortCmd = Mathf.Clamp(dcPort, -255, 255);
        dcStarboardCmd = Mathf.Clamp(dcStarboard, -255, 255);

        commandNonZero =
            Mathf.Abs(leftThrusterCmd) > 0 ||
            Mathf.Abs(rightThrusterCmd) > 0 ||
            Mathf.Abs(dcPortCmd) > 0 ||
            Mathf.Abs(dcStarboardCmd) > 0;

        injectedThisFrame = false;

        if (!injectMotorFramesToControlManager || controlManager == null)
        {
            return;
        }

        controlManager.InjectMotorFrame(
            (short)leftThrusterCmd,
            (short)rightThrusterCmd,
            (short)dcPortCmd,
            (short)dcStarboardCmd
        );

        injectedThisFrame = true;
    }

    private void CachePrivateMethods()
    {
        if (controlManager == null)
        {
            return;
        }

        stopReaderMethod =
            controlManager.GetType().GetMethod(
                "StopReader",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );
    }

    private void InvokeControlManagerStopReaderIfAvailable()
    {
        if (controlManager == null)
        {
            return;
        }

        CachePrivateMethods();

        if (stopReaderMethod != null)
        {
            try
            {
                stopReaderMethod.Invoke(controlManager, null);
            }
            catch
            {
            }
        }
    }

    private void SetBoolMember(object target, string name, bool value)
    {
        if (target == null)
        {
            return;
        }

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        FieldInfo f = target.GetType().GetField(name, flags);

        if (f != null && f.FieldType == typeof(bool))
        {
            f.SetValue(target, value);
            return;
        }

        PropertyInfo p = target.GetType().GetProperty(name, flags);

        if (p != null && p.PropertyType == typeof(bool) && p.CanWrite)
        {
            p.SetValue(target, value, null);
        }
    }

    private void SetFloatMember(object target, string name, float value)
    {
        if (target == null)
        {
            return;
        }

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        FieldInfo f = target.GetType().GetField(name, flags);

        if (f != null && f.FieldType == typeof(float))
        {
            f.SetValue(target, value);
            return;
        }

        PropertyInfo p = target.GetType().GetProperty(name, flags);

        if (p != null && p.PropertyType == typeof(float) && p.CanWrite)
        {
            p.SetValue(target, value, null);
        }
    }

    private Behaviour FindBehaviourByTypeName(string typeName)
    {
        Behaviour[] behaviours =
            GetComponentsInChildren<Behaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null &&
                behaviours[i].GetType().Name == typeName)
            {
                return behaviours[i];
            }
        }

        return null;
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
