using UnityEngine;

[DefaultExecutionOrder(3600)]
[DisallowMultipleComponent]
public class MIMISKWinchRopeSingleVisual : MonoBehaviour
{
    [Header("Managers")]
    public MIMISKUnifiedTetherManager unifiedTether;
    public MIMISKDroneCoreTetherManager tetherManager;

    [Header("Authoritative Yellow Rope Objects")]
    public string fairleadName = "WinchFairlead_for_Unity_LineRenderer_Start";

    [Tooltip("This is the actual hanging yellow cable endpoint from the winch/hook assembly.")]
    public string shortYellowDeploymentCableName = "real_mesh_short_yellow_deployment_cable_to_hook";

    [Tooltip("Use this material so the dynamic cable looks like the real winch rope.")]
    public string winchWrappedRopeMaterialSourceName = "real_mesh_yellow_cable_layer_3_wrapped_on_integrated_winch";

    public string fallbackWinchWrappedRopeName2 = "real_mesh_yellow_cable_layer_2_wrapped_on_integrated_winch";
    public string fallbackWinchWrappedRopeName1 = "real_mesh_yellow_cable_layer_1_wrapped_on_integrated_winch";

    public string hookVisualName = "small_dark_open_deployment_hook_for_miniROV";
    public string primaryRuntimeLineName = "MiniROV_ActiveYellowTetherLine";

    [Header("MiniROV Endpoint")]
    public string miniRovName = "MiniROV";
    public string miniRovTetherAnchorName = "ROV_TetherAnchor";

    [Header("Resolved References")]
    public Transform fairleadStart;
    public Transform shortYellowDeploymentCable;
    public Transform winchWrappedRopeMaterialSource;
    public Transform hookVisual;
    public Transform miniRovRoot;
    public Transform miniRovTetherAnchor;
    public LineRenderer primaryLineRenderer;

    [Header("Visual Control")]
    public bool visualEnabled = true;

    [Tooltip("Hide duplicate generated cable objects, leaving only the yellow winch rope visual.")]
    public bool hideDuplicateCableVisuals = true;

    [Tooltip("Draw one curved yellow cable using the existing MiniROV_ActiveYellowTetherLine.")]
    public bool drivePrimaryLineRenderer = true;

    [Tooltip("Copy material from the real wrapped winch rope mesh.")]
    public bool useWinchRopeMaterial = true;

    public int lineSegments = 40;
    public float lineWidthM = 0.014f;

    [Header("Cable Shape")]
    public float slackSagGain = 0.35f;
    public float maxSagM = 0.55f;
    public float minimumSagM = 0.015f;

    [Tooltip("Small side deflection so underwater cable is not a fake perfectly straight line.")]
    public bool enableCurrentDeflection = true;

    public Vector3 currentDirectionWorld = new Vector3(1.0f, 0.0f, 0.20f);
    public float currentDeflectionGain = 0.10f;
    public float maxCurrentDeflectionM = 0.30f;

    public bool enableSmallWaveMotion = true;
    public float waveAmplitudeM = 0.010f;
    public float waveSpatialFrequency = 1.8f;
    public float waveTemporalFrequency = 0.45f;

    [Header("Runtime")]
    public string cableMode = "unknown";
    public Vector3 startWorld;
    public Vector3 endWorld;
    public float straightDistanceM;
    public float deployedLengthM;
    public float slackM;
    public float stretchM;
    public float sagAmplitudeM;
    public float currentDeflectionM;

    private Vector3[] points;
    private Material copiedMaterial;

    private void Awake()
    {
        AutoFindReferences();
        ConfigureAsSingleWinchRopeVisual();
    }

    private void Start()
    {
        AutoFindReferences();
        ConfigureAsSingleWinchRopeVisual();
    }

