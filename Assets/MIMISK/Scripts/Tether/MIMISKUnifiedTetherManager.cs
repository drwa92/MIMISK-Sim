using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(250)]
[DisallowMultipleComponent]
public class MIMISKUnifiedTetherManager : MonoBehaviour
{
    public enum TetherMissionState
    {
        Disabled,
        WaitingForDroneSurfaceReady,
        ReadyForDeploy,
        CableAttachedIdle,
        Deploying,
        DynamicStabilizing,
        ReadyForRovControl,
        RovControlActive,
        WaitingForRovRecoveryReady,
        Recovering,
        RecoveredAttached,
        Fault
    }

    [Header("References - Drone / Tether")]
    public MIMISKDroneCoreMissionManager droneMissionManager;
    public MIMISKDroneCoreFlightModeManager droneFlightManager;
    public MIMISKDroneAgent droneAgent;
    public MIMISKDroneCoreTetherManager tetherManager;
    public Rigidbody droneRigidbody;

    [Header("References - MiniROV")]
    public Transform miniRovRoot;
    public Rigidbody miniRovRigidbody;
    public Transform miniRovTetherAnchor;
    public Transform cableEndFollowRoot;

    public MIMISKMiniROVAgent miniRovAgent;
    public MIMISKMiniROVMissionManager miniRovMissionManager;
    public MIMISKMiniROVPathPlanner miniRovPathPlanner;
    public MIMISKMiniROVPlantBasedController miniRovPlantController;

    [Header("Scene Endpoint Names")]
    public string miniRovName = "MiniROV";
    public string fairleadName = "WinchFairlead_for_Unity_LineRenderer_Start";
    public string yellowCableEndName = "real_mesh_short_yellow_deployment_cable_to_hook";
    public string hookVisualName = "small_dark_open_deployment_hook_for_miniROV";
    public string cableEndFollowRootName = "MiniROV_CableEndFollowRoot";
    public string miniRovTetherAnchorName = "ROV_TetherAnchor";
    public string activeYellowLineName = "MiniROV_ActiveYellowTetherLine";

    [Header("Resolved Visual References")]
    public Transform fairleadLineStart;
    public Transform yellowCableEndPoint;
    public Transform hookVisual;
    public LineRenderer activeYellowLine;

    [Header("Command Ownership")]
    public bool managerEnabled = true;
    public bool acceptKeyboardCommands = false;
    public Key deployKey = Key.U;
    public Key activateRovControlKey = Key.J;
    public Key recoverKey = Key.R;
    public Key holdKey = Key.K;
    public Key reattachKey = Key.D;
    public Key resetFaultKey = Key.F;

    [Header("Deployment Gate")]
    public bool requireDroneSurfaceStable = true;
    public bool allowSurfaceStableWithoutFullMission = true;

    [Header("Cable / Deployment")]
    public float targetDeployLengthM = 1.25f;
    public float payoutSpeedMS = 0.22f;
    public float recoverySpeedMS = 0.25f;
    public float minimumLengthM = 0.05f;
    public float maximumLengthM = 12.0f;

    [Tooltip("Release the MiniROV when its tether anchor is this far below water.")]
    public float releaseDepthBelowSurfaceM = 0.08f;

    public float minimumPayoutBeforeReleaseM = 0.10f;
    public float waterSurfaceY = 0.0f;

    [Header("Cable-Managed MiniROV Pose")]
    public Vector3 miniRovLocalOffsetOnCableEnd = Vector3.zero;
    public Vector3 miniRovLocalEulerOnCableEnd = Vector3.zero;
    public Vector3 miniRovLocalScaleOnCableEnd = Vector3.one;
    public bool forceMiniRovScaleOnAttach = true;
    public bool alignRovTetherAnchorToCableEnd = true;

    [Header("Dynamic Release")]
    public bool levelRovOnRelease = true;
    public bool setYawZeroOnRelease = true;
    public float releaseYawDeg = 0.0f;
    public bool zeroVelocitiesOnRelease = true;
    public bool useGravityAfterRelease = true;
    public float stabilizationSeconds = 1.5f;
    public bool autoActivateRovControlAfterStabilization = false;

    [Header("ROV Home / Recovery")]
    public bool recordDeploymentHome = true;
    public bool setMiniRovHomeToDeploymentPoint = true;
    public Vector3 deploymentRovWorld;
    public float deploymentRovDepthM;
    public float deploymentRovYawDeg;
    public float deploymentTetherLengthM;
    public bool deploymentHomeRecorded;

