using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKDroneModuleAdapter : MonoBehaviour
{
    [Header("References")]
    public MIMISKCommonBus bus;
    public MIMISKDroneCoreMissionManager missionManager;
    public MIMISKDroneCoreFlightModeManager flightManager;
    public Rigidbody rb;

    [Header("Adapter")]
    public bool adapterEnabled = true;
    public float publishHz = 20.0f;

    [Header("Runtime")]
    public string stateSummary = "unknown";
    public bool surfaceReady;
    public bool missionReady;

    private float timer;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void FixedUpdate()
    {
        if (!adapterEnabled || bus == null)
        {
            return;
        }

        timer += Time.fixedDeltaTime;

        float period = 1.0f / Mathf.Max(1.0f, publishHz);

        if (timer < period)
        {
            return;
        }

        timer -= period;
        PublishState();
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (bus == null)
        {
            bus = MIMISKCommonBus.GetOrCreate();
        }

        if (missionManager == null)
        {
            missionManager = GetComponent<MIMISKDroneCoreMissionManager>();
        }

        if (flightManager == null)
        {
            flightManager = GetComponent<MIMISKDroneCoreFlightModeManager>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }

    public void PublishState()
    {
        MIMISKStateMessage msg = new MIMISKStateMessage();

        msg.subsystem = MIMISKSubsystem.Drone;
        msg.moduleName = "Drone";
        msg.health = MIMISKHealth.OK;

        string missionState =
            missionManager != null ? missionManager.missionState.ToString() : "no_mission";

        string flightMode =
            flightManager != null ? flightManager.flightMode.ToString() : "no_flight_manager";

        msg.mode = missionState + "/" + flightMode;

        surfaceReady =
            flightManager != null &&
            (
                flightManager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceStable ||
                flightManager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceHold
            );

        missionReady =
            missionManager != null &&
            (
                missionManager.missionState == MIMISKDroneCoreMissionManager.MissionState.ReadyForTetherDeployment ||
                missionManager.missionState == MIMISKDroneCoreMissionManager.MissionState.Completed
            );

        msg.ready = surfaceReady || missionReady;
        msg.active = missionManager != null && missionManager.missionActive;
        msg.position = rb != null ? rb.position : transform.position;
        msg.velocity = rb != null ? rb.linearVelocity : Vector3.zero;
        msg.attitude = transform.rotation;
        msg.eventText = msg.mode;

        stateSummary = msg.mode;

        bus.PublishState(msg);
    }
}

[DisallowMultipleComponent]
public class MIMISKTetherModuleAdapter : MonoBehaviour
{
    [Header("References")]
    public MIMISKCommonBus bus;
    public MIMISKDroneCoreTetherManager tetherManager;

    [Header("Adapter")]
    public bool adapterEnabled = true;
    public float publishHz = 20.0f;

    [Header("Runtime")]
    public string stateSummary = "unknown";

    private float timer;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void FixedUpdate()
    {
        if (!adapterEnabled || bus == null || tetherManager == null)
        {
            return;
        }

        timer += Time.fixedDeltaTime;

        float period = 1.0f / Mathf.Max(1.0f, publishHz);

        if (timer < period)
        {
            return;
        }

        timer -= period;
        PublishState();
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (bus == null)
        {
            bus = MIMISKCommonBus.GetOrCreate();
        }

        if (tetherManager == null)
        {
            tetherManager = GetComponent<MIMISKDroneCoreTetherManager>();
        }
    }

    public void PublishState()
    {
        MIMISKStateMessage msg = new MIMISKStateMessage();

        msg.subsystem = MIMISKSubsystem.Tether;
        msg.moduleName = "Tether";
        msg.health = MIMISKHealth.OK;
        msg.mode = tetherManager.tetherState.ToString();
        msg.ready = tetherManager.tetherSystemEnabled;
        msg.active =
            tetherManager.tetherState == MIMISKDroneCoreTetherManager.TetherState.Deploying ||
            tetherManager.tetherState == MIMISKDroneCoreTetherManager.TetherState.Recovering;

        msg.scalarA = tetherManager.deployedLengthM;
        msg.scalarB = tetherManager.targetLengthM;
        msg.scalarC = tetherManager.winchCommandRateMS;
        msg.eventText = tetherManager.lastEvent;

        stateSummary =
            msg.mode +
            " length=" +
            tetherManager.deployedLengthM.ToString("F2") +
            " target=" +
            tetherManager.targetLengthM.ToString("F2");

        bus.PublishState(msg);
    }
}

[DisallowMultipleComponent]
public class MIMISKMiniROVModuleAdapter : MonoBehaviour
{
    [Header("References")]
    public MIMISKCommonBus bus;
    public MIMISKMiniROVModule miniRovModule;
    public Rigidbody rb;

    [Header("Adapter")]
    public bool adapterEnabled = true;
    public float publishHz = 20.0f;

    [Header("Runtime")]
    public string stateSummary = "unknown";

    private float timer;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void FixedUpdate()
    {
        if (!adapterEnabled || bus == null || miniRovModule == null)
        {
            return;
        }

        timer += Time.fixedDeltaTime;

        float period = 1.0f / Mathf.Max(1.0f, publishHz);

        if (timer < period)
        {
            return;
        }

        timer -= period;
        PublishState();
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (bus == null)
        {
            bus = MIMISKCommonBus.GetOrCreate();
        }

        if (miniRovModule == null)
        {
            miniRovModule = GetComponent<MIMISKMiniROVModule>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }

    public void PublishState()
    {
        MIMISKStateMessage msg = new MIMISKStateMessage();

        msg.subsystem = MIMISKSubsystem.MiniROV;
        msg.moduleName = "MiniROV";
        msg.mode = miniRovModule.state.ToString();
        msg.ready = miniRovModule.state != MIMISKMiniROVModule.MiniROVState.Fault;
        msg.active = miniRovModule.state == MIMISKMiniROVModule.MiniROVState.ExternalControlActive;
        msg.fault = miniRovModule.state == MIMISKMiniROVModule.MiniROVState.Fault;
        msg.health = msg.fault ? MIMISKHealth.Fault : MIMISKHealth.OK;

        msg.position = transform.position;
        msg.velocity = rb != null ? rb.linearVelocity : Vector3.zero;
        msg.attitude = transform.rotation;
        msg.scalarA = rb != null && rb.isKinematic ? 1.0f : 0.0f;
        msg.scalarB = rb != null && rb.useGravity ? 1.0f : 0.0f;
        msg.eventText = miniRovModule.lastEvent;

        stateSummary = msg.mode + " / " + miniRovModule.lastEvent;

        bus.PublishState(msg);
    }
}
