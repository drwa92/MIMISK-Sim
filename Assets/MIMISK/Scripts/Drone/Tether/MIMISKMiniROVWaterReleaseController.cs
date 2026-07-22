using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MIMISKMiniROVWaterReleaseController : MonoBehaviour
{
    public enum WaterReleaseState
    {
        Disabled,
        CableFollowKinematic,
        TouchingWater,
        ReleasedDynamicStabilizing,
        ROVControlActive,
        KinematicRecovery,
        Fault
    }

    [Header("References")]
    public MIMISKDroneCoreTetherManager tetherManager;
    public MIMISKMiniROVCableEndAttachmentManager cableAttachmentManager;

    [Header("MiniROV")]
    public Transform miniRovRoot;
    public Rigidbody miniRovRigidbody;
    public Transform miniRovTetherAnchor;
    public Collider[] miniRovColliders;

    [Header("Cable End Visual")]
    public Transform yellowCableEndPoint;
    public Transform cableEndFollowRoot;

    [Header("Water Release Logic")]
    public bool releaseEnabled = true;
    public WaterReleaseState releaseState = WaterReleaseState.CableFollowKinematic;

    public float waterSurfaceY = 0.0f;

    [Tooltip("MiniROV is released when its tether/cable point is below this height above water.")]
    public float waterTouchMarginM = 0.03f;

    [Tooltip("Delay after water contact before enabling MiniROV control.")]
    public float postReleaseStabilizationSeconds = 1.50f;

    [Tooltip("Give the MiniROV a small downward velocity when it becomes dynamic.")]
    public bool applyInitialDownwardVelocity = true;

    public float initialDownwardVelocityMS = 0.15f;

    [Header("ROV Physics Activation")]
    public bool enableGravityWhenReleased = true;
    public bool enableCollidersWhenReleased = true;

    [Tooltip("No tether force for this phase. The cable is visual/logical only.")]
    public bool disableTetherForcesForNow = true;

    [Header("ROV Component Activation")]
    public bool disableRovControlWhileCableFollowing = true;
    public bool enableRovControlAfterStabilization = true;

    [Tooltip("Class-name keywords enabled after water release. These match your MiniROV stack.")]
    public string[] rovControlComponentKeywords =
    {
        "ControlManager",
        "SensorManager",
        "UnityVirtualESP32",
        "MIMISKWaterInteraction",
        "SimpleROVBuoyancy"
    };

    [Tooltip("Keep these enabled even before release if needed. Usually empty.")]
    public string[] keepEnabledKeywords =
    {
        "Transform"
    };

    [Header("Recovery")]
    public bool enableKinematicRecoveryWhenWinchRecovers = true;
    public float recoveredLengthToleranceM = 0.04f;

    [Header("Keyboard Debug")]
    public Key forceReleaseKey = Key.J;
    public Key forceReattachKey = Key.D;

    [Header("Runtime")]
    public bool miniRovIsDynamic;
    public bool miniRovControlEnabled;
    public bool cableVisuallyAttachedToRov;
    public bool waterContactDetected;

    public float stateTimerS;
    public float distanceToCableEndM;
    public string lastEvent = "initialized";

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        ConfigureInitialCableFollowState();
    }

    private void Update()
    {
        if (!releaseEnabled)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[forceReleaseKey].wasPressedThisFrame)
        {
            ReleaseMiniROVToWaterDynamic();
        }

        if (Keyboard.current[forceReattachKey].wasPressedThisFrame)
        {
            ReattachMiniROVToCableEnd();
        }
    }

    private void FixedUpdate()
    {
        if (!releaseEnabled)
        {
            return;
        }

        stateTimerS += Time.fixedDeltaTime;

        UpdateMeasurements();

        if (releaseState == WaterReleaseState.CableFollowKinematic)
        {
            UpdateCableFollowKinematic();
        }
        else if (releaseState == WaterReleaseState.TouchingWater)
        {
            ReleaseMiniROVToWaterDynamic();
        }
        else if (releaseState == WaterReleaseState.ReleasedDynamicStabilizing)
        {
            UpdateReleasedStabilizing();
        }
        else if (releaseState == WaterReleaseState.ROVControlActive)
        {
            UpdateRovControlActive();
        }
        else if (releaseState == WaterReleaseState.KinematicRecovery)
        {
            UpdateKinematicRecovery();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (tetherManager == null)
        {
            tetherManager = GetComponent<MIMISKDroneCoreTetherManager>();
        }

        if (cableAttachmentManager == null)
        {
            cableAttachmentManager = GetComponent<MIMISKMiniROVCableEndAttachmentManager>();
        }

        if (miniRovRoot == null)
        {
            GameObject rov = GameObject.Find("MiniROV");

            if (rov != null)
            {
                miniRovRoot = rov.transform;
            }
        }

        if (miniRovRoot != null)
        {
            if (miniRovRigidbody == null)
            {
                miniRovRigidbody = miniRovRoot.GetComponent<Rigidbody>();
            }

            if (miniRovColliders == null || miniRovColliders.Length == 0)
            {
                miniRovColliders = miniRovRoot.GetComponentsInChildren<Collider>(true);
            }

            if (miniRovTetherAnchor == null)
            {
                miniRovTetherAnchor = FindDeepChild(miniRovRoot, "ROV_TetherAnchor");
            }

            if (miniRovTetherAnchor == null)
            {
                miniRovTetherAnchor = FindDeepChild(miniRovRoot, "MiniROV_TetherPoint");
            }

            if (miniRovTetherAnchor == null)
            {
                miniRovTetherAnchor = FindDeepChild(miniRovRoot, "TetherPoint");
            }
        }

        if (yellowCableEndPoint == null)
        {
            yellowCableEndPoint = FindDeepChild(transform, "real_mesh_short_yellow_deployment_cable_to_hook");
        }

        if (cableEndFollowRoot == null)
        {
            cableEndFollowRoot = FindDeepChild(transform, "MiniROV_CableEndFollowRoot");
        }

        if (cableAttachmentManager != null)
        {
            if (yellowCableEndPoint == null)
            {
                yellowCableEndPoint = cableAttachmentManager.yellowCableEndPoint;
            }

            if (cableEndFollowRoot == null)
            {
                cableEndFollowRoot = cableAttachmentManager.cableEndFollowRoot;
            }
        }
    }

    [ContextMenu("Configure Initial Cable Follow State")]
    public void ConfigureInitialCableFollowState()
    {
        AutoFindReferences();

        if (disableTetherForcesForNow && tetherManager != null)
        {
            tetherManager.enableTetherForceWhenMiniRovAttached = false;
            tetherManager.tetherStiffnessNPerM = 0.0f;
            tetherManager.tetherDampingNPerMS = 0.0f;
            tetherManager.maximumSafeTensionN = 999999.0f;
        }

        if (miniRovRigidbody != null)
        {
            miniRovRigidbody.isKinematic = true;
            miniRovRigidbody.useGravity = false;
        }

        SetMiniRovColliders(false);

        if (disableRovControlWhileCableFollowing)
        {
            SetMiniRovControlComponents(false, false);
        }

        if (tetherManager != null)
        {
            tetherManager.movingTetherEndVisual = yellowCableEndPoint;
            tetherManager.useVirtualEndpointWhenNoMiniRov = true;
            tetherManager.miniRovRigidbody = null;
            tetherManager.miniRovTetherPoint = null;
        }

        miniRovIsDynamic = false;
        miniRovControlEnabled = false;
        cableVisuallyAttachedToRov = false;
        releaseState = WaterReleaseState.CableFollowKinematic;
        stateTimerS = 0.0f;
        lastEvent = "initial_cable_follow_kinematic";
    }

    private void UpdateCableFollowKinematic()
    {
        if (ShouldReleaseAtWater())
        {
            releaseState = WaterReleaseState.TouchingWater;
            stateTimerS = 0.0f;
            lastEvent = "water_contact_detected";
            return;
        }

        if (enableKinematicRecoveryWhenWinchRecovers &&
            tetherManager != null &&
            tetherManager.tetherState == MIMISKDroneCoreTetherManager.TetherState.Recovering)
        {
            BeginKinematicRecovery();
        }
    }

    private bool ShouldReleaseAtWater()
    {
        if (miniRovRoot == null)
        {
            return false;
        }

        Vector3 p =
            miniRovTetherAnchor != null
                ? miniRovTetherAnchor.position
                : miniRovRoot.position;

        waterContactDetected =
            p.y <= waterSurfaceY + waterTouchMarginM;

        return waterContactDetected;
    }

    [ContextMenu("Release MiniROV To Water Dynamic")]
    public void ReleaseMiniROVToWaterDynamic()
    {
        AutoFindReferences();

        if (miniRovRoot == null || miniRovRigidbody == null)
        {
            releaseState = WaterReleaseState.Fault;
            lastEvent = "release_failed_missing_minirov";
            return;
        }

        // Detach from safe cable-follow root into world space.
        miniRovRoot.SetParent(null, true);

        miniRovRigidbody.isKinematic = false;
        miniRovRigidbody.useGravity = enableGravityWhenReleased;

        if (applyInitialDownwardVelocity)
        {
            Vector3 v = miniRovRigidbody.linearVelocity;
            v.y = Mathf.Min(v.y, -Mathf.Abs(initialDownwardVelocityMS));
            miniRovRigidbody.linearVelocity = v;
        }

        SetMiniRovColliders(enableCollidersWhenReleased);

        // Enable water physics/buoyancy now, but keep user control disabled
        // until the short stabilization delay is finished.
        SetMiniRovControlComponents(true, false);

        if (tetherManager != null)
        {
            tetherManager.movingTetherEndVisual = yellowCableEndPoint;

            // The line endpoint should now follow the MiniROV tether anchor.
            // No force is applied yet because enableTetherForceWhenMiniRovAttached is false.
            tetherManager.miniRovRigidbody = miniRovRigidbody;
            tetherManager.miniRovTetherPoint = miniRovTetherAnchor;
            tetherManager.useVirtualEndpointWhenNoMiniRov = false;

            if (disableTetherForcesForNow)
            {
                tetherManager.enableTetherForceWhenMiniRovAttached = false;
                tetherManager.tetherStiffnessNPerM = 0.0f;
                tetherManager.tetherDampingNPerMS = 0.0f;
                tetherManager.maximumSafeTensionN = 999999.0f;
            }
        }

        miniRovIsDynamic = true;
        miniRovControlEnabled = false;
        cableVisuallyAttachedToRov = true;

        releaseState = WaterReleaseState.ReleasedDynamicStabilizing;
        stateTimerS = 0.0f;
        lastEvent = "minirov_released_dynamic_stabilizing";

        Debug.Log("[MIMISK] MiniROV released to water: dynamic buoyancy/drag active, control pending.");
    }

    private void UpdateReleasedStabilizing()
    {
        if (stateTimerS >= postReleaseStabilizationSeconds)
        {
            ActivateMiniRovControl();
        }

        if (enableKinematicRecoveryWhenWinchRecovers &&
            tetherManager != null &&
            tetherManager.tetherState == MIMISKDroneCoreTetherManager.TetherState.Recovering)
        {
            BeginKinematicRecovery();
        }
    }

    [ContextMenu("Activate MiniROV Control")]
    public void ActivateMiniRovControl()
    {
        SetMiniRovControlComponents(true, true);

        miniRovControlEnabled = true;
        releaseState = WaterReleaseState.ROVControlActive;
        stateTimerS = 0.0f;
        lastEvent = "minirov_control_active";

        Debug.Log("[MIMISK] MiniROV control stack activated.");
    }

    private void UpdateRovControlActive()
    {
        if (enableKinematicRecoveryWhenWinchRecovers &&
            tetherManager != null &&
            tetherManager.tetherState == MIMISKDroneCoreTetherManager.TetherState.Recovering)
        {
            BeginKinematicRecovery();
        }
    }

    private void BeginKinematicRecovery()
    {
        AutoFindReferences();

        if (miniRovRoot == null)
        {
            return;
        }

        if (disableRovControlWhileCableFollowing)
        {
            SetMiniRovControlComponents(false, false);
        }

        if (miniRovRigidbody != null)
        {
            miniRovRigidbody.linearVelocity = Vector3.zero;
            miniRovRigidbody.angularVelocity = Vector3.zero;
            miniRovRigidbody.isKinematic = true;
            miniRovRigidbody.useGravity = false;
        }

        if (cableEndFollowRoot != null)
        {
            miniRovRoot.SetParent(cableEndFollowRoot, false);
            miniRovRoot.localPosition = Vector3.zero;
            miniRovRoot.localRotation = Quaternion.identity;
            miniRovRoot.localScale = Vector3.one;
        }

        if (tetherManager != null)
        {
            tetherManager.movingTetherEndVisual = yellowCableEndPoint;
            tetherManager.useVirtualEndpointWhenNoMiniRov = true;
            tetherManager.miniRovRigidbody = null;
            tetherManager.miniRovTetherPoint = null;
        }

        miniRovIsDynamic = false;
        miniRovControlEnabled = false;
        cableVisuallyAttachedToRov = false;

        releaseState = WaterReleaseState.KinematicRecovery;
        stateTimerS = 0.0f;
        lastEvent = "kinematic_recovery_started";
    }

    private void UpdateKinematicRecovery()
    {
        if (tetherManager == null)
        {
            return;
        }

        if (tetherManager.deployedLengthM <= tetherManager.minimumLengthM + recoveredLengthToleranceM)
        {
            releaseState = WaterReleaseState.CableFollowKinematic;
            stateTimerS = 0.0f;
            lastEvent = "kinematic_recovery_complete";
        }
    }

    [ContextMenu("Reattach MiniROV To Cable Follow Root")]
    public void ReattachMiniROVToCableEnd()
    {
        AutoFindReferences();

        if (cableAttachmentManager != null)
        {
            cableAttachmentManager.AttachMiniRovToCableEnd();
        }

        ConfigureInitialCableFollowState();
        lastEvent = "manual_reattach_to_cable_end";
    }

    private void UpdateMeasurements()
    {
        if (miniRovRoot != null && yellowCableEndPoint != null)
        {
            distanceToCableEndM =
                Vector3.Distance(miniRovRoot.position, yellowCableEndPoint.position);
        }
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

    private void SetMiniRovControlComponents(bool enableWaterPhysics, bool enableControl)
    {
        if (miniRovRoot == null)
        {
            return;
        }

        Behaviour[] behaviours =
            miniRovRoot.GetComponentsInChildren<Behaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour b = behaviours[i];

            if (b == null)
            {
                continue;
            }

            string typeName =
                b.GetType().Name;

            bool isWaterPhysics =
                ContainsIgnoreCase(typeName, "SimpleROVBuoyancy") ||
                ContainsIgnoreCase(typeName, "MIMISKWaterInteraction");

            bool isControl =
                ContainsIgnoreCase(typeName, "ControlManager") ||
                ContainsIgnoreCase(typeName, "SensorManager") ||
                ContainsIgnoreCase(typeName, "UnityVirtualESP32");

            if (isWaterPhysics)
            {
                b.enabled = enableWaterPhysics;
            }
            else if (isControl)
            {
                b.enabled = enableControl;
            }
        }
    }

    private bool ContainsIgnoreCase(string text, string pattern)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        return text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
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
