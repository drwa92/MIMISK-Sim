using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// V6 visible cable authority for the MIMISK physical tether.
///
/// V6 keeps the runtime extendable mesh, but changes the free tether appearance
/// from a braided/rope-looking object into a smooth yellow CAD-jacket cable that
/// visually continues the winch/reel cable. The dynamics come from the physical
/// node chain; this class only skins those nodes and suppresses obsolete visuals.
/// </summary>
[DefaultExecutionOrder(12000)]
[DisallowMultipleComponent]
public class MIMISKPhysicalTetherVisualizer : MonoBehaviour
{
    public enum RuntimeCableRenderMode
    {
        SmoothTube,
        JacketedWinchCable,
        WinchMatchedBraidedCable
    }

    [Header("References")]
    public bool autoFindReferences = true;
    public MIMISKPhysicalTetherModel physicalTether;

    [Header("Runtime Cable Object")]
    public bool visualizerEnabled = true;
    public RuntimeCableRenderMode renderMode = RuntimeCableRenderMode.JacketedWinchCable;
    public string tubeObjectName = "MIMISK_PhysicalTether_CADYellowRuntimeCable";
    public MeshFilter tubeMeshFilter;
    public MeshRenderer tubeMeshRenderer;

    [Header("Geometry")]
    [Range(4, 24)]
    public int radialSegments = 14;

    [Tooltip("V6 smooth yellow cable radius. This should look like the CAD winch cable, not a thick rope/chain.")]
    public float visualRadiusM = 0.0032f;

    [Tooltip("Lower limit for visibility. Keep below visualRadiusM unless the cable becomes invisible from the camera.")]
    public float minimumVisibleRadiusM = 0.0020f;

    [Header("Jacketed CAD Cable Surface")]
    [Tooltip("When enabled, V6 adds only very subtle jacket design ridges. This keeps the tether looking like the CAD winch cable, not like a rope wrapped around a cable.")]
    public bool enableSubtleJacketDesign = true;

    [Range(0, 8)]
    public int jacketDesignRidgeCount = 3;

    [Tooltip("Very small radial ridge depth for the jacket design. Use 0 for a perfectly smooth cable.")]
    [Range(0.0f, 0.0006f)]
    public float jacketDesignRidgeDepthM = 0.00008f;

    [Tooltip("Longitudinal seam pitch. 0 means straight longitudinal design lines; values above 0 create a very slow spiral. Keep 0 for winch-CAD matching.")]
    public float jacketDesignPitchM = 0.0f;

    [Tooltip("Try to estimate the free cable radius from the CAD reel-to-fairlead cable bounds. If unreliable, V6 falls back to visualRadiusM.")]
    public bool estimateRadiusFromCadCable = true;

    public float estimatedRadiusScale = 0.85f;
    public float minimumEstimatedRadiusM = 0.0018f;
    public float maximumEstimatedRadiusM = 0.0050f;

    [Header("Braided / Stranded Surface - Optional Debug Style")]
    [Tooltip("OFF by default in V6. The previous braid made the underwater cable look like a rope inside a cable.")]
    public bool enableBraidedSurface = false;

    [Range(0, 8)]
    public int braidStrandCount = 0;

    [Range(3, 8)]
    public int braidStrandRadialSegments = 4;

    [Tooltip("Radius of each small helical outer strand/ridge.")]
    public float braidStrandRadiusM = 0.00055f;

    [Tooltip("Distance of strand centers from cable centerline. If <= 0, V6 derives it from visualRadiusM.")]
    public float braidStrandCenterRadiusM = 0.0f;

    [Tooltip("Helical pitch in meters per full revolution. Smaller values create a tighter wire/rope surface.")]
    public float braidPitchM = 0.120f;

    [Tooltip("Advances the apparent strand twist as the winch pays out/recover, so the cable does not look frozen.")]
    public bool animateBraidWithPayout = false;

    [Tooltip("Additional rotation of the braid surface in radians per deployed cable meter.")]
    public float braidPhaseRadiansPerMeter = 2.0f * Mathf.PI;

    [Header("Visual Smoothing")]
    [Tooltip("Renders a smoothed Catmull-Rom path from the physical nodes. Metrics still use the physical nodes.")]
    public bool useSmoothedRenderPath = true;

    [Range(1, 8)]
    public int renderSubdivisionsPerSegment = 4;

    [Range(0, 4)]
    public int renderPathSmoothingPasses = 1;

    [Range(0.0f, 0.40f)]
    public float renderPathSmoothingStrength = 0.12f;

    [Header("Winch CAD Material Matching")]
    public bool useWinchCableMaterial = true;
    public bool cloneSourceMaterial = true;
    public bool searchMaterialSourceEveryFewSeconds = true;
    public float materialSearchPeriodS = 2.0f;

    public string winchWrappedRopeMaterialSourceName = "real_mesh_yellow_cable_layer_3_wrapped_on_integrated_winch";
    public string fallbackWinchWrappedRopeName2 = "real_mesh_yellow_cable_layer_2_wrapped_on_integrated_winch";
    public string fallbackWinchWrappedRopeName1 = "real_mesh_yellow_cable_layer_1_wrapped_on_integrated_winch";
    public string winchFairleadCableMaterialSourceName = "real_mesh_yellow_cable_from_integrated_reel_to_fairlead";
    public string shortDeploymentCableMaterialSourceName = "real_mesh_short_yellow_deployment_cable_to_hook";

