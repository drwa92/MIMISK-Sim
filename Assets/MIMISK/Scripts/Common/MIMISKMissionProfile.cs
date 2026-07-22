using UnityEngine;

[CreateAssetMenu(
    fileName = "MIMISK_MissionProfile",
    menuName = "MIMISK/Mission Profile",
    order = 10)]
public class MIMISKMissionProfile : ScriptableObject
{
    [Header("Mission")]
    public string missionId = "mimisk_autonomous_demo_01";
    public string description = "Drone deploys MiniROV, MiniROV inspects, returns, then tether recovers.";

    [Header("Drone Mission")]
    public TextAsset droneTrajectory;
    public bool useExistingDroneMissionManager = true;
    public bool returnHomeAfterRecovery = true;

    [Header("Tether Mission")]
    public float deployLengthM = 0.90f;
    public float payoutSpeedMS = 0.22f;
    public float recoverySpeedMS = 0.25f;
    public bool enablePhysicalTetherForce = false;

    [Header("MiniROV Mission")]
    public TextAsset miniRovTrajectory;
    public MIMISKMiniROVBackendMode miniRovBackend = MIMISKMiniROVBackendMode.UnityNative;
    public float miniRovWaypointRadiusM = 0.25f;
    public float miniRovReturnRadiusM = 0.35f;
    public bool returnToTetherBeforeRecovery = true;

    [Header("Safety")]
    public bool requireDroneSurfaceStable = true;
    public bool allowDeploymentIfAlreadyOnSurface = true;
    public bool requireExternalMiniRovStackIfHIL = true;
}
