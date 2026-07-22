using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKCommonInterfaceDashboard : MonoBehaviour
{
    [Header("References")]
    public MIMISKCommonBus bus;

    [Header("Dashboard")]
    public bool dashboardEnabled = true;

    [Header("Bus Counters")]
    public int observedStateMessages;
    public int observedCommandMessages;

    [Header("Drone State From Bus")]
    public string droneMode = "none";
    public bool droneReady;
    public bool droneActive;
    public Vector3 dronePosition;
    public Vector3 droneVelocity;
    public string droneEvent = "none";

    [Header("Tether State From Bus")]
    public string tetherMode = "none";
    public bool tetherActive;
    public float tetherLengthM;
    public float tetherTargetM;
    public float tetherWinchRateMS;
    public string tetherEvent = "none";

    [Header("MiniROV State From Bus")]
    public string miniRovMode = "none";
    public bool miniRovActive;
    public Vector3 miniRovPosition;
    public Vector3 miniRovVelocity;
    public string miniRovEvent = "none";

    [Header("Last Command From Bus")]
    public string lastCommand = "none";

    private void Awake()
    {
        AutoFindReferences();
    }

    private void OnEnable()
    {
        AutoFindReferences();

        if (bus != null)
        {
            bus.OnState += OnState;
            bus.OnCommand += OnCommand;
        }
    }

    private void OnDisable()
    {
        if (bus != null)
        {
            bus.OnState -= OnState;
            bus.OnCommand -= OnCommand;
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (bus == null)
        {
            bus = MIMISKCommonBus.GetOrCreate();
        }
    }

    private void OnState(MIMISKStateMessage msg)
    {
        if (!dashboardEnabled || msg == null)
        {
            return;
        }

        observedStateMessages++;

        if (msg.subsystem == MIMISKSubsystem.Drone)
        {
            droneMode = msg.mode;
            droneReady = msg.ready;
            droneActive = msg.active;
            dronePosition = msg.position;
            droneVelocity = msg.velocity;
            droneEvent = msg.eventText;
        }
        else if (msg.subsystem == MIMISKSubsystem.Tether)
        {
            tetherMode = msg.mode;
            tetherActive = msg.active;
            tetherLengthM = msg.scalarA;
            tetherTargetM = msg.scalarB;
            tetherWinchRateMS = msg.scalarC;
            tetherEvent = msg.eventText;
        }
        else if (msg.subsystem == MIMISKSubsystem.MiniROV)
        {
            miniRovMode = msg.mode;
            miniRovActive = msg.active;
            miniRovPosition = msg.position;
            miniRovVelocity = msg.velocity;
            miniRovEvent = msg.eventText;
        }
    }

    private void OnCommand(MIMISKCommandMessage command)
    {
        if (!dashboardEnabled || command == null)
        {
            return;
        }

        observedCommandMessages++;

        lastCommand =
            command.source + " -> " +
            command.target + " / " +
            command.verb + " / " +
            command.text;
    }
}
