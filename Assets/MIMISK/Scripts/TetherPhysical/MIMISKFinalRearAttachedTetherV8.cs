using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// V8 safe final candidate for the MIMISK deployed tether.
///
/// Design goals:
/// - Do not change the original deployment, mission, home, return, winch, agent, ROS, or gRPC logic.
/// - Use the original cable endpoint while the MiniROV is cable-managed by the drone.
/// - Use a dedicated rear MiniROV anchor only after ROV control is active.
/// - Render exactly one deployed cable as a smooth CAD-yellow jacketed cable.
/// - Move the cable using a dependency-free Verlet/PBD node chain, similar in principle to simple rope solvers.
/// - Keep force coupling OFF in this V8 component. It is a visual/physics-monitor tether first.
///
/// This component is intentionally self-contained so it can supersede the earlier visual experiments without
/// touching their serialized data. The old components can remain in the scene, but their renderers/visual drivers
/// are suppressed at runtime by this component.
/// </summary>
[DefaultExecutionOrder(32000)]
[DisallowMultipleComponent]
public class MIMISKFinalRearAttachedTetherV8 : MonoBehaviour
{
    public enum EndpointMode
    {
        Unknown,
        DeploymentCableEndpoint,
        RearMiniRovAnchor,
        FallbackMiniRovTransform
    }

    [Header("Safe Ownership")]
    [Tooltip("This component never writes to the original tether manager, mission manager, MiniROV home, or agent commands.")]
    public bool readOnlyDoNotChangeOriginalLogic = true;

    [Tooltip("V8 is visual/monitor physics only. It does not apply force to the drone or MiniROV.")]
    public bool applyForces = false;

    public bool autoFindReferences = true;

    [Header("Original MIMISK References - read only")]
    public MIMISKUnifiedTetherManager unifiedTether;
    public MIMISKDroneCoreTetherManager droneCoreTether;
    public Rigidbody droneRigidbody;
    public Rigidbody miniRovRigidbody;

    [Tooltip("Start of the deployed tether, usually WinchFairlead_for_Unity_LineRenderer_Start.")]
    public Transform startAnchor;

    [Tooltip("Original moving cable endpoint/hook used before ROV control is active. This preserves the old deployment behavior.")]
    public Transform deploymentCableEndpoint;

    [Tooltip("Dedicated rear anchor on the MiniROV body used after ROV control is active.")]
    public Transform rearMiniRovAnchor;

    public Transform miniRovRoot;

    [Header("Auto-find Names")]
    public string miniRovObjectName = "MiniROV";
    public string rearAnchorName = "MIMISK_Tether_BackAnchor";
    public string deploymentCableEndpointName = "real_mesh_short_yellow_deployment_cable_to_hook";
    public string deploymentHookEndpointName = "small_dark_open_deployment_hook_for_miniROV";
    public string cableEndFollowRootName = "MiniROV_CableEndFollowRoot";
    public string fairleadName = "WinchFairlead_for_Unity_LineRenderer_Start";

    [Header("Rear Anchor Placement")]
    [Tooltip("Use the MiniROV root BoxCollider to place the rear tether anchor. This does not modify original manager references.")]
    public bool createOrUpdateRearAnchorFromBoxCollider = true;

    [Tooltip("Manual override in MiniROV local coordinates. Use this if the BoxCollider does not match the CAD body.")]
    public bool useManualRearAnchorLocalPosition = false;

    public Vector3 manualRearAnchorLocalPosition = new Vector3(0.0f, 0.0f, -0.040f);

    [Tooltip("Which local axis points forward on the MiniROV. In this model the front camera is on local +Z, so the rear is -Z.")]
    public bool miniRovFrontIsLocalPositiveZ = true;

    [Range(-0.5f, 0.5f)]
    [Tooltip("Vertical placement on the rear BoxCollider face. 0=center, 0.5=top, -0.5=bottom.")]
    public float rearAnchorYFractionFromCenter = 0.0f;

    [Tooltip("Extra offset behind the rear BoxCollider face so the cable starts just outside the ROV body.")]
    public float rearAnchorBehindFaceOffsetM = 0.010f;

    public Vector3 computedRearAnchorLocalPosition;

    [Header("Endpoint Phase Selection")]
    [Tooltip("Before ROV control, the visible cable follows the old moving deployment endpoint/hook.")]
    public bool useDeploymentEndpointUntilRovControlActive = true;

    [Tooltip("After ROV control is active, the visible cable endpoint is locked to the rear MiniROV anchor.")]
    public bool useRearAnchorAfterRovControlActive = true;

    [Tooltip("When no unified manager is available, use Rigidbody.isKinematic to infer whether the MiniROV is released.")]
    public bool fallbackUseRigidBodyDynamicState = true;

    [Header("Cable Solver")]
    public bool simulateCable = true;
    public int minimumNodeCount = 8;
    public int maximumNodeCount = 96;
    public float targetSegmentLengthM = 0.12f;
    public int constraintIterations = 72;
    public int physicsSubsteps = 3;
    public float verletVelocityDamping = 0.965f;
    public float nodeMaxSpeedMS = 3.0f;
    public float internalDrag = 0.60f;
    public float cableMassPerMeterKg = 0.045f;
    public float cableDiameterM = 0.008f;
    public bool useGravity = true;
    public float gravityMS2 = 9.81f;
    public bool useBuoyancy = true;
    public float waterSurfaceY = 0.0f;
    public bool readWaterSurfaceFromUnifiedTether = true;
    public float waterDensityKgM3 = 997.0f;
    public float airDensityKgM3 = 1.225f;
    public float buoyancyScale = 1.0f;
    public float waterDragCoefficient = 1.2f;
    public float airDragCoefficient = 1.05f;
    public Vector3 waterCurrentWorldMS = new Vector3(0.012f, 0.0f, 0.004f);
    public Vector3 airWindWorldMS = Vector3.zero;


    [Header("Cable Contact / Anti-Cut Projection")]
    [Tooltip("Projects interior tether nodes above the seabed/terrain/contact surfaces. This prevents the visual cable from cutting through the ground while preserving original mission logic.")]
    public bool enableContactProjection = true;

    [Tooltip("Use Unity Physics raycasts to find seabed/solid surfaces below each cable node.")]
    public bool usePhysicsRaycastContact = true;

    [Tooltip("Use Terrain.activeTerrains height sampling as a stable seabed fallback.")]
    public bool useTerrainHeightContact = true;

    [Tooltip("Use a simple plane when no terrain/raycast hit is found. This is useful for synthetic underwater scenes.")]
    public bool useFallbackSeabedPlane = true;

    [Tooltip("Fallback seabed Y coordinate in world meters. Set this near the terrain floor if raycasts are not available.")]
    public float fallbackSeabedY = -2.05f;

    [Tooltip("Keep the cable this far above the contact surface to avoid z-fighting and visual clipping.")]
    public float contactSkinM = 0.035f;

    [Tooltip("Additional clearance considered contact for logging/metrics.")]
    public float contactActiveClearanceM = 0.020f;

    [Tooltip("Only apply seabed/contact projection to nodes below the water surface.")]
    public bool contactOnlyBelowWater = true;

    [Tooltip("Margin above water surface for the below-water check.")]
    public float contactWaterSurfaceMarginM = 0.05f;

    [Tooltip("Raycast start height above each node.")]
    public float contactRaycastAboveM = 6.0f;

    [Tooltip("Raycast search distance below each node.")]
    public float contactRaycastBelowM = 12.0f;

    [Tooltip("Layers considered by the contact raycast. Default is Everything; ignored names below still filter water/vehicles/tether parts.")]
    public LayerMask contactLayerMask = -1;

    public QueryTriggerInteraction contactTriggerInteraction = QueryTriggerInteraction.Ignore;

