using UnityEngine;

/// <summary>
/// Real-time physical tether model for the MIMISK drone-deployed MiniROV scene.
///
/// This component is intentionally non-destructive. It reads the existing
/// MIMISK tether/winch managers and exposes a physical node chain that can be
/// rendered by MIMISKPhysicalTetherVisualizer. Endpoint force coupling is OFF
/// by default, so the existing mission and control logic are not disturbed.
///
/// Model:
/// - adaptive node-count, variable rest length from deployed winch length
/// - V6 default Verlet-chain integration with PBD distance constraints between neighbouring nodes
/// - gravity, buoyancy, water drag, air drag, current/wind
/// - air/water transition using waterSurfaceY
/// - endpoint tension metrics and optional drone/MiniROV force coupling
/// </summary>
[DefaultExecutionOrder(1600)]
[DisallowMultipleComponent]
public class MIMISKPhysicalTetherModel : MonoBehaviour
{
    public enum EndpointSource
    {
        AutoFromExistingTetherManagers,
        ExplicitTransforms
    }

    public enum PhysicalTetherState
    {
        Uninitialized,
        Slack,
        SemiTaut,
        Taut,
        OverTension,
        SolverFault
    }

    public enum SolverMode
    {
        HybridPbd,
        VerletChain
    }

    [Header("References")]
    public bool autoFindReferences = true;
    public EndpointSource endpointSource = EndpointSource.AutoFromExistingTetherManagers;

    public MIMISKUnifiedTetherManager unifiedTether;
    public MIMISKDroneCoreTetherManager tetherManager;

    [Tooltip("Physical cable start. Usually WinchFairlead_for_Unity_LineRenderer_Start.")]
    public Transform startAnchor;

    [Tooltip("Physical cable end. Usually MiniROV_TetherPoint or ROV_TetherAnchor.")]
    public Transform endAnchor;

    public Rigidbody droneRigidbody;
    public Rigidbody miniRovRigidbody;

    [Header("Simulation")]
    public bool modelEnabled = true;
    public bool simulateInEditMode = false;

    [Tooltip("V6 default is a Verlet chain: the cable behaves like an Obi-style rope/particle chain while staying dependency-free.")]
    public SolverMode solverMode = SolverMode.VerletChain;

    [Range(10, 160)]
    public int nodeCount = 32;

    [Range(1, 96)]
    public int constraintIterations = 48;

    [Tooltip("Number of internal cable physics substeps per Unity FixedUpdate. Higher values improve stability for short deployed lengths.")]
    [Range(1, 8)]
    public int physicsSubsteps = 2;

    [Tooltip("Constraint stiffness for the inextensible cable projection. 1 is strongest; values around 0.75-0.95 are stable.")]
    [Range(0.05f, 1.0f)]
    public float segmentConstraintStiffness = 0.90f;

    [Tooltip("When ON, cable material length is preserved in both stretch and compression. This prevents the rendered cable from collapsing into a straight ribbon.")]
    public bool enforceSegmentLengthOnCompression = true;

    [Tooltip("Allowed compression strain before a segment is expanded back toward its rest length. Small values preserve deployed cable length while allowing smooth bends.")]
    [Range(0.0f, 0.35f)]
    public float maxCompressionStrain = 0.03f;

    [Tooltip("Allowed elastic segment strain before the position solver corrects the segment length.")]
    [Range(0.0f, 0.35f)]
    public float maxElasticStrain = 0.02f;

    [Header("Adaptive Solver Resolution")]
    [Tooltip("Automatically uses fewer nodes for short tethers and more nodes for longer deployments. This improves convergence and removes the over-stretched ribbon artefact caused by fixed high node counts at short cable lengths.")]
    public bool useAdaptiveNodeCount = true;

    [Range(8, 48)]
    public int minimumAdaptiveNodeCount = 12;

    [Range(16, 160)]
    public int maximumAdaptiveNodeCount = 96;

    [Tooltip("Preferred physical segment length in meters. 0.12-0.18 m is a stable range for this scene.")]
    public float targetSegmentLengthM = 0.12f;

    [Range(1, 12)]
    public int adaptiveNodeHysteresis = 4;

    [Tooltip("Rebuild the physical chain if endpoint references jump suddenly after reset/reparenting.")]
    public bool rebuildOnLargeEndpointJump = true;

    public float endpointJumpRebuildThresholdM = 1.5f;
    public float lengthJumpRebuildThresholdM = 3.0f;
    public float minimumOperationalLengthM = 0.05f;
    public float maximumNodeSpeedMS = 4.0f;

    [Header("V6 Verlet Chain Dynamics")]
    [Tooltip("Velocity retention for Verlet integration. Lower values damp oscillation; higher values keep free-rope motion. 0.94-0.98 is stable for this scene.")]
    [Range(0.80f, 0.995f)]
    public float verletVelocityDamping = 0.965f;

    [Tooltip("How much endpoint velocity is injected into the first/last cable particles. Keeps the tether visually attached to fast winch/ROV motion without violent snaps.")]
    [Range(0.0f, 1.0f)]
    public float verletEndpointVelocityInheritance = 0.55f;

    [Tooltip("Uses the same style as the attached Verlet-chain reference: endpoints locked, interior nodes integrated, then distance constraints iterated many times.")]
    public bool useStrictEndpointLocks = true;

    [Tooltip("Extra cable-wide drag applied in Verlet mode to remove underwater jitter while preserving cable sag.")]
    [Range(0.0f, 3.0f)]
    public float verletAdditionalDrag = 0.35f;

    [Tooltip("When ON, the catenary guide is weaker in Verlet mode so the node chain remains dynamic instead of becoming a scripted curve.")]
    public bool reduceCatenaryGuideInVerletMode = true;

    [Header("Endpoint Selection During Deployment")]
    [Tooltip("When the MiniROV is still cable-managed/kinematic, use the moving deployment cable endpoint instead of the released MiniROV anchor. This keeps the visible/physical tether connected to the hook/fairlead during deployment without changing mission logic.")]
    public bool useDeploymentCableEndpointWhenCableManaged = true;

    [Tooltip("Moving deployment endpoint, usually real_mesh_short_yellow_deployment_cable_to_hook. Its renderer can be hidden while the transform is still used as the physical endpoint.")]
    public Transform deploymentCableEndpoint;

    public string deploymentCableEndpointName = "real_mesh_short_yellow_deployment_cable_to_hook";
    public string deploymentHookEndpointName = "small_dark_open_deployment_hook_for_miniROV";

    [Header("V7 MiniROV Rear Attachment")]
    [Tooltip("Prefer a dedicated rear/top MiniROV anchor over the old center MiniROV_TetherPoint/ROV_TetherAnchor.")]
    public bool preferRearMiniRovTetherAnchor = true;

    [Tooltip("Force the physical tether endpoint to the rear MiniROV anchor in every mission phase. This keeps the cable fixed to the ROV body as the ROV moves and rotates.")]
    public bool forceEndAnchorToRovBackAnchor = false;

    public string rearMiniRovTetherAnchorName = "MIMISK_Tether_BackAnchor";
    public string rearMiniRovTetherAnchorFallbackName1 = "ROV_TetherAnchor";
    public string rearMiniRovTetherAnchorFallbackName2 = "MiniROV_TetherPoint";
    public Transform rovBackAnchor;
    public bool createRovBackAnchorIfMissing = true;
    public Vector3 fallbackRovBackAnchorLocal = new Vector3(0.0f, 0.012f, -0.036f);

    [Tooltip("Runtime gap between the solved tether end point and the rear MiniROV anchor. Should be nearly zero when endpoint locking is correct.")]
    public float endpointAttachmentErrorM;

    public bool endAnchorIsChildOfMiniRov;
    public Vector3 endAnchorLocalOnMiniRov;

    [Header("Realistic Shape Stabilization")]
    [Tooltip("Adds a weak catenary target during monitor-only simulation. This removes non-physical pigtail/loop artefacts while preserving the variable-length cable model.")]
    public bool useCatenaryShapeGuide = true;

    [Range(0.0f, 1.0f)]
    public float catenaryGuideStrength = 0.18f;

    [Range(0.0f, 1.0f)]
    public float catenaryVelocityDamping = 0.35f;