    private void LateUpdate()
    {
        if (!visualEnabled)
        {
            return;
        }

        AutoFindReferences();

        if (hideDuplicateCableVisuals)
        {
            HideDuplicateCableVisuals();
        }

        ConfigureLowLevelTetherVisualContract();

        startWorld = ResolveStartWorld();
        endWorld = ResolveEndWorld();

        BuildCableCurve();

        if (drivePrimaryLineRenderer)
        {
            DrawPrimaryLine();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (unifiedTether == null)
        {
            unifiedTether = GetComponent<MIMISKUnifiedTetherManager>();
        }

        if (tetherManager == null)
        {
            tetherManager = GetComponent<MIMISKDroneCoreTetherManager>();
        }

        if (fairleadStart == null)
        {
            fairleadStart = FindDeepChild(transform, fairleadName);
        }

        if (shortYellowDeploymentCable == null)
        {
            shortYellowDeploymentCable =
                FindDeepChild(transform, shortYellowDeploymentCableName);
        }

        if (hookVisual == null)
        {
            hookVisual = FindDeepChild(transform, hookVisualName);
        }

        if (winchWrappedRopeMaterialSource == null)
        {
            winchWrappedRopeMaterialSource =
                FindDeepChild(transform, winchWrappedRopeMaterialSourceName);
        }

        if (winchWrappedRopeMaterialSource == null)
        {
            winchWrappedRopeMaterialSource =
                FindDeepChild(transform, fallbackWinchWrappedRopeName2);
        }

        if (winchWrappedRopeMaterialSource == null)
        {
            winchWrappedRopeMaterialSource =
                FindDeepChild(transform, fallbackWinchWrappedRopeName1);
        }

        if (primaryLineRenderer == null)
        {
            Transform line =
                FindDeepChild(transform, primaryRuntimeLineName);

            if (line != null)
            {
                primaryLineRenderer =
                    line.GetComponent<LineRenderer>();

                if (primaryLineRenderer == null)
                {
                    primaryLineRenderer =
                        line.gameObject.AddComponent<LineRenderer>();
                }
            }
        }

        if (primaryLineRenderer == null)
        {
            GameObject go =
                new GameObject(primaryRuntimeLineName);

            go.transform.SetParent(transform, false);

            primaryLineRenderer =
                go.AddComponent<LineRenderer>();
        }

        if (miniRovRoot == null)
        {
            GameObject rov =
                GameObject.Find(miniRovName);

            if (rov != null)
            {
                miniRovRoot =
                    rov.transform;
            }
        }

        if (unifiedTether != null && unifiedTether.miniRovRoot != null)
        {
            miniRovRoot =
                unifiedTether.miniRovRoot;
        }

        if (miniRovRoot != null && miniRovTetherAnchor == null)
        {
            miniRovTetherAnchor =
                FindDeepChild(miniRovRoot, miniRovTetherAnchorName);

            if (miniRovTetherAnchor == null)
            {
                miniRovTetherAnchor =
                    FindDeepChild(miniRovRoot, "MiniROV_TetherPoint");
            }

            if (miniRovTetherAnchor == null)
            {
                miniRovTetherAnchor =
                    FindDeepChild(miniRovRoot, "TetherPoint");
            }
        }
    }

    [ContextMenu("Configure As Single Winch Rope Visual")]
    public void ConfigureAsSingleWinchRopeVisual()
    {
        AutoFindReferences();

        if (primaryLineRenderer != null)
        {
            primaryLineRenderer.useWorldSpace = true;
            primaryLineRenderer.widthMultiplier = lineWidthM;
            primaryLineRenderer.positionCount = Mathf.Max(2, lineSegments);
            primaryLineRenderer.enabled = true;
        }

        CopyMaterialFromWinchRope();

        HideDuplicateCableVisuals();

        ConfigureLowLevelTetherVisualContract();
    }

    private void ConfigureLowLevelTetherVisualContract()
    {
        if (tetherManager == null)
        {
            return;
        }

        if (fairleadStart != null)
        {
            tetherManager.fairleadLineStart =
                fairleadStart;
        }

        if (primaryLineRenderer != null)
        {
            tetherManager.tetherLineRenderer =
                primaryLineRenderer;
        }

        // Important:
        // The moving visual is the real hanging yellow cable endpoint.
        // This preserves the original physical-looking deployment cable.
        if (shortYellowDeploymentCable != null)
        {
            tetherManager.movingTetherEndVisual =
                shortYellowDeploymentCable;
        }
        else if (hookVisual != null)
        {
            tetherManager.movingTetherEndVisual =
                hookVisual;
        }

        // Do not hide the short yellow cable mesh. It is the visual end of the real tether.
        tetherManager.hideStaticShortCableMeshWhenDynamic =
            false;

        tetherManager.staticShortDeploymentCableMesh =
            null;
    }

    private Vector3 ResolveStartWorld()
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

    private Vector3 ResolveEndWorld()
    {
        bool dynamicRov =
            unifiedTether != null &&
            (
                unifiedTether.tetherState == MIMISKUnifiedTetherManager.TetherMissionState.DynamicStabilizing ||
                unifiedTether.tetherState == MIMISKUnifiedTetherManager.TetherMissionState.ReadyForRovControl ||
                unifiedTether.tetherState == MIMISKUnifiedTetherManager.TetherMissionState.RovControlActive ||
                unifiedTether.tetherState == MIMISKUnifiedTetherManager.TetherMissionState.WaitingForRovRecoveryReady
            );

        if (dynamicRov)
        {
            cableMode = "winch_rope_to_minirov_tether_anchor";

            if (miniRovTetherAnchor != null)
            {
                return miniRovTetherAnchor.position;
            }

            if (miniRovRoot != null)
            {
                return miniRovRoot.position;
            }
        }

        cableMode = "winch_rope_to_hanging_yellow_cable_end";

        if (shortYellowDeploymentCable != null)
        {
            return shortYellowDeploymentCable.position;
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

    private void BuildCableCurve()
    {
        int n =
            Mathf.Max(2, lineSegments);

        if (points == null || points.Length != n)
        {
            points =
                new Vector3[n];
        }

        deployedLengthM =
            tetherManager != null
                ? tetherManager.deployedLengthM
                : Vector3.Distance(startWorld, endWorld);

        straightDistanceM =
            Mathf.Max(
                0.001f,
                Vector3.Distance(startWorld, endWorld)
            );

        slackM =
            tetherManager != null
                ? Mathf.Max(tetherManager.slackM, deployedLengthM - straightDistanceM)
                : Mathf.Max(0.0f, deployedLengthM - straightDistanceM);

        stretchM =
            tetherManager != null
                ? tetherManager.stretchM
                : Mathf.Max(0.0f, straightDistanceM - deployedLengthM);

        sagAmplitudeM =
            Mathf.Clamp(
                minimumSagM + slackM * slackSagGain,
                0.0f,
                maxSagM
            );

        Vector3 chord =
            endWorld - startWorld;

        Vector3 horizontal =
            new Vector3(chord.x, 0.0f, chord.z);

        Vector3 side =
            horizontal.sqrMagnitude > 0.0001f
                ? Vector3.Cross(Vector3.up, horizontal.normalized).normalized
                : Vector3.right;

        Vector3 currentDir =
            currentDirectionWorld;

        currentDir.y = 0.0f;

        if (currentDir.sqrMagnitude > 0.0001f)
        {
            currentDir.Normalize();
        }
        else
        {
            currentDir = side;
        }

        currentDeflectionM =
            enableCurrentDeflection
                ? Mathf.Clamp(slackM * currentDeflectionGain, 0.0f, maxCurrentDeflectionM)
                : 0.0f;

        float t =
            Application.isPlaying
                ? Time.time
                : 0.0f;

        for (int i = 0; i < n; i++)
        {
            float u =
                (float)i / (float)(n - 1);

            Vector3 p =
                Vector3.Lerp(startWorld, endWorld, u);

            float shape =
                Mathf.Sin(Mathf.PI * u);

            // Catenary-like sag: strongest in the middle.
            p +=
                Vector3.down *
                sagAmplitudeM *
                shape;

            // Current bends the rope sideways when it has slack.
            p +=
                currentDir *
                currentDeflectionM *
                shape;

            // Subtle underwater motion, not a second cable.
            if (enableSmallWaveMotion)
            {
                float wave =
                    Mathf.Sin(
                        2.0f * Mathf.PI *
                        (u * waveSpatialFrequency + t * waveTemporalFrequency)
                    );

                p +=
                    side *
                    wave *
                    waveAmplitudeM *
                    shape *
                    Mathf.Clamp01(0.2f + slackM * 3.0f);
            }

            points[i] =
                p;
        }
    }

    private void DrawPrimaryLine()
    {
        if (primaryLineRenderer == null || points == null)
        {
            return;
        }

        primaryLineRenderer.enabled = true;
        primaryLineRenderer.useWorldSpace = true;
        primaryLineRenderer.widthMultiplier = lineWidthM;
        primaryLineRenderer.positionCount = points.Length;

        for (int i = 0; i < points.Length; i++)
        {
            primaryLineRenderer.SetPosition(i, points[i]);
        }

        if (copiedMaterial != null)
        {
            primaryLineRenderer.sharedMaterial =
                copiedMaterial;
        }
    }

    private void CopyMaterialFromWinchRope()
    {
        if (!useWinchRopeMaterial ||
            primaryLineRenderer == null ||
            copiedMaterial != null)
        {
            return;
        }

        if (winchWrappedRopeMaterialSource == null)
        {
            return;
        }

        Renderer r =
            winchWrappedRopeMaterialSource.GetComponent<Renderer>();

        if (r == null)
        {
            r =
                winchWrappedRopeMaterialSource.GetComponentInChildren<Renderer>();
        }

        if (r != null && r.sharedMaterial != null)
        {
            copiedMaterial =
                r.sharedMaterial;

            primaryLineRenderer.sharedMaterial =
                copiedMaterial;
        }
    }

    private void HideDuplicateCableVisuals()
    {
        if (!hideDuplicateCableVisuals)
        {
            return;
        }

        HideVisualIfFound("MIMISK_RealisticTetherCableMesh");
        HideVisualIfFound("MIMISK_FinalActiveYellowTether");
        HideVisualIfFound("MIMISK_Final_ActiveYellowTether");
        HideVisualIfFound("MIMISK_Final_ContinuousYellowTether");
        HideVisualIfFound("MIMISK_Final_ActiveYellowTetherLine");
        HideVisualIfFound("MIMISK_Final_ContinuousYellowTetherLine");
    }

    private void HideVisualIfFound(string objectName)
    {
        Transform t =
            FindDeepChild(transform, objectName);

        if (t == null)
        {
            return;
        }

        if (primaryLineRenderer != null &&
            t == primaryLineRenderer.transform)
        {
            return;
        }

        Renderer[] renderers =
            t.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = false;
        }

        LineRenderer[] lines =
            t.GetComponentsInChildren<LineRenderer>(true);

        for (int i = 0; i < lines.Length; i++)
        {
            lines[i].enabled = false;
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
