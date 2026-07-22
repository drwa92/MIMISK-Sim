using UnityEngine;

[DefaultExecutionOrder(1200)]
[DisallowMultipleComponent]
public class MIMISKFinalTetherEndpointRig : MonoBehaviour
{
    [Header("Managers")]
    public MIMISKDroneCoreTetherManager tetherManager;
    public MIMISKMiniROVRealisticDeploymentManager deployment;

    [Header("Scene Object Names")]
    public string fairleadName = "WinchFairlead_for_Unity_LineRenderer_Start";
    public string hookAttachPointName = "MiniROV_HookAttachPoint_for_Unity";
    public string payloadAttachPointName = "PayloadAttachPoint_center_for_UnityFixedJoint";
    public string hookVisualName = "small_dark_open_deployment_hook_for_miniROV";
    public string shortYellowCableName = "real_mesh_short_yellow_deployment_cable_to_hook";
    public string activeYellowLineName = "MiniROV_ActiveYellowTetherLine";

    public string miniRovName = "MiniROV";
    public string miniRovCableFollowRootName = "MiniROV_CableEndFollowRoot";
    public string miniRovTetherAnchorName = "ROV_TetherAnchor";

    [Header("Resolved References")]
    public Transform fairleadStart;
    public Transform hookAttachPoint;
    public Transform payloadAttachPoint;
    public Transform hookVisual;
    public Transform shortYellowCableMesh;
    public Transform tetherEndAttachRoot; // kept for compatibility; not used as parent
    public Transform miniRovRoot;
    public Transform miniRovCableFollowRoot;
    public Transform miniRovTetherAnchor;
    public Rigidbody miniRovRigidbody;
    public LineRenderer activeYellowLine;

    [Header("Behavior")]
    public bool rigEnabled = true;

    [Tooltip("Never re-parent prefab children. This must stay ON for prefab-safe operation.")]
    public bool doNotReparentPrefabChildren = true;

    [Tooltip("While cable-managed, keep hook/attach-point visuals following the short yellow cable using world offsets.")]
    public bool followCableEndpointByWorldOffset = true;

    [Tooltip("Let this rig also draw the active yellow line after the tether manager updates.")]
    public bool driveActiveYellowLine = true;

    public int lineSegments = 16;
    public float sagScale = 0.12f;
    public float maxSagM = 0.35f;

    [Header("Runtime")]
    public string endpointMode = "unknown";
    public Vector3 currentLineStartWorld;
    public Vector3 currentLineEndWorld;
    public bool dynamicRovState;
    public bool controlActiveState;

    private bool offsetsCaptured;
    private Vector3 hookAttachOffsetFromYellow;
    private Quaternion hookAttachRotationOffset;
    private Vector3 hookVisualOffsetFromYellow;
    private Quaternion hookVisualRotationOffset;
    private Vector3 payloadOffsetFromYellow;
    private Quaternion payloadRotationOffset;

    private void Awake()
    {
        AutoFindReferences();
        ConfigureEndpointRig();
    }

    private void Start()
    {
        AutoFindReferences();
        ConfigureEndpointRig();
        ApplyManagerReferences();
    }