    [Tooltip("Optional material source found in the imported drone CAD model.")]
    public Transform winchCableMaterialSource;

    [Header("Material Fallback")]
    [Tooltip("Fallback color only used if no CAD cable material is found.")]
    public Color cableColor = new Color(1.0f, 0.74f, 0.08f, 1.0f);
    public Color slackColor = new Color(1.0f, 0.82f, 0.13f, 1.0f);
    public Color tautColor = new Color(0.98f, 0.62f, 0.04f, 1.0f);
    public Color overTensionColor = new Color(1.0f, 0.18f, 0.03f, 1.0f);
    public bool colorByPhysicalState = false;
    public bool enableEmission = false;
    public float emissionIntensity = 0.0f;
    public bool forceMatteMaterialProperties = true;
    public float fallbackSmoothness = 0.28f;

    [Header("Optional Line Fallback")]
    public bool showLineFallback = false;
    public string fallbackLineObjectName = "MIMISK_PhysicalTether_DebugLine";
    public LineRenderer fallbackLine;
    public float fallbackLineWidthM = 0.0048f;

    [Header("Legacy Visual Suppression")]
    public bool suppressLegacyTetherVisuals = true;
    public bool suppressEveryFrame = true;
    public bool disableLegacyLineRenderers = true;
    public bool disableLegacyMeshRenderers = true;
    public bool disableLegacyVisualComponents = true;
    public bool disableStalePhysicalTetherRenderers = true;

    [Tooltip("Keeps the CAD wrapped reel cable, reel-to-fairlead cable, winch, spool, fairlead, and hook geometry visible.")]
    public bool keepWinchSpoolMeshesVisible = true;

    [Tooltip("Hide only the old short hanging CAD cable mesh, because the runtime cable replaces it. The Transform remains active as the physical endpoint.")]
    public bool hideLegacyShortDeploymentCableMeshes = true;

    [Tooltip("Keep the dark hook visible; V4 sometimes hid it with old yellow cable meshes.")]
    public bool keepHookVisualVisible = true;

    [Tooltip("When true, renderer suppression checks the full hierarchy path, not only local GameObject name.")]
    public bool detectLegacyRenderersByParentPath = true;

    [Header("Runtime")]
    public int cablePointCount;
    public int renderedPointCount;
    public float renderedCableLengthM;
    public float copiedCadMaterialTimeS;
    public string materialSourcePath = "none";
    public float activeVisualRadiusM;
    public int legacyLineRenderersDisabled;
    public int legacyMeshRenderersDisabled;
    public int legacyComponentsDisabled;
    public int stalePhysicalRenderersDisabled;
    public string lastEvent = "idle";

    private Mesh tubeMesh;
    private Vector3[] sourcePoints;
    private Vector3[] renderPoints;
    private Vector3[] renderSmoothScratch;
    private Vector3[] renderTangents;
    private Vector3[] renderNormals;
    private Vector3[] renderBinormals;
    private float[] renderLengths;
    private Material cableMaterial;
    private float nextMaterialSearchTimeS;

    private readonly List<Vector3> meshVertices = new List<Vector3>(4096);
    private readonly List<Vector3> meshNormals = new List<Vector3>(4096);
    private readonly List<Vector2> meshUvs = new List<Vector2>(4096);
    private readonly List<int> meshTriangles = new List<int>(8192);
    private readonly List<Vector3> strandPath = new List<Vector3>(512);

    private const float Epsilon = 0.000001f;

    private void Awake()
    {
        if (autoFindReferences)
        {
            AutoFindReferences();
        }

        EnsureTubeObjects();
    }

    private void Start()
    {
        if (autoFindReferences)
        {
            AutoFindReferences();
        }

        EnsureTubeObjects();
        CopyMaterialFromWinchCableNow();
        EstimateRadiusFromCadCableNow();

        if (suppressLegacyTetherVisuals)
        {
            SuppressLegacyVisualsNow();
        }
    }

    private void LateUpdate()
    {
        if (!visualizerEnabled)
        {
            if (tubeMeshRenderer != null)
            {
                tubeMeshRenderer.enabled = false;
            }
            return;
        }

        if (autoFindReferences)
        {
            AutoFindReferencesIfMissing();
        }

        EnsureTubeObjects();

        if (searchMaterialSourceEveryFewSeconds && Time.time >= nextMaterialSearchTimeS)
        {
            nextMaterialSearchTimeS = Time.time + Mathf.Max(0.25f, materialSearchPeriodS);
            CopyMaterialFromWinchCableNow();
            EstimateRadiusFromCadCableNow();
        }

        if (suppressLegacyTetherVisuals && suppressEveryFrame)
        {
            SuppressLegacyVisualsNow();
        }

        UpdateSourcePoints();
        UpdateRenderPath();
        BuildRuntimeCableMesh();
        UpdateFallbackLine();
        UpdateMaterialState();
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (physicalTether == null)
        {
            physicalTether = GetComponent<MIMISKPhysicalTetherModel>();
        }

        if (physicalTether == null)
        {
            physicalTether = Object.FindFirstObjectByType<MIMISKPhysicalTetherModel>();
        }

        if (winchCableMaterialSource == null)
        {
            Transform root = transform.root != null ? transform.root : transform;
            winchCableMaterialSource = FindBestWinchCableMaterialSource(root);
        }
    }

    private void AutoFindReferencesIfMissing()
    {
        if (physicalTether == null || (useWinchCableMaterial && winchCableMaterialSource == null))
        {
            AutoFindReferences();
        }
    }

