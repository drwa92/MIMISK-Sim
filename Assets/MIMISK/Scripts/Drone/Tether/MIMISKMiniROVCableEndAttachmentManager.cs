using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MIMISKMiniROVCableEndAttachmentManager : MonoBehaviour
{
    public enum CableMiniRovState
    {
        NotConfigured,
        AttachedIdle,
        ReadyToDeploy,
        Deploying,
        DeployedHolding,
        Recovering,
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
    public Collider[] miniRovColliders;

    [Header("Cable End Attachment")]
    [Tooltip("This should be real_mesh_short_yellow_deployment_cable_to_hook.")]
    public Transform yellowCableEndPoint;

    [Tooltip("Optional hook visual if separate from the yellow cable mesh.")]
    public Transform hookVisual;

    [Tooltip("Safe unscaled transform that follows the yellow cable endpoint. MiniROV is parented here, not directly to the mesh.")]
    public Transform cableEndFollowRoot;

    public string yellowCableEndName = "real_mesh_short_yellow_deployment_cable_to_hook";
    public string hookName = "small_dark_open_deployment_hook_for_miniROV";
    public string followRootName = "MiniROV_CableEndFollowRoot";

    [Header("Attachment Pose")]
    public Vector3 miniRovLocalOffsetOnCableEnd = Vector3.zero;
    public Vector3 miniRovLocalEulerOnCableEnd = Vector3.zero;
    public Vector3 miniRovLocalScaleOnCableEnd = Vector3.one;

    [Header("Deployment")]
    public bool deploymentEnabled = true;
    public CableMiniRovState cableMiniRovState = CableMiniRovState.NotConfigured;

    public bool attachMiniRovOnStart = true;
    public bool requireMissionReady = true;
    public bool requireSurfaceStable = true;

    [Tooltip("For this visual/mechanical stage, MiniROV follows the cable endpoint as a kinematic payload.")]
    public bool keepMiniRovKinematicWhileCableAttached = true;

    public bool disableMiniRovCollidersWhileAttached = true;

    [Tooltip("Reset MiniROV local scale when attached. This repairs bad inherited FBX/mesh scaling.")]
    public bool forceMiniRovScaleOnAttach = true;

    public float targetDeployLengthM = 3.0f;

    [Header("Keyboard")]
    public Key deployKey = Key.U;
    public Key recoverKey = Key.R;
    public Key stopKey = Key.K;
    public Key attachKey = Key.D;
    public Key resetFaultKey = Key.F;

    [Header("Runtime")]
    public bool safeToDeploy;
    public bool safeToRecover;
    public bool miniRovAttachedToCableEnd;

    public float distanceToCableEndM;
    public string lastEvent = "not_configured";

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        ConfigureMiniRovBody();

        if (attachMiniRovOnStart)
        {
            AttachMiniRovToCableEnd();
        }
        else
        {
            UpdateReadiness();
        }
    }

    private void Update()
    {
        if (!deploymentEnabled)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[deployKey].wasPressedThisFrame)
        {
            StartCableDeployment();
        }

        if (Keyboard.current[recoverKey].wasPressedThisFrame)
        {
            StartCableRecovery();
        }

        if (Keyboard.current[stopKey].wasPressedThisFrame)
        {
            StopCableWinch();
        }

        if (Keyboard.current[attachKey].wasPressedThisFrame)
        {
            AttachMiniRovToCableEnd();
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

        UpdateReadiness();
        UpdateStateFromTether();
    }

    private void LateUpdate()
    {
        if (!deploymentEnabled)
        {
            return;
        }

        if (miniRovAttachedToCableEnd)
        {
            UpdateCableFollowRootPose();
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

            if (miniRovColliders == null || miniRovColliders.Length == 0)
            {
                miniRovColliders =
                    miniRovRoot.GetComponentsInChildren<Collider>(true);
            }
        }
    }

    private void ConfigureMiniRovBody()
    {
        if (miniRovRoot == null)
        {
            cableMiniRovState = CableMiniRovState.NotConfigured;
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

        if (keepMiniRovKinematicWhileCableAttached)
        {
            miniRovRigidbody.isKinematic = true;
            miniRovRigidbody.useGravity = false;
        }
    }

    private void EnsureCableFollowRoot()
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

    private void UpdateCableFollowRootPose()
    {
        EnsureCableFollowRoot();

        if (cableEndFollowRoot == null)
        {
            return;
        }

        Transform endpoint =
            yellowCableEndPoint != null
                ? yellowCableEndPoint
                : hookVisual;

        if (endpoint == null)
        {
            return;
        }

        cableEndFollowRoot.SetParent(transform, true);
        cableEndFollowRoot.localScale = Vector3.one;
        cableEndFollowRoot.SetPositionAndRotation(
            endpoint.position,
            endpoint.rotation
        );
    }

    [ContextMenu("Attach MiniROV To Cable End")]
    public void AttachMiniRovToCableEnd()
    {
        AutoFindReferences();
        ConfigureMiniRovBody();

        if (miniRovRoot == null || yellowCableEndPoint == null)
        {
            cableMiniRovState = CableMiniRovState.NotConfigured;
            lastEvent = "attach_failed_missing_minirov_or_cable_endpoint";
            Debug.LogWarning("[MIMISK] MiniROV cable attach failed: missing MiniROV or yellow cable endpoint.");
            return;
        }

        EnsureCableFollowRoot();
        UpdateCableFollowRootPose();

        if (cableEndFollowRoot == null)
        {
            cableMiniRovState = CableMiniRovState.NotConfigured;
            lastEvent = "attach_failed_missing_safe_follow_root";
            return;
        }

        // Important:
        // Do NOT parent the MiniROV directly to the yellow cable mesh.
        // The mesh may have imported FBX scale/pivot transforms.
        // Parent to a clean runtime follow root instead.
        miniRovRoot.SetParent(cableEndFollowRoot, false);
        miniRovRoot.localPosition = miniRovLocalOffsetOnCableEnd;
        miniRovRoot.localRotation = Quaternion.Euler(miniRovLocalEulerOnCableEnd);

        if (forceMiniRovScaleOnAttach)
        {
            miniRovRoot.localScale = miniRovLocalScaleOnCableEnd;
        }

        if (miniRovRigidbody != null)
        {
            miniRovRigidbody.isKinematic = keepMiniRovKinematicWhileCableAttached;
            miniRovRigidbody.useGravity = !keepMiniRovKinematicWhileCableAttached;

            if (!miniRovRigidbody.isKinematic)
            {
                miniRovRigidbody.linearVelocity = Vector3.zero;
                miniRovRigidbody.angularVelocity = Vector3.zero;
            }
        }

        SetMiniRovColliders(!disableMiniRovCollidersWhileAttached);

        if (tetherManager != null)
        {
            tetherManager.movingTetherEndVisual = yellowCableEndPoint;

            // Use the yellow cable endpoint as the visual tether endpoint.
            // Do not switch to a separate MiniROV_TetherPoint at this stage.
            tetherManager.miniRovRigidbody = null;
            tetherManager.miniRovTetherPoint = null;
            tetherManager.useVirtualEndpointWhenNoMiniRov = true;

            // Keep yellow cable mesh visible because it is the actual endpoint visual.
            tetherManager.hideStaticShortCableMeshWhenDynamic = false;
            tetherManager.staticShortDeploymentCableMesh = null;

            tetherManager.targetDeployLengthM =
                Mathf.Clamp(
                    targetDeployLengthM,
                    tetherManager.minimumLengthM,
                    tetherManager.maximumLengthM
                );
        }

        miniRovAttachedToCableEnd = true;
        cableMiniRovState = CableMiniRovState.AttachedIdle;
        lastEvent = "minirov_attached_to_yellow_cable_endpoint_safe_follow_root";

        Debug.Log("[MIMISK] MiniROV safely attached to yellow cable endpoint via follow root.");
    }

    [ContextMenu("Start Cable Deployment")]
    public void StartCableDeployment()
    {
        AutoFindReferences();

        if (!miniRovAttachedToCableEnd)
        {
            AttachMiniRovToCableEnd();
        }

        UpdateReadiness();

        if (!safeToDeploy)
        {
            cableMiniRovState = CableMiniRovState.Fault;
            lastEvent =
                "deployment_rejected_not_safe_" +
                "mission_" + (missionManager != null ? missionManager.missionState.ToString() : "none") +
                "_flight_" + (flightManager != null ? flightManager.flightMode.ToString() : "none") +
                "_tether_" + (tetherManager != null ? tetherManager.tetherState.ToString() : "none");

            Debug.LogWarning("[MIMISK] MiniROV cable deployment rejected: not safe.");
            return;
        }

        if (tetherManager == null)
        {
            cableMiniRovState = CableMiniRovState.NotConfigured;
            lastEvent = "deployment_failed_tether_missing";
            return;
        }

        tetherManager.movingTetherEndVisual = yellowCableEndPoint;
        tetherManager.useVirtualEndpointWhenNoMiniRov = true;
        tetherManager.hideStaticShortCableMeshWhenDynamic = false;
        tetherManager.staticShortDeploymentCableMesh = null;

        tetherManager.targetLengthM =
            Mathf.Clamp(
                targetDeployLengthM,
                tetherManager.minimumLengthM,
                tetherManager.maximumLengthM
            );

        tetherManager.targetDeployLengthM = tetherManager.targetLengthM;

        // Start the reel directly after our own mission/surface safety gate.
        tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.Deploying;
        tetherManager.lastEvent = "deployment_started_by_safe_cable_end_attachment";

        cableMiniRovState = CableMiniRovState.Deploying;
        lastEvent = "minirov_cable_endpoint_deployment_started";

        Debug.Log("[MIMISK] MiniROV cable endpoint deployment started.");
    }

    [ContextMenu("Start Cable Recovery")]
    public void StartCableRecovery()
    {
        if (tetherManager == null)
        {
            cableMiniRovState = CableMiniRovState.NotConfigured;
            lastEvent = "recovery_failed_tether_missing";
            return;
        }

        if (tetherManager.tetherState == MIMISKDroneCoreTetherManager.TetherState.Fault)
        {
            cableMiniRovState = CableMiniRovState.Fault;
            lastEvent = "recovery_rejected_tether_fault";
            return;
        }

        tetherManager.targetLengthM = tetherManager.minimumLengthM;
        tetherManager.tetherState = MIMISKDroneCoreTetherManager.TetherState.Recovering;
        tetherManager.lastEvent = "recovery_started_by_cable_end_attachment";

        cableMiniRovState = CableMiniRovState.Recovering;
        lastEvent = "minirov_cable_endpoint_recovery_started";

        Debug.Log("[MIMISK] MiniROV cable endpoint recovery started.");
    }

    [ContextMenu("Stop Cable Winch")]
    public void StopCableWinch()
    {
        if (tetherManager == null)
        {
            return;
        }

        tetherManager.StopWinch();

        if (cableMiniRovState == CableMiniRovState.Deploying)
        {
            cableMiniRovState = CableMiniRovState.DeployedHolding;
        }

        lastEvent = "cable_winch_stopped_hold";
    }

    [ContextMenu("Reset Fault")]
    public void ResetFault()
    {
        if (tetherManager != null)
        {
            tetherManager.ResetFault();
        }

        if (cableMiniRovState == CableMiniRovState.Fault)
        {
            cableMiniRovState =
                miniRovAttachedToCableEnd
                    ? CableMiniRovState.AttachedIdle
                    : CableMiniRovState.NotConfigured;

            lastEvent = "cable_attachment_fault_reset";
        }
    }

    private void UpdateReadiness()
    {
        bool missionReady = true;

        if (requireMissionReady && missionManager != null)
        {
            missionReady =
                missionManager.missionState == MIMISKDroneCoreMissionManager.MissionState.ReadyForTetherDeployment ||
                missionManager.missionState == MIMISKDroneCoreMissionManager.MissionState.Completed;
        }

        bool surfaceReady = true;

        if (requireSurfaceStable && flightManager != null)
        {
            surfaceReady =
                flightManager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceStable ||
                flightManager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceHold;
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
            cableMiniRovState != CableMiniRovState.Fault;

        safeToRecover =
            tetherManager != null &&
            tetherManager.tetherState != MIMISKDroneCoreTetherManager.TetherState.Fault;

        if ((cableMiniRovState == CableMiniRovState.AttachedIdle ||
             cableMiniRovState == CableMiniRovState.RecoveredAttached) &&
            safeToDeploy)
        {
            cableMiniRovState = CableMiniRovState.ReadyToDeploy;
            lastEvent = "ready_to_deploy_from_cable_endpoint";
        }
    }

    private void UpdateStateFromTether()
    {
        if (miniRovRoot != null && yellowCableEndPoint != null)
        {
            distanceToCableEndM =
                Vector3.Distance(
                    miniRovRoot.position,
                    yellowCableEndPoint.position
                );
        }

        if (tetherManager == null)
        {
            return;
        }

        if (cableMiniRovState == CableMiniRovState.Deploying &&
            tetherManager.tetherState == MIMISKDroneCoreTetherManager.TetherState.HoldingDeployed)
        {
            cableMiniRovState = CableMiniRovState.DeployedHolding;
            lastEvent = "minirov_deployed_holding_on_cable_endpoint";
        }

        if (cableMiniRovState == CableMiniRovState.Recovering &&
            tetherManager.deployedLengthM <= tetherManager.minimumLengthM + 0.03f)
        {
            cableMiniRovState = CableMiniRovState.RecoveredAttached;
            lastEvent = "minirov_recovered_still_attached_to_cable_endpoint";
        }

        if (tetherManager.tetherState == MIMISKDroneCoreTetherManager.TetherState.Fault)
        {
            cableMiniRovState = CableMiniRovState.Fault;
            lastEvent = "tether_fault";
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