    private void LateUpdate()
    {
        if (!rigEnabled)
        {
            return;
        }

        AutoFindReferences();
        ConfigureEndpointRig();
        ApplyManagerReferences();
        FollowHookObjectsByWorldOffset();

        currentLineStartWorld = ResolveLineStart();
        currentLineEndWorld = ResolveLineEnd();

        if (driveActiveYellowLine)
        {
            DrawActiveYellowLine(currentLineStartWorld, currentLineEndWorld);
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (tetherManager == null)
        {
            tetherManager = GetComponent<MIMISKDroneCoreTetherManager>();
        }

        if (deployment == null)
        {
            deployment = GetComponent<MIMISKMiniROVRealisticDeploymentManager>();
        }

        if (fairleadStart == null)
        {
            fairleadStart = FindDeepChild(transform, fairleadName);
        }

        if (hookAttachPoint == null)
        {
            hookAttachPoint = FindDeepChild(transform, hookAttachPointName);
        }

        if (payloadAttachPoint == null)
        {
            payloadAttachPoint = FindDeepChild(transform, payloadAttachPointName);
        }

        if (hookVisual == null)
        {
            hookVisual = FindDeepChild(transform, hookVisualName);
        }

        if (shortYellowCableMesh == null)
        {
            shortYellowCableMesh = FindDeepChild(transform, shortYellowCableName);
        }

        if (miniRovCableFollowRoot == null)
        {
            miniRovCableFollowRoot = FindDeepChild(transform, miniRovCableFollowRootName);
        }

        if (miniRovRoot == null)
        {
            GameObject rov = GameObject.Find(miniRovName);

            if (rov != null)
            {
                miniRovRoot = rov.transform;
            }
        }

        if (deployment != null && deployment.miniRovRoot != null)
        {
            miniRovRoot = deployment.miniRovRoot;
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
        }

        if (activeYellowLine == null)
        {
            Transform line =
                FindDeepChild(transform, activeYellowLineName);

            if (line != null)
            {
                activeYellowLine =
                    line.GetComponent<LineRenderer>();

                if (activeYellowLine == null)
                {
                    activeYellowLine =
                        line.gameObject.AddComponent<LineRenderer>();
                }
            }
        }
    }

    [ContextMenu("Configure Endpoint Rig")]
    public void ConfigureEndpointRig()
    {
        AutoFindReferences();

        // Important:
        // Do NOT SetParent() hookAttachPoint/hookVisual/yellowCableMesh.
        // These are prefab-instance children. Reparenting them causes Unity prefab errors.
        // The previous correct logic was: move the visual cable endpoint transform,
        // then let MiniROV_CableEndFollowRoot follow that endpoint.

        if (!offsetsCaptured && shortYellowCableMesh != null)
        {
            CaptureWorldOffsets();
        }
    }

    private void CaptureWorldOffsets()
    {
        if (shortYellowCableMesh == null)
        {
            return;
        }

        if (hookAttachPoint != null)
        {
            hookAttachOffsetFromYellow =
                hookAttachPoint.position - shortYellowCableMesh.position;

            hookAttachRotationOffset =
                Quaternion.Inverse(shortYellowCableMesh.rotation) *
                hookAttachPoint.rotation;
        }

        if (hookVisual != null)
        {
            hookVisualOffsetFromYellow =
                hookVisual.position - shortYellowCableMesh.position;

            hookVisualRotationOffset =
                Quaternion.Inverse(shortYellowCableMesh.rotation) *
                hookVisual.rotation;
        }

        if (payloadAttachPoint != null)
        {
            payloadOffsetFromYellow =
                payloadAttachPoint.position - shortYellowCableMesh.position;

            payloadRotationOffset =
                Quaternion.Inverse(shortYellowCableMesh.rotation) *
                payloadAttachPoint.rotation;
        }

        offsetsCaptured = true;
    }

    private void FollowHookObjectsByWorldOffset()
    {
        if (!followCableEndpointByWorldOffset ||
            shortYellowCableMesh == null ||
            !offsetsCaptured)
        {
            return;
        }

        // The tether manager moves shortYellowCableMesh as the deployed cable endpoint.
        // We follow it without changing prefab hierarchy.

        if (hookAttachPoint != null &&
            hookAttachPoint != shortYellowCableMesh)
        {
            hookAttachPoint.position =
                shortYellowCableMesh.position + hookAttachOffsetFromYellow;

            hookAttachPoint.rotation =
                shortYellowCableMesh.rotation * hookAttachRotationOffset;
        }

        if (hookVisual != null &&
            hookVisual != shortYellowCableMesh)
        {
            hookVisual.position =
                shortYellowCableMesh.position + hookVisualOffsetFromYellow;

            hookVisual.rotation =
                shortYellowCableMesh.rotation * hookVisualRotationOffset;
        }

        if (payloadAttachPoint != null &&
            payloadAttachPoint != shortYellowCableMesh)
        {
            payloadAttachPoint.position =
                shortYellowCableMesh.position + payloadOffsetFromYellow;

            payloadAttachPoint.rotation =
                shortYellowCableMesh.rotation * payloadRotationOffset;
        }
    }

    private void ApplyManagerReferences()
    {
        bool dynamic =
            IsDynamicRovState();

        bool control =
            deployment != null &&
            deployment.deploymentState ==
                MIMISKMiniROVRealisticDeploymentManager.DeploymentState.ROVControlActive;

        dynamicRovState = dynamic;
        controlActiveState = control;

        if (tetherManager != null)
        {
            if (fairleadStart != null)
            {
                tetherManager.fairleadLineStart = fairleadStart;
            }

            if (activeYellowLine != null)
            {
                tetherManager.tetherLineRenderer = activeYellowLine;
            }

            // The old working endpoint was the real short yellow cable mesh.
            // Keep that as the moving visual end.
            if (shortYellowCableMesh != null)
            {
                tetherManager.movingTetherEndVisual = shortYellowCableMesh;
            }
            else if (hookVisual != null)
            {
                tetherManager.movingTetherEndVisual = hookVisual;
            }
            else if (hookAttachPoint != null)
            {
                tetherManager.movingTetherEndVisual = hookAttachPoint;
            }

            if (dynamic)
            {
                tetherManager.useVirtualEndpointWhenNoMiniRov = false;
                tetherManager.miniRovRigidbody = miniRovRigidbody;
                tetherManager.miniRovTetherPoint =
                    miniRovTetherAnchor != null
                        ? miniRovTetherAnchor
                        : miniRovRoot;
            }
            else
            {
                tetherManager.useVirtualEndpointWhenNoMiniRov = true;
                tetherManager.miniRovRigidbody = null;
                tetherManager.miniRovTetherPoint = null;
            }

            tetherManager.hideStaticShortCableMeshWhenDynamic = false;
            tetherManager.staticShortDeploymentCableMesh = null;
        }

        if (deployment != null)
        {
            // Restore the previous successful contract:
            // yellowCableEndPoint is the real yellow cable endpoint visual.
            if (shortYellowCableMesh != null)
            {
                deployment.yellowCableEndPoint = shortYellowCableMesh;
            }
            else if (hookAttachPoint != null)
            {
                deployment.yellowCableEndPoint = hookAttachPoint;
            }

            if (hookVisual != null)
            {
                deployment.hookVisual = hookVisual;
            }
            else if (hookAttachPoint != null)
            {
                deployment.hookVisual = hookAttachPoint;
            }

            if (miniRovCableFollowRoot != null)
            {
                deployment.cableEndFollowRoot = miniRovCableFollowRoot;
            }

            if (miniRovRoot != null)
            {
                deployment.miniRovRoot = miniRovRoot;
            }

            if (miniRovRigidbody != null)
            {
                deployment.miniRovRigidbody = miniRovRigidbody;
            }

            if (miniRovTetherAnchor != null)
            {
                deployment.miniRovTetherAnchor = miniRovTetherAnchor;
            }
        }
    }

    private bool IsDynamicRovState()
    {
        if (deployment == null)
        {
            return false;
        }

        return
            deployment.deploymentState ==
                MIMISKMiniROVRealisticDeploymentManager.DeploymentState.DynamicStabilizing ||
            deployment.deploymentState ==
                MIMISKMiniROVRealisticDeploymentManager.DeploymentState.ROVControlActive;
    }

    private Vector3 ResolveLineStart()
    {
        if (fairleadStart != null)
        {
            return fairleadStart.position;
        }

        if (tetherManager != null)
        {
            return tetherManager.tetherStartWorld;
        }

        return transform.position;
    }

    private Vector3 ResolveLineEnd()
    {
        if (IsDynamicRovState())
        {
            endpointMode = "dynamic_minirov_tether_anchor";

            if (miniRovTetherAnchor != null)
            {
                return miniRovTetherAnchor.position;
            }

            if (miniRovRoot != null)
            {
                return miniRovRoot.position;
            }
        }

        endpointMode = "kinematic_yellow_cable_endpoint";

        if (shortYellowCableMesh != null)
        {
            return shortYellowCableMesh.position;
        }

        if (hookAttachPoint != null)
        {
            return hookAttachPoint.position;
        }

        if (hookVisual != null)
        {
            return hookVisual.position;
        }

        if (tetherManager != null)
        {
            return tetherManager.tetherEndWorld;
        }

        return transform.position;
    }

    private void DrawActiveYellowLine(Vector3 a, Vector3 b)
    {
        if (activeYellowLine == null)
        {
            return;
        }

        int n =
            Mathf.Max(2, lineSegments);

        activeYellowLine.positionCount =
            n;

        float distance =
            Vector3.Distance(a, b);

        float sag =
            Mathf.Min(
                maxSagM,
                distance * sagScale
            );

        for (int i = 0; i < n; i++)
        {
            float u =
                n <= 1
                    ? 1.0f
                    : (float)i / (float)(n - 1);

            Vector3 p =
                Vector3.Lerp(a, b, u);

            float parabola =
                4.0f * u * (1.0f - u);

            p.y -=
                sag * parabola;

            activeYellowLine.SetPosition(i, p);
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
