using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MIMISKMiniROVStandaloneManualTest : MonoBehaviour
{
    public enum TestState
    {
        Disabled,
        PassiveKinematic,
        PassiveDynamicNoScripts,
        WaterInteractionOnly,
        ExternalControlActive,
        Fault
    }

    [Header("References")]
    public Transform miniRovRoot;
    public Rigidbody miniRovRigidbody;
    public Collider[] miniRovColliders;

    [Header("Standalone Test Pose")]
    public bool detachFromDroneOrCableOnPrepare = true;
    public bool placeAtTestPoseOnPrepare = true;
    public Vector3 testWorldPosition = new Vector3(0.0f, -0.50f, -3.0f);
    public Vector3 testWorldEuler = Vector3.zero;
    public bool forceLocalScaleOnPrepare = true;
    public Vector3 localScaleOnPrepare = Vector3.one;

    [Header("Physics")]
    public float miniRovMassKg = 0.60f;

    [Tooltip("Keep colliders OFF first. Turn ON later only if the MiniROV needs collision with terrain/obstacles.")]
    public bool enableCollidersWhenDynamic = false;

    [Tooltip("Use gravity when MIMISKWaterInteraction is active.")]
    public bool useGravityWithWaterInteraction = true;

    [Header("External Stack Gate")]
    public bool requireExternalSerialBeforeControl = true;
    public string unityEsp32SerialDevice = "/dev/unity_esp32";
    public bool externalStackReady;
    public string externalStackStatus = "not_checked";

    [Header("Keyboard")]
    public Key prepareKey = Key.P;
    public Key passiveDynamicKey = Key.J;
    public Key waterOnlyKey = Key.O;
    public Key controlKey = Key.I;
    public Key killKey = Key.K;
    public Key resetPoseKey = Key.L;

    [Header("Runtime")]
    public bool testEnabled = true;
    public TestState testState = TestState.PassiveKinematic;
    public string lastEvent = "initialized";
    public bool waterInteractionEnabled;
    public bool controlEnabled;
    public bool unityVirtualEsp32Enabled;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        ApplyPassiveKinematic();
    }

    private void Update()
    {
        if (!testEnabled)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[prepareKey].wasPressedThisFrame)
        {
            PrepareStandaloneMiniROV();
        }

        if (Keyboard.current[passiveDynamicKey].wasPressedThisFrame)
        {
            EnablePassiveDynamicOnly();
        }

        if (Keyboard.current[waterOnlyKey].wasPressedThisFrame)
        {
            EnableWaterInteractionOnly();
        }

        if (Keyboard.current[controlKey].wasPressedThisFrame)
        {
            EnableExternalControl();
        }

        if (Keyboard.current[killKey].wasPressedThisFrame)
        {
            ApplyPassiveKinematic();
        }

        if (Keyboard.current[resetPoseKey].wasPressedThisFrame)
        {
            ResetPoseOnly();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (miniRovRoot == null)
        {
            miniRovRoot = transform;
        }

        if (miniRovRigidbody == null && miniRovRoot != null)
        {
            miniRovRigidbody = miniRovRoot.GetComponent<Rigidbody>();
        }

        if (miniRovRoot != null &&
            (miniRovColliders == null || miniRovColliders.Length == 0))
        {
            miniRovColliders = miniRovRoot.GetComponentsInChildren<Collider>(true);
        }
    }

    [ContextMenu("Prepare Standalone MiniROV")]
    public void PrepareStandaloneMiniROV()
    {
        AutoFindReferences();

        if (miniRovRoot == null)
        {
            testState = TestState.Fault;
            lastEvent = "prepare_failed_missing_minirov";
            return;
        }

        if (detachFromDroneOrCableOnPrepare)
        {
            miniRovRoot.SetParent(null, true);
        }

        if (placeAtTestPoseOnPrepare)
        {
            miniRovRoot.position = testWorldPosition;
            miniRovRoot.rotation = Quaternion.Euler(testWorldEuler);
        }

        if (forceLocalScaleOnPrepare)
        {
            miniRovRoot.localScale = localScaleOnPrepare;
        }

        if (miniRovRigidbody == null)
        {
            miniRovRigidbody = miniRovRoot.GetComponent<Rigidbody>();

            if (miniRovRigidbody == null)
            {
                miniRovRigidbody = miniRovRoot.gameObject.AddComponent<Rigidbody>();
            }
        }

        miniRovRigidbody.mass = Mathf.Max(0.05f, miniRovMassKg);
        miniRovRigidbody.isKinematic = true;
        miniRovRigidbody.useGravity = false;
        miniRovRigidbody.linearVelocity = Vector3.zero;
        miniRovRigidbody.angularVelocity = Vector3.zero;

        SetMiniRovColliders(false);
        SetFinalMiniRovComponents(false, false);

        testState = TestState.PassiveKinematic;
        lastEvent = "standalone_prepared_passive_kinematic";

        Debug.Log("[MIMISK] MiniROV standalone prepared. Press J dynamic-only, O water-only, I external control.");
    }

    [ContextMenu("Apply Passive Kinematic")]
    public void ApplyPassiveKinematic()
    {
        AutoFindReferences();

        if (miniRovRigidbody != null)
        {
            if (!miniRovRigidbody.isKinematic)
            {
                miniRovRigidbody.linearVelocity = Vector3.zero;
                miniRovRigidbody.angularVelocity = Vector3.zero;
            }

            miniRovRigidbody.isKinematic = true;
            miniRovRigidbody.useGravity = false;
        }

        SetMiniRovColliders(false);
        SetFinalMiniRovComponents(false, false);

        testState = TestState.PassiveKinematic;
        lastEvent = "passive_kinematic_all_minirov_scripts_off";
    }

    [ContextMenu("Enable Passive Dynamic Only")]
    public void EnablePassiveDynamicOnly()
    {
        AutoFindReferences();

        if (miniRovRigidbody == null)
        {
            testState = TestState.Fault;
            lastEvent = "passive_dynamic_failed_missing_rigidbody";
            return;
        }

        miniRovRigidbody.isKinematic = false;
        miniRovRigidbody.useGravity = false;
        miniRovRigidbody.linearVelocity = Vector3.zero;
        miniRovRigidbody.angularVelocity = Vector3.zero;

        SetMiniRovColliders(false);
        SetFinalMiniRovComponents(false, false);

        testState = TestState.PassiveDynamicNoScripts;
        lastEvent = "passive_dynamic_no_scripts_no_gravity_no_colliders";

        Debug.Log("[MIMISK] J: MiniROV Rigidbody dynamic only. No water/control/colliders.");
    }

    [ContextMenu("Enable Water Interaction Only")]
    public void EnableWaterInteractionOnly()
    {
        AutoFindReferences();

        if (miniRovRigidbody == null)
        {
            testState = TestState.Fault;
            lastEvent = "water_only_failed_missing_rigidbody";
            return;
        }

        miniRovRigidbody.isKinematic = false;
        miniRovRigidbody.useGravity = useGravityWithWaterInteraction;

        SetMiniRovColliders(enableCollidersWhenDynamic);

        // Only MIMISKWaterInteraction ON.
        // UnityVirtualESP32 and ControlManager stay OFF.
        SetFinalMiniRovComponents(true, false);

        testState = TestState.WaterInteractionOnly;
        lastEvent = "water_interaction_only_enabled";

        Debug.Log("[MIMISK] O: MIMISKWaterInteraction enabled only. Control still OFF.");
    }

    [ContextMenu("Enable External Control")]
    public void EnableExternalControl()
    {
        AutoFindReferences();

        if (miniRovRigidbody == null)
        {
            testState = TestState.Fault;
            lastEvent = "control_failed_missing_rigidbody";
            return;
        }

        if (!CheckExternalStackReady())
        {
            lastEvent = "control_rejected_external_stack_not_ready_" + externalStackStatus;

            Debug.LogWarning(
                "[MIMISK] MiniROV external control not enabled. " +
                "Start socat, my_app and command_sender first. Status: " +
                externalStackStatus
            );

            return;
        }

        miniRovRigidbody.isKinematic = false;
        miniRovRigidbody.useGravity = useGravityWithWaterInteraction;

        SetMiniRovColliders(enableCollidersWhenDynamic);

        // Final working stack:
        // MIMISKWaterInteraction + UnityVirtualESP32 + ControlManager.
        SetFinalMiniRovComponents(true, true);

        testState = TestState.ExternalControlActive;
        lastEvent = "external_control_active_final_stack";

        Debug.Log("[MIMISK] I: MiniROV external stack active. Use Raspberry/ESP/gamepad control.");
    }

    [ContextMenu("Reset Pose Only")]
    public void ResetPoseOnly()
    {
        AutoFindReferences();

        if (miniRovRoot == null)
        {
            return;
        }

        miniRovRoot.SetParent(null, true);
        miniRovRoot.position = testWorldPosition;
        miniRovRoot.rotation = Quaternion.Euler(testWorldEuler);

        if (forceLocalScaleOnPrepare)
        {
            miniRovRoot.localScale = localScaleOnPrepare;
        }

        if (miniRovRigidbody != null && !miniRovRigidbody.isKinematic)
        {
            miniRovRigidbody.linearVelocity = Vector3.zero;
            miniRovRigidbody.angularVelocity = Vector3.zero;
        }

        lastEvent = "pose_reset_only";
    }

    public bool CheckExternalStackReady()
    {
        if (!requireExternalSerialBeforeControl)
        {
            externalStackReady = true;
            externalStackStatus = "serial_check_disabled";
            return true;
        }

        if (string.IsNullOrEmpty(unityEsp32SerialDevice))
        {
            externalStackReady = true;
            externalStackStatus = "empty_serial_path_check_passed";
            return true;
        }

        externalStackReady = File.Exists(unityEsp32SerialDevice);
        externalStackStatus = externalStackReady
            ? "serial_ready_" + unityEsp32SerialDevice
            : "missing_serial_" + unityEsp32SerialDevice;

        return externalStackReady;
    }

    private void SetMiniRovColliders(bool enabled)
    {
        if (miniRovColliders == null)
        {
            return;
        }

        for (int i = 0; i < miniRovColliders.Length; i++)
        {
            if (miniRovColliders[i] != null)
            {
                miniRovColliders[i].enabled = enabled;
            }
        }
    }

    private void SetFinalMiniRovComponents(bool enableWaterInteraction, bool enableExternalControl)
    {
        waterInteractionEnabled = false;
        controlEnabled = false;
        unityVirtualEsp32Enabled = false;

        if (miniRovRoot == null)
        {
            return;
        }

        Behaviour[] behaviours = miniRovRoot.GetComponentsInChildren<Behaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour b = behaviours[i];

            if (b == null)
            {
                continue;
            }

            string typeName = b.GetType().Name;

            bool isWaterInteraction =
                ContainsIgnoreCase(typeName, "MIMISKWaterInteraction");

            bool isUnityVirtualEsp32 =
                ContainsIgnoreCase(typeName, "UnityVirtualESP32");

            bool isControlManager =
                ContainsIgnoreCase(typeName, "ControlManager");

            bool forceDisabled =
                ContainsIgnoreCase(typeName, "SensorManager") ||
                ContainsIgnoreCase(typeName, "SimpleROVBuoyancy");

            if (forceDisabled)
            {
                b.enabled = false;
            }
            else if (isWaterInteraction)
            {
                b.enabled = enableWaterInteraction;
                waterInteractionEnabled |= b.enabled;
            }
            else if (isUnityVirtualEsp32)
            {
                b.enabled = enableExternalControl;
                unityVirtualEsp32Enabled |= b.enabled;
            }
            else if (isControlManager)
            {
                b.enabled = enableExternalControl;
                controlEnabled |= b.enabled;
            }
        }
    }

    private bool ContainsIgnoreCase(string text, string pattern)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        return text.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