    [Tooltip("Slack-to-sag gain for the catenary guide. Higher values create more visible cable sag.")]
    public float slackToSagScale = 0.35f;

    public float maximumCatenarySagM = 0.35f;
    public float maximumCurrentLateralBendM = 0.08f;
    public float currentLateralBendPerSlackM = 0.05f;

    [Tooltip("Smooths sharp local cable kinks after constraint projection. This is not a visual spline; it is a small physical regularization step for the node chain.")]
    public bool useBendingSmoothing = true;

    [Range(0, 8)]
    public int bendingSmoothingIterations = 2;

    [Range(0.0f, 0.5f)]
    public float bendingSmoothingStrength = 0.12f;

    public float maximumBendingCorrectionM = 0.035f;

    [Header("Cable Physical Parameters")]
    [Tooltip("Outer cable diameter in meters.")]
    public float cableDiameterM = 0.006f;

    [Tooltip("Cable mass per meter in kg/m.")]
    public float massPerMeterKg = 0.035f;

    [Tooltip("Axial stiffness used for tension estimation and endpoint force coupling.")]
    public float axialStiffnessNPerM = 90.0f;

    [Tooltip("Axial damping used for tension estimation and endpoint force coupling.")]
    public float axialDampingNPerMS = 3.0f;

    [Tooltip("Small damping to suppress high-frequency cable jitter.")]
    public float internalLinearDamping = 0.35f;

    public bool useGravity = true;
    public float gravityMS2 = 9.81f;

    [Header("Air / Water Transition")]
    public bool readWaterSurfaceFromUnifiedTether = true;
    public float fallbackWaterSurfaceY = 0.0f;

    public float waterDensityKgM3 = 997.0f;
    public float airDensityKgM3 = 1.225f;

    [Tooltip("Multiplier on buoyancy. 1.0 means displaced-volume buoyancy.")]
    public float buoyancyScale = 1.0f;

    public float waterDragCoefficient = 1.20f;
    public float airDragCoefficient = 1.05f;
    public float dragScale = 1.0f;

    [Tooltip("World-space water current used for underwater tether nodes.")]
    public Vector3 waterCurrentWorldMS = new Vector3(0.035f, 0.0f, 0.010f);

    [Tooltip("World-space wind used for above-water tether nodes.")]
    public Vector3 airWindWorldMS = Vector3.zero;

    [Header("Optional Seabed Plane Contact")]
    public bool enableSeabedPlaneContact = false;
    public float seabedY = -4.0f;
    public float seabedRestitution = 0.05f;
    public float seabedFriction = 0.65f;

    [Header("Winch Length Source")]
    public bool readDeployedLengthFromTetherManager = true;

    [Tooltip("Used if no existing tether manager is available.")]
    public float manualDeployedLengthM = 3.0f;

    [Tooltip("Monitor-mode stabilizer. When force coupling is OFF, the visual/physical chain is not allowed to be shorter than endpoint distance plus this slack. Disable for strict winch-length validation.")]
    public bool preventImpossibleShortCableInMonitorMode = true;

    public float monitorModeMinimumSlackM = 0.18f;

    [Header("Endpoint Force Coupling")]
    [Tooltip("Leave OFF at first. When ON, cable tension pulls the MiniROV at its tether point.")]
    public bool applyForcesToMiniRov = false;

    [Tooltip("Leave OFF at first. When ON, cable reaction pulls the drone at the fairlead.")]
    public bool applyForcesToDrone = false;

    public bool onlyApplyForcesWhenMiniRovDynamic = true;
    public float maximumAppliedForceN = 35.0f;
    public float forceLowPassTimeS = 0.08f;

    [Tooltip("Copies physical tension/slack/stretch into the legacy tether manager metrics for downstream modules.")]
    public bool writeCompatibilityMetricsToTetherManager = false;

    [Tooltip("When physical forces are enabled, turn off the older point-to-point tether force to avoid double force application.")]
    public bool disableLegacyPointTetherForceWhenApplyingPhysicalForces = true;

    [Header("Optional Line Renderer Fallback")]
    public bool driveLineRenderer = false;
    public bool createLineRendererIfMissing = false;
    public LineRenderer lineRenderer;
    public string runtimeLineObjectName = "MIMISK_PhysicalTether_Line";
    public float lineWidthM = 0.018f;


    [Header("Monitor-Only Metric Stabilization")]
    [Tooltip("When endpoint force coupling is off, suppress false tension caused by visual-only PBD solver residuals if the commanded cable is geometrically slack.")]
    public bool debiasMonitorOnlyTension = true;

    [Tooltip("Slack above this value is treated as geometrically slack for monitor-mode tension debiasing.")]
    public float monitorTautSlackForTensionM = 0.025f;

    [Tooltip("Approximate fraction of submerged/effective self weight retained as low residual tension in monitor-only mode.")]
    public float monitorSelfWeightTensionScale = 0.45f;

    [Header("Classification Thresholds")]
    public float slackClassificationThresholdM = 0.05f;
    public float tautTensionThresholdN = 1.5f;
    public float overTensionThresholdN = 35.0f;

    [Header("Runtime Metrics")]
    public PhysicalTetherState physicalState = PhysicalTetherState.Uninitialized;
    public string lastEvent = "not_initialized";
    public string endpointMode = "unknown";

    public Vector3 startWorld;
    public Vector3 endWorld;
    public float commandedDeployedLengthM;
    public float deployedLengthM;
    public float effectiveLengthExtensionM;
    public float restSegmentLengthM;
    public float straightDistanceM;
    public float geometricCableLengthM;
    public float slackM;
    public float geometricStretchM;
    public float elasticStretchM;
    public float maxSegmentStrain;
    public float startTensionN;
    public float endTensionN;
    public float maxTensionN;
    public float meanTensionN;
    public float sagDepthM;
    public float submergedFraction;
    public float winchRateMS;
    public Vector3 filteredForceOnMiniRovN;
    public Vector3 filteredForceOnDroneN;
    public bool solverHealthy = true;
    public bool initialized;

    private Vector3[] positions;
    private Vector3[] previousPositions;
    private Vector3[] velocities;
    private Vector3[] forces;
    private float[] tensions;
    private Vector3[] smoothingScratch;

    private int cachedNodeCount = -1;
    private Vector3 lastStartWorld;
    private Vector3 lastEndWorld;
    private float previousDeployedLengthM;
    private float previousCommandedLengthM;
    private Material fallbackLineMaterial;

    private const float Epsilon = 0.0001f;

    public int CablePointCount
    {
        get
        {
            return positions != null && initialized ? positions.Length : 0;
        }
    }

    public bool HasValidCable
    {
        get
        {
            return initialized && positions != null && positions.Length >= 2 && solverHealthy;
        }
    }

    private void Awake()
    {
        if (autoFindReferences)
        {
            AutoFindReferences();
        }

        EnsureArrays();
    }

    private void OnEnable()
    {
        if (Application.isPlaying || simulateInEditMode)
        {
            RebuildCableFromCurrentEndpoints();
        }
    }

    private void FixedUpdate()
    {
        if (!modelEnabled)
        {
            return;
        }

        if (!Application.isPlaying && !simulateInEditMode)
        {
            return;
        }

        if (autoFindReferences)
        {
            AutoFindReferencesIfMissing();
        }

        float dt = Mathf.Max(0.0005f, Time.fixedDeltaTime);

        if (!ResolveEndpointsAndLength())
        {
            physicalState = PhysicalTetherState.Uninitialized;
            lastEvent = "missing_endpoints";
            initialized = false;
            return;
        }

        EnsureArrays();

        if (!initialized || ShouldRebuildBecauseEndpointJumped())
        {
            RebuildCableFromCurrentEndpoints();
        }

        if (!initialized)
        {
            return;
        }

        SimulateCable(dt);
        UpdateMetrics(dt);
        ApplyEndpointForces(dt);
        WriteCompatibilityMetricsIfRequested();

        previousCommandedLengthM = commandedDeployedLengthM;
        previousDeployedLengthM = deployedLengthM;
        lastStartWorld = startWorld;
        lastEndWorld = endWorld;
    }