    public bool requireRovRecoveryReadyBeforeWinchRecovery = true;
    public bool requestRovReturnHomeWhenRecoverRequested = true;
    public bool allowRecoveryWhenRovNearDeploymentHome = true;
    public float recoveryHomeToleranceM = 0.35f;

    [Header("Adaptive Semi-Taut Tether")]
    public bool adaptiveSlackManagement = true;
    public float desiredOperationalSlackM = 0.20f;
    public float slackDeadbandM = 0.05f;
    public bool allowAutoSlackRecovery = true;

    [Header("Final Tether Force")]
    public bool enableTetherForce = false;
    public float tetherStiffnessNPerM = 0.0f;
    public float tetherDampingNPerMS = 0.0f;
    public float maximumSafeTensionN = 999999.0f;

    [Header("Runtime")]
    public TetherMissionState tetherState = TetherMissionState.WaitingForDroneSurfaceReady;
    public bool droneSurfaceStable;
    public bool droneReadyForTether;
    public bool safeToDeploy;
    public bool droneMotionAllowed = true;
    public bool miniRovCableManaged;
    public bool miniRovDynamic;
    public bool rovControlActive;
    public bool waterPhysicsEnabled;
    public bool rovControlStackEnabled;

    public float stateTimerS;
    public float rovDepthBelowSurfaceM;
    public float requiredCableLengthM;
    public float adaptiveTargetLengthM;
    public float distanceToDeploymentHomeM;
    public string lastEvent = "not_configured";

    private Collider[] miniRovColliders;

    private void Awake()
    {
        AutoFindReferences();
        ConfigureAuthoritativeDefaults();
    }

    private void Start()
    {
        RefreshReadiness();

        if (safeToDeploy)
        {
            AttachRovToCableEnd();
        }
    }

    private void Update()
    {
        if (!managerEnabled)
        {
            return;
        }

        if (!acceptKeyboardCommands || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[deployKey].wasPressedThisFrame)
        {
            StartDeployment();
        }

        if (Keyboard.current[activateRovControlKey].wasPressedThisFrame)
        {
            ActivateRovControl();
        }

        if (Keyboard.current[recoverKey].wasPressedThisFrame)
        {
            RequestRecovery();
        }

        if (Keyboard.current[holdKey].wasPressedThisFrame)
        {
            HoldWinch();
        }

        if (Keyboard.current[reattachKey].wasPressedThisFrame)
        {
            AttachRovToCableEnd();
        }

        if (Keyboard.current[resetFaultKey].wasPressedThisFrame)
        {
            ResetFault();
        }
    }

    private void FixedUpdate()
    {
        if (!managerEnabled)
        {
            return;
        }

        float dt = Time.fixedDeltaTime;
        stateTimerS += dt;

        RefreshReadiness();
        UpdateDroneMotionInterlock();

        switch (tetherState)
        {
            case TetherMissionState.WaitingForDroneSurfaceReady:
            case TetherMissionState.RecoveredAttached:
                if (safeToDeploy)
                {
                    tetherState = TetherMissionState.ReadyForDeploy;
                    lastEvent = "ready_for_deploy";
                    stateTimerS = 0.0f;
                }
                break;

            case TetherMissionState.Deploying:
                if (DetectReleaseDepth())
                {
                    ReleaseRovToWaterPassive();
                }
                break;

            case TetherMissionState.DynamicStabilizing:
                if (stateTimerS >= stabilizationSeconds)
                {
                    tetherState = TetherMissionState.ReadyForRovControl;
                    lastEvent = "rov_released_water_physics_ready_for_control";
                    stateTimerS = 0.0f;

                    if (autoActivateRovControlAfterStabilization)
                    {
                        ActivateRovControl();
                    }
                }
                break;

            case TetherMissionState.RovControlActive:
                UpdateAdaptiveSlack();
                break;

            case TetherMissionState.WaitingForRovRecoveryReady:
                if (IsRovReadyForWinchRecovery())
                {
                    BeginWinchRecovery();
                }
                break;

            case TetherMissionState.Recovering:
                if (tetherManager != null &&
                    tetherManager.deployedLengthM <= tetherManager.minimumLengthM + 0.04f)
                {
                    CompleteRecovery();
                }
                break;
        }
    }

