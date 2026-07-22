using System;
using UnityEngine;

public enum MIMISKSubsystem
{
    None,
    Mission,
    Drone,
    Tether,
    MiniROV,
    Bridge,
    Camera,
    Logger
}

public enum MIMISKCommandVerb
{
    None,

    StartMission,
    DeployTether,
    EnableMiniROVControl,
    StartMiniROVMission,
    RecoverMiniROV,
    ReturnDroneHome,
    HoldTether,
    AbortMission,
    ResetMission,

    Configure,
    Activate,
    Deactivate,
    ResetFault,
    SetPassive,
    ReleaseToWorld,
    EnableExternalControl
}

public enum MIMISKHealth
{
    Unknown,
    OK,
    Warning,
    Fault
}

public enum MIMISKMiniROVBackendMode
{
    UnityNative,
    ESP_Raspberry_HIL,
    ROS2_External
}

[Serializable]
public class MIMISKCommandMessage
{
    public double time;
    public string source = "unknown";
    public MIMISKSubsystem target = MIMISKSubsystem.None;
    public MIMISKCommandVerb verb = MIMISKCommandVerb.None;
    public float value;
    public string text = "";
}

[Serializable]
public class MIMISKStateMessage
{
    public double time;
    public MIMISKSubsystem subsystem = MIMISKSubsystem.None;
    public string moduleName = "unknown";
    public string mode = "unknown";
    public MIMISKHealth health = MIMISKHealth.Unknown;

    public bool ready;
    public bool active;
    public bool fault;

    public Vector3 position;
    public Vector3 velocity;
    public Quaternion attitude = Quaternion.identity;

    public float scalarA;
    public float scalarB;
    public float scalarC;

    public string eventText = "";
}