    private void LateUpdate()
    {
        if (!modelEnabled)
        {
            return;
        }

        if (autoFindReferences)
        {
            AutoFindReferencesIfMissing();
        }

        if (positions == null || !initialized)
        {
            if (ResolveEndpointsAndLength())
            {
                EnsureArrays();
                RebuildCableFromCurrentEndpoints();
                UpdateMetrics(Mathf.Max(0.0005f, Time.fixedDeltaTime));
            }
        }

        if (driveLineRenderer)
        {
            UpdateLineRenderer();
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

        if (droneRigidbody == null)
        {
            droneRigidbody = GetComponent<Rigidbody>();
        }

        if (tetherManager != null)
        {
            tetherManager.AutoFindReferences();

            if (startAnchor == null)
            {
                startAnchor = tetherManager.fairleadLineStart != null
                    ? tetherManager.fairleadLineStart
                    : (tetherManager.tetherAnchor != null ? tetherManager.tetherAnchor : tetherManager.winchPoint);
            }

            if (miniRovRigidbody == null)
            {
                miniRovRigidbody = tetherManager.miniRovRigidbody;
            }
        }

        if (unifiedTether != null)
        {
            if (startAnchor == null && unifiedTether.fairleadLineStart != null)
            {
                startAnchor = unifiedTether.fairleadLineStart;
            }

            if (miniRovRigidbody == null)
            {
                miniRovRigidbody = unifiedTether.miniRovRigidbody;
            }
        }

        if (deploymentCableEndpoint == null)
        {
            deploymentCableEndpoint = FindDeploymentCableEndpoint();
        }

        if (preferRearMiniRovTetherAnchor || forceEndAnchorToRovBackAnchor)
        {
            Transform rear = FindPreferredRearMiniRovTetherAnchor();
            if (rear != null)
            {
                rovBackAnchor = rear;
                endAnchor = rear;
            }
        }

        if (endAnchor == null)
        {
            endAnchor = FindBestMiniRovTetherAnchor();
        }

        if (lineRenderer == null && tetherManager != null && driveLineRenderer)
        {
            lineRenderer = tetherManager.tetherLineRenderer;
        }

        if (lineRenderer == null && createLineRendererIfMissing && driveLineRenderer)
        {
            Transform existing = FindDeepChild(transform.root, runtimeLineObjectName);

            if (existing == null)
            {
                GameObject go = new GameObject(runtimeLineObjectName);
                go.transform.SetParent(transform.root, false);
                existing = go.transform;
            }

            lineRenderer = existing.GetComponent<LineRenderer>();

            if (lineRenderer == null)
            {
                lineRenderer = existing.gameObject.AddComponent<LineRenderer>();
            }
        }
    }

    private void AutoFindReferencesIfMissing()
    {
        if (unifiedTether == null || tetherManager == null || startAnchor == null || endAnchor == null ||
            (forceEndAnchorToRovBackAnchor && rovBackAnchor == null))
        {
            AutoFindReferences();
        }
    }

    [ContextMenu("Rebuild Cable From Current Endpoints")]
    public void RebuildCableFromCurrentEndpoints()
    {
        if (!ResolveEndpointsAndLength())
        {
            initialized = false;
            physicalState = PhysicalTetherState.Uninitialized;
            lastEvent = "rebuild_failed_missing_endpoints";
            return;
        }

        EnsureArrays();

        int n = positions.Length;
        Vector3 chord = endWorld - startWorld;
        float chordLength = Mathf.Max(Epsilon, chord.magnitude);
        Vector3 horizontal = new Vector3(chord.x, 0.0f, chord.z);

        Vector3 currentDir = waterCurrentWorldMS;
        currentDir.y = 0.0f;
        if (currentDir.sqrMagnitude > Epsilon)
        {
            currentDir.Normalize();
        }
        else
        {
            currentDir = Vector3.right;
        }

        Vector3 side = horizontal.sqrMagnitude > Epsilon
            ? Vector3.Cross(Vector3.up, horizontal.normalized).normalized
            : currentDir;

        float slack = Mathf.Max(0.0f, deployedLengthM - chordLength);
        float sag = Mathf.Clamp(slack * slackToSagScale + 0.015f, 0.0f, maximumCatenarySagM);
        float sideCurve = Mathf.Clamp(slack * currentLateralBendPerSlackM + waterCurrentWorldMS.magnitude * 0.02f, 0.0f, maximumCurrentLateralBendM);

        for (int i = 0; i < n; i++)
        {
            float u = (float)i / Mathf.Max(1.0f, (float)(n - 1));
            float shape = Mathf.Sin(Mathf.PI * u);
            Vector3 p = Vector3.Lerp(startWorld, endWorld, u);

            // Downward catenary-like sag plus a lateral current-induced bend.
            p += Vector3.down * sag * shape;
            p += side * sideCurve * shape;

            positions[i] = p;
            previousPositions[i] = p;
            velocities[i] = Vector3.zero;
            forces[i] = Vector3.zero;
            tensions[Mathf.Min(i, tensions.Length - 1)] = 0.0f;
        }

        positions[0] = startWorld;
        positions[n - 1] = endWorld;
        previousPositions[0] = startWorld;
        previousPositions[n - 1] = endWorld;

        initialized = true;
        solverHealthy = true;
        lastStartWorld = startWorld;
        lastEndWorld = endWorld;
        previousCommandedLengthM = commandedDeployedLengthM;
        previousDeployedLengthM = deployedLengthM;
        lastEvent = "physical_tether_rebuilt";
    }

    [ContextMenu("Safe Mode - Telemetry Only")]
    public void SetTelemetryOnlyMode()
    {
        applyForcesToMiniRov = false;
        applyForcesToDrone = false;
        writeCompatibilityMetricsToTetherManager = false;
        lastEvent = "safe_mode_telemetry_only";
    }

    [ContextMenu("Enable Physical Force Coupling")]
    public void EnablePhysicalForceCoupling()
    {
        applyForcesToMiniRov = true;
        applyForcesToDrone = true;
        writeCompatibilityMetricsToTetherManager = true;

        if (disableLegacyPointTetherForceWhenApplyingPhysicalForces && tetherManager != null)
        {
            tetherManager.enableTetherForceWhenMiniRovAttached = false;
        }

        lastEvent = "physical_force_coupling_enabled";
    }

    [ContextMenu("Disable Physical Force Coupling")]
    public void DisablePhysicalForceCoupling()
    {
        applyForcesToMiniRov = false;
        applyForcesToDrone = false;
        writeCompatibilityMetricsToTetherManager = false;
        filteredForceOnMiniRovN = Vector3.zero;
        filteredForceOnDroneN = Vector3.zero;
        lastEvent = "physical_force_coupling_disabled";
    }

    public Vector3 GetCablePointWorld(int index)
    {
        if (positions == null || positions.Length == 0)
        {
            return transform.position;
        }

        int i = Mathf.Clamp(index, 0, positions.Length - 1);
        return positions[i];
    }

    public int CopyCablePointsWorld(Vector3[] target)
    {
        if (target == null || positions == null || !initialized)
        {
            return 0;
        }

        int count = Mathf.Min(target.Length, positions.Length);
        for (int i = 0; i < count; i++)
        {
            target[i] = positions[i];
        }

        return count;
    }

    public float GetWaterSurfaceY()
    {
        if (readWaterSurfaceFromUnifiedTether && unifiedTether != null)
        {
            return unifiedTether.waterSurfaceY;
        }

        return fallbackWaterSurfaceY;
    }

    private bool ResolveEndpointsAndLength()
    {
        bool haveStart = false;
        bool haveEnd = false;

        if (endpointSource == EndpointSource.ExplicitTransforms)
        {
            if (startAnchor != null)
            {
                startWorld = startAnchor.position;
                haveStart = true;
            }

            if (endAnchor != null)
            {
                endWorld = endAnchor.position;
                endpointMode = "explicit_end_anchor";
                haveEnd = true;
            }
        }
        else
        {
            if (startAnchor != null)
            {
                startWorld = startAnchor.position;
                haveStart = true;
            }
            else if (tetherManager != null)
            {
                if (tetherManager.fairleadLineStart != null)
                {
                    startWorld = tetherManager.fairleadLineStart.position;
                    haveStart = true;
                }
                else if (tetherManager.tetherAnchor != null)
                {
                    startWorld = tetherManager.tetherAnchor.position;
                    haveStart = true;
                }
                else if (tetherManager.winchPoint != null)
                {
                    startWorld = tetherManager.winchPoint.position;
                    haveStart = true;
                }
                else if (!IsInvalidVector(tetherManager.tetherStartWorld))
                {
                    startWorld = tetherManager.tetherStartWorld;
                    haveStart = true;
                }
            }

            if (!haveStart)
            {
                startWorld = transform.position;
                haveStart = true;
            }

            if (deploymentCableEndpoint == null)
            {
                deploymentCableEndpoint = FindDeploymentCableEndpoint();
            }

            if (forceEndAnchorToRovBackAnchor)
            {
                Transform rear = FindPreferredRearMiniRovTetherAnchor();
                if (rear != null)
                {
                    rovBackAnchor = rear;
                    endAnchor = rear;
                    endWorld = rear.position;
                    endpointMode = "rov_back_anchor_forced";
                    haveEnd = true;
                }
            }

            if (!haveEnd && ShouldUseDeploymentCableEndpoint())
            {
                endWorld = deploymentCableEndpoint.position;
                endpointMode = "deployment_cable_endpoint";
                haveEnd = true;
            }

            // V7.4: once the MiniROV is released for control, prefer the dedicated rear
            // physical anchor before the old center/top MiniROV_TetherPoint. This changes
            // only the physical/visual tether endpoint; the original deployment managers
            // keep using their original ROV_TetherAnchor references.
            if (!haveEnd && preferRearMiniRovTetherAnchor)
            {
                Transform rear = FindPreferredRearMiniRovTetherAnchor();
                if (rear != null)
                {
                    rovBackAnchor = rear;
                    endAnchor = rear;
                    endWorld = rear.position;
                    endpointMode = "minirov_rear_tether_anchor";
                    haveEnd = true;
                }
            }

            if (!haveEnd && endAnchor != null)
            {
                endWorld = endAnchor.position;
                endpointMode = "minirov_tether_anchor";
                haveEnd = true;
            }
            else if (!haveEnd)
            {
                Transform best = FindBestMiniRovTetherAnchor();
                if (best != null)
                {
                    endAnchor = best;
                    endWorld = best.position;
                    endpointMode = best == rovBackAnchor ? "minirov_rear_tether_anchor_auto" : "minirov_tether_anchor_auto";
                    haveEnd = true;
                }
                else if (tetherManager != null && !IsInvalidVector(tetherManager.tetherEndWorld))
                {
                    endWorld = tetherManager.tetherEndWorld;
                    endpointMode = "legacy_tether_end_world";
                    haveEnd = true;
                }
            }
        }

        if (!haveStart || !haveEnd)
        {
            return false;
        }

        straightDistanceM = Mathf.Max(0.001f, Vector3.Distance(startWorld, endWorld));
        commandedDeployedLengthM = GetCommandedDeployedLength();
        deployedLengthM = commandedDeployedLengthM;
        effectiveLengthExtensionM = 0.0f;

        bool monitorMode = !applyForcesToMiniRov && !applyForcesToDrone;
        if (preventImpossibleShortCableInMonitorMode && monitorMode)
        {
            float feasibleLength = straightDistanceM + Mathf.Max(0.0f, monitorModeMinimumSlackM);
            if (deployedLengthM < feasibleLength)
            {
                deployedLengthM = feasibleLength;
                effectiveLengthExtensionM = deployedLengthM - commandedDeployedLengthM;
            }
        }

        deployedLengthM = Mathf.Max(minimumOperationalLengthM, deployedLengthM);
        ApplyAdaptiveNodeCountForCurrentLength(deployedLengthM);
        restSegmentLengthM = deployedLengthM / Mathf.Max(1.0f, (float)(Mathf.Max(2, nodeCount) - 1));

        return true;
    }

    private void ApplyAdaptiveNodeCountForCurrentLength(float lengthM)
    {
        if (!useAdaptiveNodeCount)
        {
            return;
        }

        float segmentLength = Mathf.Max(0.04f, targetSegmentLengthM);
        int desired = Mathf.CeilToInt(Mathf.Max(minimumOperationalLengthM, lengthM) / segmentLength) + 1;
        desired = Mathf.Clamp(desired, Mathf.Max(8, minimumAdaptiveNodeCount), Mathf.Max(minimumAdaptiveNodeCount, maximumAdaptiveNodeCount));

        int hysteresis = Mathf.Max(1, adaptiveNodeHysteresis);
        bool firstAllocation = positions == null || cachedNodeCount < 0 || !initialized;

        if (firstAllocation || Mathf.Abs(desired - nodeCount) >= hysteresis)
        {
            nodeCount = desired;
        }
    }

    private float GetCommandedDeployedLength()
    {
        float length = Mathf.Max(minimumOperationalLengthM, manualDeployedLengthM);

        if (readDeployedLengthFromTetherManager && tetherManager != null)
        {
            if (tetherManager.deployedLengthM > minimumOperationalLengthM)
            {
                length = tetherManager.deployedLengthM;
            }
            else if (tetherManager.targetLengthM > minimumOperationalLengthM)
            {
                length = tetherManager.targetLengthM;
            }
        }
        else if (unifiedTether != null)
        {
            length = Mathf.Max(length, unifiedTether.targetDeployLengthM);
        }

        if (tetherManager != null)
        {
            length = Mathf.Clamp(length, tetherManager.minimumLengthM, tetherManager.maximumLengthM);
        }

        return Mathf.Max(minimumOperationalLengthM, length);
    }

    private void EnsureArrays()
    {
        int n = Mathf.Clamp(nodeCount, 10, 160);
        nodeCount = n;

        if (positions == null || positions.Length != n || cachedNodeCount != n)
        {
            positions = new Vector3[n];
            previousPositions = new Vector3[n];
            velocities = new Vector3[n];
            forces = new Vector3[n];
            tensions = new float[n - 1];
            smoothingScratch = new Vector3[n];
            cachedNodeCount = n;
            initialized = false;
        }
    }

    private bool ShouldRebuildBecauseEndpointJumped()
    {
        if (!rebuildOnLargeEndpointJump)
        {
            return false;
        }

        if ((startWorld - lastStartWorld).magnitude > endpointJumpRebuildThresholdM)
        {
            lastEvent = "rebuild_start_endpoint_jump";
            return true;
        }

        if ((endWorld - lastEndWorld).magnitude > endpointJumpRebuildThresholdM)
        {
            lastEvent = "rebuild_end_endpoint_jump";
            return true;
        }

        if (Mathf.Abs(deployedLengthM - previousDeployedLengthM) > lengthJumpRebuildThresholdM)
        {
            lastEvent = "rebuild_length_jump";
            return true;
        }

        return false;
    }

    private void SimulateCable(float dt)
    {
        if (solverMode == SolverMode.VerletChain)
        {
            SimulateCableVerletChain(dt);
            return;
        }

        SimulateCableHybridPbd(dt);
    }

    private void SimulateCableHybridPbd(float dt)
    {
        int n = positions.Length;
        if (n < 2)
        {
            solverHealthy = false;
            physicalState = PhysicalTetherState.SolverFault;
            return;
        }

        restSegmentLengthM = deployedLengthM / Mathf.Max(1.0f, (float)(n - 1));

        Vector3 startVelocity = EstimateStartVelocity(dt);
        Vector3 endVelocity = EstimateEndVelocity(dt);

        int substeps = Mathf.Clamp(physicsSubsteps, 1, 8);
        int iterations = Mathf.Clamp(constraintIterations, 1, 96);
        float h = Mathf.Max(0.0005f, dt / Mathf.Max(1, substeps));

        float radius = Mathf.Max(0.0005f, cableDiameterM * 0.5f);
        float crossSectionArea = Mathf.PI * radius * radius;
        float projectedAreaPerNode = Mathf.Max(0.000001f, cableDiameterM * restSegmentLengthM);
        float nodeMass = Mathf.Max(0.0005f, massPerMeterKg * restSegmentLengthM);
        float nodeVolume = crossSectionArea * restSegmentLengthM;
        float waterY = GetWaterSurfaceY();

        for (int substep = 0; substep < substeps; substep++)
        {
            for (int i = 0; i < n; i++)
            {
                previousPositions[i] = positions[i];
            }

            positions[0] = startWorld;
            positions[n - 1] = endWorld;
            velocities[0] = startVelocity;
            velocities[n - 1] = endVelocity;

            for (int i = 1; i < n - 1; i++)
            {
                Vector3 p = positions[i];
                bool underwater = p.y < waterY;
                float density = underwater ? waterDensityKgM3 : airDensityKgM3;
                float cd = underwater ? waterDragCoefficient : airDragCoefficient;
                Vector3 fluidVelocity = underwater ? waterCurrentWorldMS : airWindWorldMS;

                Vector3 f = Vector3.zero;

                if (useGravity)
                {
                    f += Vector3.down * nodeMass * gravityMS2;
                }

                if (underwater)
                {
                    f += Vector3.up * waterDensityKgM3 * gravityMS2 * nodeVolume * buoyancyScale;
                }

                Vector3 relativeVelocity = velocities[i] - fluidVelocity;
                float speed = relativeVelocity.magnitude;
                if (speed > 0.0001f)
                {
                    f += -0.5f * density * cd * projectedAreaPerNode * speed * relativeVelocity * dragScale;
                }

                f += -velocities[i] * internalLinearDamping * nodeMass;

                forces[i] = f;
                velocities[i] += (f / nodeMass) * h;

                if (velocities[i].magnitude > maximumNodeSpeedMS)
                {
                    velocities[i] = velocities[i].normalized * maximumNodeSpeedMS;
                }

                positions[i] += velocities[i] * h;
            }

            ApplyCatenaryShapeGuide(h);

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                positions[0] = startWorld;
                positions[n - 1] = endWorld;

                if ((iteration & 1) == 0)
                {
                    for (int i = 0; i < n - 1; i++)
                    {
                        ProjectSegmentConstraint(i, restSegmentLengthM, segmentConstraintStiffness);
                    }
                }
                else
                {
                    for (int i = n - 2; i >= 0; i--)
                    {
                        ProjectSegmentConstraint(i, restSegmentLengthM, segmentConstraintStiffness);
                    }
                }

                if (enableSeabedPlaneContact)
                {
                    ApplySeabedContactToInteriorNodes();
                }
            }

            ApplyBendingSmoothingAndReproject(restSegmentLengthM);

            positions[0] = startWorld;
            positions[n - 1] = endWorld;

            for (int i = 1; i < n - 1; i++)
            {
                velocities[i] = (positions[i] - previousPositions[i]) / h;
                if (velocities[i].magnitude > maximumNodeSpeedMS)
                {
                    velocities[i] = velocities[i].normalized * maximumNodeSpeedMS;
                }
            }

            velocities[0] = startVelocity;
            velocities[n - 1] = endVelocity;
        }

        solverHealthy = true;
        for (int i = 0; i < n; i++)
        {
            if (IsInvalidVector(positions[i]) || IsInvalidVector(velocities[i]))
            {
                solverHealthy = false;
                physicalState = PhysicalTetherState.SolverFault;
                lastEvent = "solver_fault_invalid_node";
                break;
            }
        }
    }