    private void LateUpdate()
    {
        if (!managerEnabled)
        {
            return;
        }

        if (IsCableManagedState())
        {
            SyncRovToCableEndpointWhileKinematic();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (droneMissionManager == null)
        {
            droneMissionManager = GetComponent<MIMISKDroneCoreMissionManager>();
        }

        if (droneFlightManager == null)
        {
            droneFlightManager = GetComponent<MIMISKDroneCoreFlightModeManager>();
        }

        if (droneAgent == null)
        {
            droneAgent = GetComponent<MIMISKDroneAgent>();
        }

        if (tetherManager == null)
        {
            tetherManager = GetComponent<MIMISKDroneCoreTetherManager>();
        }

        if (droneRigidbody == null)
        {
            droneRigidbody = GetComponent<Rigidbody>();
        }

        if (fairleadLineStart == null)
        {
            fairleadLineStart = FindDeepChild(transform, fairleadName);
        }

        if (yellowCableEndPoint == null)
        {
            yellowCableEndPoint = FindDeepChild(transform, yellowCableEndName);
        }

        if (hookVisual == null)
        {
            hookVisual = FindDeepChild(transform, hookVisualName);
        }

        if (cableEndFollowRoot == null)
        {
            cableEndFollowRoot = FindDeepChild(transform, cableEndFollowRootName);
        }

        if (activeYellowLine == null)
        {
            Transform line = FindDeepChild(transform, activeYellowLineName);

            if (line != null)
            {
                activeYellowLine = line.GetComponent<LineRenderer>();

                if (activeYellowLine == null)
                {
                    activeYellowLine = line.gameObject.AddComponent<LineRenderer>();
                }
            }
        }

        if (miniRovRoot == null)
        {
            GameObject rov = GameObject.Find(miniRovName);

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
                miniRovTetherAnchor = FindDeepChild(miniRovRoot, miniRovTetherAnchorName);
            }

            if (miniRovTetherAnchor == null)
            {
                miniRovTetherAnchor = FindDeepChild(miniRovRoot, "MiniROV_TetherPoint");
            }

            if (miniRovTetherAnchor == null)
            {
                miniRovTetherAnchor = FindDeepChild(miniRovRoot, "TetherPoint");
            }

            if (miniRovAgent == null)
            {
                miniRovAgent = miniRovRoot.GetComponent<MIMISKMiniROVAgent>();
            }

            if (miniRovMissionManager == null)
            {
                miniRovMissionManager = miniRovRoot.GetComponent<MIMISKMiniROVMissionManager>();
            }

            if (miniRovPathPlanner == null)
            {
                miniRovPathPlanner = miniRovRoot.GetComponent<MIMISKMiniROVPathPlanner>();
            }

            if (miniRovPlantController == null)
            {
                miniRovPlantController = miniRovRoot.GetComponent<MIMISKMiniROVPlantBasedController>();
            }

            if (miniRovColliders == null || miniRovColliders.Length == 0)
            {
                miniRovColliders = miniRovRoot.GetComponentsInChildren<Collider>(true);
            }
        }
    }

    [ContextMenu("Configure Authoritative Defaults")]
    public void ConfigureAuthoritativeDefaults()
    {
        AutoFindReferences();

        if (tetherManager != null)
        {
            tetherManager.acceptKeyboardCommands = false;
            tetherManager.minimumLengthM = minimumLengthM;
            tetherManager.maximumLengthM = maximumLengthM;
            tetherManager.targetDeployLengthM = targetDeployLengthM;
            tetherManager.payoutSpeedMS = payoutSpeedMS;
            tetherManager.recoverySpeedMS = recoverySpeedMS;

            tetherManager.enableTetherForceWhenMiniRovAttached = enableTetherForce;
            tetherManager.tetherStiffnessNPerM = tetherStiffnessNPerM;
            tetherManager.tetherDampingNPerMS = tetherDampingNPerMS;
            tetherManager.maximumSafeTensionN = maximumSafeTensionN;

            if (fairleadLineStart != null)
            {
                tetherManager.fairleadLineStart = fairleadLineStart;
            }

            if (activeYellowLine != null)
            {
                tetherManager.tetherLineRenderer = activeYellowLine;
            }
        }

        SetRovComponents(false, false);
    }

