using UnityEngine;

[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public class MIMISKFinalTetherLineSynchronizer : MonoBehaviour
{
    [Header("References")]
    public MIMISKDroneCoreTetherManager tetherManager;
    public MIMISKMiniROVRealisticDeploymentManager deployment;
    public LineRenderer lineRenderer;

    [Header("Endpoint Names")]
    public string fairleadName = "WinchFairlead_for_Unity_LineRenderer_Start";
    public string hookName = "small_dark_open_deployment_hook_for_miniROV";
    public string yellowCableEndName = "real_mesh_short_yellow_deployment_cable_to_hook";
    public string rovAnchorName = "ROV_TetherAnchor";

    [Header("Visual")]
    public bool synchronizerEnabled = true;
    public bool preferHookForKinematicEndpoint = true;
    public int lineSegments = 16;
    public float sagScale = 0.15f;
    public float maxSagM = 0.45f;

    [Header("Runtime")]
    public Vector3 visualStartWorld;
    public Vector3 visualEndWorld;
    public string visualMode = "unknown";

    private Transform fairlead;
    private Transform hook;
    private Transform yellowCableEnd;
    private Transform rovAnchor;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void LateUpdate()
    {
        if (!synchronizerEnabled)
        {
            return;
        }

        AutoFindReferences();

        if (lineRenderer == null)
        {
            return;
        }

        visualStartWorld =
            ResolveStart();

        visualEndWorld =
            ResolveEnd();

        DrawLine(
            visualStartWorld,
            visualEndWorld
        );
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (tetherManager == null)
        {
            tetherManager =
                GetComponent<MIMISKDroneCoreTetherManager>();
        }

        if (deployment == null)
        {
            deployment =
                GetComponent<MIMISKMiniROVRealisticDeploymentManager>();
        }

        if (lineRenderer == null && tetherManager != null)
        {
            lineRenderer =
                tetherManager.tetherLineRenderer;
        }

        if (lineRenderer == null)
        {
            Transform line =
                FindDeepChild(transform, "MiniROV_ActiveYellowTetherLine");

            if (line == null)
            {
                line =
                    FindDeepChild(transform, "TetherLine");
            }

            if (line != null)
            {
                lineRenderer =
                    line.GetComponent<LineRenderer>();

                if (lineRenderer == null)
                {
                    lineRenderer =
                        line.gameObject.AddComponent<LineRenderer>();
                }
            }
        }

        if (fairlead == null)
        {
            fairlead =
                FindDeepChild(transform, fairleadName);
        }

        if (hook == null)
        {
            hook =
                FindDeepChild(transform, hookName);
        }

        if (yellowCableEnd == null)
        {
            yellowCableEnd =
                FindDeepChild(transform, yellowCableEndName);
        }

        if (deployment != null && deployment.miniRovRoot != null)
        {
            if (rovAnchor == null)
            {
                rovAnchor =
                    FindDeepChild(deployment.miniRovRoot, rovAnchorName);
            }

            if (rovAnchor == null)
            {
                rovAnchor =
                    deployment.miniRovTetherAnchor;
            }
        }
    }

    private Vector3 ResolveStart()
    {
        if (fairlead != null)
        {
            return fairlead.position;
        }

        if (tetherManager != null)
        {
            return tetherManager.tetherStartWorld;
        }

        return transform.position;
    }

    private Vector3 ResolveEnd()
    {
        if (deployment == null)
        {
            visualMode = "no_deployment";

            if (tetherManager != null)
            {
                return tetherManager.tetherEndWorld;
            }

            return transform.position;
        }

        bool dynamicRov =
            deployment.deploymentState ==
                MIMISKMiniROVRealisticDeploymentManager.DeploymentState.DynamicStabilizing ||
            deployment.deploymentState ==
                MIMISKMiniROVRealisticDeploymentManager.DeploymentState.ROVControlActive;

        if (dynamicRov)
        {
            visualMode = "dynamic_rov_anchor";

            if (rovAnchor != null)
            {
                return rovAnchor.position;
            }

            if (deployment.miniRovTetherAnchor != null)
            {
                return deployment.miniRovTetherAnchor.position;
            }

            if (deployment.miniRovRoot != null)
            {
                return deployment.miniRovRoot.position;
            }
        }

        visualMode = "kinematic_cable_endpoint";

        if (preferHookForKinematicEndpoint && hook != null)
        {
            return hook.position;
        }

        if (yellowCableEnd != null)
        {
            return yellowCableEnd.position;
        }

        if (hook != null)
        {
            return hook.position;
        }

        if (tetherManager != null)
        {
            return tetherManager.tetherEndWorld;
        }

        return transform.position;
    }

    private void DrawLine(Vector3 a, Vector3 b)
    {
        int n =
            Mathf.Max(2, lineSegments);

        lineRenderer.positionCount =
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

            lineRenderer.SetPosition(i, p);
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