    private void SimulateCableVerletChain(float dt)
    {
        int n = positions.Length;
        if (n < 2)
        {
            solverHealthy = false;
            physicalState = PhysicalTetherState.SolverFault;
            return;
        }

        restSegmentLengthM = deployedLengthM / Mathf.Max(1.0f, (float)(n - 1));

        Vector3 startVelocity = EstimateStartVelocity(dt);
        Vector3 endVelocity = EstimateEndVelocity(dt);

        int substeps = Mathf.Clamp(physicsSubsteps, 1, 8);
        int iterations = Mathf.Clamp(constraintIterations, 1, 128);
        float h = Mathf.Max(0.0005f, dt / Mathf.Max(1, substeps));

        float radius = Mathf.Max(0.0005f, cableDiameterM * 0.5f);
        float crossSectionArea = Mathf.PI * radius * radius;
        float projectedAreaPerNode = Mathf.Max(0.000001f, cableDiameterM * restSegmentLengthM);
        float nodeMass = Mathf.Max(0.0005f, massPerMeterKg * restSegmentLengthM);
        float nodeVolume = crossSectionArea * restSegmentLengthM;
        float waterY = GetWaterSurfaceY();
        float damping = Mathf.Clamp(verletVelocityDamping, 0.80f, 0.995f);
        float endpointInheritance = Mathf.Clamp01(verletEndpointVelocityInheritance);

        for (int substep = 0; substep < substeps; substep++)
        {
            if (useStrictEndpointLocks)
            {
                positions[0] = startWorld;
                positions[n - 1] = endWorld;
                previousPositions[0] = startWorld - startVelocity * h * endpointInheritance;
                previousPositions[n - 1] = endWorld - endVelocity * h * endpointInheritance;
                velocities[0] = startVelocity;
                velocities[n - 1] = endVelocity;
            }

            for (int i = 1; i < n - 1; i++)
            {
                Vector3 current = positions[i];
                Vector3 velocity = (positions[i] - previousPositions[i]) / h;

                if (velocity.magnitude > maximumNodeSpeedMS)
                {
                    velocity = velocity.normalized * maximumNodeSpeedMS;
                }

                velocities[i] = velocity;

                bool underwater = current.y < waterY;
                float density = underwater ? waterDensityKgM3 : airDensityKgM3;
                float cd = underwater ? waterDragCoefficient : airDragCoefficient;
                Vector3 fluidVelocity = underwater ? waterCurrentWorldMS : airWindWorldMS;

                Vector3 force = Vector3.zero;

                if (useGravity)
                {
                    force += Vector3.down * nodeMass * gravityMS2;
                }

                if (underwater)
                {
                    force += Vector3.up * waterDensityKgM3 * gravityMS2 * nodeVolume * buoyancyScale;
                }

                Vector3 relativeVelocity = velocity - fluidVelocity;
                float speed = relativeVelocity.magnitude;
                if (speed > 0.0001f)
                {
                    force += -0.5f * density * cd * projectedAreaPerNode * speed * relativeVelocity * dragScale;
                }

                // Fluid/internal damping: enough to suppress jitter, not enough to
                // remove visible cable sag and delayed rope-like motion.
                force += -velocity * (internalLinearDamping + verletAdditionalDrag) * nodeMass;

                Vector3 acceleration = force / nodeMass;
                Vector3 displacement = (positions[i] - previousPositions[i]) * damping;
                Vector3 next = current + displacement + acceleration * h * h;

                previousPositions[i] = current;
                positions[i] = next;
            }

            // The guide only removes impossible pigtails/loops in monitor mode. In
            // Verlet mode it is weakened so the node chain remains dynamic.
            float originalGuide = catenaryGuideStrength;
            if (reduceCatenaryGuideInVerletMode)
            {
                catenaryGuideStrength *= 0.35f;
            }
            ApplyCatenaryShapeGuide(h);
            catenaryGuideStrength = originalGuide;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                if (useStrictEndpointLocks)
                {
                    positions[0] = startWorld;
                    positions[n - 1] = endWorld;
                }

                if ((iteration & 1) == 0)
                {
                    for (int i = 0; i < n - 1; i++)
                    {
                        ProjectSegmentConstraint(i, restSegmentLengthM, 1.0f);
                    }
                }
                else
                {
                    for (int i = n - 2; i >= 0; i--)
                    {
                        ProjectSegmentConstraint(i, restSegmentLengthM, 1.0f);
                    }
                }

                if (enableSeabedPlaneContact)
                {
                    ApplySeabedContactToInteriorNodes();
                }
            }

            ApplyBendingSmoothingAndReproject(restSegmentLengthM);

            positions[0] = startWorld;
            positions[n - 1] = endWorld;

            for (int i = 1; i < n - 1; i++)
            {
                velocities[i] = (positions[i] - previousPositions[i]) / h;
                if (velocities[i].magnitude > maximumNodeSpeedMS)
                {
                    velocities[i] = velocities[i].normalized * maximumNodeSpeedMS;
                    previousPositions[i] = positions[i] - velocities[i] * h;
                }
            }

            velocities[0] = startVelocity;
            velocities[n - 1] = endVelocity;
        }