    [ContextMenu("Attach ROV To Cable End")]
    public void AttachRovToCableEnd()
    {
        AutoFindReferences();

        if (miniRovRoot == null || miniRovRigidbody == null || yellowCableEndPoint == null)
        {
            SetFault("attach_failed_missing_minirov_or_yellow_cable_endpoint");
            return;
        }

        EnsureFollowRoot();
        UpdateCableFollowRootPose();

        if (cableEndFollowRoot == null)
        {
            SetFault("attach_failed_missing_cable_follow_root");
            return;
        }

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

        miniRovRigidbody.isKinematic = true;
        miniRovRigidbody.useGravity = false;
        miniRovRigidbody.linearVelocity = Vector3.zero;
        miniRovRigidbody.angularVelocity = Vector3.zero;

        SetColliders(false);
        SetRovComponents(false, false);
        ConfigureTetherForCableManagedEndpoint();

        miniRovCableManaged = true;
        miniRovDynamic = false;
        rovControlActive = false;

        tetherState = safeToDeploy
            ? TetherMissionState.ReadyForDeploy
            : TetherMissionState.CableAttachedIdle;

        lastEvent = "rov_attached_to_yellow_cable_endpoint_via_clean_follow_root";
        stateTimerS = 0.0f;
    }

    [ContextMenu("Deploy ROV")]
    public void StartDeployment()
    {
        AutoFindReferences();
        RefreshReadiness();

        if (!safeToDeploy)
        {
            lastEvent = "deploy_rejected_drone_not_surface_ready";
            return;
        }

        if (!IsCableManagedState())
        {
            AttachRovToCableEnd();
        }

        ConfigureTetherForCableManagedEndpoint();

        if (tetherManager != null)
        {
            tetherManager.targetDeployLengthM = targetDeployLengthM;
            tetherManager.targetLengthM = Mathf.Clamp(
                targetDeployLengthM,
                tetherManager.minimumLengthM,
                tetherManager.maximumLengthM
            );
            tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.Deploying;
            tetherManager.lastEvent = "unified_tether_deploy_started";
        }

        tetherState = TetherMissionState.Deploying;
        stateTimerS = 0.0f;
        lastEvent = "deploy_started_lowering_rov_to_water";
    }

    [ContextMenu("Activate ROV Control")]
    public void ActivateRovControl()
    {
        AutoFindReferences();

        if (miniRovRoot == null || miniRovRigidbody == null)
        {
            SetFault("activate_control_failed_missing_minirov");
            return;
        }

        if (cableEndFollowRoot != null && miniRovRoot.IsChildOf(cableEndFollowRoot))
        {
            miniRovRoot.SetParent(null, true);
        }

        miniRovRigidbody.isKinematic = false;
        miniRovRigidbody.useGravity = useGravityAfterRelease;

        ConfigureTetherForDynamicMiniRov();
        SetColliders(true);
        SetRovComponents(true, true);

        if (miniRovAgent != null)
        {
            miniRovAgent.AutoFindReferences();
        }

        rovControlActive = true;
        miniRovDynamic = true;
        miniRovCableManaged = false;
        tetherState = TetherMissionState.RovControlActive;
        stateTimerS = 0.0f;
        lastEvent = "rov_control_active_final_stack_enabled";
    }

    [ContextMenu("Request Recovery")]
    public void RequestRecovery()
    {
        AutoFindReferences();

        if (!requireRovRecoveryReadyBeforeWinchRecovery || IsRovReadyForWinchRecovery())
        {
            BeginWinchRecovery();
            return;
        }

        if (requestRovReturnHomeWhenRecoverRequested)
        {
            RequestMiniRovReturnHome();
        }

        tetherState = TetherMissionState.WaitingForRovRecoveryReady;
        stateTimerS = 0.0f;
        lastEvent = "recovery_waiting_for_rov_return_home";
    }

    [ContextMenu("Stop Winch / Hold")]
    public void HoldWinch()
    {
        if (tetherManager != null)
        {
            tetherManager.targetLengthM = tetherManager.deployedLengthM;
            tetherManager.winchCommandRateMS = 0.0f;
            tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.HoldingDeployed;
        }

        lastEvent = "winch_hold";
    }

