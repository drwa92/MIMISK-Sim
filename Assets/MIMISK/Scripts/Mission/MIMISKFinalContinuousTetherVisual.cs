using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKFinalContinuousTetherVisual : MonoBehaviour
{
    [Header("References")]
    public MIMISKFinalMissionPlanner finalPlanner;
    public Transform tetherStart;
    public Transform rovTetherAnchor;
    public Transform hookVisual;

    [Header("Line Renderer")]
    public LineRenderer line;
    public string lineObjectName = "MIMISK_Final_ContinuousYellowTether";
    public int segments = 28;
    public float widthM = 0.022f;

    [Header("Cable Shape")]
    public float baseSagM = 0.10f;
    public float sagPerMeter = 0.055f;
    public float maxSagM = 0.65f;

    [Tooltip("Small sideways bend so the line does not look perfectly mathematical.")]
    public float lateralCurveM = 0.025f;

    [Header("Behavior")]
    public bool activeOnlyDuringROVControl = true;
    public bool disableOtherTetherLineRenderers = true;
    public bool moveHookVisualToROVAnchor = true;

    [Header("Runtime")]
    public bool visualActive;
    public string lastEvent = "idle";

    private bool disabledOthers;

    private void Awake()
    {
        AutoFindReferences();
        EnsureLine();
    }

    private void LateUpdate()
    {
        if (ShouldBeActive())
        {
            if (!visualActive)
            {
                ActivateVisual();
            }

            UpdateLine();
        }
        else
        {
            if (visualActive)
            {
                DeactivateVisual();
            }
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (finalPlanner == null)
        {
            finalPlanner = GetComponent<MIMISKFinalMissionPlanner>();
        }

        if (tetherStart == null)
        {
            tetherStart =
                FindDeepChild(transform, "WinchFairlead_for_Unity_LineRenderer_Start");
        }

        if (tetherStart == null)
        {
            tetherStart =
                FindDeepChild(transform, "TetherAnchor");
        }

        if (tetherStart == null)
        {
            tetherStart =
                FindDeepChild(transform, "WinchPoint");
        }

        if (rovTetherAnchor == null)
        {
            GameObject rov = GameObject.Find("MiniROV");

            if (rov != null)
            {
                rovTetherAnchor =
                    FindDeepChild(rov.transform, "ROV_TetherAnchor");

                if (rovTetherAnchor == null)
                {
                    rovTetherAnchor =
                        FindDeepChild(rov.transform, "MiniROV_TetherPoint");
                }

                if (rovTetherAnchor == null)
                {
                    rovTetherAnchor =
                        FindDeepChild(rov.transform, "TetherPoint");
                }
            }
        }

        if (hookVisual == null)
        {
            hookVisual =
                FindDeepChild(transform, "small_dark_open_deployment_hook_for_miniROV");
        }
    }

    private bool ShouldBeActive()
    {
        if (finalPlanner == null)
        {
            return visualActive;
        }

        if (!activeOnlyDuringROVControl)
        {
            return true;
        }

        return finalPlanner.state ==
            MIMISKFinalMissionPlanner.FinalMissionState.MiniROVControlActive;
    }

    [ContextMenu("Activate Visual")]
    public void ActivateVisual()
    {
        AutoFindReferences();
        EnsureLine();

        if (disableOtherTetherLineRenderers && !disabledOthers)
        {
            DisableOtherTetherLines();
            disabledOthers = true;
        }

        if (line != null)
        {
            line.enabled = true;
        }

        visualActive = true;
        lastEvent = "active";
    }

    [ContextMenu("Deactivate Visual")]
    public void DeactivateVisual()
    {
        if (line != null)
        {
            line.enabled = false;
        }

        visualActive = false;
        lastEvent = "inactive";
    }

    private void EnsureLine()
    {
        if (line != null)
        {
            ConfigureLine();
            return;
        }

        GameObject go =
            GameObject.Find(lineObjectName);

        if (go == null)
        {
            go = new GameObject(lineObjectName);
            go.transform.SetParent(transform.root, false);
        }

        line =
            go.GetComponent<LineRenderer>();

        if (line == null)
        {
            line = go.AddComponent<LineRenderer>();
        }

        ConfigureLine();
    }

    private void ConfigureLine()
    {
        if (line == null)
        {
            return;
        }

        line.useWorldSpace = true;
        line.positionCount = Mathf.Max(2, segments);
        line.widthMultiplier = widthM;
        line.numCornerVertices = 5;
        line.numCapVertices = 5;

        if (line.sharedMaterial == null)
        {
            Shader shader =
                Shader.Find("Sprites/Default");

            if (shader != null)
            {
                Material mat =
                    new Material(shader);

                mat.color =
                    new Color(1.0f, 0.74f, 0.04f, 1.0f);

                line.sharedMaterial = mat;
            }
        }
    }

    private void UpdateLine()
    {
        AutoFindReferences();

        if (line == null || tetherStart == null || rovTetherAnchor == null)
        {
            return;
        }

        int n =
            Mathf.Max(2, segments);

        line.positionCount = n;

        Vector3 a =
            tetherStart.position;

        Vector3 b =
            rovTetherAnchor.position;

        Vector3 ab =
            b - a;

        float distance =
            ab.magnitude;

        float sag =
            Mathf.Clamp(
                baseSagM + distance * sagPerMeter,
                baseSagM,
                maxSagM
            );

        Vector3 horizontalDir =
            new Vector3(ab.x, 0.0f, ab.z);

        if (horizontalDir.sqrMagnitude < 0.0001f)
        {
            horizontalDir = transform.right;
        }
        else
        {
            horizontalDir.Normalize();
        }

        Vector3 lateral =
            Vector3.Cross(Vector3.up, horizontalDir).normalized;

        for (int i = 0; i < n; i++)
        {
            float u =
                i / Mathf.Max(1.0f, (float)(n - 1));

            Vector3 p =
                Vector3.Lerp(a, b, u);

            float sagShape =
                Mathf.Sin(Mathf.PI * u);

            p +=
                Vector3.down * sag * sagShape;

            p +=
                lateral *
                lateralCurveM *
                Mathf.Sin(2.0f * Mathf.PI * u);

            line.SetPosition(i, p);
        }

        if (moveHookVisualToROVAnchor && hookVisual != null)
        {
            hookVisual.position = b;
            hookVisual.rotation = rovTetherAnchor.rotation;
        }

        lastEvent = "updated_distance_" + distance.ToString("F2");
    }

    private void DisableOtherTetherLines()
    {
        LineRenderer[] lines =
            Object.FindObjectsByType<LineRenderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0; i < lines.Length; i++)
        {
            LineRenderer lr = lines[i];

            if (lr == null || lr == line)
            {
                continue;
            }

            string n =
                lr.gameObject.name.ToLowerInvariant();

            bool looksLikeTether =
                n.Contains("tether") ||
                n.Contains("cable") ||
                n.Contains("line");

            if (looksLikeTether)
            {
                lr.enabled = false;
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
