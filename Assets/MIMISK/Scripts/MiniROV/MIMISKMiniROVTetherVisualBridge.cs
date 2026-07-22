using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKMiniROVTetherVisualBridge : MonoBehaviour
{
    [Header("References")]
    public Transform tetherStart;
    public Transform miniRovRoot;
    public Transform rovTetherAnchor;

    [Header("Visual Cable")]
    public LineRenderer activeTetherLine;
    public string activeLineName = "MiniROV_ActiveYellowTetherLine";
    public float lineWidthM = 0.018f;
    public int lineSegments = 16;
    public float slackSagM = 0.08f;

    [Header("Optional Visual Hook")]
    public Transform hookVisual;
    public bool moveHookVisualToRovAnchor = true;

    [Header("Duplicate Cleanup")]
    public bool disableOtherTetherLineRenderers = true;

    [Header("Runtime")]
    public bool active;
    public string lastEvent = "idle";

    private void Awake()
    {
        AutoFindReferences();
    }

    private void LateUpdate()
    {
        if (!active)
        {
            return;
        }

        UpdateTetherLine();
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (tetherStart == null)
        {
            tetherStart = FindDeepChild(transform.root, "WinchFairlead_for_Unity_LineRenderer_Start");
        }

        if (tetherStart == null)
        {
            tetherStart = FindDeepChild(transform.root, "TetherAnchor");
        }

        if (tetherStart == null)
        {
            tetherStart = FindDeepChild(transform.root, "WinchPoint");
        }

        if (miniRovRoot == null)
        {
            GameObject rov = GameObject.Find("MiniROV");

            if (rov != null)
            {
                miniRovRoot = rov.transform;
            }
        }

        if (miniRovRoot != null && rovTetherAnchor == null)
        {
            rovTetherAnchor = FindDeepChild(miniRovRoot, "ROV_TetherAnchor");
        }

        if (miniRovRoot != null && rovTetherAnchor == null)
        {
            rovTetherAnchor = FindDeepChild(miniRovRoot, "MiniROV_TetherPoint");
        }

        if (miniRovRoot != null && rovTetherAnchor == null)
        {
            rovTetherAnchor = FindDeepChild(miniRovRoot, "TetherPoint");
        }

        if (hookVisual == null)
        {
            hookVisual = FindDeepChild(transform.root, "small_dark_open_deployment_hook_for_miniROV");
        }

        EnsureLineRenderer();
    }

    [ContextMenu("Activate Free Swimming Tether")]
    public void ActivateFreeSwimmingTether()
    {
        AutoFindReferences();

        if (disableOtherTetherLineRenderers)
        {
            DisableOtherLineRenderers();
        }

        EnsureLineRenderer();

        if (activeTetherLine != null)
        {
            activeTetherLine.enabled = true;
        }

        active = true;
        lastEvent = "free_swimming_tether_active";

        UpdateTetherLine();

        Debug.Log("[MIMISK] MiniROV active tether visual is now attached to ROV_TetherAnchor.");
    }

    private void EnsureLineRenderer()
    {
        if (activeTetherLine != null)
        {
            ConfigureLineRenderer(activeTetherLine);
            return;
        }

        GameObject existing = GameObject.Find(activeLineName);

        if (existing == null)
        {
            existing = new GameObject(activeLineName);
            existing.transform.SetParent(transform.root, false);
        }

        activeTetherLine = existing.GetComponent<LineRenderer>();

        if (activeTetherLine == null)
        {
            activeTetherLine = existing.AddComponent<LineRenderer>();
        }

        ConfigureLineRenderer(activeTetherLine);
    }

    private void ConfigureLineRenderer(LineRenderer lr)
    {
        lr.useWorldSpace = true;
        lr.positionCount = Mathf.Max(2, lineSegments);
        lr.widthMultiplier = lineWidthM;

        if (lr.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                Material mat = new Material(shader);
                mat.color = new Color(1.0f, 0.78f, 0.05f, 1.0f);
                lr.sharedMaterial = mat;
            }
        }
    }

    private void UpdateTetherLine()
    {
        if (activeTetherLine == null ||
            tetherStart == null ||
            rovTetherAnchor == null)
        {
            return;
        }

        int n = Mathf.Max(2, lineSegments);

        activeTetherLine.positionCount = n;

        Vector3 a = tetherStart.position;
        Vector3 b = rovTetherAnchor.position;

        for (int i = 0; i < n; i++)
        {
            float u = i / Mathf.Max(1.0f, (float)(n - 1));

            Vector3 p = Vector3.Lerp(a, b, u);
            p += Vector3.down * Mathf.Sin(Mathf.PI * u) * slackSagM;

            activeTetherLine.SetPosition(i, p);
        }

        if (moveHookVisualToRovAnchor && hookVisual != null)
        {
            hookVisual.position = b;
            hookVisual.rotation = rovTetherAnchor.rotation;
        }
    }

    private void DisableOtherLineRenderers()
    {
        LineRenderer[] lines =
            Object.FindObjectsByType<LineRenderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] == null)
            {
                continue;
            }

            if (activeTetherLine != null && lines[i] == activeTetherLine)
            {
                continue;
            }

            string n = lines[i].gameObject.name.ToLowerInvariant();

            bool looksLikeTether =
                n.Contains("tether") ||
                n.Contains("cable") ||
                n.Contains("line");

            if (looksLikeTether)
            {
                lines[i].enabled = false;
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