    [ContextMenu("Copy Material From Winch Cable Now")]
    public void CopyMaterialFromWinchCableNow()
    {
        if (!useWinchCableMaterial)
        {
            EnsureCableMaterial();
            return;
        }

        if (winchCableMaterialSource == null)
        {
            Transform root = transform.root != null ? transform.root : transform;
            winchCableMaterialSource = FindBestWinchCableMaterialSource(root);
        }

        Renderer sourceRenderer = null;
        if (winchCableMaterialSource != null)
        {
            sourceRenderer = winchCableMaterialSource.GetComponent<Renderer>();
            if (sourceRenderer == null)
            {
                sourceRenderer = winchCableMaterialSource.GetComponentInChildren<Renderer>(true);
            }
        }

        if (sourceRenderer != null && sourceRenderer.sharedMaterial != null)
        {
            cableMaterial = cloneSourceMaterial
                ? new Material(sourceRenderer.sharedMaterial)
                : sourceRenderer.sharedMaterial;

            cableMaterial.name = "MIMISK_Runtime_JacketedCAD_Tether_Cable_Material";
            ApplyMatteProperties(cableMaterial);
            materialSourcePath = GetHierarchyPath(sourceRenderer.transform);
            copiedCadMaterialTimeS = Application.isPlaying ? Time.time : 0.0f;

            if (tubeMeshRenderer != null)
            {
                tubeMeshRenderer.sharedMaterial = cableMaterial;
            }

            lastEvent = "copied_cad_winch_cable_material";
            return;
        }

        EnsureCableMaterial();
        materialSourcePath = "fallback_runtime_yellow_material";
    }

    [ContextMenu("Estimate Radius From CAD Cable Now")]
    public void EstimateRadiusFromCadCableNow()
    {
        activeVisualRadiusM = Mathf.Max(minimumVisibleRadiusM, visualRadiusM);

        if (!estimateRadiusFromCadCable || winchCableMaterialSource == null)
        {
            return;
        }

        Renderer sourceRenderer = winchCableMaterialSource.GetComponent<Renderer>();
        if (sourceRenderer == null)
        {
            sourceRenderer = winchCableMaterialSource.GetComponentInChildren<Renderer>(true);
        }

        if (sourceRenderer == null)
        {
            return;
        }

        Bounds b = sourceRenderer.bounds;
        Vector3 e = b.extents;
        float minExtent = Mathf.Min(Mathf.Max(0.0f, e.x), Mathf.Max(0.0f, e.y), Mathf.Max(0.0f, e.z));
        float estimated = Mathf.Clamp(minExtent * Mathf.Max(0.05f, estimatedRadiusScale), minimumEstimatedRadiusM, maximumEstimatedRadiusM);

        if (estimated > 0.0001f && !float.IsNaN(estimated) && !float.IsInfinity(estimated))
        {
            visualRadiusM = estimated;
            activeVisualRadiusM = Mathf.Max(minimumVisibleRadiusM, visualRadiusM);
        }
    }

