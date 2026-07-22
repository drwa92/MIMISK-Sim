using UnityEngine;

[DefaultExecutionOrder(-250)]
[DisallowMultipleComponent]
public class MIMISKMiniROVPreDeploymentSafetyGuard : MonoBehaviour
{
    [Header("References")]
    public MIMISKMiniROVRealisticDeploymentManager deployment;
    public Transform miniRovRoot;
    public Rigidbody miniRovRigidbody;
    public Collider[] miniRovColliders;

    [Header("Guard")]
    public bool guardEnabled = true;

    public bool enforcePassiveBeforeWaterRelease = true;
    public bool enforcePassiveDuringRecovery = true;

    public bool disableCollidersBeforeRelease = true;
    public bool disableControlBeforeRelease = true;
    public bool disableWaterPhysicsBeforeRelease = true;

    [Header("Runtime")]
    public string guardState = "unknown";
    public bool miniRovPassive;
    public bool miniRovDynamic;
    public bool rovControlAllowed;
    public bool waterPhysicsAllowed;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        ApplyGuard();
    }

    private void FixedUpdate()
    {
        ApplyGuard();
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (deployment == null)
        {
            deployment = GetComponent<MIMISKMiniROVRealisticDeploymentManager>();
        }

        if (miniRovRoot == null)
        {
            if (deployment != null && deployment.miniRovRoot != null)
            {
                miniRovRoot = deployment.miniRovRoot;
            }
            else
            {
                GameObject rov = GameObject.Find("MiniROV");

                if (rov != null)
                {
                    miniRovRoot = rov.transform;
                }
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

    [ContextMenu("Apply Guard Now")]
    public void ApplyGuard()
    {
        if (!guardEnabled)
        {
            return;
        }

        AutoFindReferences();

        if (miniRovRoot == null)
        {
            guardState = "missing_minirov";
            return;
        }

        MIMISKMiniROVRealisticDeploymentManager.DeploymentState state =
            deployment != null
                ? deployment.deploymentState
                : MIMISKMiniROVRealisticDeploymentManager.DeploymentState.NotConfigured;

        bool preDeploymentOrCableFollow =
            state == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.NotConfigured ||
            state == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.CableAttachedIdle ||
            state == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.ReadyToDeploy ||
            state == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.CablePayoutToWater ||
            state == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.RecoveredAttached;

        bool recovering =
            state == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.RecoveringKinematic;

        bool stabilizing =
            state == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.WaterTouchDetected ||
            state == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.DynamicStabilizing;

        bool controlActive =
            state == MIMISKMiniROVRealisticDeploymentManager.DeploymentState.ROVControlActive;

        if ((preDeploymentOrCableFollow && enforcePassiveBeforeWaterRelease) ||
            (recovering && enforcePassiveDuringRecovery))
        {
            ApplyPassiveCableState(recovering ? "passive_recovery" : "passive_predeployment");
            return;
        }

        if (stabilizing)
        {
            ApplyWaterStabilizingState();
            return;
        }

        if (controlActive)
        {
            ApplyROVControlActiveState();
            return;
        }

        guardState = "no_action_" + state.ToString();
    }

    private void ApplyPassiveCableState(string label)
    {
        guardState = label;

        miniRovPassive = true;
        miniRovDynamic = false;
        rovControlAllowed = false;
        waterPhysicsAllowed = false;

        if (miniRovRigidbody != null)
        {
            miniRovRigidbody.isKinematic = true;
            miniRovRigidbody.useGravity = false;
        }

        if (disableCollidersBeforeRelease)
        {
            SetMiniRovColliders(false);
        }

        SetMiniRovComponents(
            enableWaterPhysics: !disableWaterPhysicsBeforeRelease,
            enableControl: !disableControlBeforeRelease
        );
    }

    private void ApplyWaterStabilizingState()
    {
        guardState = "water_stabilizing";

        miniRovPassive = false;
        miniRovDynamic = true;
        rovControlAllowed = false;
        waterPhysicsAllowed = true;

        if (miniRovRigidbody != null)
        {
            miniRovRigidbody.isKinematic = false;
            miniRovRigidbody.useGravity = true;
        }

        SetMiniRovColliders(true);

        SetMiniRovComponents(
            enableWaterPhysics: true,
            enableControl: false
        );
    }

    private void ApplyROVControlActiveState()
    {
        guardState = "rov_control_active";

        miniRovPassive = false;
        miniRovDynamic = true;
        rovControlAllowed = true;
        waterPhysicsAllowed = true;

        if (miniRovRigidbody != null)
        {
            miniRovRigidbody.isKinematic = false;
            miniRovRigidbody.useGravity = true;
        }

        SetMiniRovColliders(true);

        SetMiniRovComponents(
            enableWaterPhysics: true,
            enableControl: true
        );
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

    private void SetMiniRovComponents(bool enableWaterPhysics, bool enableControl)
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

        return text.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