    [ContextMenu("Reset Fault")]
    public void ResetFault()
    {
        tetherState = safeToDeploy
            ? TetherMissionState.ReadyForDeploy
            : TetherMissionState.WaitingForDroneSurfaceReady;

        if (tetherManager != null && tetherManager.tetherState == MIMISKDroneCoreTetherManager.TetherState.Fault)
        {
            tetherManager.ResetFault();
        }

        lastEvent = "fault_reset";
    }

    private void ReleaseRovToWaterPassive()
    {
        AutoFindReferences();

        if (miniRovRoot == null || miniRovRigidbody == null)
        {
            SetFault("release_failed_missing_minirov");
            return;
        }

        SyncRovToCableEndpointWhileKinematic();

        Vector3 releasePosition = miniRovRoot.position;
        Quaternion releaseRotation = miniRovRoot.rotation;

        if (levelRovOnRelease || setYawZeroOnRelease)
        {
            Vector3 e = releaseRotation.eulerAngles;

            if (levelRovOnRelease)
            {
                e.x = 0.0f;
                e.z = 0.0f;
            }

            if (setYawZeroOnRelease)
            {
                e.y = releaseYawDeg;
            }

            releaseRotation = Quaternion.Euler(e);
        }

        miniRovRoot.SetParent(null, true);
        miniRovRoot.SetPositionAndRotation(releasePosition, releaseRotation);

        miniRovRigidbody.position = releasePosition;
        miniRovRigidbody.rotation = releaseRotation;
        miniRovRigidbody.isKinematic = false;
        miniRovRigidbody.useGravity = useGravityAfterRelease;

        if (zeroVelocitiesOnRelease)
        {
            miniRovRigidbody.linearVelocity = Vector3.zero;
            miniRovRigidbody.angularVelocity = Vector3.zero;
        }

        SetColliders(true);
        ConfigureTetherForDynamicMiniRov();

        // Water physics ON, control OFF.
        SetRovComponents(true, false);

        RecordDeploymentHome();

        if (tetherManager != null)
        {
            tetherManager.targetLengthM = tetherManager.deployedLengthM;
            tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.HoldingDeployed;
            tetherManager.lastEvent = "unified_tether_rov_released_passive";
        }

        miniRovCableManaged = false;
        miniRovDynamic = true;
        rovControlActive = false;
        tetherState = TetherMissionState.DynamicStabilizing;
        stateTimerS = 0.0f;
        lastEvent = "rov_released_to_root_water_physics_enabled_control_waiting";
    }

    private void BeginWinchRecovery()
    {
        AutoFindReferences();

        if (miniRovRoot == null || miniRovRigidbody == null)
        {
            SetFault("recovery_failed_missing_minirov");
            return;
        }

        ConfigureTetherForCableManagedEndpoint();
        EnsureFollowRoot();
        UpdateCableFollowRootPose();

        miniRovRoot.SetParent(cableEndFollowRoot, false);
        miniRovRoot.localPosition = miniRovLocalOffsetOnCableEnd;
        miniRovRoot.localRotation = Quaternion.Euler(miniRovLocalEulerOnCableEnd);

        if (forceMiniRovScaleOnAttach)
        {
            miniRovRoot.localScale = miniRovLocalScaleOnCableEnd;
        }

        miniRovRigidbody.isKinematic = true;
        miniRovRigidbody.useGravity = false;
        miniRovRigidbody.linearVelocity = Vector3.zero;
        miniRovRigidbody.angularVelocity = Vector3.zero;

        SetColliders(false);
        SetRovComponents(false, false);

        if (tetherManager != null)
        {
            tetherManager.targetLengthM = tetherManager.minimumLengthM;
            tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.Recovering;
            tetherManager.lastEvent = "unified_tether_recovery_started";
        }

        rovControlActive = false;
        miniRovDynamic = false;
        miniRovCableManaged = true;
        tetherState = TetherMissionState.Recovering;
        stateTimerS = 0.0f;
        lastEvent = "recovery_started_rov_attached_to_follow_root";
    }

    private void CompleteRecovery()
    {
        AttachRovToCableEnd();

        tetherState = TetherMissionState.RecoveredAttached;
        miniRovCableManaged = true;
        miniRovDynamic = false;
        rovControlActive = false;
        stateTimerS = 0.0f;
        lastEvent = "recovered_attached_drone_motion_allowed";

        if (tetherManager != null)
        {
            tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.Recovered;
        }
    }