    [ContextMenu("Suppress Legacy Tether Visuals Now")]
    public void SuppressLegacyVisualsNow()
    {
        legacyLineRenderersDisabled = 0;
        legacyMeshRenderersDisabled = 0;
        legacyComponentsDisabled = 0;
        stalePhysicalRenderersDisabled = 0;

        if (disableStalePhysicalTetherRenderers)
        {
            DisableStalePhysicalTetherRenderersNow();
        }

        if (disableLegacyVisualComponents)
        {
            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour == this || behaviour == physicalTether)
                {
                    continue;
                }

                string typeName = behaviour.GetType().Name;

                if (typeName == "MIMISKFinalTetherEndpointRig")
                {
                    TrySetBoolFieldOrProperty(behaviour, "driveActiveYellowLine", false);
                    continue;
                }

                if (IsLegacyVisualComponent(typeName))
                {
                    if (behaviour.enabled)
                    {
                        behaviour.enabled = false;
                        legacyComponentsDisabled++;
                    }
                }
            }
        }

        if (disableLegacyLineRenderers)
        {
            LineRenderer[] lines = Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lines.Length; i++)
            {
                LineRenderer line = lines[i];
                if (line == null || IsOurObject(line.gameObject))
                {
                    continue;
                }

                if (!IsLegacyVisualObjectName(line.gameObject))
                {
                    continue;
                }

                if (line.enabled)
                {
                    line.enabled = false;
                    legacyLineRenderersDisabled++;
                }
            }
        }

        if (disableLegacyMeshRenderers)
        {
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r == tubeMeshRenderer || IsOurObject(r.gameObject))
                {
                    continue;
                }

                if (!IsLegacyVisualObjectName(r.gameObject))
                {
                    continue;
                }

                if (ShouldKeepRendererVisible(r.gameObject))
                {
                    if (!r.enabled)
                    {
                        r.enabled = true;
                    }
                    continue;
                }

                if (r.enabled)
                {
                    r.enabled = false;
                    legacyMeshRenderersDisabled++;
                }
            }
        }

        if (tubeMeshRenderer != null)
        {
            tubeMeshRenderer.enabled = true;
        }

        lastEvent = "v5_legacy_visuals_suppressed_runtime_cable_active";
    }

    [ContextMenu("Disable Stale Physical Tether Renderers Now")]
    public void DisableStalePhysicalTetherRenderersNow()
    {
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || IsOurObject(r.gameObject))
            {
                continue;
            }

            if (!IsStalePhysicalTetherVisualObject(r.gameObject))
            {
                continue;
            }

            if (r.enabled)
            {
                r.enabled = false;
                stalePhysicalRenderersDisabled++;
            }
        }

        LineRenderer[] lines = Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lines.Length; i++)
        {
            LineRenderer line = lines[i];
            if (line == null || IsOurObject(line.gameObject))
            {
                continue;
            }

            if (!IsStalePhysicalTetherVisualObject(line.gameObject))
            {
                continue;
            }

            if (line.enabled)
            {
                line.enabled = false;
                stalePhysicalRenderersDisabled++;
            }
        }
    }

    private bool IsLegacyVisualComponent(string typeName)
    {
        return
            typeName == "MIMISKSingleYellowTetherVisualAuthority" ||
            typeName == "MIMISKTetherCableModel" ||
            typeName == "MIMISKFinalTetherLineSynchronizer" ||
            typeName == "MIMISKMiniROVTetherVisualBridge" ||
            typeName == "MIMISKFinalContinuousTetherVisual" ||
            typeName == "MIMISKTetherVisual" ||
            typeName == "MIMISKFinalTetherVisualBridge" ||
            typeName == "MIMISKWinchRopeSingleVisual";
    }

    private void EnsureTubeObjects()
    {
        if (tubeMeshFilter == null)
        {
            GameObject globalExisting = GameObject.Find(tubeObjectName);
            Transform existing = globalExisting != null ? globalExisting.transform : FindDeepChild(transform.root, tubeObjectName);
            if (existing == null)
            {
                GameObject go = new GameObject(tubeObjectName);
                go.transform.SetParent(null, false);
                existing = go.transform;
            }

            EnsureWorldIdentity(existing);
            tubeMeshFilter = existing.GetComponent<MeshFilter>();
            if (tubeMeshFilter == null)
            {
                tubeMeshFilter = existing.gameObject.AddComponent<MeshFilter>();
            }

            tubeMeshRenderer = existing.GetComponent<MeshRenderer>();
            if (tubeMeshRenderer == null)
            {
                tubeMeshRenderer = existing.gameObject.AddComponent<MeshRenderer>();
            }
        }

        if (tubeMeshRenderer == null && tubeMeshFilter != null)
        {
            tubeMeshRenderer = tubeMeshFilter.GetComponent<MeshRenderer>();
        }

        if (tubeMesh == null)
        {
            tubeMesh = new Mesh();
            tubeMesh.name = "MIMISK_Physical_Tether_CADYellow_RuntimeMesh";
            tubeMesh.MarkDynamic();
        }

        if (tubeMeshFilter != null && tubeMeshFilter.sharedMesh != tubeMesh)
        {
            tubeMeshFilter.sharedMesh = tubeMesh;
        }

        EnsureCableMaterial();

        if (tubeMeshRenderer != null)
        {
            tubeMeshRenderer.enabled = true;
            tubeMeshRenderer.sharedMaterial = cableMaterial;
            tubeMeshRenderer.shadowCastingMode = ShadowCastingMode.On;
            tubeMeshRenderer.receiveShadows = true;
        }
    }

    private void UpdateSourcePoints()
    {
        cablePointCount = physicalTether != null ? physicalTether.CablePointCount : 0;
        if (cablePointCount < 2)
        {
            if (physicalTether != null)
            {
                physicalTether.RebuildCableFromCurrentEndpoints();
                cablePointCount = physicalTether.CablePointCount;
            }
        }

        if (cablePointCount < 2)
        {
            sourcePoints = null;
            renderPoints = null;
            renderedPointCount = 0;
            return;
        }

        if (sourcePoints == null || sourcePoints.Length != cablePointCount)
        {
            sourcePoints = new Vector3[cablePointCount];
        }

        physicalTether.CopyCablePointsWorld(sourcePoints);
    }

    private void UpdateRenderPath()
    {
        if (sourcePoints == null || sourcePoints.Length < 2)
        {
            renderPoints = null;
            renderedPointCount = 0;
            renderedCableLengthM = 0.0f;
            return;
        }

        int sourceCount = sourcePoints.Length;
        int subdivisions = Mathf.Clamp(renderSubdivisionsPerSegment, 1, 8);

        if (!useSmoothedRenderPath || subdivisions <= 1 || sourceCount < 4)
        {
            renderPoints = sourcePoints;
            renderedPointCount = sourcePoints.Length;
            PrepareRenderFramesAndLengths(renderPoints);
            return;
        }

        int renderedCount = ((sourceCount - 1) * subdivisions) + 1;
        if (renderPoints == null || renderPoints.Length != renderedCount)
        {
            renderPoints = new Vector3[renderedCount];
        }

        int k = 0;
        for (int i = 0; i < sourceCount - 1; i++)
        {
            Vector3 p0 = sourcePoints[Mathf.Max(0, i - 1)];
            Vector3 p1 = sourcePoints[i];
            Vector3 p2 = sourcePoints[i + 1];
            Vector3 p3 = sourcePoints[Mathf.Min(sourceCount - 1, i + 2)];

            for (int s = 0; s < subdivisions; s++)
            {
                float t = (float)s / (float)subdivisions;
                renderPoints[k++] = CatmullRom(p0, p1, p2, p3, t);
            }
        }

        renderPoints[k] = sourcePoints[sourceCount - 1];
        renderedPointCount = renderPoints.Length;
        SmoothRenderPathIfRequested();
        PrepareRenderFramesAndLengths(renderPoints);
    }

    private void SmoothRenderPathIfRequested()
    {
        if (renderPoints == null || renderPoints.Length < 4)
        {
            return;
        }

        int passes = Mathf.Clamp(renderPathSmoothingPasses, 0, 4);
        float strength = Mathf.Clamp01(renderPathSmoothingStrength);
        if (passes == 0 || strength <= 0.0001f)
        {
            return;
        }

        int n = renderPoints.Length;
        if (renderSmoothScratch == null || renderSmoothScratch.Length != n)
        {
            renderSmoothScratch = new Vector3[n];
        }

        for (int pass = 0; pass < passes; pass++)
        {
            renderSmoothScratch[0] = renderPoints[0];
            renderSmoothScratch[n - 1] = renderPoints[n - 1];

            for (int i = 1; i < n - 1; i++)
            {
                Vector3 midpoint = 0.5f * (renderPoints[i - 1] + renderPoints[i + 1]);
                renderSmoothScratch[i] = Vector3.Lerp(renderPoints[i], midpoint, strength);
            }

            for (int i = 1; i < n - 1; i++)
            {
                renderPoints[i] = renderSmoothScratch[i];
            }
        }
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2.0f * p1) +
                       (-p0 + p2) * t +
                       (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2 +
                       (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t3);
    }

    private void PrepareRenderFramesAndLengths(Vector3[] path)
    {
        renderedCableLengthM = 0.0f;
        if (path == null || path.Length < 2)
        {
            return;
        }

        int n = path.Length;
        EnsureFrameArrays(n);

        Vector3 previousNormal = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            Vector3 tangent = ComputeTangent(path, i);
            renderTangents[i] = tangent;

            Vector3 normal;
            if (i == 0 || previousNormal.sqrMagnitude < Epsilon)
            {
                normal = Vector3.ProjectOnPlane(Vector3.up, tangent);
                if (normal.sqrMagnitude < Epsilon)
                {
                    normal = Vector3.ProjectOnPlane(Vector3.right, tangent);
                }
                if (normal.sqrMagnitude < Epsilon)
                {
                    normal = Vector3.ProjectOnPlane(Vector3.forward, tangent);
                }
                normal.Normalize();
            }
            else
            {
                normal = Vector3.ProjectOnPlane(previousNormal, tangent);
                if (normal.sqrMagnitude < Epsilon)
                {
                    normal = Vector3.Cross(Vector3.right, tangent);
                }
                if (normal.sqrMagnitude < Epsilon)
                {
                    normal = Vector3.Cross(Vector3.forward, tangent);
                }
                normal.Normalize();
            }

            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
            normal = Vector3.Cross(binormal, tangent).normalized;

            renderNormals[i] = normal;
            renderBinormals[i] = binormal;
            previousNormal = normal;

            if (i == 0)
            {
                renderLengths[i] = 0.0f;
            }
            else
            {
                renderedCableLengthM += Vector3.Distance(path[i - 1], path[i]);
                renderLengths[i] = renderedCableLengthM;
            }
        }
    }

    private void EnsureFrameArrays(int n)
    {
        if (renderTangents == null || renderTangents.Length != n)
        {
            renderTangents = new Vector3[n];
            renderNormals = new Vector3[n];
            renderBinormals = new Vector3[n];
            renderLengths = new float[n];
        }
    }

    private static Vector3 ComputeTangent(Vector3[] path, int i)
    {
        Vector3 tangent;
        if (i == 0)
        {
            tangent = path[1] - path[0];
        }
        else if (i == path.Length - 1)
        {
            tangent = path[i] - path[i - 1];
        }
        else
        {
            tangent = path[i + 1] - path[i - 1];
        }

        if (tangent.sqrMagnitude < Epsilon)
        {
            tangent = Vector3.forward;
        }

        return tangent.normalized;
    }

    private void BuildRuntimeCableMesh()
    {
        Vector3[] path = renderPoints != null && renderPoints.Length >= 2 ? renderPoints : sourcePoints;
        if (tubeMesh == null || path == null || path.Length < 2)
        {
            return;
        }

        int pointCount = path.Length;
        int coreRadial = Mathf.Clamp(radialSegments, 4, 24);
        float radius = Mathf.Max(minimumVisibleRadiusM, visualRadiusM);
        activeVisualRadiusM = radius;

        meshVertices.Clear();
        meshNormals.Clear();
        meshUvs.Clear();
        meshTriangles.Clear();

        Transform meshTransform = tubeMeshFilter != null ? tubeMeshFilter.transform : transform;
        EnsureWorldIdentity(meshTransform);

        // Core cable body. V6 defaults to a smooth jacketed CAD cable.
        bool applyJacketDesign = renderMode == RuntimeCableRenderMode.JacketedWinchCable && enableSubtleJacketDesign;
        AppendTube(path, radius, coreRadial, meshTransform, 0.0f, applyJacketDesign ? 1.0f : 0.0f);

        // Optional debug/experimental style only. OFF by default because it looked like
        // a rope wrapped around the tether rather than the CAD yellow winch cable.
        if (renderMode == RuntimeCableRenderMode.WinchMatchedBraidedCable && enableBraidedSurface && braidStrandCount > 0)
        {
            PrepareRenderFramesAndLengths(path);

            int strands = Mathf.Clamp(braidStrandCount, 1, 8);
            int strandRadial = Mathf.Clamp(braidStrandRadialSegments, 3, 8);
            float strandRadius = Mathf.Max(0.0001f, braidStrandRadiusM);
            float centerRadius = braidStrandCenterRadiusM > 0.0f
                ? braidStrandCenterRadiusM
                : radius + strandRadius * 0.45f;
            float pitch = Mathf.Max(0.01f, braidPitchM);
            float payoutPhase = 0.0f;

            if (animateBraidWithPayout && physicalTether != null)
            {
                payoutPhase = physicalTether.deployedLengthM * braidPhaseRadiansPerMeter;
            }

            for (int s = 0; s < strands; s++)
            {
                strandPath.Clear();
                float phase = payoutPhase + 2.0f * Mathf.PI * (float)s / (float)strands;

                for (int i = 0; i < pointCount; i++)
                {
                    float angle = phase + (renderLengths[i] / pitch) * 2.0f * Mathf.PI;
                    Vector3 radialDir = Mathf.Cos(angle) * renderNormals[i] + Mathf.Sin(angle) * renderBinormals[i];
                    strandPath.Add(path[i] + radialDir * centerRadius);
                }

                AppendTubeFromList(strandPath, strandRadius, strandRadial, meshTransform, phase, 0.0f);
            }
        }

        tubeMesh.Clear();
        tubeMesh.SetVertices(meshVertices);
        tubeMesh.SetNormals(meshNormals);
        tubeMesh.SetUVs(0, meshUvs);
        tubeMesh.SetTriangles(meshTriangles, 0, true);
        tubeMesh.RecalculateBounds();
    }

    private void AppendTube(Vector3[] path, float radius, int radial, Transform meshTransform, float uvPhase, float normalScale)
    {
        int n = path.Length;
        int baseVertex = meshVertices.Count;
        Vector3 previousNormal = Vector3.zero;
        float cumulative = 0.0f;

        for (int i = 0; i < n; i++)
        {
            Vector3 tangent = ComputeTangent(path, i);
            Vector3 normal = GetTransportedNormal(tangent, previousNormal);
            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
            normal = Vector3.Cross(binormal, tangent).normalized;
            previousNormal = normal;

            if (i > 0)
            {
                cumulative += Vector3.Distance(path[i - 1], path[i]);
            }

            for (int j = 0; j < radial; j++)
            {
                float angle = 2.0f * Mathf.PI * ((float)j / (float)radial) + uvPhase;
                Vector3 radialDir = Mathf.Cos(angle) * normal + Mathf.Sin(angle) * binormal;
                float localRadius = normalScale > 0.5f ? CalculateJacketedRadius(radius, angle, cumulative) : radius;
                meshVertices.Add(meshTransform.InverseTransformPoint(path[i] + radialDir * localRadius));
                meshNormals.Add(meshTransform.InverseTransformDirection(radialDir).normalized);
                meshUvs.Add(new Vector2(cumulative / Mathf.Max(0.001f, radius * 8.0f), (float)j / (float)radial));
            }
        }

        AppendTubeTriangles(baseVertex, n, radial);
    }

    private float CalculateJacketedRadius(float baseRadius, float angle, float cumulativeLength)
    {
        if (!enableSubtleJacketDesign || jacketDesignRidgeCount <= 0 || jacketDesignRidgeDepthM <= 0.000001f)
        {
            return baseRadius;
        }

        float phase = angle * Mathf.Max(1, jacketDesignRidgeCount);
        if (jacketDesignPitchM > 0.001f)
        {
            phase += (cumulativeLength / jacketDesignPitchM) * 2.0f * Mathf.PI;
        }

        float ridge = Mathf.Max(0.0f, Mathf.Cos(phase));
        ridge = ridge * ridge;
        return baseRadius + ridge * jacketDesignRidgeDepthM;
    }

    private void AppendTubeFromList(List<Vector3> path, float radius, int radial, Transform meshTransform, float uvPhase, float normalScale)
    {
        int n = path.Count;
        if (n < 2)
        {
            return;
        }

        int baseVertex = meshVertices.Count;
        Vector3 previousNormal = Vector3.zero;
        float cumulative = 0.0f;

        for (int i = 0; i < n; i++)
        {
            Vector3 tangent;
            if (i == 0)
            {
                tangent = path[1] - path[0];
            }
            else if (i == n - 1)
            {
                tangent = path[i] - path[i - 1];
            }
            else
            {
                tangent = path[i + 1] - path[i - 1];
            }
            if (tangent.sqrMagnitude < Epsilon) tangent = Vector3.forward;
            tangent.Normalize();

            Vector3 normal = GetTransportedNormal(tangent, previousNormal);
            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
            normal = Vector3.Cross(binormal, tangent).normalized;
            previousNormal = normal;

            if (i > 0)
            {
                cumulative += Vector3.Distance(path[i - 1], path[i]);
            }

            for (int j = 0; j < radial; j++)
            {
                float angle = 2.0f * Mathf.PI * ((float)j / (float)radial) + uvPhase;
                Vector3 radialDir = Mathf.Cos(angle) * normal + Mathf.Sin(angle) * binormal;
                meshVertices.Add(meshTransform.InverseTransformPoint(path[i] + radialDir * radius));
                meshNormals.Add(meshTransform.InverseTransformDirection(radialDir).normalized);
                meshUvs.Add(new Vector2(cumulative / Mathf.Max(0.001f, radius * 8.0f), (float)j / (float)radial));
            }
        }

        AppendTubeTriangles(baseVertex, n, radial);
    }

    private static Vector3 GetTransportedNormal(Vector3 tangent, Vector3 previousNormal)
    {
        Vector3 normal;
        if (previousNormal.sqrMagnitude > Epsilon)
        {
            normal = Vector3.ProjectOnPlane(previousNormal, tangent);
            if (normal.sqrMagnitude > Epsilon)
            {
                return normal.normalized;
            }
        }

        normal = Vector3.ProjectOnPlane(Vector3.up, tangent);
        if (normal.sqrMagnitude < Epsilon)
        {
            normal = Vector3.ProjectOnPlane(Vector3.right, tangent);
        }
        if (normal.sqrMagnitude < Epsilon)
        {
            normal = Vector3.ProjectOnPlane(Vector3.forward, tangent);
        }

        return normal.normalized;
    }

    private void AppendTubeTriangles(int baseVertex, int pointCount, int radial)
    {
        for (int i = 0; i < pointCount - 1; i++)
        {
            for (int j = 0; j < radial; j++)
            {
                int jNext = (j + 1) % radial;

                int a = baseVertex + i * radial + j;
                int b = baseVertex + i * radial + jNext;
                int c = baseVertex + (i + 1) * radial + j;
                int d = baseVertex + (i + 1) * radial + jNext;

                meshTriangles.Add(a);
                meshTriangles.Add(c);
                meshTriangles.Add(b);

                meshTriangles.Add(b);
                meshTriangles.Add(c);
                meshTriangles.Add(d);
            }
        }
    }

    private void UpdateFallbackLine()
    {
        if (!showLineFallback)
        {
            if (fallbackLine != null)
            {
                fallbackLine.enabled = false;
            }
            return;
        }

        Vector3[] path = renderPoints != null && renderPoints.Length >= 2 ? renderPoints : sourcePoints;
        if (path == null || path.Length < 2)
        {
            return;
        }

        if (fallbackLine == null)
        {
            GameObject globalExisting = GameObject.Find(fallbackLineObjectName);
            Transform existing = globalExisting != null ? globalExisting.transform : FindDeepChild(transform.root, fallbackLineObjectName);
            if (existing == null)
            {
                GameObject go = new GameObject(fallbackLineObjectName);
                go.transform.SetParent(null, false);
                existing = go.transform;
            }

            fallbackLine = existing.GetComponent<LineRenderer>();
            if (fallbackLine == null)
            {
                fallbackLine = existing.gameObject.AddComponent<LineRenderer>();
            }
        }

        fallbackLine.enabled = true;
        fallbackLine.useWorldSpace = true;
        fallbackLine.positionCount = path.Length;
        fallbackLine.widthMultiplier = fallbackLineWidthM;
        fallbackLine.sharedMaterial = cableMaterial;
        fallbackLine.startColor = cableColor;
        fallbackLine.endColor = cableColor;

        for (int i = 0; i < path.Length; i++)
        {
            fallbackLine.SetPosition(i, path[i]);
        }
    }

    private void UpdateMaterialState()
    {
        if (cableMaterial == null)
        {
            return;
        }

        Color c = cableColor;
        if (colorByPhysicalState && physicalTether != null)
        {
            if (physicalTether.physicalState == MIMISKPhysicalTetherModel.PhysicalTetherState.Slack)
            {
                c = slackColor;
            }
            else if (physicalTether.physicalState == MIMISKPhysicalTetherModel.PhysicalTetherState.Taut)
            {
                c = tautColor;
            }
            else if (physicalTether.physicalState == MIMISKPhysicalTetherModel.PhysicalTetherState.OverTension)
            {
                c = overTensionColor;
            }
        }

        // Only overwrite source material color if colorByPhysicalState is enabled or this is our fallback material.
        if (colorByPhysicalState || materialSourcePath == "fallback_runtime_yellow_material" || materialSourcePath == "none")
        {
            SetMaterialColor(cableMaterial, c);
        }
    }

    private void EnsureCableMaterial()
    {
        if (cableMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("HDRP/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        cableMaterial = new Material(shader);
        cableMaterial.name = "MIMISK_Runtime_JacketedCAD_Tether_Cable_Fallback";
        SetMaterialColor(cableMaterial, cableColor);
        materialSourcePath = "fallback_runtime_yellow_material";
    }

    private void ApplyMatteProperties(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (forceMatteMaterialProperties)
        {
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", fallbackSmoothness);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.0f);
            }
        }

        if (!enableEmission)
        {
            if (material.HasProperty("_EmissiveColor"))
            {
                material.SetColor("_EmissiveColor", Color.black);
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }
        }
    }

    private void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_UnlitColor"))
        {
            material.SetColor("_UnlitColor", color);
        }

        if (forceMatteMaterialProperties)
        {
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", fallbackSmoothness);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.0f);
            }
        }

        if (enableEmission)
        {
            Color emissive = color * Mathf.Max(0.0f, emissionIntensity);
            if (material.HasProperty("_EmissiveColor"))
            {
                material.SetColor("_EmissiveColor", emissive);
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emissive);
            }
        }
        else
        {
            if (material.HasProperty("_EmissiveColor"))
            {
                material.SetColor("_EmissiveColor", Color.black);
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }
        }
    }

    private bool IsOurObject(GameObject go)
    {
        if (go == null)
        {
            return false;
        }

        if (tubeMeshFilter != null && go == tubeMeshFilter.gameObject)
        {
            return true;
        }

        if (fallbackLine != null && go == fallbackLine.gameObject)
        {
            return true;
        }

        string n = go.name;
        return n == tubeObjectName || n == fallbackLineObjectName;
    }

    private bool IsStalePhysicalTetherVisualObject(GameObject go)
    {
        if (go == null || IsOurObject(go))
        {
            return false;
        }

        string n = go.name.ToLowerInvariant();
        if (!n.Contains("mimisk_physicaltether"))
        {
            return false;
        }

        if (go == gameObject)
        {
            return false;
        }

        return n.Contains("tube") || n.Contains("line") || n.Contains("visual") || n.Contains("cable") || n.Contains("mesh");
    }

    private static void EnsureWorldIdentity(Transform t)
    {
        if (t == null)
        {
            return;
        }

        if (t.parent != null)
        {
            t.SetParent(null, true);
        }

        t.position = Vector3.zero;
        t.rotation = Quaternion.identity;
        t.localScale = Vector3.one;
    }

    private bool IsLegacyVisualObjectName(GameObject go)
    {
        if (go == null)
        {
            return false;
        }

        string n = detectLegacyRenderersByParentPath
            ? GetHierarchyPath(go.transform).ToLowerInvariant()
            : go.name.ToLowerInvariant();

        if (n.Contains("mimisk_physicaltether"))
        {
            return false;
        }

        if (ShouldKeepNameVisible(n))
        {
            return false;
        }

        if (hideLegacyShortDeploymentCableMeshes &&
            (n.Contains("real_mesh_short_yellow_deployment_cable_to_hook") ||
             n.Contains("short_yellow_deployment_cable") ||
             n.Contains("yellow_deployment_cable")))
        {
            return true;
        }

        return
            n.Contains("activeyellowtether") ||
            n.Contains("continuousyellowtether") ||
            n.Contains("yellowtetherline") ||
            n.Contains("yellowtether") ||
            n.Contains("final_tether") ||
            n.Contains("tetherline") ||
            n.Contains("tether_line") ||
            n.Contains("tether visual") ||
            n.Contains("tethervisual") ||
            n.Contains("tethercable") ||
            n.Contains("tether_cable") ||
            n.Contains("winchrope") ||
            n.Contains("ropevisual") ||
            n.Contains("cablemodel") ||
            n.Contains("rope_line") ||
            n.Contains("cable_line");
    }

    private bool ShouldKeepRendererVisible(GameObject go)
    {
        if (go == null)
        {
            return false;
        }

        return ShouldKeepNameVisible(GetHierarchyPath(go.transform).ToLowerInvariant());
    }

    private bool ShouldKeepNameVisible(string n)
    {
        if (keepHookVisualVisible && n.Contains("small_dark_open_deployment_hook_for_minirov"))
        {
            return true;
        }

        if (!keepWinchSpoolMeshesVisible)
        {
            return false;
        }

        return
            n.Contains("real_mesh_yellow_cable_layer_1_wrapped_on_integrated_winch") ||
            n.Contains("real_mesh_yellow_cable_layer_2_wrapped_on_integrated_winch") ||
            n.Contains("real_mesh_yellow_cable_layer_3_wrapped_on_integrated_winch") ||
            n.Contains("real_mesh_yellow_cable_from_integrated_reel_to_fairlead") ||
            n.Contains("winchfairlead_for_unity_linerenderer_start") ||
            n.Contains("winch_reel_spin_pivot_unity_rotate_local_x") ||
            n.Contains("fairlead") ||
            n.Contains("spool") ||
            n.Contains("reel");
    }

    private Transform FindBestWinchCableMaterialSource(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Transform t = FindDeepChildContains(root, winchFairleadCableMaterialSourceName);
        if (t != null) return t;

        t = FindDeepChildContains(root, shortDeploymentCableMaterialSourceName);
        if (t != null) return t;

        t = FindDeepChildContains(root, winchWrappedRopeMaterialSourceName);
        if (t != null) return t;

        t = FindDeepChildContains(root, fallbackWinchWrappedRopeName2);
        if (t != null) return t;

        t = FindDeepChildContains(root, fallbackWinchWrappedRopeName1);
        if (t != null) return t;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            string path = GetHierarchyPath(r.transform).ToLowerInvariant();
            if ((path.Contains("yellow") || path.Contains("cable") || path.Contains("rope")) &&
                !path.Contains("physicaltether"))
            {
                return r.transform;
            }
        }

        return null;
    }

    private static void TrySetBoolFieldOrProperty(MonoBehaviour behaviour, string memberName, bool value)
    {
        if (behaviour == null)
        {
            return;
        }

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo field = behaviour.GetType().GetField(memberName, flags);
        if (field != null && field.FieldType == typeof(bool))
        {
            field.SetValue(behaviour, value);
            return;
        }

        PropertyInfo property = behaviour.GetType().GetProperty(memberName, flags);
        if (property != null && property.PropertyType == typeof(bool) && property.CanWrite)
        {
            property.SetValue(behaviour, value, null);
        }
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null)
        {
            return string.Empty;
        }

        string path = t.name;
        Transform p = t.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }

        return path;
    }

    private static Transform FindDeepChild(Transform root, string childName)
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
            Transform found = FindDeepChild(root.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindDeepChildContains(Transform root, string namePart)
    {
        if (root == null || string.IsNullOrEmpty(namePart))
        {
            return null;
        }

        string target = namePart.ToLowerInvariant();
        if (root.name.ToLowerInvariant().Contains(target))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChildContains(root.GetChild(i), namePart);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