    [Tooltip("Do not collide the tether against the drone, MiniROV, winch/cable visual meshes, water surface, cameras, or vegetation by name.")]
    public bool useContactNameIgnoreFilter = true;

    [Tooltip("Comma-separated lower/upper-case-insensitive keywords ignored by contact raycasts.")]
    public string contactIgnoredNameKeywords = "water,ocean,surface,global volume,tether,cable,rope,winch,hook,drone,minirov,camera,light,kelp,plant,grass,leaf,algae,coral";

    [Range(0.0f, 1.0f)]
    [Tooltip("Damps downward Verlet velocity when a node is projected out of contact.")]
    public float contactVelocityDamping = 0.85f;

    [Header("Cable Contact Diagnostics")]
    public bool contactProjectionActive;
    public int contactNodeCount;
    public float contactLengthM;
    public float maxContactPenetrationM;
    public float lowestNodeClearanceM;
    public string contactSurfaceSource = "none";

    [Header("Performance Safe Mode")]
    [Tooltip("Keeps the final runtime tether lightweight so it cannot slow the MiniROV planner/control loop. It does not change any original MIMISK mission logic.")]
    public bool performanceSafeMode = true;

    [Tooltip("When false, contact is projected only once per selected physics tick instead of during every constraint iteration. This is the recommended real-time setting.")]
    public bool projectContactDuringConstraintIterations = false;

    [Range(1, 30)]
    [Tooltip("Apply contact projection once every N FixedUpdates. 1 = every physics tick, 3-5 = much faster and usually enough for visual anti-clipping.")]
    public int contactFixedUpdateStride = 4;

    [Tooltip("Suppress old tether renderers once at startup/manual install instead of scanning the whole scene every rendered frame.")]
    public bool suppressLegacyVisualsOnceOnly = true;

    [Tooltip("If once-only suppression is disabled, this is the minimum seconds between scene-wide legacy visual scans.")]
    public float legacyVisualSuppressionIntervalS = 2.0f;

    [Header("Visual Length Policy")]
    [Tooltip("Read the deployed length from the original winch/tether manager, but do not write anything back.")]
    public bool readCommandedLengthFromOriginalManagers = true;

    public float manualCableLengthM = 1.25f;

    [Tooltip("In read-only mode the visual cable cannot be shorter than the endpoint distance plus this slack. This prevents impossible short cable artifacts without altering the real manager length.")]
    public float minimumVisualSlackM = 0.08f;

    public float maximumVisualSlackM = 0.50f;

    [Tooltip("When the ROV moves away from the drone, the V8 visual cable extends to keep the endpoint attached. The original winch length is not modified.")]
    public bool allowVisualLengthExtensionForEndpointSync = true;

    [Header("Catenary / Rope Shape")]
    [Tooltip("Weak shape guide that distributes slack along the whole cable instead of making a local pigtail near the ROV.")]
    public bool useWeakCatenaryGuide = true;

    [Range(0.0f, 1.0f)]
    public float catenaryGuideStrength = 0.16f;

    public float slackToSagScale = 0.32f;
    public float maximumSagM = 0.30f;
    public float maximumLateralCurrentBendM = 0.06f;
    public float currentBendPerSlackM = 0.04f;

    [Header("Runtime CAD-yellow Cable Visual")]
    public bool renderCable = true;
    public string runtimeCableObjectName = "MIMISK_V8_FinalRearAttachedRuntimeCable";
    public MeshFilter cableMeshFilter;
    public MeshRenderer cableMeshRenderer;
    public int radialSegments = 14;
    public float visualRadiusM = 0.0040f;
    public float minimumVisualRadiusM = 0.0028f;

    [Tooltip("Use subtle jacket ridges only. This is not braid/rope; it is a smooth yellow cable similar to the CAD winch cable.")]
    public bool enableSubtleJacketRidges = false;
    public int jacketRidgeCount = 3;
    public float jacketRidgeDepthM = 0.00004f;

    public bool smoothRenderPath = true;
    public int renderSubdivisionsPerSegment = 3;
    public int renderSmoothingPasses = 1;
    public float renderSmoothingStrength = 0.08f;

    [Header("Material")]
    public bool copyWinchCableMaterial = true;
    public bool cloneCopiedMaterial = true;
    public Transform materialSource;
    public string fairleadCableMaterialSourceName = "real_mesh_yellow_cable_from_integrated_reel_to_fairlead";
    public string wrappedCableMaterialSourceName = "real_mesh_yellow_cable_layer_3_wrapped_on_integrated_winch";
    public Color fallbackCableColor = new Color(1.0f, 0.72f, 0.08f, 1.0f);
    public bool forceMatteMaterial = true;
    public bool disableEmission = true;
    public float fallbackSmoothness = 0.25f;
    public string materialSourcePath = "none";

    [Header("Legacy Visual Suppression")]
    public bool suppressLegacyVisuals = true;
    public bool suppressEveryFrame = true;
    public bool disableLegacyVisualComponents = true;
    public bool disableLegacyRenderers = true;
    public bool keepCadWinchCableVisible = true;
    public bool keepHookVisible = true;

    [Header("Diagnostics")]
    public EndpointMode endpointMode = EndpointMode.Unknown;
    public string endpointModeText = "unknown";
    public Vector3 startWorld;
    public Vector3 endWorld;
    public float commandedLengthM;
    public float visualCableLengthM;
    public float effectiveVisualLengthExtensionM;
    public float straightDistanceM;
    public float slackM;
    public float sagDepthM;
    public float geometricCableLengthM;
    public float rearAnchorErrorM;
    public int runtimeNodeCount;
    public int renderedPointCount;
    public bool solverHealthy = true;
    public string lastEvent = "idle";
    public int legacyVisualComponentsDisabled;
    public int legacyRenderersDisabled;
    public int stalePhysicalRenderersDisabled;

    [Header("Gizmos")]
    public bool drawDebugGizmos = true;
    public float anchorGizmoRadius = 0.025f;

    private Vector3[] nodes;
    private Vector3[] previousNodes;
    private Vector3[] nodeVelocities;
    private Vector3[] renderPoints;
    private Vector3[] renderScratch;
    private Vector3[] renderTangents;
    private Vector3[] renderNormals;
    private Vector3[] renderBinormals;
    private float[] renderLengths;
    private Mesh cableMesh;
    private Material cableMaterial;

    private readonly List<Vector3> meshVertices = new List<Vector3>(4096);
    private readonly List<Vector3> meshNormals = new List<Vector3>(4096);
    private readonly List<Vector2> meshUvs = new List<Vector2>(4096);
    private readonly List<int> meshTriangles = new List<int>(8192);
    private readonly RaycastHit[] contactRaycastHits = new RaycastHit[32];
    private string[] contactIgnoreKeywordsCache;
    private string contactIgnoreKeywordsLast = null;
    private int contactFixedUpdateCounter;
    private bool projectContactThisFixedUpdate = true;
    private bool legacySuppressionDone;
    private float lastLegacySuppressionTime = -9999.0f;


    private Vector3 lastStartWorld;
    private Vector3 lastEndWorld;
    private float lastVisualCableLengthM;
    private bool initialized;

    private const float Epsilon = 0.000001f;

    private void Awake()
    {
        if (autoFindReferences)
        {
            AutoFindReferences();
        }

        EnsureRearAnchor();
        EnsureCableMeshObjects();
        EnsureMaterial();
    }

    private void Start()
    {
        if (autoFindReferences)
        {
            AutoFindReferences();
        }

        EnsureRearAnchor();
        EnsureCableMeshObjects();
        CopyMaterialFromWinchCableNow();
        SuppressLegacyVisualsNow();
        RebuildCableNow();
    }