    private void RefreshReadiness()
    {
        droneSurfaceStable = false;
        droneReadyForTether = false;

        if (droneFlightManager != null)
        {
            droneSurfaceStable =
                droneFlightManager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceStable ||
                droneFlightManager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceHold;
        }

        if (droneMissionManager != null)
        {
            droneReadyForTether =
                droneMissionManager.missionState == MIMISKDroneCoreMissionManager.MissionState.ReadyForTetherDeployment ||
                droneMissionManager.missionState == MIMISKDroneCoreMissionManager.MissionState.Completed;
        }

        if (allowSurfaceStableWithoutFullMission && droneSurfaceStable)
        {
            droneReadyForTether = true;
        }

        safeToDeploy =
            droneReadyForTether &&
            (!requireDroneSurfaceStable || droneSurfaceStable) &&
            tetherState != TetherMissionState.Fault;

        if (tetherState == TetherMissionState.WaitingForDroneSurfaceReady && safeToDeploy)
        {
            tetherState = TetherMissionState.ReadyForDeploy;
            lastEvent = "surface_ready_for_tether_deploy";
            stateTimerS = 0.0f;
        }
    }

    private void UpdateDroneMotionInterlock()
    {
        droneMotionAllowed =
            tetherState == TetherMissionState.WaitingForDroneSurfaceReady ||
            tetherState == TetherMissionState.ReadyForDeploy ||
            tetherState == TetherMissionState.CableAttachedIdle ||
            tetherState == TetherMissionState.RecoveredAttached ||
            tetherState == TetherMissionState.Fault;
    }

    private bool DetectReleaseDepth()
    {
        Vector3 p = GetRovReleasePointWorld();

        rovDepthBelowSurfaceM = Mathf.Max(0.0f, waterSurfaceY - p.y);

        bool belowDepth =
            p.y <= waterSurfaceY - Mathf.Max(0.0f, releaseDepthBelowSurfaceM);

        bool enoughCable =
            tetherManager == null ||
            tetherManager.deployedLengthM >= minimumPayoutBeforeReleaseM;

        return belowDepth && enoughCable;
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

        return transform.position;
    }

    private bool IsCableManagedState()
    {
        return
            tetherState == TetherMissionState.CableAttachedIdle ||
            tetherState == TetherMissionState.ReadyForDeploy ||
            tetherState == TetherMissionState.Deploying ||
            tetherState == TetherMissionState.Recovering ||
            tetherState == TetherMissionState.RecoveredAttached;
    }

    private void EnsureFollowRoot()
    {
        if (cableEndFollowRoot != null)
        {
            return;
        }

        Transform existing = FindDeepChild(transform, cableEndFollowRootName);

        if (existing != null)
        {
            cableEndFollowRoot = existing;
            return;
        }

        GameObject go = new GameObject(cableEndFollowRootName);
        go.transform.SetParent(transform, true);
        go.transform.localScale = Vector3.one;
        cableEndFollowRoot = go.transform;
    }

    private void UpdateCableFollowRootPose()
    {
        EnsureFollowRoot();

        if (cableEndFollowRoot == null || yellowCableEndPoint == null)
        {
            return;
        }

        cableEndFollowRoot.SetParent(transform, true);
        cableEndFollowRoot.localScale = Vector3.one;
        cableEndFollowRoot.SetPositionAndRotation(
            yellowCableEndPoint.position,
            yellowCableEndPoint.rotation
        );
    }

    private void SyncRovToCableEndpointWhileKinematic()
    {
        if (miniRovRoot == null || miniRovRigidbody == null)
        {
            return;
        }

        UpdateCableFollowRootPose();

        if (cableEndFollowRoot != null && miniRovRoot.parent != cableEndFollowRoot)
        {
            miniRovRoot.SetParent(cableEndFollowRoot, false);
        }

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

        miniRovRigidbody.position = miniRovRoot.position;
        miniRovRigidbody.rotation = miniRovRoot.rotation;
        miniRovRigidbody.isKinematic = true;
        miniRovRigidbody.useGravity = false;
    }

    private void AlignRovTetherAnchorToCableEndpoint()
    {
        if (miniRovRoot == null || miniRovTetherAnchor == null || yellowCableEndPoint == null)
        {
            return;
        }

        Vector3 anchorWorld = miniRovTetherAnchor.position;
        Vector3 delta = yellowCableEndPoint.position - anchorWorld;
        miniRovRoot.position += delta;
    }

