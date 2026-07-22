using UnityEngine;

/// <summary>
/// Lightweight runtime monitor that checks whether the physical tether endpoint is locked to the MiniROV rear anchor
/// and whether the deployed winch length is following MiniROV motion with the configured slack margin.
/// This script does not control the ROV; it only keeps references synchronized and exposes diagnostics for logging.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1600)]
public class MIMISKPhysicalTetherRovSyncMonitor : MonoBehaviour
{
    [Header("References")]
    public bool autoFindReferences = true;
    public MIMISKPhysicalTetherModel physicalTether;
    public MIMISKMiniROVRearTetherAnchor rearAnchorProvider;
    public Transform rearAnchor;
    public MIMISKUnifiedTetherManager unifiedTether;
    public MIMISKDroneCoreTetherManager tetherManager;
    public MIMISKTetherSmartWinchController smartWinch;
    public Rigidbody miniRovRigidbody;

    [Header("Monitor")]
    public bool monitorEnabled = true;
    [Tooltip("V7.2 safety: when true, the monitor never overwrites the original UnifiedTether/DroneCore/SmartWinch endpoint references. It only monitors and optionally points the physical tether at the rear anchor.")]
    public bool preserveExistingMissionLogic = true;
    public bool writeRearAnchorToControllersEveryFrame = false;
    public float desiredSlackM = 0.12f;
    public float lengthSyncToleranceM = 0.22f;
    public float endpointGapToleranceM = 0.010f;

    [Header("Diagnostics")]
    public float requiredLengthM;
    public float desiredLengthWithSlackM;
    public float currentDeployedLengthM;
    public float lengthErrorM;
    public float physicalEndAnchorGapM;
    public float cableLastNodeGapM;
    public bool endAnchorLockedToRear;
    public bool winchLengthSynchronizedToRov;
    public bool cableLastNodeLockedToRear;
    public Vector3 rearAnchorLocalPosition;
    public Vector3 rearAnchorWorldPosition;
    public string lastAction = "not_initialized";

    private void Awake()
    {
        AutoFindReferencesNow();
    }

    private void OnEnable()
    {
        AutoFindReferencesNow();
    }

    private void LateUpdate()
    {
        if (!monitorEnabled)
        {
            return;
        }

        if (autoFindReferences)
        {
            AutoFindReferencesNow();
        }

        if (writeRearAnchorToControllersEveryFrame)
        {
            ApplyRearAnchorReferencesNow();
        }

        UpdateDiagnostics();
    }

    [ContextMenu("Auto Find References Now")]
    public void AutoFindReferencesNow()
    {
        if (physicalTether == null)
        {
            physicalTether = GetComponent<MIMISKPhysicalTetherModel>();
        }

        if (unifiedTether == null)
        {
            unifiedTether = GetComponent<MIMISKUnifiedTetherManager>();
        }

        if (tetherManager == null)
        {
            tetherManager = GetComponent<MIMISKDroneCoreTetherManager>();
        }

        if (smartWinch == null)
        {
            smartWinch = GetComponent<MIMISKTetherSmartWinchController>();
        }

        if (rearAnchorProvider == null)
        {
            rearAnchorProvider = Object.FindFirstObjectByType<MIMISKMiniROVRearTetherAnchor>();
        }

        if (rearAnchor == null && rearAnchorProvider != null)
        {
            rearAnchor = rearAnchorProvider.GetAnchorTransform();
        }

        if (miniRovRigidbody == null)
        {
            if (unifiedTether != null && unifiedTether.miniRovRigidbody != null)
            {
                miniRovRigidbody = unifiedTether.miniRovRigidbody;
            }
            else if (tetherManager != null && tetherManager.miniRovRigidbody != null)
            {
                miniRovRigidbody = tetherManager.miniRovRigidbody;
            }
            else if (rearAnchor != null)
            {
                miniRovRigidbody = rearAnchor.GetComponentInParent<Rigidbody>();
            }
        }
    }