    private void FixedUpdate()
    {
        if (!simulateCable)
        {
            return;
        }

        if (autoFindReferences)
        {
            AutoFindReferencesIfMissing();
        }

        EnsureRearAnchor();
        ResolveEndpointsAndLength();
        EnsureNodeArraysForLength();

        if (!initialized || ShouldRebuild())
        {
            RebuildCableNow();
        }

        contactFixedUpdateCounter++;
        int contactStride = Mathf.Max(1, contactFixedUpdateStride);
        projectContactThisFixedUpdate = !performanceSafeMode || contactStride <= 1 || (contactFixedUpdateCounter % contactStride) == 0;

        SimulateVerletCable(Mathf.Max(0.0005f, Time.fixedDeltaTime));

        lastStartWorld = startWorld;
        lastEndWorld = endWorld;
        lastVisualCableLengthM = visualCableLengthM;
    }

    private void LateUpdate()
    {
        if (autoFindReferences)
        {
            AutoFindReferencesIfMissing();
        }

        EnsureRearAnchor();
        ResolveEndpointsAndLength();

        if (!initialized)
        {
            EnsureNodeArraysForLength();
            RebuildCableNow();
        }

        if (suppressLegacyVisuals && suppressEveryFrame)
        {
            bool shouldSuppress = true;
            if (performanceSafeMode)
            {
                if (suppressLegacyVisualsOnceOnly && legacySuppressionDone)
                {
                    shouldSuppress = false;
                }
                else if (!suppressLegacyVisualsOnceOnly && Time.unscaledTime - lastLegacySuppressionTime < Mathf.Max(0.25f, legacyVisualSuppressionIntervalS))
                {
                    shouldSuppress = false;
                }
            }

            if (shouldSuppress)
            {
                SuppressLegacyVisualsNow();
            }
        }

        if (renderCable)
        {
            EnsureCableMeshObjects();
            UpdateRenderPath();
            BuildCableMesh();
        }
        else if (cableMeshRenderer != null)
        {
            cableMeshRenderer.enabled = false;
        }

        UpdateDiagnostics();
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (unifiedTether == null)
        {
            unifiedTether = GetComponent<MIMISKUnifiedTetherManager>();
        }
        if (unifiedTether == null)
        {
            unifiedTether = Object.FindFirstObjectByType<MIMISKUnifiedTetherManager>();
        }

        if (droneCoreTether == null)
        {
            droneCoreTether = GetComponent<MIMISKDroneCoreTetherManager>();
        }
        if (droneCoreTether == null)
        {
            droneCoreTether = Object.FindFirstObjectByType<MIMISKDroneCoreTetherManager>();
        }

        if (droneRigidbody == null)
        {
            droneRigidbody = GetComponent<Rigidbody>();
        }

        if (miniRovRigidbody == null)
        {
            if (unifiedTether != null && unifiedTether.miniRovRigidbody != null)
            {
                miniRovRigidbody = unifiedTether.miniRovRigidbody;
            }
            else if (droneCoreTether != null && droneCoreTether.miniRovRigidbody != null)
            {
                miniRovRigidbody = droneCoreTether.miniRovRigidbody;
            }
        }

        if (miniRovRoot == null)
        {
            GameObject rov = GameObject.Find(miniRovObjectName);
            if (rov != null)
            {
                miniRovRoot = rov.transform;
            }
            else if (miniRovRigidbody != null)
            {
                miniRovRoot = miniRovRigidbody.transform;
            }
        }

        if (startAnchor == null)
        {
            if (droneCoreTether != null)
            {
                if (droneCoreTether.fairleadLineStart != null) startAnchor = droneCoreTether.fairleadLineStart;
                else if (droneCoreTether.tetherAnchor != null) startAnchor = droneCoreTether.tetherAnchor;
                else if (droneCoreTether.winchPoint != null) startAnchor = droneCoreTether.winchPoint;
            }

            if (startAnchor == null && unifiedTether != null && unifiedTether.fairleadLineStart != null)
            {
                startAnchor = unifiedTether.fairleadLineStart;
            }

            if (startAnchor == null)
            {
                startAnchor = FindDeepChildContains(transform.root, fairleadName);
            }
        }

        if (deploymentCableEndpoint == null)
        {
            deploymentCableEndpoint = FindDeploymentEndpoint();
        }

        if (rearMiniRovAnchor == null && miniRovRoot != null)
        {
            rearMiniRovAnchor = FindDeepChild(miniRovRoot, rearAnchorName);
        }

        if (materialSource == null)
        {
            materialSource = FindBestMaterialSource();
        }
    }

    private void AutoFindReferencesIfMissing()
    {
        if (unifiedTether == null || droneCoreTether == null || startAnchor == null || deploymentCableEndpoint == null || miniRovRoot == null || rearMiniRovAnchor == null || materialSource == null)
        {
            AutoFindReferences();
        }
    }

    [ContextMenu("Create/Update Rear Anchor From MiniROV BoxCollider")]
    public void EnsureRearAnchor()
    {
        if (miniRovRoot == null)
        {
            GameObject rov = GameObject.Find(miniRovObjectName);
            if (rov != null)
            {
                miniRovRoot = rov.transform;
            }
            else if (miniRovRigidbody != null)
            {
                miniRovRoot = miniRovRigidbody.transform;
            }
        }

        if (miniRovRoot == null)
        {
            return;
        }

        if (rearMiniRovAnchor == null)
        {
            Transform existing = FindDeepChild(miniRovRoot, rearAnchorName);
            if (existing == null)
            {
                GameObject go = new GameObject(rearAnchorName);
                go.transform.SetParent(miniRovRoot, false);
                existing = go.transform;
            }
            rearMiniRovAnchor = existing;
        }

        if (!createOrUpdateRearAnchorFromBoxCollider)
        {
            return;
        }

        Vector3 local = useManualRearAnchorLocalPosition ? manualRearAnchorLocalPosition : ComputeRearAnchorLocalFromBoxCollider();
        computedRearAnchorLocalPosition = local;
        rearMiniRovAnchor.localPosition = local;
        rearMiniRovAnchor.localRotation = Quaternion.identity;
        rearMiniRovAnchor.localScale = Vector3.one;
    }

    private Vector3 ComputeRearAnchorLocalFromBoxCollider()
    {
        BoxCollider box = miniRovRoot != null ? miniRovRoot.GetComponent<BoxCollider>() : null;
        if (box == null && miniRovRoot != null)
        {
            box = miniRovRoot.GetComponentInChildren<BoxCollider>();
        }

        if (box == null)
        {
            return manualRearAnchorLocalPosition;
        }

        Vector3 c = box.center;
        Vector3 s = box.size;
        float rearSign = miniRovFrontIsLocalPositiveZ ? -1.0f : 1.0f;
        float rearZ = c.z + rearSign * (s.z * 0.5f + Mathf.Max(0.0f, rearAnchorBehindFaceOffsetM));
        float y = c.y + Mathf.Clamp(rearAnchorYFractionFromCenter, -0.5f, 0.5f) * s.y;
        return new Vector3(c.x, y, rearZ);
    }

