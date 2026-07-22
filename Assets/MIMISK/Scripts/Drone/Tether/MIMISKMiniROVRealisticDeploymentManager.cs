using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class MIMISKMiniROVRealisticDeploymentManager : MonoBehaviour
{
    public enum DeploymentState
    {
        NotConfigured,
        CableAttachedIdle,
        ReadyToDeploy,
        CablePayoutToWater,
        KinematicDeployedHolding,
        WaterTouchDetected,
        DynamicStabilizing,
        ROVControlActive,
        RecoveringKinematic,
        RecoveredAttached,
        Fault
    }

    [Header("References")]
    public MIMISKDroneCoreTetherManager tetherManager;
    public MIMISKDroneCoreMissionManager missionManager;
    public MIMISKDroneCoreFlightModeManager flightManager;

    [Header("MiniROV")]
    public Transform miniRovRoot;
    public Rigidbody miniRovRigidbody;
    public Transform miniRovTetherAnchor;
    public Collider[] miniRovColliders;
    public Collider[] droneColliders;

    [Header("MiniROV Autonomy / Agent")]
    public MIMISKMiniROVMissionManager miniRovMissionManager;
    public MIMISKMiniROVPathPlanner miniRovPathPlanner;
    public MIMISKMiniROVAgent miniRovAgent;

    [Header("Cable Endpoint")]
    public Transform yellowCableEndPoint;
    public Transform hookVisual;
    public Transform cableEndFollowRoot;

    public string yellowCableEndName = "real_mesh_short_yellow_deployment_cable_to_hook";
    public string hookName = "small_dark_open_deployment_hook_for_miniROV";
    public string followRootName = "MiniROV_CableEndFollowRoot";

    [Header("Final Cable Endpoint Contract")]
    [Tooltip("Use the hook visual as the cable endpoint when available. This avoids FBX yellow-cable mesh pivot/scale offsets.")]
    public bool preferHookVisualAsCableEndpoint = true;

    [Tooltip("World-space correction applied to the selected cable endpoint if the hook pivot is not exactly at the cable end.")]
    public Vector3 cableEndpointWorldOffset = Vector3.zero;

    public Vector3 resolvedCableEndpointWorld;
    public string resolvedCableEndpointName = "none";

    [Header("MiniROV Pose On Cable Endpoint")]
    public bool alignRovTetherAnchorToCableEnd = true;
    public Vector3 miniRovLocalOffsetOnCableEnd = Vector3.zero;
    public Vector3 miniRovLocalEulerOnCableEnd = Vector3.zero;
    public Vector3 miniRovLocalScaleOnCableEnd = Vector3.one;
    public bool forceMiniRovScaleOnAttach = true;

    [Header("Dynamic Release Parent")]
    [Tooltip("Normally OFF. OFF means MiniROV is unparented to scene root on dynamic release.")]
    public bool parentToDeployedWorldRootOnRelease = false;

    public Transform deployedWorldRoot;
    public string deployedWorldRootName = "MiniROV_DeployedWorldRoot";

    [Header("Deployment Safety")]
    public bool deploymentEnabled = true;
    public bool requireMissionReady = true;

    [Tooltip("If ON, SurfaceStable/SurfaceHold is enough to deploy even when the full drone mission did not run.")]
    public bool allowSurfaceStableAsMissionReady = true;
    public bool requireSurfaceStable = true;
    public bool attachMiniRovOnStart = true;

    [Header("Reel Deployment")]
    public float targetDeployLengthM = 3.0f;
    public float payoutSpeedMS = 0.22f;
    public float recoverySpeedMS = 0.25f;

    [Header("Water Touch / Release")]
    public float waterSurfaceY = 0.0f;
    public float waterTouchMarginM = 0.03f;

    [Tooltip("ROV is not released immediately at first water touch. It must be visibly below the surface by this amount.")]
    public float releaseDepthBelowSurfaceM = 0.25f;

    [Tooltip("Minimum reel payout before water release is allowed. Prevents instant release at U press.")]
    public float minimumPayoutBeforeReleaseM = 0.45f;
    public bool stopReelAtWaterTouch = true;

    [Tooltip("For safe debugging, keep OFF. U lowers the ROV and stops at release depth without making it dynamic.")]
    public bool autoReleaseToDynamicAtWaterDepth = false;

    [Tooltip("If ON, reaching water/release depth stops the reel and keeps the MiniROV kinematic/attached.")]
    public bool stopAndHoldKinematicAtReleaseDepth = true;
    public float postWaterTouchStabilizationS = 1.50f;

    [Header("ROV Activation")]
    public bool keepRovKinematicBeforeWaterTouch = true;
    public bool disableRovControlBeforeWaterTouch = true;
    public bool enableRovWaterPhysicsAtTouch = true;
    public bool enableRovControlAfterStabilization = true;
    public bool enableCollidersAfterWaterTouch = true;

    [Header("Collision Isolation")]
    public bool ignoreMiniRovDroneCollisions = true;

    [Tooltip("For Phase 3C, keep this OFF. We disable MiniROV colliders during cable-follow/recovery instead of repeatedly calling Physics.IgnoreCollision.")]
    public bool usePhysicsIgnoreCollision = false;

    [Tooltip("MiniROV colliders are disabled while it is carried by the visual cable endpoint.")]
    public bool disableMiniRovCollidersDuringCableFollow = true;

    [Tooltip("MiniROV colliders are disabled during kinematic winch recovery so it cannot push the drone.")]
    public bool disableMiniRovCollidersDuringRecovery = true;
    public bool applySmallDownwardVelocityAtRelease = true;
    public float initialDownwardVelocityMS = 0.10f;

    [Header("Passive Tether / Slack Manager")]
    [Tooltip("Phase 3C: visual/logical tether only. No tether force yet.")]
    public bool disableTetherForceForNow = true;

    [Tooltip("When ROV control is active, the reel target length follows ROV distance plus this slack.")]
    public bool adaptiveSlackManagement = true;

    public float desiredOperationalSlackM = 0.20f;
    public float slackDeadbandM = 0.05f;

    [Tooltip("Allow reel to slowly recover excess slack while ROV is active.")]
    public bool allowAutoSlackRecovery = true;

    [Header("Recovery")]
    public bool kinematicRecoveryForNow = true;
    public float recoveredLengthToleranceM = 0.04f;

    [Header("Final Recovery Gate")]
    [Tooltip("If ON, kinematic tether recovery is allowed only when the ROV is near the recorded deployment/home point or MiniROV reports RecoveryReady.")]
    public bool requireRovNearDeploymentHomeForRecovery = true;

    public float recoveryHomeDistanceToleranceM = 0.35f;

    [Header("Keyboard")]
    public bool acceptKeyboardCommands = true;
    public Key deployKey = Key.U;
    public Key recoverKey = Key.R;
    public Key stopKey = Key.K;
    public Key reattachKey = Key.D;
    public Key resetFaultKey = Key.F;

    [Header("Runtime")]
    public DeploymentState deploymentState = DeploymentState.NotConfigured;
    public bool safeToDeploy;
    public bool safeToRecover;
    public bool waterContactDetected;
    public bool rovDynamic;
    public bool rovControlActive;

    public float stateTimerS;
    public float distanceRovAnchorToCableEndM;
    public float rovDepthBelowSurfaceM;
    public float currentRequiredCableLengthM;
    public float adaptiveTargetLengthM;

    public string lastEvent = "not_configured";

    [Header("Deployment Home / Recovery Point")]
    public bool recordHomeOnDynamicRelease = true;
    public bool setMiniRovHomeOnDynamicRelease = true;
    public bool deploymentHomeRecorded;
    public Vector3 deploymentRovWorld;
    public float deploymentRovDepthM;
    public float deploymentRovYawDeg;
    public float deploymentTetherLengthM;
    public string deploymentHomeEvent = "not_recorded";

    [Header("Release Orientation")]
    public bool levelRovOnDynamicRelease = true;
    public bool setRovYawZeroOnRelease = true;
    public float releaseYawDeg = 0.0f;
    public bool zeroAngularVelocityOnRelease = true;
    public bool zeroHorizontalVelocityOnRelease = true;

    [Header("Recovery Coordination")]
    public bool requireMiniRovRecoveryReadyBeforeKinematicRecovery = true;
    public bool requestMiniRovReturnHomeWhenRecoverRequested = true;
    public bool miniRovRecoveryReady;
    public string recoveryCoordinationEvent = "idle";

    [Header("Tether-Based Relative Localization Estimate")]
    public bool enableTetherLocalizationEstimate = true;
    public float tetherConstantOffsetM = 0.0f;
    public float tetherLengthUncertaintyM = 0.05f;
    public float tetherBearingUncertaintyDeg = 20.0f;
    public float estimatedHorizontalRangeM;
    public float estimatedBearingDeg;
    public Vector3 estimatedRovWorldFromTether;
    public float estimatedLocalizationErrorM;


    private Vector3 recoveryStartWorld;
    private Vector3 recoveryEndWorld;
    private float recoveryStartLengthM;
    private float recoveryEndLengthM;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        ConfigureMiniRovBody();

        if (attachMiniRovOnStart)
        {
            AttachRovToCableEndpoint();
        }
        else
        {
            UpdateReadiness();
        }
    }

    private void Update()
    {
        if (!deploymentEnabled || !acceptKeyboardCommands)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[deployKey].wasPressedThisFrame)
        {
            StartDeployment();
        }

        if (Keyboard.current[recoverKey].wasPressedThisFrame)
        {
            StartRecovery();
        }

        if (Keyboard.current[stopKey].wasPressedThisFrame)
        {
            StopWinchHold();
        }

        if (Keyboard.current[reattachKey].wasPressedThisFrame)
        {
            AttachRovToCableEndpoint();
        }

        if (Keyboard.current[resetFaultKey].wasPressedThisFrame)
        {
            ResetFault();
        }
    }

    private void FixedUpdate()
    {
        if (!deploymentEnabled)
        {
            return;
        }

        stateTimerS += Time.fixedDeltaTime;

        UpdateMeasurements();
        UpdateReadiness();
        UpdateTetherLocalizationEstimate();

        if (deploymentState == DeploymentState.CablePayoutToWater)
        {
            UpdateCablePayoutToWater();
        }
        else if (deploymentState == DeploymentState.WaterTouchDetected)
        {
            ReleaseRovIntoWater();
        }
        else if (deploymentState == DeploymentState.DynamicStabilizing)
        {
            UpdateDynamicStabilizing();
        }
        else if (deploymentState == DeploymentState.ROVControlActive)
        {
            UpdateRovControlActive();
        }
        else if (deploymentState == DeploymentState.RecoveringKinematic)
        {
            UpdateKinematicRecovery();
        }
    }

    private void LateUpdate()
    {
        if (!deploymentEnabled)
        {
            return;
        }

        bool kinematicCableState =
            deploymentState == DeploymentState.CableAttachedIdle ||
            deploymentState == DeploymentState.ReadyToDeploy ||
            deploymentState == DeploymentState.CablePayoutToWater ||
            deploymentState == DeploymentState.RecoveringKinematic ||
            deploymentState == DeploymentState.RecoveredAttached;

        if (kinematicCableState)
        {
            SyncRovToCableEndpointWhileKinematic();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (tetherManager == null)
        {
            tetherManager = GetComponent<MIMISKDroneCoreTetherManager>();
        }

        if (missionManager == null)
        {
            missionManager = GetComponent<MIMISKDroneCoreMissionManager>();
        }

        if (flightManager == null)
        {
            flightManager = GetComponent<MIMISKDroneCoreFlightModeManager>();
        }

        if (yellowCableEndPoint == null)
        {
            yellowCableEndPoint = FindDeepChild(transform, yellowCableEndName);
        }

        if (hookVisual == null)
        {
            hookVisual = FindDeepChild(transform, hookName);
        }

        if (cableEndFollowRoot == null)
        {
            cableEndFollowRoot = FindDeepChild(transform, followRootName);
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

            if (miniRovColliders == null || miniRovColliders.Length == 0)
            {
                miniRovColliders = miniRovRoot.GetComponentsInChildren<Collider>(true);
            }

            if (droneColliders == null || droneColliders.Length == 0)
            {
                BuildDroneColliderList();
            }
        }
    }

    private void ConfigureMiniRovBody()
    {
        if (miniRovRoot == null)
        {
            deploymentState = DeploymentState.NotConfigured;
            lastEvent = "minirov_missing";
            return;
        }

        if (miniRovRigidbody == null)
        {
            miniRovRigidbody = miniRovRoot.GetComponent<Rigidbody>();
        }

        if (miniRovRigidbody == null)
        {
            miniRovRigidbody = miniRovRoot.gameObject.AddComponent<Rigidbody>();
        }

        miniRovRigidbody.isKinematic = true;
        miniRovRigidbody.useGravity = false;
    }

    private void EnsureFollowRoot()
    {
        if (cableEndFollowRoot != null)
        {
            return;
        }

        Transform existing = FindDeepChild(transform, followRootName);

        if (existing != null)
        {
            cableEndFollowRoot = existing;
            return;
        }

        GameObject go = new GameObject(followRootName);
        go.transform.SetParent(transform, true);
        go.transform.localScale = Vector3.one;
        cableEndFollowRoot = go.transform;
    }

    private void UpdateFollowRootFromCableEndpoint()
    {
        EnsureFollowRoot();

        if (cableEndFollowRoot == null)
        {
            return;
        }

        Transform endpoint =
            yellowCableEndPoint != null ? yellowCableEndPoint : hookVisual;

        if (endpoint == null)
        {
            return;
        }

        cableEndFollowRoot.SetParent(transform, true);
        cableEndFollowRoot.localScale = Vector3.one;
        cableEndFollowRoot.SetPositionAndRotation(endpoint.position, endpoint.rotation);
    }

    [ContextMenu("Attach ROV To Cable Endpoint")]
    public void AttachRovToCableEndpoint()
    {
        AutoFindReferences();
        ConfigureMiniRovBody();

        if (miniRovRoot == null || yellowCableEndPoint == null)
        {
            deploymentState = DeploymentState.NotConfigured;
            lastEvent = "attach_failed_missing_references";
            Debug.LogWarning("[MIMISK] Realistic ROV attach failed: missing MiniROV or yellow cable endpoint.");
            return;
        }

        EnsureFollowRoot();
        UpdateFollowRootFromCableEndpoint();

        miniRovRoot.SetParent(cableEndFollowRoot, false);
        miniRovRoot.localRotation = Quaternion.Euler(miniRovLocalEulerOnCableEnd);
        miniRovRoot.localPosition = miniRovLocalOffsetOnCableEnd;

        if (forceMiniRovScaleOnAttach)
        {
            miniRovRoot.localScale = miniRovLocalScaleOnCableEnd;
        }

        if (alignRovTetherAnchorToCableEnd)
        {
            AlignRovTetherAnchorToCableEndpoint();
        }

        if (miniRovRigidbody != null)
        {
            miniRovRigidbody.isKinematic = true;
            miniRovRigidbody.useGravity = false;
        }

        SetMiniRovCableManagedCollisionMode();
        SetRovComponents(false, false);

        ConfigureTetherForVirtualCableEndpoint();

        deploymentState = DeploymentState.CableAttachedIdle;
        rovDynamic = false;
        rovControlActive = false;
        waterContactDetected = false;
        stateTimerS = 0.0f;
        lastEvent = "rov_attached_to_cable_endpoint_waiting";

        Debug.Log("[MIMISK] MiniROV attached to cable endpoint for staged deployment.");
    }

    private void AlignRovTetherAnchorToCableEndpoint()
    {
        if (miniRovRoot == null ||
            miniRovTetherAnchor == null ||
            cableEndFollowRoot == null)
        {
            return;
        }

        Vector3 anchorLocal =
            cableEndFollowRoot.InverseTransformPoint(miniRovTetherAnchor.position);

        Vector3 desiredAnchorLocal =
            miniRovLocalOffsetOnCableEnd;

        Vector3 correction =
            desiredAnchorLocal - anchorLocal;

        miniRovRoot.localPosition += correction;
    }

    private void ConfigureTetherForVirtualCableEndpoint()
    {
        if (tetherManager == null)
        {
            return;
        }

        tetherManager.movingTetherEndVisual = yellowCableEndPoint;
        tetherManager.useVirtualEndpointWhenNoMiniRov = true;
        tetherManager.miniRovRigidbody = null;
        tetherManager.miniRovTetherPoint = null;

        tetherManager.hideStaticShortCableMeshWhenDynamic = false;
        tetherManager.staticShortDeploymentCableMesh = null;

        tetherManager.enableTetherForceWhenMiniRovAttached = false;
        tetherManager.tetherStiffnessNPerM = 0.0f;
        tetherManager.tetherDampingNPerMS = 0.0f;
        tetherManager.maximumSafeTensionN = 999999.0f;

        tetherManager.targetDeployLengthM =
            Mathf.Clamp(
                targetDeployLengthM,
                tetherManager.minimumLengthM,
                tetherManager.maximumLengthM
            );
    }

    [ContextMenu("Start Deployment")]
    public void StartDeployment()
    {
        AutoFindReferences();

        if (deploymentState == DeploymentState.NotConfigured)
        {
            AttachRovToCableEndpoint();
        }

        UpdateReadiness();

        if (!safeToDeploy)
        {
            deploymentState = DeploymentState.Fault;
            lastEvent =
                "deploy_rejected_" +
                "mission_" + (missionManager != null ? missionManager.missionState.ToString() : "none") +
                "_flight_" + (flightManager != null ? flightManager.flightMode.ToString() : "none") +
                "_tether_" + (tetherManager != null ? tetherManager.tetherState.ToString() : "none");

            Debug.LogWarning("[MIMISK] Realistic MiniROV deployment rejected: not safe. " + lastEvent);
            return;
        }

        ConfigureTetherForVirtualCableEndpoint();

        tetherManager.payoutSpeedMS = payoutSpeedMS;
        tetherManager.targetLengthM =
            Mathf.Clamp(
                targetDeployLengthM,
                tetherManager.minimumLengthM,
                tetherManager.maximumLengthM
            );

        tetherManager.targetDeployLengthM = tetherManager.targetLengthM;
        tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.Deploying;
        tetherManager.lastEvent = "realistic_rov_deployment_payout_started";

        deploymentState = DeploymentState.CablePayoutToWater;
        stateTimerS = 0.0f;
        lastEvent = "reel_payout_until_rov_water_touch";

        Debug.Log("[MIMISK] Reel payout started. MiniROV follows cable endpoint until water contact.");
    }


    private void SyncRovToCableEndpointWhileKinematic()
    {
        if (miniRovRoot == null ||
            cableEndFollowRoot == null ||
            yellowCableEndPoint == null)
        {
            return;
        }

        UpdateFollowRootFromCableEndpoint();

        if (miniRovRoot.parent != cableEndFollowRoot)
        {
            miniRovRoot.SetParent(cableEndFollowRoot, false);
        }

        miniRovRoot.localRotation =
            Quaternion.Euler(miniRovLocalEulerOnCableEnd);

        miniRovRoot.localPosition =
            miniRovLocalOffsetOnCableEnd;

        if (forceMiniRovScaleOnAttach)
        {
            miniRovRoot.localScale = miniRovLocalScaleOnCableEnd;
        }

        if (alignRovTetherAnchorToCableEnd)
        {
            AlignRovTetherAnchorToCableEndpoint();
        }

        if (miniRovRigidbody != null && miniRovRigidbody.isKinematic)
        {
            // Synchronize Rigidbody pose with the transform without setting velocity.
            miniRovRigidbody.position = miniRovRoot.position;
            miniRovRigidbody.rotation = miniRovRoot.rotation;
        }
    }

    private Vector3 GetRovReleasePointWorld()
    {
        if (miniRovTetherAnchor != null)
        {
            return miniRovTetherAnchor.position;
        }

        if (miniRovRoot != null)
        {
            return miniRovRoot.position;
        }

        return Vector3.zero;
    }

    private void UpdateCablePayoutToWater()
    {
        if (DetectWaterContact())
        {
            if (autoReleaseToDynamicAtWaterDepth)
            {
                deploymentState = DeploymentState.WaterTouchDetected;
                stateTimerS = 0.0f;
                lastEvent = "rov_reached_release_depth_auto_dynamic_release";
            }
            else if (stopAndHoldKinematicAtReleaseDepth)
            {
                StopAndHoldAtReleaseDepth();
            }
            else
            {
                deploymentState = DeploymentState.WaterTouchDetected;
                stateTimerS = 0.0f;
                lastEvent = "rov_reached_release_depth";
            }
        }
    }

    private bool DetectWaterContact()
    {
        if (miniRovRoot == null)
        {
            return false;
        }

        Vector3 p =
            GetRovReleasePointWorld();

        // Depth is positive below the water surface.
        rovDepthBelowSurfaceM =
            Mathf.Max(0.0f, waterSurfaceY - p.y);

        bool firstWaterContact =
            p.y <= waterSurfaceY + waterTouchMarginM;

        bool reachedReleaseDepth =
            p.y <= waterSurfaceY - Mathf.Max(0.0f, releaseDepthBelowSurfaceM);

        bool enoughCablePaidOut =
            tetherManager == null ||
            tetherManager.deployedLengthM >= minimumPayoutBeforeReleaseM;

        waterContactDetected =
            firstWaterContact && reachedReleaseDepth && enoughCablePaidOut;

        return waterContactDetected;
    }

    private void ReleaseRovIntoWater()
    {
        if (miniRovRoot == null || miniRovRigidbody == null)
        {
            deploymentState = DeploymentState.Fault;
            lastEvent = "release_failed_missing_rov";
            return;
        }

        // Final synchronization before dynamic release.
        // This guarantees that the ROV's current world position is the same
        // as the end of the yellow cable, not an old Rigidbody position.
        SyncRovToCableEndpointWhileKinematic();

        Vector3 releasePosition = miniRovRoot.position;
        Quaternion releaseRotation = miniRovRoot.rotation;

        if (levelRovOnDynamicRelease || setRovYawZeroOnRelease)
        {
            Vector3 euler =
                releaseRotation.eulerAngles;

            if (levelRovOnDynamicRelease)
            {
                euler.x = 0.0f;
                euler.z = 0.0f;
            }

            if (setRovYawZeroOnRelease)
            {
                euler.y = releaseYawDeg;
            }

            releaseRotation =
                Quaternion.Euler(euler);
        }

        if (stopReelAtWaterTouch && tetherManager != null)
        {
            tetherManager.targetLengthM = tetherManager.deployedLengthM;
            tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.HoldingDeployed;
            tetherManager.winchCommandRateMS = 0.0f;
            tetherManager.lastEvent = "reel_stopped_at_rov_release_depth";
        }

        miniRovRoot.SetParent(null, true);
        miniRovRoot.SetPositionAndRotation(releasePosition, releaseRotation);

        miniRovRigidbody.position = releasePosition;
        miniRovRigidbody.rotation = releaseRotation;
        miniRovRigidbody.isKinematic = false;
        miniRovRigidbody.useGravity = true;

        Vector3 releaseVelocity =
            miniRovRigidbody.linearVelocity;

        if (zeroHorizontalVelocityOnRelease)
        {
            releaseVelocity.x = 0.0f;
            releaseVelocity.z = 0.0f;
        }

        if (applySmallDownwardVelocityAtRelease)
        {
            releaseVelocity.y =
                Mathf.Min(
                    releaseVelocity.y,
                    -Mathf.Abs(initialDownwardVelocityMS)
                );
        }

        miniRovRigidbody.linearVelocity =
            releaseVelocity;

        if (zeroAngularVelocityOnRelease)
        {
            miniRovRigidbody.angularVelocity = Vector3.zero;
        }

        SetMiniRovReleasedCollisionMode();

        // Enable water physics now, but keep thruster/control disabled
        // until the stabilization delay finishes.
        SetRovComponents(true, false);

        if (tetherManager != null)
        {
            tetherManager.useVirtualEndpointWhenNoMiniRov = false;
            tetherManager.miniRovRigidbody = miniRovRigidbody;
            tetherManager.miniRovTetherPoint =
                miniRovTetherAnchor != null ? miniRovTetherAnchor : miniRovRoot;

            if (disableTetherForceForNow)
            {
                tetherManager.enableTetherForceWhenMiniRovAttached = false;
                tetherManager.tetherStiffnessNPerM = 0.0f;
                tetherManager.tetherDampingNPerMS = 0.0f;
                tetherManager.maximumSafeTensionN = 999999.0f;
            }
        }

        RecordDeploymentHomeAndSetMiniROVHome();

        deploymentState = DeploymentState.DynamicStabilizing;
        rovDynamic = true;
        rovControlActive = false;
        stateTimerS = 0.0f;
        lastEvent = "rov_dynamic_at_current_cable_position_buoyancy_active_control_waiting";

        Debug.Log("[MIMISK] MiniROV released at current cable-end position. Buoyancy active; ROV control disabled until stable.");
    }

    private void UpdateDynamicStabilizing()
    {
        if (stateTimerS >= postWaterTouchStabilizationS)
        {
            ActivateRovControl();
        }
    }

    private void ActivateRovControl()
    {
        SetRovComponents(true, enableRovControlAfterStabilization);

        rovControlActive = enableRovControlAfterStabilization;
        deploymentState = DeploymentState.ROVControlActive;
        stateTimerS = 0.0f;
        lastEvent = rovControlActive
            ? "rov_control_active_tether_visual_attached"
            : "rov_stable_control_not_enabled";

        Debug.Log("[MIMISK] MiniROV control stage reached.");
    }

    private void UpdateRovControlActive()
    {
        if (adaptiveSlackManagement)
        {
            UpdateAdaptiveSlackManagement();
        }
    }

    private void UpdateAdaptiveSlackManagement()
    {
        if (tetherManager == null)
        {
            return;
        }

        if (tetherManager.tetherState == MIMISKDroneCoreTetherManager.TetherState.Fault)
        {
            deploymentState = DeploymentState.Fault;
            lastEvent = "tether_fault_during_slack_management";
            return;
        }

        float required =
            tetherManager.straightDistanceM + desiredOperationalSlackM;

        currentRequiredCableLengthM = required;

        adaptiveTargetLengthM =
            Mathf.Clamp(
                required,
                tetherManager.minimumLengthM,
                tetherManager.maximumLengthM
            );

        float error =
            adaptiveTargetLengthM - tetherManager.deployedLengthM;

        tetherManager.targetLengthM = adaptiveTargetLengthM;

        if (error > slackDeadbandM)
        {
            tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.Deploying;
            tetherManager.lastEvent = "auto_slack_payout_for_rov_motion";
        }
        else if (error < -slackDeadbandM && allowAutoSlackRecovery)
        {
            tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.Recovering;
            tetherManager.lastEvent = "auto_slack_recovery_for_rov_motion";
        }
        else
        {
            tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.HoldingDeployed;
            tetherManager.lastEvent = "auto_slack_hold";
        }
    }


    private void RecordDeploymentHomeAndSetMiniROVHome()
    {
        if (!recordHomeOnDynamicRelease || miniRovRoot == null)
        {
            return;
        }

        deploymentRovWorld =
            miniRovRoot.position;

        deploymentRovDepthM =
            Mathf.Max(
                0.0f,
                waterSurfaceY - deploymentRovWorld.y
            );

        deploymentRovYawDeg =
            miniRovRoot.eulerAngles.y;

        deploymentTetherLengthM =
            tetherManager != null
                ? tetherManager.deployedLengthM
                : 0.0f;

        deploymentHomeRecorded = true;
        deploymentHomeEvent = "recorded_at_dynamic_release";

        if (!setMiniRovHomeOnDynamicRelease)
        {
            return;
        }

        AutoFindMiniRovAutonomyReferences();

        if (miniRovPathPlanner != null)
        {
            miniRovPathPlanner.homeWorld =
                deploymentRovWorld;

            miniRovPathPlanner.homeDepthM =
                deploymentRovDepthM;

            miniRovPathPlanner.homeSet =
                true;

            miniRovPathPlanner.lastEvent =
                "home_set_from_tether_deployment";
        }

        if (miniRovMissionManager != null)
        {
            miniRovMissionManager.lastEvent =
                "home_set_from_tether_deployment";
        }
    }

    private void AutoFindMiniRovAutonomyReferences()
    {
        if (miniRovRoot == null)
        {
            return;
        }

        if (miniRovMissionManager == null)
        {
            miniRovMissionManager =
                miniRovRoot.GetComponent<MIMISKMiniROVMissionManager>();
        }

        if (miniRovPathPlanner == null)
        {
            miniRovPathPlanner =
                miniRovRoot.GetComponent<MIMISKMiniROVPathPlanner>();
        }

        if (miniRovAgent == null)
        {
            miniRovAgent =
                miniRovRoot.GetComponent<MIMISKMiniROVAgent>();
        }
    }

    private void UpdateTetherLocalizationEstimate()
    {
        if (!enableTetherLocalizationEstimate ||
            tetherManager == null)
        {
            return;
        }

        Vector3 start =
            tetherManager.tetherStartWorld;

        Vector3 actual =
            miniRovTetherAnchor != null
                ? miniRovTetherAnchor.position
                : (miniRovRoot != null ? miniRovRoot.position : start);

        float length =
            Mathf.Max(
                0.0f,
                tetherManager.deployedLengthM - tetherConstantOffsetM
            );

        float depth =
            Mathf.Max(
                0.0f,
                waterSurfaceY - actual.y
            );

        estimatedHorizontalRangeM =
            Mathf.Sqrt(
                Mathf.Max(
                    0.0f,
                    length * length - depth * depth
                )
            );

        Vector3 horizontal =
            actual - start;

        horizontal.y = 0.0f;

        if (horizontal.sqrMagnitude > 0.0001f)
        {
            estimatedBearingDeg =
                Mathf.Atan2(
                    horizontal.x,
                    horizontal.z
                ) * Mathf.Rad2Deg;

            Vector3 dir =
                horizontal.normalized;

            estimatedRovWorldFromTether =
                start +
                dir * estimatedHorizontalRangeM +
                Vector3.down * depth;

            Vector3 actualHorizontal =
                actual - start;

            actualHorizontal.y = 0.0f;

            estimatedLocalizationErrorM =
                Mathf.Abs(
                    actualHorizontal.magnitude -
                    estimatedHorizontalRangeM
                );
        }
    }

    private void RefreshMiniRovRecoveryReady()
    {
        AutoFindMiniRovAutonomyReferences();

        miniRovRecoveryReady = false;

        if (miniRovMissionManager != null &&
            miniRovMissionManager.recoveryReady)
        {
            miniRovRecoveryReady = true;
            recoveryCoordinationEvent = "minirov_mission_recovery_ready";
            return;
        }

        if (miniRovAgent != null)
        {
            MIMISK.Common.MIMISKState state =
                miniRovAgent.GetState();

            if (state != null && state.recoveryReady)
            {
                miniRovRecoveryReady = true;
                recoveryCoordinationEvent = "minirov_agent_recovery_ready";
                return;
            }
        }

        recoveryCoordinationEvent = "minirov_not_recovery_ready";
    }

    private bool CanStartKinematicRecovery(out string reason)
    {
        reason = "ok";

        if (!requireMiniRovRecoveryReadyBeforeKinematicRecovery)
        {
            return true;
        }

        if (!rovControlActive)
        {
            return true;
        }

        RefreshMiniRovRecoveryReady();

        if (miniRovRecoveryReady)
        {
            return true;
        }

        reason = recoveryCoordinationEvent;
        return false;
    }

    private void RequestMiniRovReturnHomeForRecovery()
    {
        AutoFindMiniRovAutonomyReferences();

        if (miniRovAgent != null)
        {
            miniRovAgent.Execute(
                new MIMISK.Common.MIMISKCommand
                {
                    verb = MIMISK.Common.MIMISKAgentCommandVerb.ReturnHome,
                    source = "tether_recovery_request"
                }
            );

            recoveryCoordinationEvent =
                "requested_return_home_via_minirov_agent";

            return;
        }

        if (miniRovMissionManager != null)
        {
            miniRovMissionManager.ReturnHome();

            recoveryCoordinationEvent =
                "requested_return_home_via_mission_manager";
        }
    }

    private void StopAndHoldAtReleaseDepth()
    {
        if (tetherManager != null)
        {
            tetherManager.targetLengthM = tetherManager.deployedLengthM;
            tetherManager.tetherState =
                MIMISKDroneCoreTetherManager.TetherState.HoldingDeployed;
            tetherManager.winchCommandRateMS = 0.0f;
            tetherManager.lastEvent = "kinematic_hold_at_release_depth";
        }

        if (miniRovRigidbody != null)
        {
            miniRovRigidbody.isKinematic = true;
            miniRovRigidbody.useGravity = false;
        }

        deploymentState = DeploymentState.KinematicDeployedHolding;
        rovDynamic = false;
        rovControlActive = false;
        stateTimerS = 0.0f;
        lastEvent = "rov_kinematic_hold_at_release_depth_press_j_for_dynamic_release";

        Debug.Log("[MIMISK] ROV reached release depth. Reel stopped. ROV is safely held kinematic on cable endpoint.");
    }

    [ContextMenu("Force Dynamic Release From Kinematic Hold")]
    public void ForceDynamicReleaseFromKinematicHold()
    {
        if (deploymentState != DeploymentState.KinematicDeployedHolding &&
            deploymentState != DeploymentState.CablePayoutToWater &&
            deploymentState != DeploymentState.ReadyToDeploy)
        {
            Debug.LogWarning("[MIMISK] Dynamic release requested but ROV is not in a safe release-hold state.");
            return;
        }

        deploymentState = DeploymentState.WaterTouchDetected;
        stateTimerS = 0.0f;
        lastEvent = "manual_dynamic_release_requested_from_kinematic_hold";

        Debug.Log("[MIMISK] Manual dynamic MiniROV release requested.");
    }

    [ContextMenu("Start Recovery")]
    public void StartRecovery()
    {
        if (tetherManager == null)
        {
            deploymentState = DeploymentState.Fault;
            lastEvent = "recovery_failed_tether_missing";
            return;
        }

        string reason;

        if (!CanStartKinematicRecovery(out reason))
        {
            if (requestMiniRovReturnHomeWhenRecoverRequested)
            {
                RequestMiniRovReturnHomeForRecovery();
            }

            lastEvent =
                "recovery_waiting_for_minirov_" +
                reason;

            return;
        }

        if (kinematicRecoveryForNow)
        {
            BeginKinematicRecovery();
        }
        else
        {
            tetherManager.targetLengthM = tetherManager.minimumLengthM;
            tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.Recovering;
            lastEvent = "dynamic_recovery_requested_no_force_mode";
        }
    }

    private void BeginKinematicRecovery()
    {
        // During kinematic recovery the MiniROV must not collide with or push the drone.
        SetMiniRovCableManagedCollisionMode();
        if (miniRovRoot == null || tetherManager == null)
        {
            deploymentState = DeploymentState.Fault;
            lastEvent = "kinematic_recovery_missing_reference";
            return;
        }

        SetRovComponents(false, false);

        if (miniRovRigidbody != null)
        {
            miniRovRigidbody.linearVelocity = Vector3.zero;
            miniRovRigidbody.angularVelocity = Vector3.zero;
            miniRovRigidbody.isKinematic = true;
            miniRovRigidbody.useGravity = false;
        }

        // Reconnect to the safe cable endpoint follow root.
        EnsureFollowRoot();
        UpdateFollowRootFromCableEndpoint();

        miniRovRoot.SetParent(cableEndFollowRoot, false);
        miniRovRoot.localPosition = miniRovLocalOffsetOnCableEnd;
        miniRovRoot.localRotation = Quaternion.Euler(miniRovLocalEulerOnCableEnd);

        if (forceMiniRovScaleOnAttach)
        {
            miniRovRoot.localScale = miniRovLocalScaleOnCableEnd;
        }

        if (alignRovTetherAnchorToCableEnd)
        {
            AlignRovTetherAnchorToCableEndpoint();
        }

        ConfigureTetherForVirtualCableEndpoint();

        tetherManager.recoverySpeedMS = recoverySpeedMS;
        tetherManager.targetLengthM = tetherManager.minimumLengthM;
        tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.Recovering;
        tetherManager.lastEvent = "kinematic_recovery_reel_started";

        deploymentState = DeploymentState.RecoveringKinematic;
        rovDynamic = false;
        rovControlActive = false;
        stateTimerS = 0.0f;
        lastEvent = "kinematic_recovery_started";
    }

    private void UpdateKinematicRecovery()
    {
        SetMiniRovCableManagedCollisionMode();
        if (tetherManager == null)
        {
            return;
        }

        SyncRovToCableEndpointWhileKinematic();

        tetherManager.targetLengthM = tetherManager.minimumLengthM;

        if (tetherManager.deployedLengthM <= tetherManager.minimumLengthM + recoveredLengthToleranceM)
        {
            tetherManager.deployedLengthM = tetherManager.minimumLengthM;
            tetherManager.targetLengthM = tetherManager.minimumLengthM;
            tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.HoldingDeployed;
            tetherManager.winchCommandRateMS = 0.0f;

            SyncRovToCableEndpointWhileKinematic();

            deploymentState = DeploymentState.RecoveredAttached;
            rovDynamic = false;
            rovControlActive = false;
            stateTimerS = 0.0f;
            lastEvent = "rov_recovered_and_anchor_locked_to_cable_endpoint";
        }
    }

    [ContextMenu("Stop Winch Hold")]
    public void StopWinchHold()
    {
        if (tetherManager == null)
        {
            return;
        }

        tetherManager.StopWinch();

        if (deploymentState == DeploymentState.CablePayoutToWater)
        {
            deploymentState = DeploymentState.ReadyToDeploy;
        }

        lastEvent = "manual_winch_hold";
    }

    [ContextMenu("Reset Fault")]
    public void ResetFault()
    {
        if (tetherManager != null)
        {
            tetherManager.ResetFault();
        }

        if (deploymentState == DeploymentState.Fault)
        {
            deploymentState = DeploymentState.CableAttachedIdle;
            lastEvent = "fault_reset";
        }
    }

    private void UpdateReadiness()
    {
        bool missionReady = true;

        bool surfaceReady = true;

        if (requireSurfaceStable && flightManager != null)
        {
            surfaceReady =
                flightManager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceStable ||
                flightManager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceHold;
        }

        if (requireMissionReady && missionManager != null)
        {
            missionReady =
                missionManager.IsReadyForTetherDeployment();

            if (allowSurfaceStableAsMissionReady && surfaceReady)
            {
                missionReady = true;
            }
        }

        bool tetherReady =
            tetherManager != null &&
            tetherManager.tetherState != MIMISKDroneCoreTetherManager.TetherState.Fault;

        safeToDeploy =
            missionReady &&
            surfaceReady &&
            tetherReady &&
            miniRovRoot != null &&
            yellowCableEndPoint != null &&
            tetherManager != null &&
            deploymentState != DeploymentState.Fault;

        safeToRecover =
            tetherManager != null &&
            tetherManager.tetherState != MIMISKDroneCoreTetherManager.TetherState.Fault;

        if ((deploymentState == DeploymentState.CableAttachedIdle ||
             deploymentState == DeploymentState.RecoveredAttached) &&
            safeToDeploy)
        {
            deploymentState = DeploymentState.ReadyToDeploy;
            lastEvent = "ready_to_deploy";
        }
    }

    private void UpdateMeasurements()
    {
        if (miniRovRoot != null && yellowCableEndPoint != null)
        {
            distanceRovAnchorToCableEndM =
                Vector3.Distance(
                    miniRovTetherAnchor != null ? miniRovTetherAnchor.position : miniRovRoot.position,
                    yellowCableEndPoint.position
                );
        }

        if (miniRovRoot != null)
        {
            Vector3 p =
                miniRovTetherAnchor != null ? miniRovTetherAnchor.position : miniRovRoot.position;

            rovDepthBelowSurfaceM =
                Mathf.Max(0.0f, waterSurfaceY - p.y);
        }
    }


    private void BuildDroneColliderList()
    {
        Collider[] all =
            GetComponentsInChildren<Collider>(true);

        List<Collider> result =
            new List<Collider>();

        for (int i = 0; i < all.Length; i++)
        {
            Collider c = all[i];

            if (c == null)
            {
                continue;
            }

            if (miniRovRoot != null &&
                c.transform.IsChildOf(miniRovRoot))
            {
                continue;
            }

            result.Add(c);
        }

        droneColliders = result.ToArray();
    }

    private void SetMiniRovDroneCollisionsIgnored(bool ignored)
    {
        if (!ignoreMiniRovDroneCollisions)
        {
            return;
        }

        if (miniRovColliders == null || miniRovColliders.Length == 0)
        {
            if (miniRovRoot != null)
            {
                miniRovColliders =
                    miniRovRoot.GetComponentsInChildren<Collider>(true);
            }
        }

        if (droneColliders == null || droneColliders.Length == 0)
        {
            BuildDroneColliderList();
        }

        if (miniRovColliders == null || droneColliders == null)
        {
            return;
        }

        for (int i = 0; i < miniRovColliders.Length; i++)
        {
            Collider rovCollider = miniRovColliders[i];

            if (rovCollider == null)
            {
                continue;
            }

            for (int j = 0; j < droneColliders.Length; j++)
            {
                Collider droneCollider = droneColliders[j];

                if (droneCollider == null ||
                    droneCollider == rovCollider)
                {
                    continue;
                }

                Physics.IgnoreCollision(
                    rovCollider,
                    droneCollider,
                    ignored
                );
            }
        }
    }

    private void SetMiniRovCableManagedCollisionMode()
    {
        SetMiniRovDroneCollisionsIgnored(true);

        if (disableMiniRovCollidersDuringCableFollow ||
            disableMiniRovCollidersDuringRecovery)
        {
            SetMiniRovColliders(false);
        }
    }

    private void SetMiniRovReleasedCollisionMode()
    {
        SetMiniRovDroneCollisionsIgnored(true);

        if (enableCollidersAfterWaterTouch)
        {
            SetMiniRovColliders(true);
        }
        else
        {
            SetMiniRovColliders(false);
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

        SetMiniRovDroneCollisionsIgnored(true);
    }

    private void SetRovComponents(bool enableWaterPhysics, bool enableControl)
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

            // Final MiniROV Unity-side stack:
            //   - MIMISKWaterInteraction handles water behaviour.
            //   - UnityVirtualESP32 handles Unity <-> ESP32 serial bridge.
            //   - ControlManager handles MiniROV manual/control commands.
            //
            // SensorManager and SimpleROVBuoyancy are deliberately disabled
            // because they were not used in the final working MiniROV stack.
            bool isFinalWater =
                ContainsIgnoreCase(typeName, "MIMISKWaterInteraction");

            bool isFinalControl =
                ContainsIgnoreCase(typeName, "UnityVirtualESP32") ||
                ContainsIgnoreCase(typeName, "ControlManager");

            bool forceDisabled =
                ContainsIgnoreCase(typeName, "SensorManager") ||
                ContainsIgnoreCase(typeName, "SimpleROVBuoyancy");

            if (forceDisabled)
            {
                b.enabled = false;
            }
            else if (isFinalWater)
            {
                b.enabled = enableWaterPhysics;
            }
            else if (isFinalControl)
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