    private void ConfigureTetherForCableManagedEndpoint()
    {
        if (tetherManager == null)
        {
            return;
        }

        tetherManager.acceptKeyboardCommands = false;
        tetherManager.useVirtualEndpointWhenNoMiniRov = true;
        tetherManager.miniRovRigidbody = null;
        tetherManager.miniRovTetherPoint = null;

        if (yellowCableEndPoint != null)
        {
            tetherManager.movingTetherEndVisual = yellowCableEndPoint;
        }
        else if (hookVisual != null)
        {
            tetherManager.movingTetherEndVisual = hookVisual;
        }

        if (activeYellowLine != null)
        {
            tetherManager.tetherLineRenderer = activeYellowLine;
        }

        if (fairleadLineStart != null)
        {
            tetherManager.fairleadLineStart = fairleadLineStart;
        }

        tetherManager.hideStaticShortCableMeshWhenDynamic = false;
        tetherManager.staticShortDeploymentCableMesh = null;

        tetherManager.enableTetherForceWhenMiniRovAttached = false;
        tetherManager.tetherStiffnessNPerM = 0.0f;
        tetherManager.tetherDampingNPerMS = 0.0f;
        tetherManager.maximumSafeTensionN = maximumSafeTensionN;
    }

    private void ConfigureTetherForDynamicMiniRov()
    {
        if (tetherManager == null)
        {
            return;
        }

        tetherManager.useVirtualEndpointWhenNoMiniRov = false;
        tetherManager.miniRovRigidbody = miniRovRigidbody;
        tetherManager.miniRovTetherPoint =
            miniRovTetherAnchor != null ? miniRovTetherAnchor : miniRovRoot;

        if (activeYellowLine != null)
        {
            tetherManager.tetherLineRenderer = activeYellowLine;
        }

        tetherManager.hideStaticShortCableMeshWhenDynamic = false;
        tetherManager.staticShortDeploymentCableMesh = null;

        tetherManager.enableTetherForceWhenMiniRovAttached = enableTetherForce;
        tetherManager.tetherStiffnessNPerM = tetherStiffnessNPerM;
        tetherManager.tetherDampingNPerMS = tetherDampingNPerMS;
        tetherManager.maximumSafeTensionN = maximumSafeTensionN;
    }

    private void RecordDeploymentHome()
    {
        if (!recordDeploymentHome || miniRovRoot == null)
        {
            return;
        }

        deploymentRovWorld = miniRovRoot.position;
        deploymentRovDepthM = Mathf.Max(0.0f, waterSurfaceY - deploymentRovWorld.y);
        deploymentRovYawDeg = miniRovRoot.eulerAngles.y;
        deploymentTetherLengthM = tetherManager != null ? tetherManager.deployedLengthM : 0.0f;
        deploymentHomeRecorded = true;

        if (!setMiniRovHomeToDeploymentPoint)
        {
            return;
        }

        if (miniRovPathPlanner != null)
        {
            miniRovPathPlanner.homeWorld = deploymentRovWorld;
            miniRovPathPlanner.homeDepthM = deploymentRovDepthM;
            miniRovPathPlanner.homeSet = true;
            miniRovPathPlanner.lastEvent = "home_set_from_unified_tether_deployment";
        }

        if (miniRovMissionManager != null)
        {
            miniRovMissionManager.lastEvent = "home_set_from_unified_tether_deployment";
        }
    }

    private void UpdateAdaptiveSlack()
    {
        if (!adaptiveSlackManagement || tetherManager == null || miniRovTetherAnchor == null)
        {
            return;
        }

        Vector3 start =
            fairleadLineStart != null
                ? fairleadLineStart.position
                : tetherManager.tetherStartWorld;

        Vector3 end =
            miniRovTetherAnchor.position;

        requiredCableLengthM =
            Vector3.Distance(start, end);

        adaptiveTargetLengthM =
            Mathf.Clamp(
                requiredCableLengthM + desiredOperationalSlackM,
                tetherManager.minimumLengthM,
                tetherManager.maximumLengthM
            );

        float error =
            adaptiveTargetLengthM - tetherManager.deployedLengthM;

        tetherManager.targetLengthM = adaptiveTargetLengthM;

        if (error > slackDeadbandM)
        {
            tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.Deploying;
            tetherManager.lastEvent = "unified_tether_auto_slack_payout";
        }
        else if (error < -slackDeadbandM && allowAutoSlackRecovery)
        {
            tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.Recovering;
            tetherManager.lastEvent = "unified_tether_auto_slack_recovery";
        }
        else
        {
            tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.HoldingDeployed;
            tetherManager.lastEvent = "unified_tether_auto_slack_hold";
        }
    }

