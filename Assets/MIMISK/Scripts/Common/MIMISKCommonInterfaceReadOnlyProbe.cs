using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKCommonInterfaceReadOnlyProbe : MonoBehaviour
{
    [Header("Common Interface")]
    public MIMISKCommonBus bus;
    public bool probeEnabled = true;
    public bool publishToBus = true;
    public float publishHz = 10.0f;

    [Header("Existing Drone Module References")]
    public GameObject droneObject;
    public Rigidbody droneRigidbody;
    public MIMISKDroneCoreMissionManager droneMission;
    public MIMISKDroneCoreFlightModeManager droneFlightMode;
    public MIMISKFinalMissionPlanner finalPlanner;

    [Header("Existing Tether Module References")]
    public MIMISKDroneCoreTetherManager tetherManager;

    [Header("Existing MiniROV Module References")]
    public GameObject miniRovObject;
    public Rigidbody miniRovRigidbody;
    public MIMISKMiniROVModule miniRovModule;

    [Header("Reference Check")]
    public bool foundDroneObject;
    public bool foundDroneMission;
    public bool foundDroneFlightMode;
    public bool foundFinalPlanner;
    public bool foundTetherManager;
    public bool foundMiniRovObject;
    public bool foundMiniRovModule;

    [Header("Live Drone State From Existing Modules")]
    public string droneMissionState = "unknown";
    public string droneFlightModeState = "unknown";
    public bool droneMissionActive;
    public bool droneReadyForTether;
    public Vector3 dronePosition;
    public Vector3 droneVelocity;

    [Header("Live Tether State From Existing Module")]
    public string tetherState = "unknown";
    public float tetherDeployedLengthM;
    public float tetherTargetLengthM;
    public float tetherWinchRateMS;
    public string tetherLastEvent = "unknown";

    [Header("Live MiniROV State From Existing Module")]
    public string miniRovState = "unknown";
    public string miniRovLastEvent = "unknown";
    public Vector3 miniRovPosition;
    public Vector3 miniRovVelocity;
    public bool miniRovKinematic;
    public bool miniRovGravity;

    [Header("Bus Runtime")]
    public int publishedStateMessages;
    public string lastProbeEvent = "idle";

    private float publishTimer;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        RefreshLiveState();
    }

    private void FixedUpdate()
    {
        if (!probeEnabled)
        {
            return;
        }

        publishTimer += Time.fixedDeltaTime;

        float period =
            1.0f / Mathf.Max(1.0f, publishHz);

        if (publishTimer < period)
        {
            return;
        }

        publishTimer -= period;

        RefreshLiveState();

        if (publishToBus)
        {
            PublishAllStates();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (bus == null)
        {
            bus = MIMISKCommonBus.GetOrCreate();
        }

        if (droneObject == null)
        {
            droneObject = GameObject.Find("Drone");
        }

        if (droneObject != null)
        {
            if (droneRigidbody == null)
            {
                droneRigidbody = droneObject.GetComponent<Rigidbody>();
            }

            if (droneMission == null)
            {
                droneMission =
                    droneObject.GetComponent<MIMISKDroneCoreMissionManager>();
            }

            if (droneFlightMode == null)
            {
                droneFlightMode =
                    droneObject.GetComponent<MIMISKDroneCoreFlightModeManager>();
            }

            if (finalPlanner == null)
            {
                finalPlanner =
                    droneObject.GetComponent<MIMISKFinalMissionPlanner>();
            }

            if (tetherManager == null)
            {
                tetherManager =
                    droneObject.GetComponent<MIMISKDroneCoreTetherManager>();
            }
        }

        if (tetherManager == null)
        {
            tetherManager =
                FindSceneComponent<MIMISKDroneCoreTetherManager>();
        }

        if (miniRovObject == null)
        {
            miniRovObject = GameObject.Find("MiniROV");
        }

        if (miniRovObject != null)
        {
            if (miniRovRigidbody == null)
            {
                miniRovRigidbody =
                    miniRovObject.GetComponent<Rigidbody>();
            }

            if (miniRovModule == null)
            {
                miniRovModule =
                    miniRovObject.GetComponent<MIMISKMiniROVModule>();
            }
        }

        UpdateFoundFlags();

        lastProbeEvent =
            "references_checked_drone_" + foundDroneObject +
            "_tether_" + foundTetherManager +
            "_rov_" + foundMiniRovModule;

        Debug.Log("[MIMISK Common Probe] " + lastProbeEvent);
    }

    [ContextMenu("Refresh Live State")]
    public void RefreshLiveState()
    {
        AutoFindReferences();

        if (droneMission != null)
        {
            droneMissionState = droneMission.missionState.ToString();
            droneMissionActive = droneMission.missionActive;

            droneReadyForTether =
                droneMission.missionState ==
                    MIMISKDroneCoreMissionManager.MissionState.ReadyForTetherDeployment ||
                droneMission.missionState ==
                    MIMISKDroneCoreMissionManager.MissionState.Completed;
        }
        else
        {
            droneMissionState = "missing";
            droneMissionActive = false;
            droneReadyForTether = false;
        }

        if (droneFlightMode != null)
        {
            droneFlightModeState =
                droneFlightMode.flightMode.ToString();
        }
        else
        {
            droneFlightModeState = "missing";
        }

        if (droneRigidbody != null)
        {
            dronePosition = droneRigidbody.position;
            droneVelocity = droneRigidbody.linearVelocity;
        }
        else if (droneObject != null)
        {
            dronePosition = droneObject.transform.position;
            droneVelocity = Vector3.zero;
        }

        if (tetherManager != null)
        {
            tetherState = tetherManager.tetherState.ToString();
            tetherDeployedLengthM = tetherManager.deployedLengthM;
            tetherTargetLengthM = tetherManager.targetLengthM;
            tetherWinchRateMS = tetherManager.winchCommandRateMS;
            tetherLastEvent = tetherManager.lastEvent;
        }
        else
        {
            tetherState = "missing";
            tetherLastEvent = "missing_tether_manager";
            tetherDeployedLengthM = 0.0f;
            tetherTargetLengthM = 0.0f;
            tetherWinchRateMS = 0.0f;
        }

        if (miniRovModule != null)
        {
            miniRovState = miniRovModule.state.ToString();
            miniRovLastEvent = miniRovModule.lastEvent;
        }
        else
        {
            miniRovState = "missing";
            miniRovLastEvent = "missing_minirov_module";
        }

        if (miniRovRigidbody != null)
        {
            miniRovPosition = miniRovRigidbody.position;
            miniRovVelocity = miniRovRigidbody.linearVelocity;
            miniRovKinematic = miniRovRigidbody.isKinematic;
            miniRovGravity = miniRovRigidbody.useGravity;
        }
        else if (miniRovObject != null)
        {
            miniRovPosition = miniRovObject.transform.position;
            miniRovVelocity = Vector3.zero;
            miniRovKinematic = false;
            miniRovGravity = false;
        }

        lastProbeEvent = "live_state_refreshed";
    }

    [ContextMenu("Publish All States Now")]
    public void PublishAllStates()
    {
        if (bus == null)
        {
            bus = MIMISKCommonBus.GetOrCreate();
        }

        if (bus == null)
        {
            return;
        }

        PublishDroneState();
        PublishTetherState();
        PublishMiniROVState();

        lastProbeEvent = "states_published_to_common_bus";
    }

    private void PublishDroneState()
    {
        MIMISKStateMessage msg =
            new MIMISKStateMessage();

        msg.subsystem = MIMISKSubsystem.Drone;
        msg.moduleName = "ExistingDroneModule";
        msg.mode = droneMissionState + "/" + droneFlightModeState;
        msg.health =
            foundDroneMission && foundDroneFlightMode
                ? MIMISKHealth.OK
                : MIMISKHealth.Warning;

        msg.ready = droneReadyForTether;
        msg.active = droneMissionActive;
        msg.fault = false;
        msg.position = dronePosition;
        msg.velocity = droneVelocity;

        if (droneObject != null)
        {
            msg.attitude = droneObject.transform.rotation;
        }

        msg.eventText = msg.mode;

        bus.PublishState(msg);
        publishedStateMessages++;
    }

    private void PublishTetherState()
    {
        MIMISKStateMessage msg =
            new MIMISKStateMessage();

        msg.subsystem = MIMISKSubsystem.Tether;
        msg.moduleName = "ExistingTetherModule";
        msg.mode = tetherState;
        msg.health =
            foundTetherManager
                ? MIMISKHealth.OK
                : MIMISKHealth.Warning;

        msg.ready = foundTetherManager;
        msg.active =
            tetherManager != null &&
            (
                tetherManager.tetherState ==
                    MIMISKDroneCoreTetherManager.TetherState.Deploying ||
                tetherManager.tetherState ==
                    MIMISKDroneCoreTetherManager.TetherState.Recovering
            );

        msg.fault = false;
        msg.scalarA = tetherDeployedLengthM;
        msg.scalarB = tetherTargetLengthM;
        msg.scalarC = tetherWinchRateMS;
        msg.eventText = tetherLastEvent;

        bus.PublishState(msg);
        publishedStateMessages++;
    }

    private void PublishMiniROVState()
    {
        MIMISKStateMessage msg =
            new MIMISKStateMessage();

        msg.subsystem = MIMISKSubsystem.MiniROV;
        msg.moduleName = "ExistingMiniROVModule";
        msg.mode = miniRovState;
        msg.health =
            foundMiniRovModule
                ? MIMISKHealth.OK
                : MIMISKHealth.Warning;

        msg.ready = foundMiniRovModule;
        msg.active =
            miniRovModule != null &&
            miniRovModule.state ==
                MIMISKMiniROVModule.MiniROVState.ExternalControlActive;

        msg.fault =
            miniRovModule != null &&
            miniRovModule.state ==
                MIMISKMiniROVModule.MiniROVState.Fault;

        msg.position = miniRovPosition;
        msg.velocity = miniRovVelocity;

        if (miniRovObject != null)
        {
            msg.attitude = miniRovObject.transform.rotation;
        }

        msg.scalarA = miniRovKinematic ? 1.0f : 0.0f;
        msg.scalarB = miniRovGravity ? 1.0f : 0.0f;
        msg.eventText = miniRovLastEvent;

        bus.PublishState(msg);
        publishedStateMessages++;
    }

    private void UpdateFoundFlags()
    {
        foundDroneObject = droneObject != null;
        foundDroneMission = droneMission != null;
        foundDroneFlightMode = droneFlightMode != null;
        foundFinalPlanner = finalPlanner != null;
        foundTetherManager = tetherManager != null;
        foundMiniRovObject = miniRovObject != null;
        foundMiniRovModule = miniRovModule != null;
    }

    private T FindSceneComponent<T>() where T : Component
    {
        T[] components =
            Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        if (components != null && components.Length > 0)
        {
            return components[0];
        }

        return null;
    }
}