    [ContextMenu("Apply Rear Anchor References Now")]
    public void ApplyRearAnchorReferencesNow()
    {
        if (rearAnchor == null && rearAnchorProvider != null)
        {
            rearAnchor = rearAnchorProvider.GetAnchorTransform();
        }

        if (rearAnchor == null)
        {
            lastAction = "apply_failed_rear_anchor_missing";
            return;
        }

        if (!preserveExistingMissionLogic)
        {
            if (unifiedTether != null)
            {
                unifiedTether.miniRovTetherAnchor = rearAnchor;
                unifiedTether.miniRovTetherAnchorName = rearAnchor.name;
            }

            if (tetherManager != null)
            {
                tetherManager.miniRovTetherPoint = rearAnchor;
            }

            if (smartWinch != null)
            {
                smartWinch.miniRovTetherPoint = rearAnchor;
            }
        }

        if (physicalTether != null)
        {
            physicalTether.rovBackAnchor = rearAnchor;
            physicalTether.forceEndAnchorToRovBackAnchor = false;
            physicalTether.preferRearMiniRovTetherAnchor = true;
            physicalTether.useDeploymentCableEndpointWhenCableManaged = true;
        }

        lastAction = preserveExistingMissionLogic ? "rear_anchor_applied_to_physical_tether_only" : "rear_anchor_references_applied";
    }

    public void UpdateDiagnostics()
    {
        if (rearAnchor == null)
        {
            endAnchorLockedToRear = false;
            winchLengthSynchronizedToRov = false;
            cableLastNodeLockedToRear = false;
            lastAction = "diagnostic_failed_rear_anchor_missing";
            return;
        }

        rearAnchorWorldPosition = rearAnchor.position;
        Transform rovRoot = rearAnchor.GetComponentInParent<Rigidbody>() != null
            ? rearAnchor.GetComponentInParent<Rigidbody>().transform
            : rearAnchor.parent;

        if (rovRoot != null)
        {
            rearAnchorLocalPosition = rovRoot.InverseTransformPoint(rearAnchor.position);
        }

        Vector3 start = rearAnchor.position;
        if (physicalTether != null)
        {
            start = physicalTether.startWorld;
        }
        else if (tetherManager != null)
        {
            start = tetherManager.tetherStartWorld;
        }

        requiredLengthM = Vector3.Distance(start, rearAnchor.position);
        desiredLengthWithSlackM = requiredLengthM + Mathf.Max(0.0f, desiredSlackM);

        if (physicalTether != null)
        {
            currentDeployedLengthM = physicalTether.deployedLengthM;
            physicalEndAnchorGapM = Vector3.Distance(physicalTether.endWorld, rearAnchor.position);
            int n = physicalTether.CablePointCount;
            cableLastNodeGapM = n > 0 ? Vector3.Distance(physicalTether.GetCablePointWorld(n - 1), rearAnchor.position) : float.PositiveInfinity;
            endAnchorLockedToRear = physicalTether.endAnchor == rearAnchor && physicalEndAnchorGapM <= endpointGapToleranceM;
            cableLastNodeLockedToRear = cableLastNodeGapM <= endpointGapToleranceM;
        }
        else if (tetherManager != null)
        {
            currentDeployedLengthM = tetherManager.deployedLengthM;
            physicalEndAnchorGapM = Vector3.Distance(tetherManager.tetherEndWorld, rearAnchor.position);
            cableLastNodeGapM = physicalEndAnchorGapM;
            endAnchorLockedToRear = physicalEndAnchorGapM <= endpointGapToleranceM;
            cableLastNodeLockedToRear = endAnchorLockedToRear;
        }
        else
        {
            currentDeployedLengthM = 0.0f;
            physicalEndAnchorGapM = float.PositiveInfinity;
            cableLastNodeGapM = float.PositiveInfinity;
            endAnchorLockedToRear = false;
            cableLastNodeLockedToRear = false;
        }

        lengthErrorM = currentDeployedLengthM - desiredLengthWithSlackM;
        winchLengthSynchronizedToRov = Mathf.Abs(lengthErrorM) <= Mathf.Max(0.01f, lengthSyncToleranceM);

        if (!endAnchorLockedToRear)
        {
            lastAction = "endpoint_not_locked_to_rear_anchor";
        }
        else if (!winchLengthSynchronizedToRov)
        {
            lastAction = "winch_length_tracking_rear_anchor_with_error";
        }
        else
        {
            lastAction = "rov_anchor_and_winch_length_synchronized";
        }
    }
}