    private bool IsRovReadyForWinchRecovery()
    {
        if (!deploymentHomeRecorded || miniRovRoot == null)
        {
            return false;
        }

        bool missionReady =
            miniRovMissionManager != null &&
            miniRovMissionManager.recoveryReady;

        Vector3 p = miniRovRoot.position;

        float horizontal =
            Vector2.Distance(
                new Vector2(p.x, p.z),
                new Vector2(deploymentRovWorld.x, deploymentRovWorld.z)
            );

        float depth =
            Mathf.Abs(
                Mathf.Max(0.0f, waterSurfaceY - p.y) -
                deploymentRovDepthM
            );

        distanceToDeploymentHomeM =
            Mathf.Sqrt(horizontal * horizontal + depth * depth);

        bool nearHome =
            distanceToDeploymentHomeM <= recoveryHomeToleranceM;

        return missionReady || (allowRecoveryWhenRovNearDeploymentHome && nearHome);
    }

    private void RequestMiniRovReturnHome()
    {
        if (miniRovMissionManager != null)
        {
            miniRovMissionManager.ReturnHome();
            lastEvent = "requested_minirov_return_home_before_recovery";
        }
        else
        {
            lastEvent = "return_home_request_failed_missing_minirov_mission_manager";
        }
    }

    private void SetColliders(bool enabled)
    {
        if (miniRovColliders == null || miniRovColliders.Length == 0)
        {
            if (miniRovRoot != null)
            {
                miniRovColliders = miniRovRoot.GetComponentsInChildren<Collider>(true);
            }
        }

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

            string typeName = b.GetType().Name;

            bool water =
                Contains(typeName, "MIMISKWaterInteraction") ||
                Contains(typeName, "SimpleROVBuoyancy");

            bool finalControl =
                Contains(typeName, "MIMISKMiniROVAgent") ||
                Contains(typeName, "MIMISKMiniROVMissionManager") ||
                Contains(typeName, "MIMISKMiniROVPathPlanner") ||
                Contains(typeName, "MIMISKMiniROVPlantBasedController") ||
                Contains(typeName, "MIMISKMiniROVRuntimeCsvLogger") ||
                Contains(typeName, "ControlManager") ||
                Contains(typeName, "UnityVirtualESP32") ||
                Contains(typeName, "SensorManager");

            bool legacyOwner =
                Contains(typeName, "MIMISKMiniROVCoreController") ||
                Contains(typeName, "MIMISKMiniROVUDPGamepadReceiver") ||
                Contains(typeName, "MIMISKMiniROVDirectRaspberryBypassInput");

            if (legacyOwner)
            {
                b.enabled = false;
            }
            else if (water)
            {
                b.enabled = enableWaterPhysics;
            }
            else if (finalControl)
            {
                b.enabled = enableControl;
            }
        }

        if (miniRovAgent != null)
        {
            miniRovAgent.enabled = enableControl;
            miniRovAgent.agentEnabled = enableControl;
        }

        if (miniRovMissionManager != null)
        {
            miniRovMissionManager.enabled = enableControl;
            miniRovMissionManager.missionManagerEnabled = enableControl;
            miniRovMissionManager.missionEnabled = enableControl;
        }

        if (miniRovPathPlanner != null)
        {
            miniRovPathPlanner.enabled = enableControl;
        }

        if (miniRovPlantController != null)
        {
            miniRovPlantController.enabled = enableControl;
            miniRovPlantController.controllerEnabled = enableControl;
        }

        waterPhysicsEnabled = enableWaterPhysics;
        rovControlStackEnabled = enableControl;
    }

    private bool Contains(string value, string token)
    {
        return
            value != null &&
            value.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void SetFault(string reason)
    {
        tetherState = TetherMissionState.Fault;
        lastEvent = reason;

        if (tetherManager != null)
        {
            tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.Fault;
        }
    }

    private Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
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