        solverHealthy = true;
        for (int i = 0; i < n; i++)
        {
            if (IsInvalidVector(positions[i]) || IsInvalidVector(velocities[i]))
            {
                solverHealthy = false;
                physicalState = PhysicalTetherState.SolverFault;
                lastEvent = "solver_fault_invalid_verlet_node";
                break;
            }
        }
    }

    private bool ShouldUseDeploymentCableEndpoint()
    {
        if (forceEndAnchorToRovBackAnchor)
        {
            return false;
        }

        if (!useDeploymentCableEndpointWhenCableManaged || deploymentCableEndpoint == null)
        {
            return false;
        }

        if (unifiedTether != null)
        {
            return unifiedTether.miniRovCableManaged ||
                   (!unifiedTether.miniRovDynamic && !unifiedTether.rovControlActive) ||
                   unifiedTether.tetherState == MIMISKUnifiedTetherManager.TetherMissionState.ReadyForDeploy ||
                   unifiedTether.tetherState == MIMISKUnifiedTetherManager.TetherMissionState.CableAttachedIdle ||
                   unifiedTether.tetherState == MIMISKUnifiedTetherManager.TetherMissionState.Deploying ||
                   unifiedTether.tetherState == MIMISKUnifiedTetherManager.TetherMissionState.RecoveredAttached;
        }

        if (tetherManager != null)
        {
            return tetherManager.useVirtualEndpointWhenNoMiniRov || tetherManager.miniRovTetherPoint == null;
        }

        return false;
    }

    private void ApplyCatenaryShapeGuide(float dt)
    {
        if (!useCatenaryShapeGuide || positions == null || positions.Length < 3)
        {
            return;
        }

        int n = positions.Length;
        float chordLength = Mathf.Max(0.001f, Vector3.Distance(startWorld, endWorld));
        float slack = Mathf.Max(0.0f, deployedLengthM - chordLength);
        if (slack <= 0.002f)
        {
            return;
        }

        bool monitorMode = !applyForcesToMiniRov && !applyForcesToDrone;
        float guideStrength = Mathf.Clamp01(catenaryGuideStrength);
        if (!monitorMode)
        {
            guideStrength *= 0.25f;
        }

        if (guideStrength <= 0.0001f)
        {
            return;
        }

        float sag = Mathf.Clamp(slack * Mathf.Max(0.0f, slackToSagScale), 0.0f, maximumCatenarySagM);

        Vector3 chord = endWorld - startWorld;
        Vector3 horizontal = new Vector3(chord.x, 0.0f, chord.z);
        Vector3 lateralDir = Vector3.zero;

        Vector3 currentHorizontal = new Vector3(waterCurrentWorldMS.x, 0.0f, waterCurrentWorldMS.z);
        if (currentHorizontal.sqrMagnitude > 0.00001f)
        {
            lateralDir = currentHorizontal.normalized;
        }
        else if (horizontal.sqrMagnitude > 0.00001f)
        {
            lateralDir = Vector3.Cross(Vector3.up, horizontal.normalized).normalized;
        }
        else
        {
            lateralDir = Vector3.right;
        }

        float lateral = Mathf.Clamp(slack * currentLateralBendPerSlackM, 0.0f, maximumCurrentLateralBendM);
        float alpha = 1.0f - Mathf.Exp(-guideStrength * Mathf.Max(1.0f, dt * 60.0f));
        alpha = Mathf.Clamp01(alpha);

        for (int i = 1; i < n - 1; i++)
        {
            float u = (float)i / Mathf.Max(1.0f, (float)(n - 1));
            float shape = Mathf.Sin(Mathf.PI * u);
            Vector3 target = Vector3.Lerp(startWorld, endWorld, u);
            target += Vector3.down * sag * shape;
            target += lateralDir * lateral * shape;

            positions[i] = Vector3.Lerp(positions[i], target, alpha);
            velocities[i] *= Mathf.Clamp01(1.0f - alpha * Mathf.Clamp01(catenaryVelocityDamping));
        }
    }

    private void ApplyBendingSmoothingAndReproject(float targetLength)
    {
        if (!useBendingSmoothing || positions == null || positions.Length < 4)
        {
            return;
        }

        int n = positions.Length;
        int passes = Mathf.Clamp(bendingSmoothingIterations, 0, 8);
        if (passes <= 0)
        {
            return;
        }

        if (smoothingScratch == null || smoothingScratch.Length != n)
        {
            smoothingScratch = new Vector3[n];
        }

        float strength = Mathf.Clamp01(bendingSmoothingStrength);
        float maxCorrection = Mathf.Max(0.0f, maximumBendingCorrectionM);

        for (int pass = 0; pass < passes; pass++)
        {
            smoothingScratch[0] = startWorld;
            smoothingScratch[n - 1] = endWorld;

            for (int i = 1; i < n - 1; i++)
            {
                Vector3 target = (positions[i - 1] + positions[i + 1]) * 0.5f;
                Vector3 correction = target - positions[i];
                if (correction.magnitude > maxCorrection && maxCorrection > 0.0f)
                {
                    correction = correction.normalized * maxCorrection;
                }

                smoothingScratch[i] = positions[i] + correction * strength;
            }

            for (int i = 1; i < n - 1; i++)
            {
                positions[i] = smoothingScratch[i];
            }

            positions[0] = startWorld;
            positions[n - 1] = endWorld;

            // Reproject a few length constraints after smoothing so the cable keeps
            // its material length instead of becoming a visually short spline.
            for (int k = 0; k < 2; k++)
            {
                for (int i = 0; i < n - 1; i++)
                {
                    ProjectSegmentConstraint(i, targetLength, 1.0f);
                }
                for (int i = n - 2; i >= 0; i--)
                {
                    ProjectSegmentConstraint(i, targetLength, 1.0f);
                }
            }
        }
    }

    private void ProjectSegmentConstraint(int segmentIndex, float targetLength, float stiffness)
    {
        int n = positions != null ? positions.Length : 0;
        if (n < 2 || segmentIndex < 0 || segmentIndex >= n - 1)
        {
            return;
        }

        Vector3 p0 = positions[segmentIndex];
        Vector3 p1 = positions[segmentIndex + 1];
        Vector3 delta = p1 - p0;
        float length = delta.magnitude;
        if (length <= Epsilon)
        {
            return;
        }

        float maxLength = targetLength * (1.0f + Mathf.Max(0.0f, maxElasticStrain));
        float minLength = targetLength * (1.0f - Mathf.Clamp01(maxCompressionStrain));

        float error = 0.0f;
        if (length > maxLength)
        {
            error = length - maxLength;
        }
        else if (enforceSegmentLengthOnCompression && length < minLength)
        {
            error = length - minLength;
        }
        else
        {
            return;
        }

        Vector3 correction = delta / length * (error * Mathf.Clamp01(stiffness));

        bool p0Fixed = segmentIndex == 0;
        bool p1Fixed = segmentIndex + 1 == n - 1;

        if (p0Fixed && !p1Fixed)
        {
            positions[segmentIndex + 1] -= correction;
        }
        else if (!p0Fixed && p1Fixed)
        {
            positions[segmentIndex] += correction;
        }
        else if (!p0Fixed && !p1Fixed)
        {
            positions[segmentIndex] += correction * 0.5f;
            positions[segmentIndex + 1] -= correction * 0.5f;
        }
    }

    private void ApplySeabedContactToInteriorNodes()
    {
        float minY = seabedY + Mathf.Max(0.0005f, cableDiameterM * 0.5f);
        for (int i = 1; i < positions.Length - 1; i++)
        {
            if (positions[i].y >= minY)
            {
                continue;
            }

            positions[i].y = minY;
            if (velocities[i].y < 0.0f)
            {
                velocities[i].y = -velocities[i].y * Mathf.Clamp01(seabedRestitution);
            }

            Vector3 horizontalVelocity = new Vector3(velocities[i].x, 0.0f, velocities[i].z);
            horizontalVelocity *= Mathf.Clamp01(1.0f - seabedFriction * Time.fixedDeltaTime);
            velocities[i].x = horizontalVelocity.x;
            velocities[i].z = horizontalVelocity.z;
        }
    }

    private void UpdateMetrics(float dt)
    {
        int n = positions != null ? positions.Length : 0;
        if (n < 2)
        {
            physicalState = PhysicalTetherState.Uninitialized;
            return;
        }

        straightDistanceM = Mathf.Max(0.001f, Vector3.Distance(startWorld, endWorld));
        geometricCableLengthM = 0.0f;
        elasticStretchM = 0.0f;
        maxSegmentStrain = 0.0f;
        maxTensionN = 0.0f;
        meanTensionN = 0.0f;
        sagDepthM = 0.0f;
        submergedFraction = 0.0f;

        float waterY = GetWaterSurfaceY();
        int submergedNodes = 0;

        for (int i = 0; i < n; i++)
        {
            if (positions[i].y < waterY)
            {
                submergedNodes++;
            }

            float u = (float)i / Mathf.Max(1.0f, (float)(n - 1));
            float lineY = Mathf.Lerp(startWorld.y, endWorld.y, u);
            sagDepthM = Mathf.Max(sagDepthM, lineY - positions[i].y);
        }

        for (int i = 0; i < n - 1; i++)
        {
            Vector3 a = positions[i];
            Vector3 b = positions[i + 1];
            Vector3 delta = b - a;
            float length = delta.magnitude;
            geometricCableLengthM += length;

            float stretch = Mathf.Max(0.0f, length - restSegmentLengthM);
            elasticStretchM += stretch;

            float strain = stretch / Mathf.Max(0.001f, restSegmentLengthM);
            maxSegmentStrain = Mathf.Max(maxSegmentStrain, strain);

            Vector3 dir = length > Epsilon ? delta / length : Vector3.zero;
            float relativeSpeed = Vector3.Dot(velocities[i + 1] - velocities[i], dir);
            float tension = axialStiffnessNPerM * stretch + axialDampingNPerMS * Mathf.Max(0.0f, relativeSpeed);
            tension = Mathf.Max(0.0f, tension);

            tensions[i] = tension;
            meanTensionN += tension;
            maxTensionN = Mathf.Max(maxTensionN, tension);
        }

        meanTensionN /= Mathf.Max(1.0f, (float)(n - 1));
        startTensionN = tensions.Length > 0 ? tensions[0] : 0.0f;
        endTensionN = tensions.Length > 0 ? tensions[tensions.Length - 1] : 0.0f;
        slackM = Mathf.Max(0.0f, deployedLengthM - straightDistanceM);
        geometricStretchM = Mathf.Max(0.0f, straightDistanceM - deployedLengthM);
        submergedFraction = (float)submergedNodes / Mathf.Max(1.0f, (float)n);
        winchRateMS = (deployedLengthM - previousDeployedLengthM) / Mathf.Max(0.0005f, dt);

        bool monitorMode = !applyForcesToMiniRov && !applyForcesToDrone;
        bool geometricallySlack = geometricStretchM <= 0.002f && slackM > 0.0f;

        // In monitor-only mode the node chain is visual/telemetry-only; it is not allowed
        // to pull the ROV or drone. PBD residual stretch can otherwise create false
        // 40-50 N tension spikes even when the deployed cable is longer than the chord.
        // Keep a small self-weight-derived residual tension, but do not report residual
        // solver stretch as real axial cable tension until endpoint force coupling is ON.
        if (debiasMonitorOnlyTension && monitorMode && geometricallySlack)
        {
            float slackScale = Mathf.Clamp01(1.0f - (slackM / Mathf.Max(0.001f, monitorTautSlackForTensionM)));
            float residualSelfWeightN = Mathf.Max(0.0f, massPerMeterKg * deployedLengthM * gravityMS2 * monitorSelfWeightTensionScale);
            float capN = residualSelfWeightN + 0.5f;

            startTensionN = Mathf.Min(startTensionN * slackScale + residualSelfWeightN * (1.0f - slackScale), capN);
            endTensionN = Mathf.Min(endTensionN * slackScale + residualSelfWeightN * (1.0f - slackScale), capN);
            maxTensionN = Mathf.Min(maxTensionN * slackScale + residualSelfWeightN * (1.0f - slackScale), capN);
            meanTensionN = Mathf.Min(meanTensionN * slackScale + residualSelfWeightN * (1.0f - slackScale), capN);
        }

        bool monitorSlackState = monitorMode && geometricallySlack && slackM >= slackClassificationThresholdM;

        if (!solverHealthy)
        {
            physicalState = PhysicalTetherState.SolverFault;
        }
        else if (monitorSlackState)
        {
            physicalState = PhysicalTetherState.Slack;
        }
        else if (maxTensionN >= overTensionThresholdN)
        {
            physicalState = PhysicalTetherState.OverTension;
        }
        else if (maxTensionN >= tautTensionThresholdN || geometricStretchM > 0.01f)
        {
            physicalState = PhysicalTetherState.Taut;
        }
        else if (slackM >= slackClassificationThresholdM)
        {
            physicalState = PhysicalTetherState.Slack;
        }
        else
        {
            physicalState = PhysicalTetherState.SemiTaut;
        }

        UpdateEndpointAttachmentDiagnostics();
    }

    private void ApplyEndpointForces(float dt)
    {
        if (!applyForcesToMiniRov && !applyForcesToDrone)
        {
            filteredForceOnMiniRovN = Vector3.Lerp(filteredForceOnMiniRovN, Vector3.zero, 0.5f);
            filteredForceOnDroneN = Vector3.Lerp(filteredForceOnDroneN, Vector3.zero, 0.5f);
            return;
        }

        if (onlyApplyForcesWhenMiniRovDynamic && unifiedTether != null)
        {
            bool dynamic = unifiedTether.miniRovDynamic || unifiedTether.rovControlActive;
            if (!dynamic)
            {
                return;
            }
        }

        int n = positions != null ? positions.Length : 0;
        if (n < 2)
        {
            return;
        }

        Vector3 forceOnRov = Vector3.zero;
        Vector3 forceOnDrone = Vector3.zero;

        if (endTensionN > 0.0f)
        {
            Vector3 dirEndToCable = positions[n - 2] - positions[n - 1];
            if (dirEndToCable.sqrMagnitude > Epsilon)
            {
                forceOnRov = dirEndToCable.normalized * Mathf.Min(endTensionN, maximumAppliedForceN);
            }
        }

        if (startTensionN > 0.0f)
        {
            Vector3 dirStartToCable = positions[1] - positions[0];
            if (dirStartToCable.sqrMagnitude > Epsilon)
            {
                forceOnDrone = dirStartToCable.normalized * Mathf.Min(startTensionN, maximumAppliedForceN);
            }
        }

        float alpha = 1.0f - Mathf.Exp(-dt / Mathf.Max(0.001f, forceLowPassTimeS));
        filteredForceOnMiniRovN = Vector3.Lerp(filteredForceOnMiniRovN, forceOnRov, alpha);
        filteredForceOnDroneN = Vector3.Lerp(filteredForceOnDroneN, forceOnDrone, alpha);

        if (applyForcesToMiniRov && miniRovRigidbody != null)
        {
            miniRovRigidbody.AddForceAtPosition(filteredForceOnMiniRovN, endWorld, ForceMode.Force);
        }

        if (applyForcesToDrone && droneRigidbody != null)
        {
            droneRigidbody.AddForceAtPosition(filteredForceOnDroneN, startWorld, ForceMode.Force);
        }
    }

    private void WriteCompatibilityMetricsIfRequested()
    {
        if (!writeCompatibilityMetricsToTetherManager || tetherManager == null)
        {
            return;
        }

        tetherManager.tetherStartWorld = startWorld;
        tetherManager.tetherEndWorld = endWorld;
        tetherManager.straightDistanceM = straightDistanceM;
        tetherManager.slackM = slackM;
        tetherManager.stretchM = geometricStretchM;
        tetherManager.rawTensionN = maxTensionN;
        tetherManager.tensionN = maxTensionN;

        if (disableLegacyPointTetherForceWhenApplyingPhysicalForces)
        {
            tetherManager.enableTetherForceWhenMiniRovAttached = false;
        }
    }

    private void UpdateLineRenderer()
    {
        if (lineRenderer == null)
        {
            if (createLineRendererIfMissing)
            {
                AutoFindReferences();
            }

            if (lineRenderer == null)
            {
                return;
            }
        }

        EnsureFallbackLineMaterial();
        lineRenderer.enabled = true;
        lineRenderer.useWorldSpace = true;
        lineRenderer.widthMultiplier = Mathf.Max(0.002f, lineWidthM);
        lineRenderer.positionCount = positions != null ? positions.Length : 0;
        lineRenderer.sharedMaterial = fallbackLineMaterial;
        lineRenderer.startColor = new Color(1.0f, 0.82f, 0.08f, 1.0f);
        lineRenderer.endColor = new Color(1.0f, 0.82f, 0.08f, 1.0f);

        if (positions == null)
        {
            return;
        }

        for (int i = 0; i < positions.Length; i++)
        {
            lineRenderer.SetPosition(i, positions[i]);
        }
    }

    private Vector3 EstimateStartVelocity(float dt)
    {
        if (droneRigidbody != null)
        {
            return droneRigidbody.linearVelocity;
        }

        return (startWorld - lastStartWorld) / Mathf.Max(0.0005f, dt);
    }

    private Vector3 EstimateEndVelocity(float dt)
    {
        if (miniRovRigidbody != null)
        {
            return miniRovRigidbody.GetPointVelocity(endWorld);
        }

        return (endWorld - lastEndWorld) / Mathf.Max(0.0005f, dt);
    }

    private Transform FindDeploymentCableEndpoint()
    {
        if (unifiedTether != null)
        {
            if (unifiedTether.yellowCableEndPoint != null)
            {
                return unifiedTether.yellowCableEndPoint;
            }

            if (unifiedTether.cableEndFollowRoot != null)
            {
                return unifiedTether.cableEndFollowRoot;
            }
        }

        if (tetherManager != null)
        {
            if (tetherManager.movingTetherEndVisual != null)
            {
                return tetherManager.movingTetherEndVisual;
            }

            if (tetherManager.staticShortDeploymentCableMesh != null)
            {
                return tetherManager.staticShortDeploymentCableMesh;
            }
        }

        Transform t = FindDeepChild(transform, deploymentCableEndpointName);
        if (t != null)
        {
            return t;
        }

        t = FindDeepChild(transform, deploymentHookEndpointName);
        if (t != null)
        {
            return t;
        }

        t = FindDeepChild(transform, "MiniROV_CableEndFollowRoot");
        if (t != null)
        {
            return t;
        }

        return null;
    }

    private Transform FindBestMiniRovTetherAnchor()
    {
        if (preferRearMiniRovTetherAnchor || forceEndAnchorToRovBackAnchor)
        {
            Transform rear = FindPreferredRearMiniRovTetherAnchor();
            if (rear != null)
            {
                return rear;
            }
        }

        if (unifiedTether != null && unifiedTether.miniRovTetherAnchor != null)
        {
            return unifiedTether.miniRovTetherAnchor;
        }

        if (tetherManager != null && tetherManager.miniRovTetherPoint != null)
        {
            return tetherManager.miniRovTetherPoint;
        }

        GameObject rov = GameObject.Find("MiniROV");
        if (rov == null)
        {
            return null;
        }

        Transform t = FindDeepChild(rov.transform, rearMiniRovTetherAnchorName);
        if (t != null)
        {
            return t;
        }

        t = FindDeepChild(rov.transform, "MiniROV_TetherPoint");
        if (t != null)
        {
            return t;
        }

        t = FindDeepChild(rov.transform, "ROV_TetherAnchor");
        if (t != null)
        {
            return t;
        }

        t = FindDeepChild(rov.transform, "TetherPoint");
        if (t != null)
        {
            return t;
        }

        return rov.transform;
    }

    private Transform FindPreferredRearMiniRovTetherAnchor()
    {
        if (rovBackAnchor != null)
        {
            return rovBackAnchor;
        }

        MIMISKMiniROVRearTetherAnchor anchorProvider = Object.FindFirstObjectByType<MIMISKMiniROVRearTetherAnchor>();
        if (anchorProvider != null)
        {
            anchorProvider.AutoPlaceNow();
            Transform provided = anchorProvider.GetAnchorTransform();
            if (provided != null)
            {
                rovBackAnchor = provided;
                return provided;
            }
        }

        GameObject rov = GameObject.Find("MiniROV");
        if (rov == null)
        {
            if (miniRovRigidbody != null)
            {
                rov = miniRovRigidbody.gameObject;
            }
            else if (unifiedTether != null && unifiedTether.miniRovRigidbody != null)
            {
                rov = unifiedTether.miniRovRigidbody.gameObject;
            }
            else if (tetherManager != null && tetherManager.miniRovRigidbody != null)
            {
                rov = tetherManager.miniRovRigidbody.gameObject;
            }
        }

        if (rov == null)
        {
            return null;
        }

        Transform t = FindDeepChild(rov.transform, rearMiniRovTetherAnchorName);
        if (t != null)
        {
            rovBackAnchor = t;
            return t;
        }

        if (!string.IsNullOrEmpty(rearMiniRovTetherAnchorFallbackName1))
        {
            t = FindDeepChild(rov.transform, rearMiniRovTetherAnchorFallbackName1);
            if (t != null && !createRovBackAnchorIfMissing)
            {
                rovBackAnchor = t;
                return t;
            }
        }

        if (!string.IsNullOrEmpty(rearMiniRovTetherAnchorFallbackName2))
        {
            t = FindDeepChild(rov.transform, rearMiniRovTetherAnchorFallbackName2);
            if (t != null && !createRovBackAnchorIfMissing)
            {
                rovBackAnchor = t;
                return t;
            }
        }

        if (createRovBackAnchorIfMissing)
        {
            MIMISKMiniROVRearTetherAnchor provider = rov.GetComponent<MIMISKMiniROVRearTetherAnchor>();
            if (provider == null)
            {
                provider = rov.AddComponent<MIMISKMiniROVRearTetherAnchor>();
            }

            provider.anchorName = rearMiniRovTetherAnchorName;
            provider.fallbackLocalPosition = fallbackRovBackAnchorLocal;
            provider.preserveExistingMissionLogic = true;
            provider.assignReferencesEveryFrame = false;
            provider.writeToUnifiedTetherManager = false;
            provider.writeToDroneCoreTetherManager = false;
            provider.writeToSmartWinchController = false;
            provider.writeToPhysicalTetherModel = true;
            provider.AutoPlaceNow();
            provider.AssignReferencesNow();

            Transform created = provider.GetAnchorTransform();
            if (created != null)
            {
                rovBackAnchor = created;
                return created;
            }
        }

        return null;
    }

    private void UpdateEndpointAttachmentDiagnostics()
    {
        endpointAttachmentErrorM = 0.0f;
        endAnchorIsChildOfMiniRov = false;
        endAnchorLocalOnMiniRov = Vector3.zero;

        if (forceEndAnchorToRovBackAnchor && rovBackAnchor == null)
        {
            rovBackAnchor = FindPreferredRearMiniRovTetherAnchor();
        }

        Transform anchor = rovBackAnchor != null ? rovBackAnchor : endAnchor;
        if (anchor == null)
        {
            return;
        }

        endpointAttachmentErrorM = Vector3.Distance(endWorld, anchor.position);

        Rigidbody rovRb = anchor.GetComponentInParent<Rigidbody>();
        Transform rovRoot = rovRb != null ? rovRb.transform : anchor.parent;
        if (rovRoot != null)
        {
            endAnchorIsChildOfMiniRov = anchor.IsChildOf(rovRoot);
            endAnchorLocalOnMiniRov = rovRoot.InverseTransformPoint(anchor.position);
        }
    }

    private void EnsureFallbackLineMaterial()
    {
        if (fallbackLineMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");

        fallbackLineMaterial = new Material(shader);
        fallbackLineMaterial.name = "MIMISK_Runtime_Physical_Tether_Line_Yellow";
        SetMaterialColor(fallbackLineMaterial, new Color(1.0f, 0.82f, 0.08f, 1.0f));
    }

    private static void SetMaterialColor(Material mat, Color color)
    {
        if (mat == null)
        {
            return;
        }

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }

        if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }
    }

    private static bool IsInvalidVector(Vector3 v)
    {
        return float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
               float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z);
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
}