    [ContextMenu("Rebuild Cable Now")]
    public void RebuildCableNow()
    {
        ResolveEndpointsAndLength();
        EnsureNodeArraysForLength();

        if (nodes == null || nodes.Length < 2)
        {
            initialized = false;
            return;
        }

        int n = nodes.Length;
        float chordLength = Mathf.Max(0.001f, Vector3.Distance(startWorld, endWorld));
        float slack = Mathf.Max(0.0f, visualCableLengthM - chordLength);
        Vector3 chord = endWorld - startWorld;
        Vector3 horizontal = new Vector3(chord.x, 0.0f, chord.z);
        Vector3 lateral = GetLateralBendDirection(horizontal);
        float sag = Mathf.Clamp(slack * slackToSagScale + 0.005f, 0.0f, maximumSagM);
        float side = Mathf.Clamp(slack * currentBendPerSlackM, 0.0f, maximumLateralCurrentBendM);

        for (int i = 0; i < n; i++)
        {
            float u = (float)i / Mathf.Max(1, n - 1);
            float shape = Mathf.Sin(Mathf.PI * u);
            Vector3 p = Vector3.Lerp(startWorld, endWorld, u);
            p += Vector3.down * sag * shape;
            p += lateral * side * shape;
            nodes[i] = p;
            previousNodes[i] = p;
            nodeVelocities[i] = Vector3.zero;
        }

        nodes[0] = startWorld;
        nodes[n - 1] = endWorld;
        previousNodes[0] = startWorld;
        previousNodes[n - 1] = endWorld;

        initialized = true;
        solverHealthy = true;
        lastStartWorld = startWorld;
        lastEndWorld = endWorld;
        lastVisualCableLengthM = visualCableLengthM;
        lastEvent = "v8_rebuilt";
    }

    [ContextMenu("Suppress Legacy Visuals Now")]
    public void SuppressLegacyVisualsNow()
    {
        legacyVisualComponentsDisabled = 0;
        legacyRenderersDisabled = 0;
        stalePhysicalRenderersDisabled = 0;

        if (disableLegacyVisualComponents)
        {
            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour b = behaviours[i];
                if (b == null || b == this)
                {
                    continue;
                }

                string typeName = b.GetType().Name;
                if (IsLegacyVisualComponent(typeName))
                {
                    if (b.enabled)
                    {
                        TrySetBoolFieldOrProperty(b, "visualAuthorityEnabled", false);
                        TrySetBoolFieldOrProperty(b, "visualEnabled", false);
                        TrySetBoolFieldOrProperty(b, "cableModelEnabled", false);
                        TrySetBoolFieldOrProperty(b, "synchronizerEnabled", false);
                        TrySetBoolFieldOrProperty(b, "driveActiveYellowLine", false);
                        b.enabled = false;
                        legacyVisualComponentsDisabled++;
                    }
                }
            }
        }

