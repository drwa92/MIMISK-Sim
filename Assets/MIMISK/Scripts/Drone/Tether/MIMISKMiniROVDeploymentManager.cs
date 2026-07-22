using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MIMISKMiniROVDeploymentManager : MonoBehaviour
{
    public enum DeploymentState
    {
        NotConfigured,
        Stowed,
        ReadyToDeploy,
        ReleasedDeploying,
        DeployedHolding,
        Recovering,
        RecoveredDocked,
        Fault
    }

    [Header("References")]
    public MIMISKDroneCoreTetherManager tetherManager;
    public MIMISKDroneCoreMissionManager missionManager;
    public MIMISKDroneCoreFlightModeManager flightManager;

    [Header("MiniROV")]
    public Transform miniRovRoot;
    public Rigidbody miniRovRigidbody;
    public Transform miniRovTetherPoint;
    public Collider[] miniRovColliders;

    [Header("Carry / Docking")]
    public Transform miniRovCarrySlot;
    public Transform hookVisual;

    [Header("Deployment")]
    public bool deploymentEnabled = true;
    public DeploymentState deploymentState = DeploymentState.NotConfigured;

    public bool stowOnStart = true;
    public bool snapToCarrySlotOnSetup = true;
    public bool disableCollidersWhenStowed = true;
    public bool requireTetherReady = true;

    [Tooltip("Default MiniROV mass. The physical pod prototype is about 600 g, but adjust if your Unity model differs.")]
    public float miniRovMassKg = 0.60f;

    public bool useGravityWhenReleased = true;
    public bool unparentWhenReleased = true;

    [Header("Recovery / Docking")]
    public float dockingCaptureDistanceM = 0.35f;
    public float dockingCaptureSpeedMS = 0.35f;
    public bool autoDockWhenRecovered = true;

    [Header("Keyboard")]
    public Key deployKey = Key.U;
    public Key recoverKey = Key.R;
    public Key stopKey = Key.K;
    public Key dockKey = Key.D;
    public Key resetFaultKey = Key.F;

    [Header("Runtime")]
    public bool miniRovReleased;
    public bool miniRovDocked;
    public bool safeToDeploy;
    public bool safeToRecover;

    public float distanceToCarrySlotM;
    public float miniRovSpeedMS;
    public string lastEvent = "not_configured";

    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Transform originalParent;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        ConfigureMiniRovRigidbody();

        if (miniRovRoot != null)
        {
            originalParent = miniRovRoot.parent;
            originalLocalPosition = miniRovRoot.localPosition;
            originalLocalRotation = miniRovRoot.localRotation;
        }

        if (stowOnStart)
        {
            StowMiniRov();
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
            StartMiniRovDeployment();
        }

        if (Keyboard.current[recoverKey].wasPressedThisFrame)
        {
            StartMiniRovRecovery();
        }

        if (Keyboard.current[stopKey].wasPressedThisFrame)
        {
            StopWinchAndHold();
        }

        if (Keyboard.current[dockKey].wasPressedThisFrame)
        {
            DockMiniRovNow();
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
        UpdateDeploymentState();
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

        if (miniRovCarrySlot == null)
        {
            miniRovCarrySlot = FindDeepChild(transform, "MiniROV_CarrySlot");
        }

        if (hookVisual == null && tetherManager != null)
        {
            hookVisual = tetherManager.movingTetherEndVisual;
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
                miniRovRigidbody =
                    miniRovRoot.GetComponent<Rigidbody>();
            }

            if (miniRovTetherPoint == null)
            {
                miniRovTetherPoint =
                    FindDeepChild(miniRovRoot, "MiniROV_TetherPoint");
            }

            if (miniRovTetherPoint == null)
            {
                miniRovTetherPoint =
                    FindDeepChild(miniRovRoot, "TetherPoint");
            }

            if (miniRovTetherPoint == null)
            {
                miniRovTetherPoint =
                    FindDeepChild(miniRovRoot, "TetherAnchor");
            }

            if (miniRovTetherPoint == null)
            {
                GameObject tp = new GameObject("MiniROV_TetherPoint");
                tp.transform.SetParent(miniRovRoot, false);
                tp.transform.localPosition = Vector3.zero;
                tp.transform.localRotation = Quaternion.identity;
                miniRovTetherPoint = tp.transform;
            }

            if (miniRovColliders == null || miniRovColliders.Length == 0)
            {
                miniRovColliders =
                    miniRovRoot.GetComponentsInChildren<Collider>(true);
            }
        }
    }

    private void ConfigureMiniRovRigidbody()
    {
        if (miniRovRoot == null)
        {
            deploymentState = DeploymentState.NotConfigured;
            lastEvent = "minirov_missing";
            return;
        }

        if (miniRovRigidbody == null)
        {
            miniRovRigidbody =
                miniRovRoot.GetComponent<Rigidbody>();
        }

        if (miniRovRigidbody == null)
        {
            miniRovRigidbody =
                miniRovRoot.gameObject.AddComponent<Rigidbody>();
        }

        miniRovRigidbody.mass =
            Mathf.Max(0.05f, miniRovMassKg);

        miniRovRigidbody.useGravity = false;
        miniRovRigidbody.isKinematic = true;
        miniRovRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    [ContextMenu("Stow MiniROV")]
    public void StowMiniRov()
    {
        AutoFindReferences();
        ConfigureMiniRovRigidbody();

        if (miniRovRoot == null || miniRovCarrySlot == null)
        {
            deploymentState = DeploymentState.NotConfigured;
            lastEvent = "stow_failed_missing_references";
            return;
        }

        miniRovRoot.SetParent(miniRovCarrySlot, false);

        miniRovRoot.localPosition = Vector3.zero;
        miniRovRoot.localRotation = Quaternion.identity;

        if (miniRovRigidbody != null)
        {
            miniRovRigidbody.linearVelocity = Vector3.zero;
            miniRovRigidbody.angularVelocity = Vector3.zero;
            miniRovRigidbody.useGravity = false;
            miniRovRigidbody.isKinematic = true;
        }

        SetMiniRovColliders(!disableCollidersWhenStowed);

        if (tetherManager != null)
        {
            tetherManager.miniRovRigidbody = null;
            tetherManager.miniRovTetherPoint = null;
            tetherManager.useVirtualEndpointWhenNoMiniRov = true;
        }

        miniRovReleased = false;
        miniRovDocked = true;
        deploymentState = DeploymentState.Stowed;
        lastEvent = "minirov_stowed";
    }

    [ContextMenu("Start MiniROV Deployment")]
    public void StartMiniRovDeployment()
    {
        AutoFindReferences();
        UpdateReadiness();

        if (!safeToDeploy)
        {
            deploymentState = DeploymentState.Fault;
            lastEvent = "deployment_rejected_not_safe";
            Debug.LogWarning("[MIMISK] MiniROV deployment rejected: not safe.");
            return;
        }

        if (miniRovRoot == null ||
            miniRovRigidbody == null ||
            tetherManager == null)
        {
            deploymentState = DeploymentState.NotConfigured;
            lastEvent = "deployment_failed_missing_references";
            return;
        }

        PlaceMiniRovAtHook();

        if (unparentWhenReleased)
        {
            miniRovRoot.SetParent(null, true);
        }

        miniRovRigidbody.isKinematic = false;
        miniRovRigidbody.useGravity = useGravityWhenReleased;
        miniRovRigidbody.linearVelocity = Vector3.zero;
        miniRovRigidbody.angularVelocity = Vector3.zero;

        SetMiniRovColliders(true);

        tetherManager.miniRovRigidbody = miniRovRigidbody;
        tetherManager.miniRovTetherPoint = miniRovTetherPoint;
        tetherManager.useVirtualEndpointWhenNoMiniRov = false;

        tetherManager.StartDeployment();

        miniRovReleased = true;
        miniRovDocked = false;
        deploymentState = DeploymentState.ReleasedDeploying;
        lastEvent = "minirov_released_deploying";

        Debug.Log("[MIMISK] MiniROV released and tether deployment started.");
    }

    [ContextMenu("Start MiniROV Recovery")]
    public void StartMiniRovRecovery()
    {
        if (tetherManager == null)
        {
            deploymentState = DeploymentState.NotConfigured;
            lastEvent = "recovery_failed_tether_missing";
            return;
        }

        tetherManager.StartRecovery();

        deploymentState = DeploymentState.Recovering;
        lastEvent = "minirov_recovery_started";
    }

    [ContextMenu("Stop Winch And Hold")]
    public void StopWinchAndHold()
    {
        if (tetherManager == null)
        {
            return;
        }

        tetherManager.StopWinch();

        if (deploymentState == DeploymentState.ReleasedDeploying)
        {
            deploymentState = DeploymentState.DeployedHolding;
        }

        lastEvent = "winch_hold_requested";
    }

    [ContextMenu("Dock MiniROV Now")]
    public void DockMiniRovNow()
    {
        DockMiniRov();
    }

    [ContextMenu("Reset Fault")]
    public void ResetFault()
    {
        if (deploymentState == DeploymentState.Fault)
        {
            deploymentState = miniRovDocked
                ? DeploymentState.Stowed
                : DeploymentState.DeployedHolding;

            lastEvent = "deployment_fault_reset";
        }

        if (tetherManager != null)
        {
            tetherManager.ResetFault();
        }
    }

    private void UpdateReadiness()
    {
        bool missionReady = true;

        if (missionManager != null)
        {
            missionReady =
                missionManager.missionState == MIMISKDroneCoreMissionManager.MissionState.ReadyForTetherDeployment ||
                missionManager.missionState == MIMISKDroneCoreMissionManager.MissionState.Completed;
        }

        bool surfaceReady = true;

        if (flightManager != null)
        {
            surfaceReady =
                flightManager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceStable ||
                flightManager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceHold;
        }

        bool tetherReady =
            tetherManager != null &&
            tetherManager.safeToDeploy;

        if (!requireTetherReady)
        {
            tetherReady = true;
        }

        safeToDeploy =
            missionReady &&
            surfaceReady &&
            tetherReady &&
            miniRovRoot != null &&
            miniRovRigidbody != null &&
            tetherManager != null &&
            deploymentState != DeploymentState.Fault;

        safeToRecover =
            tetherManager != null &&
            tetherManager.safeToRecover &&
            miniRovReleased &&
            deploymentState != DeploymentState.Fault;

        if (deploymentState == DeploymentState.Stowed &&
            safeToDeploy)
        {
            deploymentState = DeploymentState.ReadyToDeploy;
            lastEvent = "ready_to_deploy";
        }
    }

    private void UpdateDeploymentState()
    {
        if (miniRovRigidbody != null)
        {
            miniRovSpeedMS =
                miniRovRigidbody.linearVelocity.magnitude;
        }

        if (miniRovRoot != null && miniRovCarrySlot != null)
        {
            distanceToCarrySlotM =
                Vector3.Distance(
                    miniRovRoot.position,
                    miniRovCarrySlot.position
                );
        }

        if (deploymentState == DeploymentState.ReleasedDeploying &&
            tetherManager != null &&
            tetherManager.tetherState == MIMISKDroneCoreTetherManager.TetherState.HoldingDeployed)
        {
            deploymentState = DeploymentState.DeployedHolding;
            lastEvent = "minirov_deployed_holding";
        }

        if (deploymentState == DeploymentState.Recovering)
        {
            bool lengthRecovered =
                tetherManager != null &&
                tetherManager.deployedLengthM <= tetherManager.minimumLengthM + 0.03f;

            bool closeEnough =
                distanceToCarrySlotM <= dockingCaptureDistanceM;

            bool slowEnough =
                miniRovSpeedMS <= dockingCaptureSpeedMS;

            if (autoDockWhenRecovered &&
                lengthRecovered &&
                (closeEnough || tetherManager.miniRovTetherPoint == null) &&
                slowEnough)
            {
                DockMiniRov();
            }
        }
    }

    private void PlaceMiniRovAtHook()
    {
        if (miniRovRoot == null)
        {
            return;
        }

        Vector3 hookPosition =
            hookVisual != null
                ? hookVisual.position
                : (
                    tetherManager != null
                        ? tetherManager.tetherEndWorld
                        : miniRovRoot.position
                  );

        Vector3 tetherOffset =
            miniRovTetherPoint != null
                ? miniRovTetherPoint.position - miniRovRoot.position
                : Vector3.zero;

        miniRovRoot.position =
            hookPosition - tetherOffset;

        if (miniRovCarrySlot != null)
        {
            miniRovRoot.rotation =
                miniRovCarrySlot.rotation;
        }
    }

    private void DockMiniRov()
    {
        if (miniRovRoot == null ||
            miniRovCarrySlot == null)
        {
            deploymentState = DeploymentState.Fault;
            lastEvent = "dock_failed_missing_references";
            return;
        }

        if (miniRovRigidbody != null)
        {
            miniRovRigidbody.linearVelocity = Vector3.zero;
            miniRovRigidbody.angularVelocity = Vector3.zero;
            miniRovRigidbody.useGravity = false;
            miniRovRigidbody.isKinematic = true;
        }

        miniRovRoot.SetParent(miniRovCarrySlot, false);
        miniRovRoot.localPosition = Vector3.zero;
        miniRovRoot.localRotation = Quaternion.identity;

        SetMiniRovColliders(!disableCollidersWhenStowed);

        if (tetherManager != null)
        {
            tetherManager.miniRovRigidbody = null;
            tetherManager.miniRovTetherPoint = null;
            tetherManager.useVirtualEndpointWhenNoMiniRov = true;
            tetherManager.StopWinch();
        }

        miniRovReleased = false;
        miniRovDocked = true;
        deploymentState = DeploymentState.RecoveredDocked;
        lastEvent = "minirov_docked";
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