        if (!disableLegacyRenderers)
        {
            return;
        }

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || r == cableMeshRenderer)
            {
                continue;
            }

            string path = GetHierarchyPath(r.transform).ToLowerInvariant();
            if (IsAllowedVisibleRendererPath(path))
            {
                continue;
            }

            if (IsLegacyTetherRendererPath(path))
            {
                if (r.enabled)
                {
                    r.enabled = false;
                    if (path.Contains("physicaltether") || path.Contains("runtimecable")) stalePhysicalRenderersDisabled++;
                    else legacyRenderersDisabled++;
                }
            }
        }

        LineRenderer[] lines = Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lines.Length; i++)
        {
            LineRenderer line = lines[i];
            if (line == null)
            {
                continue;
            }

            string path = GetHierarchyPath(line.transform).ToLowerInvariant();
            if (IsAllowedVisibleRendererPath(path))
            {
                continue;
            }

            if (IsLegacyTetherRendererPath(path))
            {
                if (line.enabled)
                {
                    line.enabled = false;
                    legacyRenderersDisabled++;
                }
            }
        }

        if (cableMeshRenderer != null && renderCable)
        {
            cableMeshRenderer.enabled = true;
        }

        legacySuppressionDone = true;
        lastLegacySuppressionTime = Time.unscaledTime;
    }

    private void ResolveEndpointsAndLength()
    {
        if (startAnchor != null)
        {
            startWorld = startAnchor.position;
        }
        else
        {
            startWorld = transform.position;
        }

        bool rovReleased = IsRovControlActiveOrReleased();
        if (useDeploymentEndpointUntilRovControlActive && !rovReleased && deploymentCableEndpoint != null)
        {
            endWorld = deploymentCableEndpoint.position;
            endpointMode = EndpointMode.DeploymentCableEndpoint;
        }
        else if (useRearAnchorAfterRovControlActive && rearMiniRovAnchor != null)
        {
            endWorld = rearMiniRovAnchor.position;
            endpointMode = EndpointMode.RearMiniRovAnchor;
        }
        else if (deploymentCableEndpoint != null)
        {
            endWorld = deploymentCableEndpoint.position;
            endpointMode = EndpointMode.DeploymentCableEndpoint;
        }
        else if (miniRovRoot != null)
        {
            endWorld = miniRovRoot.position;
            endpointMode = EndpointMode.FallbackMiniRovTransform;
        }
        else
        {
            endWorld = transform.position;
            endpointMode = EndpointMode.Unknown;
        }

        endpointModeText = endpointMode.ToString();
        straightDistanceM = Vector3.Distance(startWorld, endWorld);
        commandedLengthM = GetCommandedLengthFromOriginalManagers();

        float desired = commandedLengthM;
        float minFeasible = straightDistanceM + Mathf.Max(0.0f, minimumVisualSlackM);
        if (allowVisualLengthExtensionForEndpointSync)
        {
            desired = Mathf.Max(desired, minFeasible);
        }

        if (maximumVisualSlackM > minimumVisualSlackM)
        {
            desired = Mathf.Min(desired, straightDistanceM + maximumVisualSlackM);
        }

        visualCableLengthM = Mathf.Max(0.05f, desired);
        effectiveVisualLengthExtensionM = Mathf.Max(0.0f, visualCableLengthM - commandedLengthM);
        slackM = Mathf.Max(0.0f, visualCableLengthM - straightDistanceM);

        if (readWaterSurfaceFromUnifiedTether && unifiedTether != null)
        {
            waterSurfaceY = unifiedTether.waterSurfaceY;
        }
    }

    private bool IsRovControlActiveOrReleased()
    {
        if (unifiedTether != null)
        {
            return unifiedTether.rovControlActive || unifiedTether.miniRovDynamic;
        }

        if (fallbackUseRigidBodyDynamicState && miniRovRigidbody != null)
        {
            return !miniRovRigidbody.isKinematic;
        }

        return false;
    }

    private float GetCommandedLengthFromOriginalManagers()
    {
        float length = Mathf.Max(0.05f, manualCableLengthM);

        if (readCommandedLengthFromOriginalManagers && droneCoreTether != null)
        {
            if (droneCoreTether.deployedLengthM > 0.01f)
            {
                length = droneCoreTether.deployedLengthM;
            }
            else if (droneCoreTether.targetLengthM > 0.01f)
            {
                length = droneCoreTether.targetLengthM;
            }

            if (droneCoreTether.maximumLengthM > 0.05f)
            {
                length = Mathf.Min(length, droneCoreTether.maximumLengthM);
            }
            if (droneCoreTether.minimumLengthM > 0.0f)
            {
                length = Mathf.Max(length, droneCoreTether.minimumLengthM);
            }
        }
        else if (readCommandedLengthFromOriginalManagers && unifiedTether != null)
        {
            length = Mathf.Max(length, unifiedTether.targetDeployLengthM);
        }

        return Mathf.Max(0.05f, length);
    }

    private void EnsureNodeArraysForLength()
    {
        int desired = Mathf.CeilToInt(Mathf.Max(visualCableLengthM, 0.05f) / Mathf.Max(0.04f, targetSegmentLengthM)) + 1;
        desired = Mathf.Clamp(desired, Mathf.Max(3, minimumNodeCount), Mathf.Max(minimumNodeCount, maximumNodeCount));
        runtimeNodeCount = desired;

        if (nodes == null || nodes.Length != desired)
        {
            nodes = new Vector3[desired];
            previousNodes = new Vector3[desired];
            nodeVelocities = new Vector3[desired];
            initialized = false;
        }
    }

    private bool ShouldRebuild()
    {
        if (nodes == null || nodes.Length < 2)
        {
            return true;
        }

        if ((startWorld - lastStartWorld).magnitude > 0.75f)
        {
            lastEvent = "v8_rebuild_start_jump";
            return true;
        }

        if ((endWorld - lastEndWorld).magnitude > 0.75f)
        {
            lastEvent = "v8_rebuild_end_jump";
            return true;
        }

        if (Mathf.Abs(visualCableLengthM - lastVisualCableLengthM) > 0.75f)
        {
            lastEvent = "v8_rebuild_length_jump";
            return true;
        }

        return false;
    }

    private void SimulateVerletCable(float dt)
    {
        if (nodes == null || nodes.Length < 2)
        {
            solverHealthy = false;
            return;
        }

        int n = nodes.Length;
        int substeps = Mathf.Clamp(physicsSubsteps, 1, 8);
        int iterations = Mathf.Clamp(constraintIterations, 1, 160);
        float h = Mathf.Max(0.0005f, dt / substeps);
        float targetSegmentLength = visualCableLengthM / Mathf.Max(1, n - 1);
        float radius = Mathf.Max(0.0005f, cableDiameterM * 0.5f);
        float crossSectionArea = Mathf.PI * radius * radius;
        float nodeVolume = crossSectionArea * targetSegmentLength;
        float nodeMass = Mathf.Max(0.0005f, cableMassPerMeterKg * targetSegmentLength);
        float projectedArea = Mathf.Max(0.000001f, cableDiameterM * targetSegmentLength);

        ResetContactDiagnostics();

        for (int step = 0; step < substeps; step++)
        {
            nodes[0] = startWorld;
            nodes[n - 1] = endWorld;
            previousNodes[0] = startWorld;
            previousNodes[n - 1] = endWorld;

            for (int i = 1; i < n - 1; i++)
            {
                Vector3 p = nodes[i];
                Vector3 velocity = (nodes[i] - previousNodes[i]) / h;
                if (velocity.magnitude > nodeMaxSpeedMS)
                {
                    velocity = velocity.normalized * nodeMaxSpeedMS;
                }

                bool underwater = p.y < waterSurfaceY;
                float density = underwater ? waterDensityKgM3 : airDensityKgM3;
                float cd = underwater ? waterDragCoefficient : airDragCoefficient;
                Vector3 fluidVelocity = underwater ? waterCurrentWorldMS : airWindWorldMS;

                Vector3 force = Vector3.zero;
                if (useGravity)
                {
                    force += Vector3.down * nodeMass * gravityMS2;
                }
                if (useBuoyancy && underwater)
                {
                    force += Vector3.up * waterDensityKgM3 * gravityMS2 * nodeVolume * buoyancyScale;
                }

                Vector3 rel = velocity - fluidVelocity;
                float speed = rel.magnitude;
                if (speed > 0.0001f)
                {
                    force += -0.5f * density * cd * projectedArea * speed * rel;
                }
                force += -velocity * internalDrag * nodeMass;

                Vector3 acceleration = force / nodeMass;
                Vector3 next = p + (p - previousNodes[i]) * Mathf.Clamp(verletVelocityDamping, 0.80f, 0.995f) + acceleration * h * h;
                previousNodes[i] = p;
                nodes[i] = next;
            }

            ApplyWeakCatenaryGuide(h);

            for (int k = 0; k < iterations; k++)
            {
                nodes[0] = startWorld;
                nodes[n - 1] = endWorld;

                if ((k & 1) == 0)
                {
                    for (int i = 0; i < n - 1; i++)
                    {
                        ProjectDistanceConstraint(i, targetSegmentLength);
                    }
                }
                else
                {
                    for (int i = n - 2; i >= 0; i--)
                    {
                        ProjectDistanceConstraint(i, targetSegmentLength);
                    }
                }

                if (enableContactProjection && projectContactThisFixedUpdate && projectContactDuringConstraintIterations && (k % 4 == 3 || k == iterations - 1))
                {
                    ProjectCableContactConstraints(targetSegmentLength, h, false);
                }
            }

            nodes[0] = startWorld;
            nodes[n - 1] = endWorld;
            if (enableContactProjection && projectContactThisFixedUpdate)
            {
                ProjectCableContactConstraints(targetSegmentLength, h, true);
            }
            else if (step == substeps - 1)
            {
                MarkContactSkippedDiagnostics();
            }
            nodes[0] = startWorld;
            nodes[n - 1] = endWorld;

            for (int i = 1; i < n - 1; i++)
            {
                nodeVelocities[i] = (nodes[i] - previousNodes[i]) / h;
            }
        }

        solverHealthy = true;
        for (int i = 0; i < n; i++)
        {
            if (IsInvalidVector(nodes[i]))
            {
                solverHealthy = false;
                lastEvent = "v8_solver_fault_invalid_node";
                return;
            }
        }
    }

    private void ResetContactDiagnostics()
    {
        contactProjectionActive = false;
        contactNodeCount = 0;
        contactLengthM = 0.0f;
        maxContactPenetrationM = 0.0f;
        lowestNodeClearanceM = 0.0f;
        contactSurfaceSource = enableContactProjection ? "none" : "disabled";
    }

    private void MarkContactSkippedDiagnostics()
    {
        contactProjectionActive = false;
        contactNodeCount = 0;
        contactLengthM = 0.0f;
        maxContactPenetrationM = 0.0f;
        lowestNodeClearanceM = 0.0f;
        contactSurfaceSource = enableContactProjection ? "throttled_for_performance" : "disabled";
    }

    private void ProjectCableContactConstraints(float targetSegmentLength, float dt, bool recordMetrics)
    {
        if (!enableContactProjection || nodes == null || nodes.Length < 3)
        {
            if (recordMetrics)
            {
                contactProjectionActive = false;
                contactNodeCount = 0;
                contactLengthM = 0.0f;
                maxContactPenetrationM = 0.0f;
                lowestNodeClearanceM = 0.0f;
                contactSurfaceSource = "disabled";
            }
            return;
        }

        int localContactNodes = 0;
        float localMaxPenetration = 0.0f;
        float localLowestClearance = float.PositiveInfinity;
        string localSource = "none";
        bool anySurface = false;

        for (int i = 1; i < nodes.Length - 1; i++)
        {
            Vector3 p = nodes[i];
            if (contactOnlyBelowWater && p.y > waterSurfaceY + contactWaterSurfaceMarginM)
            {
                continue;
            }

            float surfaceY;
            string source;
            if (!TryResolveContactSurfaceY(p, out surfaceY, out source))
            {
                continue;
            }

            anySurface = true;
            float floorY = surfaceY + Mathf.Max(0.0f, contactSkinM);
            float clearance = p.y - floorY;
            localLowestClearance = Mathf.Min(localLowestClearance, clearance);

            if (clearance <= contactActiveClearanceM)
            {
                localContactNodes++;
                if (source != "none") localSource = source;
            }

            if (clearance < 0.0f)
            {
                float penetration = -clearance;
                localMaxPenetration = Mathf.Max(localMaxPenetration, penetration);
                p.y = floorY;
                nodes[i] = p;

                if (contactVelocityDamping > 0.0f)
                {
                    Vector3 oldPrev = previousNodes[i];
                    Vector3 velocity = nodes[i] - oldPrev;
                    if (velocity.y < 0.0f)
                    {
                        velocity.y *= 1.0f - Mathf.Clamp01(contactVelocityDamping);
                        previousNodes[i] = nodes[i] - velocity;
                    }
                    else
                    {
                        previousNodes[i] = new Vector3(oldPrev.x, Mathf.Min(oldPrev.y, nodes[i].y), oldPrev.z);
                    }
                }
            }
        }

        if (recordMetrics)
        {
            contactProjectionActive = anySurface;
            contactNodeCount = localContactNodes;
            contactLengthM = Mathf.Max(0.0f, localContactNodes * Mathf.Max(0.0f, targetSegmentLength));
            maxContactPenetrationM = localMaxPenetration;
            lowestNodeClearanceM = float.IsPositiveInfinity(localLowestClearance) ? 0.0f : localLowestClearance;
            contactSurfaceSource = anySurface ? localSource : "none";
        }
    }

    private bool TryResolveContactSurfaceY(Vector3 p, out float surfaceY, out string source)
    {
        surfaceY = float.NegativeInfinity;
        source = "none";
        bool found = false;

        if (useTerrainHeightContact)
        {
            Terrain[] terrains = Terrain.activeTerrains;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null) continue;
                Vector3 tp = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (p.x < tp.x || p.z < tp.z || p.x > tp.x + size.x || p.z > tp.z + size.z) continue;

                float y = terrain.SampleHeight(p) + tp.y;
                if (!found || y > surfaceY)
                {
                    surfaceY = y;
                    source = "terrain";
                    found = true;
                }
            }
        }

        if (usePhysicsRaycastContact)
        {
            Vector3 origin = new Vector3(p.x, Mathf.Max(p.y + contactRaycastAboveM, waterSurfaceY + contactRaycastAboveM), p.z);
            float distance = Mathf.Max(0.1f, contactRaycastAboveM + contactRaycastBelowM + Mathf.Max(0.0f, origin.y - p.y));
            int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, contactRaycastHits, distance, contactLayerMask, contactTriggerInteraction);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = contactRaycastHits[i];
                if (hit.collider == null) continue;
                if (IsIgnoredContactHit(hit.collider.transform)) continue;
                float y = hit.point.y;
                if (!found || y > surfaceY)
                {
                    surfaceY = y;
                    source = "physics:" + hit.collider.name;
                    found = true;
                }
            }
        }

        if (useFallbackSeabedPlane)
        {
            if (!found || fallbackSeabedY > surfaceY)
            {
                surfaceY = fallbackSeabedY;
                source = "fallback_plane";
                found = true;
            }
        }

        return found;
    }

    private bool IsIgnoredContactHit(Transform hitTransform)
    {
        if (hitTransform == null) return true;
        if (hitTransform == transform || hitTransform.IsChildOf(transform)) return true;
        if (miniRovRoot != null && (hitTransform == miniRovRoot || hitTransform.IsChildOf(miniRovRoot))) return true;
        if (cableMeshRenderer != null && hitTransform == cableMeshRenderer.transform) return true;

        if (!useContactNameIgnoreFilter) return false;

        if (contactIgnoreKeywordsCache == null || contactIgnoreKeywordsLast != contactIgnoredNameKeywords)
        {
            contactIgnoreKeywordsLast = contactIgnoredNameKeywords;
            contactIgnoreKeywordsCache = (contactIgnoredNameKeywords ?? string.Empty).Split(',');
            for (int i = 0; i < contactIgnoreKeywordsCache.Length; i++)
            {
                contactIgnoreKeywordsCache[i] = contactIgnoreKeywordsCache[i].Trim().ToLowerInvariant();
            }
        }

        string path = GetHierarchyPath(hitTransform).ToLowerInvariant();
        for (int i = 0; i < contactIgnoreKeywordsCache.Length; i++)
        {
            string keyword = contactIgnoreKeywordsCache[i];
            if (!string.IsNullOrEmpty(keyword) && path.Contains(keyword))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyWeakCatenaryGuide(float dt)
    {
        if (!useWeakCatenaryGuide || nodes == null || nodes.Length < 4)
        {
            return;
        }

        float chordLength = Mathf.Max(0.001f, Vector3.Distance(startWorld, endWorld));
        float slack = Mathf.Max(0.0f, visualCableLengthM - chordLength);
        if (slack < 0.002f)
        {
            return;
        }

        int n = nodes.Length;
        Vector3 chord = endWorld - startWorld;
        Vector3 horizontal = new Vector3(chord.x, 0.0f, chord.z);
        Vector3 lateral = GetLateralBendDirection(horizontal);
        float sag = Mathf.Clamp(slack * slackToSagScale, 0.0f, maximumSagM);
        float side = Mathf.Clamp(slack * currentBendPerSlackM, 0.0f, maximumLateralCurrentBendM);
        float alpha = 1.0f - Mathf.Exp(-Mathf.Clamp01(catenaryGuideStrength) * Mathf.Max(1.0f, dt * 60.0f));
        alpha = Mathf.Clamp01(alpha);

        for (int i = 1; i < n - 1; i++)
        {
            float u = (float)i / Mathf.Max(1, n - 1);
            float shape = Mathf.Sin(Mathf.PI * u);
            Vector3 target = Vector3.Lerp(startWorld, endWorld, u) + Vector3.down * sag * shape + lateral * side * shape;
            nodes[i] = Vector3.Lerp(nodes[i], target, alpha);
        }
    }

    private void ProjectDistanceConstraint(int i, float targetLength)
    {
        int n = nodes.Length;
        if (i < 0 || i >= n - 1)
        {
            return;
        }

        Vector3 p0 = nodes[i];
        Vector3 p1 = nodes[i + 1];
        Vector3 delta = p1 - p0;
        float len = delta.magnitude;
        if (len < Epsilon)
        {
            return;
        }

        float error = len - targetLength;
        Vector3 correction = delta / len * error;
        bool p0Fixed = i == 0;
        bool p1Fixed = i + 1 == n - 1;

        if (p0Fixed && !p1Fixed)
        {
            nodes[i + 1] -= correction;
        }
        else if (!p0Fixed && p1Fixed)
        {
            nodes[i] += correction;
        }
        else if (!p0Fixed && !p1Fixed)
        {
            nodes[i] += correction * 0.5f;
            nodes[i + 1] -= correction * 0.5f;
        }
    }

    private Vector3 GetLateralBendDirection(Vector3 horizontalChord)
    {
        Vector3 current = new Vector3(waterCurrentWorldMS.x, 0.0f, waterCurrentWorldMS.z);
        if (current.sqrMagnitude > Epsilon)
        {
            return current.normalized;
        }

        if (horizontalChord.sqrMagnitude > Epsilon)
        {
            return Vector3.Cross(Vector3.up, horizontalChord.normalized).normalized;
        }

        return Vector3.right;
    }

    private void UpdateRenderPath()
    {
        if (nodes == null || nodes.Length < 2)
        {
            renderPoints = null;
            renderedPointCount = 0;
            return;
        }

        if (!smoothRenderPath || nodes.Length < 4 || renderSubdivisionsPerSegment <= 1)
        {
            renderPoints = nodes;
            renderedPointCount = nodes.Length;
            PrepareRenderFrames(renderPoints);
            return;
        }

        int subdivisions = Mathf.Clamp(renderSubdivisionsPerSegment, 1, 8);
        int count = (nodes.Length - 1) * subdivisions + 1;
        if (renderPoints == null || renderPoints.Length != count)
        {
            renderPoints = new Vector3[count];
        }

        int k = 0;
        for (int i = 0; i < nodes.Length - 1; i++)
        {
            Vector3 p0 = nodes[Mathf.Max(0, i - 1)];
            Vector3 p1 = nodes[i];
            Vector3 p2 = nodes[i + 1];
            Vector3 p3 = nodes[Mathf.Min(nodes.Length - 1, i + 2)];
            for (int s = 0; s < subdivisions; s++)
            {
                float t = (float)s / subdivisions;
                renderPoints[k++] = CatmullRom(p0, p1, p2, p3, t);
            }
        }
        renderPoints[count - 1] = nodes[nodes.Length - 1];

        SmoothRenderPath();
        renderedPointCount = renderPoints.Length;
        PrepareRenderFrames(renderPoints);
    }

    private void SmoothRenderPath()
    {
        if (renderPoints == null || renderPoints.Length < 4)
        {
            return;
        }

        int passes = Mathf.Clamp(renderSmoothingPasses, 0, 4);
        float strength = Mathf.Clamp01(renderSmoothingStrength);
        if (passes <= 0 || strength <= 0.0001f)
        {
            return;
        }

        if (renderScratch == null || renderScratch.Length != renderPoints.Length)
        {
            renderScratch = new Vector3[renderPoints.Length];
        }

        int n = renderPoints.Length;
        for (int pass = 0; pass < passes; pass++)
        {
            renderScratch[0] = renderPoints[0];
            renderScratch[n - 1] = renderPoints[n - 1];
            for (int i = 1; i < n - 1; i++)
            {
                Vector3 midpoint = 0.5f * (renderPoints[i - 1] + renderPoints[i + 1]);
                renderScratch[i] = Vector3.Lerp(renderPoints[i], midpoint, strength);
            }
            for (int i = 1; i < n - 1; i++)
            {
                renderPoints[i] = renderScratch[i];
            }
        }
    }

    private void EnsureCableMeshObjects()
    {
        if (cableMeshFilter == null)
        {
            GameObject existing = GameObject.Find(runtimeCableObjectName);
            if (existing == null)
            {
                existing = new GameObject(runtimeCableObjectName);
            }
            existing.transform.SetParent(null, true);
            existing.transform.position = Vector3.zero;
            existing.transform.rotation = Quaternion.identity;
            existing.transform.localScale = Vector3.one;

            cableMeshFilter = existing.GetComponent<MeshFilter>();
            if (cableMeshFilter == null)
            {
                cableMeshFilter = existing.AddComponent<MeshFilter>();
            }

            cableMeshRenderer = existing.GetComponent<MeshRenderer>();
            if (cableMeshRenderer == null)
            {
                cableMeshRenderer = existing.AddComponent<MeshRenderer>();
            }
        }

        if (cableMeshRenderer == null && cableMeshFilter != null)
        {
            cableMeshRenderer = cableMeshFilter.GetComponent<MeshRenderer>();
        }

        if (cableMesh == null)
        {
            cableMesh = new Mesh();
            cableMesh.name = "MIMISK_V8_Final_Rear_Attached_Tether_Mesh";
            cableMesh.MarkDynamic();
        }

        if (cableMeshFilter != null && cableMeshFilter.sharedMesh != cableMesh)
        {
            cableMeshFilter.sharedMesh = cableMesh;
        }

        EnsureMaterial();

        if (cableMeshRenderer != null)
        {
            cableMeshRenderer.enabled = renderCable;
            cableMeshRenderer.sharedMaterial = cableMaterial;
            cableMeshRenderer.shadowCastingMode = ShadowCastingMode.On;
            cableMeshRenderer.receiveShadows = true;
        }
    }

    private void BuildCableMesh()
    {
        Vector3[] path = renderPoints != null && renderPoints.Length >= 2 ? renderPoints : nodes;
        if (path == null || path.Length < 2 || cableMesh == null || cableMeshFilter == null)
        {
            return;
        }

        float radius = Mathf.Max(minimumVisualRadiusM, visualRadiusM);
        int radial = Mathf.Clamp(radialSegments, 4, 24);
        Transform meshTransform = cableMeshFilter.transform;
        meshTransform.SetParent(null, true);
        meshTransform.position = Vector3.zero;
        meshTransform.rotation = Quaternion.identity;
        meshTransform.localScale = Vector3.one;

        meshVertices.Clear();
        meshNormals.Clear();
        meshUvs.Clear();
        meshTriangles.Clear();

        int baseVertex = 0;
        for (int i = 0; i < path.Length; i++)
        {
            Vector3 tangent = ComputeTangent(path, i);
            Vector3 normal = GetRenderNormal(path, i, tangent);
            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
            normal = Vector3.Cross(binormal, tangent).normalized;

            float cumulative = (renderLengths != null && i < renderLengths.Length) ? renderLengths[i] : 0.0f;
            for (int j = 0; j < radial; j++)
            {
                float angle = 2.0f * Mathf.PI * ((float)j / radial);
                Vector3 radialDir = Mathf.Cos(angle) * normal + Mathf.Sin(angle) * binormal;
                float localRadius = radius;
                if (enableSubtleJacketRidges && jacketRidgeCount > 0)
                {
                    float ridge = Mathf.Max(0.0f, Mathf.Cos(angle * jacketRidgeCount));
                    localRadius += ridge * ridge * jacketRidgeDepthM;
                }
                meshVertices.Add(meshTransform.InverseTransformPoint(path[i] + radialDir * localRadius));
                meshNormals.Add(meshTransform.InverseTransformDirection(radialDir).normalized);
                meshUvs.Add(new Vector2(cumulative / Mathf.Max(0.001f, radius * 8.0f), (float)j / radial));
            }
        }

        for (int i = 0; i < path.Length - 1; i++)
        {
            for (int j = 0; j < radial; j++)
            {
                int jn = (j + 1) % radial;
                int a = baseVertex + i * radial + j;
                int b = baseVertex + i * radial + jn;
                int c = baseVertex + (i + 1) * radial + j;
                int d = baseVertex + (i + 1) * radial + jn;
                meshTriangles.Add(a);
                meshTriangles.Add(c);
                meshTriangles.Add(b);
                meshTriangles.Add(b);
                meshTriangles.Add(c);
                meshTriangles.Add(d);
            }
        }

        cableMesh.Clear();
        cableMesh.SetVertices(meshVertices);
        cableMesh.SetNormals(meshNormals);
        cableMesh.SetUVs(0, meshUvs);
        cableMesh.SetTriangles(meshTriangles, 0, true);
        cableMesh.RecalculateBounds();
    }

    private void PrepareRenderFrames(Vector3[] path)
    {
        if (path == null || path.Length < 2)
        {
            return;
        }

        int n = path.Length;
        if (renderTangents == null || renderTangents.Length != n)
        {
            renderTangents = new Vector3[n];
            renderNormals = new Vector3[n];
            renderBinormals = new Vector3[n];
            renderLengths = new float[n];
        }

        geometricCableLengthM = 0.0f;
        Vector3 previousNormal = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            Vector3 tangent = ComputeTangent(path, i);
            Vector3 normal;
            if (i == 0 || previousNormal.sqrMagnitude < Epsilon)
            {
                normal = Vector3.ProjectOnPlane(Vector3.up, tangent);
                if (normal.sqrMagnitude < Epsilon) normal = Vector3.ProjectOnPlane(Vector3.right, tangent);
                if (normal.sqrMagnitude < Epsilon) normal = Vector3.ProjectOnPlane(Vector3.forward, tangent);
                normal.Normalize();
            }
            else
            {
                normal = Vector3.ProjectOnPlane(previousNormal, tangent);
                if (normal.sqrMagnitude < Epsilon) normal = Vector3.ProjectOnPlane(Vector3.up, tangent);
                normal.Normalize();
            }

            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
            normal = Vector3.Cross(binormal, tangent).normalized;
            previousNormal = normal;

            renderTangents[i] = tangent;
            renderNormals[i] = normal;
            renderBinormals[i] = binormal;
            if (i == 0)
            {
                renderLengths[i] = 0.0f;
            }
            else
            {
                geometricCableLengthM += Vector3.Distance(path[i - 1], path[i]);
                renderLengths[i] = geometricCableLengthM;
            }
        }
    }

    private Vector3 GetRenderNormal(Vector3[] path, int i, Vector3 tangent)
    {
        if (renderNormals != null && i < renderNormals.Length && renderNormals[i].sqrMagnitude > Epsilon)
        {
            return renderNormals[i];
        }

        Vector3 normal = Vector3.ProjectOnPlane(Vector3.up, tangent);
        if (normal.sqrMagnitude < Epsilon) normal = Vector3.ProjectOnPlane(Vector3.right, tangent);
        if (normal.sqrMagnitude < Epsilon) normal = Vector3.forward;
        return normal.normalized;
    }

    private static Vector3 ComputeTangent(Vector3[] path, int i)
    {
        Vector3 tangent;
        if (i <= 0)
        {
            tangent = path[1] - path[0];
        }
        else if (i >= path.Length - 1)
        {
            tangent = path[path.Length - 1] - path[path.Length - 2];
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

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2.0f * p1) + (-p0 + p2) * t + (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2 + (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t3);
    }

    [ContextMenu("Copy Material From Winch Cable Now")]
    public void CopyMaterialFromWinchCableNow()
    {
        if (!copyWinchCableMaterial)
        {
            EnsureMaterial();
            return;
        }

        if (materialSource == null)
        {
            materialSource = FindBestMaterialSource();
        }

        Renderer sourceRenderer = materialSource != null ? materialSource.GetComponent<Renderer>() : null;
        if (sourceRenderer == null && materialSource != null)
        {
            sourceRenderer = materialSource.GetComponentInChildren<Renderer>(true);
        }

        if (sourceRenderer != null && sourceRenderer.sharedMaterial != null)
        {
            cableMaterial = cloneCopiedMaterial ? new Material(sourceRenderer.sharedMaterial) : sourceRenderer.sharedMaterial;
            cableMaterial.name = "MIMISK_V8_CAD_Yellow_Tether_Material";
            ApplyMaterialProperties(cableMaterial, fallbackCableColor, false);
            materialSourcePath = GetHierarchyPath(sourceRenderer.transform);
            if (cableMeshRenderer != null)
            {
                cableMeshRenderer.sharedMaterial = cableMaterial;
            }
            return;
        }

        EnsureMaterial();
    }

    private void EnsureMaterial()
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
        cableMaterial.name = "MIMISK_V8_Fallback_Yellow_Tether_Material";
        materialSourcePath = "fallback";
        ApplyMaterialProperties(cableMaterial, fallbackCableColor, true);
    }

    private void ApplyMaterialProperties(Material mat, Color color, bool forceColor)
    {
        if (mat == null)
        {
            return;
        }

        if (forceColor)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor", color);
        }

        if (forceMatteMaterial)
        {
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", fallbackSmoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.0f);
        }

        if (disableEmission)
        {
            if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", Color.black);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
        }
    }

    private Transform FindDeploymentEndpoint()
    {
        Transform root = transform.root != null ? transform.root : transform;
        Transform t = FindDeepChildContains(root, deploymentCableEndpointName);
        if (t != null) return t;
        t = FindDeepChildContains(root, deploymentHookEndpointName);
        if (t != null) return t;
        t = FindDeepChildContains(root, cableEndFollowRootName);
        if (t != null) return t;
        return null;
    }

    private Transform FindBestMaterialSource()
    {
        Transform root = transform.root != null ? transform.root : transform;
        Transform t = FindDeepChildContains(root, fairleadCableMaterialSourceName);
        if (t != null) return t;
        t = FindDeepChildContains(root, wrappedCableMaterialSourceName);
        if (t != null) return t;
        t = FindDeepChildContains(root, "real_mesh_yellow_cable_layer_2_wrapped_on_integrated_winch");
        if (t != null) return t;
        t = FindDeepChildContains(root, "real_mesh_yellow_cable_layer_1_wrapped_on_integrated_winch");
        if (t != null) return t;
        t = FindDeepChildContains(root, deploymentCableEndpointName);
        if (t != null) return t;
        return null;
    }

    private bool IsLegacyVisualComponent(string typeName)
    {
        return typeName == "MIMISKSingleYellowTetherVisualAuthority" ||
               typeName == "MIMISKWinchRopeSingleVisual" ||
               typeName == "MIMISKTetherCableModel" ||
               typeName == "MIMISKFinalTetherLineSynchronizer" ||
               typeName == "MIMISKMiniROVTetherVisualBridge" ||
               typeName == "MIMISKFinalContinuousTetherVisual" ||
               typeName == "MIMISKTetherVisual" ||
               typeName == "MIMISKFinalTetherVisualBridge" ||
               typeName == "MIMISKPhysicalTetherVisualizer";
    }

    private bool IsLegacyTetherRendererPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path.Contains("activeyellowtether") ||
               path.Contains("continuousyellowtether") ||
               path.Contains("yellowtetherline") ||
               path.Contains("yellowtether") ||
               path.Contains("final_tether") ||
               path.Contains("tetherline") ||
               path.Contains("tether_line") ||
               path.Contains("tethervisual") ||
               path.Contains("tether_visual") ||
               path.Contains("tethercable") ||
               path.Contains("tether_cable") ||
               path.Contains("realistictethercablemesh") ||
               path.Contains("realistic_tether") ||
               path.Contains("short_yellow_deployment_cable") ||
               path.Contains("yellow_deployment_cable") ||
               path.Contains("real_mesh_short_yellow_deployment_cable_to_hook") ||
               path.Contains("winchrope") ||
               path.Contains("ropevisual") ||
               path.Contains("rope_line") ||
               path.Contains("cable_line") ||
               path.Contains("mimisk_physicaltether") ||
               path.Contains("cadyellowruntimecable") ||
               path.Contains("winchmatchedruntimecable") ||
               path.Contains("tubecable") ||
               path.Contains("runtimecable");
    }

    private bool IsAllowedVisibleRendererPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (path.Contains(runtimeCableObjectName.ToLowerInvariant()))
        {
            return true;
        }

        if (keepHookVisible && path.Contains("small_dark_open_deployment_hook_for_minirov"))
        {
            return true;
        }

        if (!keepCadWinchCableVisible)
        {
            return false;
        }

        return path.Contains("real_mesh_yellow_cable_layer_1_wrapped_on_integrated_winch") ||
               path.Contains("real_mesh_yellow_cable_layer_2_wrapped_on_integrated_winch") ||
               path.Contains("real_mesh_yellow_cable_layer_3_wrapped_on_integrated_winch") ||
               path.Contains("real_mesh_yellow_cable_from_integrated_reel_to_fairlead") ||
               path.Contains("winchfairlead_for_unity_linerenderer_start") ||
               path.Contains("winch_reel_spin_pivot_unity_rotate_local_x") ||
               path.Contains("fairlead") ||
               path.Contains("spool") ||
               path.Contains("reel");
    }

    private void UpdateDiagnostics()
    {
        sagDepthM = 0.0f;
        if (nodes != null && nodes.Length > 2)
        {
            float minY = Mathf.Min(startWorld.y, endWorld.y);
            for (int i = 1; i < nodes.Length - 1; i++)
            {
                sagDepthM = Mathf.Max(sagDepthM, minY - nodes[i].y);
            }
        }

        if (rearMiniRovAnchor != null)
        {
            rearAnchorErrorM = endpointMode == EndpointMode.RearMiniRovAnchor ? Vector3.Distance(endWorld, rearMiniRovAnchor.position) : 0.0f;
        }
        else
        {
            rearAnchorErrorM = -1.0f;
        }
    }

    private static bool IsInvalidVector(Vector3 v)
    {
        return float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
               float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z);
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

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
        {
            return;
        }

        if (startAnchor != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(startAnchor.position, anchorGizmoRadius);
        }

        if (deploymentCableEndpoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(deploymentCableEndpoint.position, anchorGizmoRadius);
        }

        if (rearMiniRovAnchor != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(rearMiniRovAnchor.position, anchorGizmoRadius * 1.25f);
        }

        if (nodes != null && nodes.Length >= 2)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < nodes.Length - 1; i++)
            {
                Gizmos.DrawLine(nodes[i], nodes[i + 1]);
            }
        }
    }
}
