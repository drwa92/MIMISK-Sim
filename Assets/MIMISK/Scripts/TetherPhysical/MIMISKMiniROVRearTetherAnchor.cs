using UnityEngine;

/// <summary>
/// Creates and maintains the physical tether attachment point on the rear/top of the MiniROV.
/// The generated anchor is a child of the MiniROV, so it follows MiniROV translation and rotation exactly.
/// V7.4 safety: this component does NOT write into the original deployment/control managers by default.
/// It only provides a rear physical/visual endpoint for the new runtime tether. The original cable-managed
/// deployment logic continues to use ROV_TetherAnchor / MiniROV_TetherPoint.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-250)]
public class MIMISKMiniROVRearTetherAnchor : MonoBehaviour
{
    public enum RearAxisMode
    {
        AutoFromFrontCamera,
        PositiveLocalZIsFront,
        NegativeLocalZIsFront
    }

    [Header("Anchor")]
    public bool anchorEnabled = true;
    public string anchorName = "MIMISK_Tether_BackAnchor";
    public Transform anchor;
    public bool createIfMissing = true;
    public bool forceAnchorChildOfMiniRov = true;

    [Header("Placement")]
    public bool autoPlaceFromMiniRovBounds = true;
    public bool preferRootBoxCollider = true;
    public bool preferBodyModelBounds = true;
    public bool useRendererBounds = true;
    public bool useColliderBoundsIfNoRenderer = true;
    public bool includeInactiveChildren = true;
    public bool ignoreCableTetherAndDebugRenderers = true;
    public string preferredBodyModelName = "Body_Model";
    public RearAxisMode rearAxisMode = RearAxisMode.AutoFromFrontCamera;
    public string frontCameraName = "FrontCamera";
    public Transform frontReference;
    public Vector3 fallbackLocalPosition = new Vector3(0.0f, 0.012f, -0.036f);
    public bool useManualLocalPosition = false;
    public Vector3 manualLocalPosition = new Vector3(0.0f, 0.012f, -0.036f);
    public float rearOutsideClearanceM = 0.006f;
    public float topOutsideClearanceM = 0.0f;

    [Header("V7.4 Box Collider Rear-Face Placement")]
    [Tooltip("Use the MiniROV root BoxCollider as the authoritative body geometry. For your MiniROV this gives z = center.z - size.z/2, i.e. the true rear face, instead of using renderer bounds or old center tether points.")]
    public bool useRootBoxColliderRearFacePlacement = true;

    [Tooltip("Normalized X location on the root BoxCollider. 0 is center, -0.5 is left side, +0.5 is right side.")]
    [Range(-0.5f, 0.5f)]
    public float boxLocalXFractionFromCenter = 0.0f;

    [Tooltip("Normalized Y location on the root BoxCollider. 0 is vertical center, +0.5 is top face, -0.5 is bottom face. A small positive value places the tether slightly above the rear-center without floating above the ROV.")]
    [Range(-0.5f, 0.5f)]
    public float boxLocalYFractionFromCenter = 0.15f;

    [Tooltip("Offset outside the rear face. Use 0.003-0.008 m so the cable appears attached to the outside of the CAD shell rather than inside the ROV mesh.")]
    public float boxRearFaceClearanceM = 0.006f;

    [Tooltip("When true the anchor is clamped exactly on the BoxCollider rear face. When false it is moved slightly outside by boxRearFaceClearanceM.")]
    public bool keepAnchorOnColliderRearFace = false;

    public float xOffsetM = 0.0f;
    public float yOffsetM = 0.0f;
    public float zOffsetM = 0.0f;
    public float maximumAbsLocalZ = 0.60f;
    public float maximumAbsLocalY = 0.35f;

    [Header("Reference Writing")]
    public bool maintainAnchorEveryFrame = true;
    public bool assignReferencesEveryFrame = false;

    [Tooltip("V7.2 safety: keep the original deployment/control logic untouched. When true, this component will NOT overwrite UnifiedTetherManager, DroneCoreTetherManager, or SmartWinch endpoint references. The rear anchor is only a physical/visual endpoint for the new tether model.")]
    public bool preserveExistingMissionLogic = true;

    public bool writeToUnifiedTetherManager = false;
    public bool writeToDroneCoreTetherManager = false;
    public bool writeToSmartWinchController = false;
    public bool writeToPhysicalTetherModel = true;
    public bool writeToRovSyncMonitor = true;

    [Header("Diagnostics")]
    public Vector3 anchorLocalPosition;
    public Vector3 anchorWorldPosition;
    public Vector3 boundsMinLocal;
    public Vector3 boundsMaxLocal;
    public bool boundsValid;
    public string boundsSource = "not_initialized";
    public string frontAxisEvidence = "not_initialized";
    public string lastEvent = "not_initialized";

    private float nextSlowReferenceSearchTime;

    private void Awake()
    {
        AutoPlaceNow();
        AssignReferencesNow();
    }

    private void OnEnable()
    {
        AutoPlaceNow();
        AssignReferencesNow();
    }

    private void Start()
    {
        AutoPlaceNow();
        AssignReferencesNow();
    }

    private void LateUpdate()
    {
        if (!anchorEnabled)
        {
            return;
        }

        if (maintainAnchorEveryFrame)
        {
            AutoPlaceNow();
        }

        if (assignReferencesEveryFrame)
        {
            AssignReferencesNow();
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            AutoPlaceNow();
        }
    }

    [ContextMenu("Auto Place Rear Tether Anchor Now")]
    public void AutoPlaceNow()
    {
        if (!anchorEnabled)
        {
            return;
        }

        EnsureAnchorObject();
        if (anchor == null)
        {
            lastEvent = "anchor_missing";
            return;
        }

        Vector3 local = ComputeAnchorLocalPosition();
        anchor.localPosition = local;
        anchor.localRotation = Quaternion.identity;
        anchor.localScale = Vector3.one;

        anchorLocalPosition = local;
        anchorWorldPosition = anchor.position;
        lastEvent = "rear_anchor_placed";
    }

    [ContextMenu("Assign Rear Anchor To Tether Controllers Now")]
    public void AssignReferencesNow()
    {
        if (!anchorEnabled)
        {
            return;
        }

        EnsureAnchorObject();
        if (anchor == null)
        {
            lastEvent = "assign_failed_anchor_missing";
            return;
        }

        // V7.2 safety rule:
        // The rear physical anchor must NOT replace the endpoint references used by the original
        // deployment/release/control stack. Those references are used by MIMISKUnifiedTetherManager
        // to attach the MiniROV to the moving winch cable endpoint. Overwriting them can create a
        // transform-feedback loop and send the ROV to unrealistic coordinates.
        if (!preserveExistingMissionLogic)
        {
            if (writeToUnifiedTetherManager)
            {
                MIMISKUnifiedTetherManager unified = Object.FindFirstObjectByType<MIMISKUnifiedTetherManager>();
                if (unified != null)
                {
                    unified.miniRovTetherAnchor = anchor;
                    unified.miniRovTetherAnchorName = anchorName;
                }
            }

            if (writeToDroneCoreTetherManager)
            {
                MIMISKDroneCoreTetherManager legacy = Object.FindFirstObjectByType<MIMISKDroneCoreTetherManager>();
                if (legacy != null)
                {
                    legacy.miniRovTetherPoint = anchor;
                }
            }

            if (writeToSmartWinchController)
            {
                MIMISKTetherSmartWinchController smartWinch = Object.FindFirstObjectByType<MIMISKTetherSmartWinchController>();
                if (smartWinch != null)
                {
                    smartWinch.miniRovTetherPoint = anchor;
                }
            }
        }

        if (writeToPhysicalTetherModel)
        {
            MIMISKPhysicalTetherModel physical = Object.FindFirstObjectByType<MIMISKPhysicalTetherModel>();
            if (physical != null)
            {
                physical.preferRearMiniRovTetherAnchor = true;
                physical.forceEndAnchorToRovBackAnchor = false;
                physical.useDeploymentCableEndpointWhenCableManaged = true;
                physical.rearMiniRovTetherAnchorName = anchorName;
                physical.rearMiniRovTetherAnchorFallbackName1 = "ROV_TetherAnchor";
                physical.rearMiniRovTetherAnchorFallbackName2 = "MiniROV_TetherPoint";
                physical.rovBackAnchor = anchor;
            }
        }

        if (writeToRovSyncMonitor)
        {
            MIMISKPhysicalTetherRovSyncMonitor sync = Object.FindFirstObjectByType<MIMISKPhysicalTetherRovSyncMonitor>();
            if (sync != null)
            {
                sync.rearAnchorProvider = this;
                sync.rearAnchor = anchor;
                sync.preserveExistingMissionLogic = true;
                sync.writeRearAnchorToControllersEveryFrame = false;
            }
        }

        lastEvent = preserveExistingMissionLogic ? "rear_anchor_assigned_to_physical_tether_only" : "rear_anchor_references_assigned";
    }

    public Transform GetAnchorTransform()
    {
        EnsureAnchorObject();
        return anchor;
    }

    [ContextMenu("V7.4 Use Root BoxCollider Rear-Face Preset")]
    public void UseRootBoxColliderRearFacePreset()
    {
        useManualLocalPosition = false;
        autoPlaceFromMiniRovBounds = true;
        preferRootBoxCollider = true;
        useRootBoxColliderRearFacePlacement = true;
        rearAxisMode = RearAxisMode.AutoFromFrontCamera;
        boxLocalXFractionFromCenter = 0.0f;
        boxLocalYFractionFromCenter = 0.15f;
        boxRearFaceClearanceM = 0.006f;
        keepAnchorOnColliderRearFace = false;
        fallbackLocalPosition = new Vector3(0.0f, 0.012f, -0.036f);
        manualLocalPosition = fallbackLocalPosition;
        preserveExistingMissionLogic = true;
        writeToUnifiedTetherManager = false;
        writeToDroneCoreTetherManager = false;
        writeToSmartWinchController = false;
        writeToPhysicalTetherModel = true;
        writeToRovSyncMonitor = true;
        AutoPlaceNow();
        AssignReferencesNow();
    }

    private void EnsureAnchorObject()
    {
        if (anchor != null)
        {
            if (forceAnchorChildOfMiniRov && anchor.parent != transform)
            {
                anchor.SetParent(transform, true);
            }
            return;
        }

        anchor = FindDeepChild(transform, anchorName);
        if (anchor == null && createIfMissing)
        {
            GameObject go = new GameObject(anchorName);
            go.transform.SetParent(transform, false);
            anchor = go.transform;
        }

        if (anchor != null && forceAnchorChildOfMiniRov && anchor.parent != transform)
        {
            anchor.SetParent(transform, true);
        }
    }

    private Vector3 ComputeAnchorLocalPosition()
    {
        if (useManualLocalPosition || !autoPlaceFromMiniRovBounds)
        {
            boundsValid = false;
            boundsSource = useManualLocalPosition ? "manual_local_position" : "fallback_local_position";
            return useManualLocalPosition ? manualLocalPosition : fallbackLocalPosition;
        }

        if (useRootBoxColliderRearFacePlacement)
        {
            BoxCollider rootBox = GetComponent<BoxCollider>();
            if (rootBox != null && rootBox.enabled)
            {
                Bounds rb = new Bounds(rootBox.center, rootBox.size);
                if (IsUsableBounds(rb))
                {
                    boundsValid = true;
                    boundsMinLocal = rb.min;
                    boundsMaxLocal = rb.max;
                    boundsSource = "root_box_collider_rear_face_v7_4";

                    float frontSign = DetermineFrontSign();
                    float halfX = rootBox.size.x * 0.5f;
                    float halfY = rootBox.size.y * 0.5f;
                    float halfZ = rootBox.size.z * 0.5f;

                    float x = rootBox.center.x + Mathf.Clamp(boxLocalXFractionFromCenter, -0.5f, 0.5f) * rootBox.size.x + xOffsetM;
                    float y = rootBox.center.y + Mathf.Clamp(boxLocalYFractionFromCenter, -0.5f, 0.5f) * rootBox.size.y + yOffsetM;
                    float rearFaceZ = frontSign >= 0.0f ? rootBox.center.z - halfZ : rootBox.center.z + halfZ;
                    float clearance = keepAnchorOnColliderRearFace ? 0.0f : Mathf.Max(0.0f, boxRearFaceClearanceM);
                    float z = rearFaceZ - frontSign * clearance + zOffsetM;

                    x = Mathf.Clamp(x, rootBox.center.x - halfX - 0.05f, rootBox.center.x + halfX + 0.05f);
                    y = Mathf.Clamp(y, rootBox.center.y - halfY - 0.05f, rootBox.center.y + halfY + 0.05f);

                    if (maximumAbsLocalZ > 0.0f)
                    {
                        z = Mathf.Clamp(z, -maximumAbsLocalZ, maximumAbsLocalZ);
                    }

                    if (maximumAbsLocalY > 0.0f)
                    {
                        y = Mathf.Clamp(y, -maximumAbsLocalY, maximumAbsLocalY);
                    }

                    return new Vector3(x, y, z);
                }
            }
        }

        Bounds b;
        if (TryGetPreferredLocalBounds(out b))
        {
            boundsValid = true;
            boundsMinLocal = b.min;
            boundsMaxLocal = b.max;

            float frontSign = DetermineFrontSign();
            float rearZ = frontSign >= 0.0f ? b.min.z : b.max.z;
            float z = rearZ - frontSign * Mathf.Max(0.0f, rearOutsideClearanceM);
            float y = b.max.y + Mathf.Max(0.0f, topOutsideClearanceM);
            float x = xOffsetM;

            z += zOffsetM;
            y += yOffsetM;

            if (maximumAbsLocalZ > 0.0f)
            {
                z = Mathf.Clamp(z, -maximumAbsLocalZ, maximumAbsLocalZ);
            }

            if (maximumAbsLocalY > 0.0f)
            {
                y = Mathf.Clamp(y, -maximumAbsLocalY, maximumAbsLocalY);
            }

            return new Vector3(x, y, z);
        }

        boundsValid = false;
        boundsSource = "fallback_local_position_no_bounds";
        boundsMinLocal = Vector3.zero;
        boundsMaxLocal = Vector3.zero;
        return fallbackLocalPosition;
    }

    private bool TryGetPreferredLocalBounds(out Bounds localBounds)
    {
        localBounds = new Bounds(Vector3.zero, Vector3.zero);

        if (preferRootBoxCollider)
        {
            BoxCollider rootBox = GetComponent<BoxCollider>();
            if (rootBox != null && rootBox.enabled)
            {
                localBounds = new Bounds(rootBox.center, rootBox.size);
                boundsSource = "minirov_root_box_collider";
                return IsUsableBounds(localBounds);
            }
        }

        Transform boundsRoot = transform;
        Transform body = FindDeepChild(transform, preferredBodyModelName);
        if (preferBodyModelBounds && body != null)
        {
            boundsRoot = body;
        }

        if (useRendererBounds && TryComputeRendererLocalBounds(boundsRoot, out localBounds))
        {
            boundsSource = boundsRoot == transform ? "minirov_renderer_bounds" : "body_model_renderer_bounds";
            return true;
        }

        if (useColliderBoundsIfNoRenderer && TryComputeColliderLocalBounds(boundsRoot, out localBounds))
        {
            boundsSource = boundsRoot == transform ? "minirov_collider_bounds" : "body_model_collider_bounds";
            return true;
        }

        if (boundsRoot != transform && useRendererBounds && TryComputeRendererLocalBounds(transform, out localBounds))
        {
            boundsSource = "minirov_renderer_bounds_fallback";
            return true;
        }

        if (boundsRoot != transform && useColliderBoundsIfNoRenderer && TryComputeColliderLocalBounds(transform, out localBounds))
        {
            boundsSource = "minirov_collider_bounds_fallback";
            return true;
        }

        return false;
    }

    private bool TryComputeRendererLocalBounds(Transform root, out Bounds localBounds)
    {
        localBounds = new Bounds(Vector3.zero, Vector3.zero);
        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactiveChildren);
        bool have = false;
        Matrix4x4 worldToMiniRov = transform.worldToLocalMatrix;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled && !includeInactiveChildren)
            {
                continue;
            }

            if (!IsRendererUsableForRovBounds(r))
            {
                continue;
            }

            EncapsulateWorldBounds(worldToMiniRov, r.bounds, ref localBounds, ref have);
        }

        return have && IsUsableBounds(localBounds);
    }

    private bool TryComputeColliderLocalBounds(Transform root, out Bounds localBounds)
    {
        localBounds = new Bounds(Vector3.zero, Vector3.zero);
        if (root == null)
        {
            return false;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(includeInactiveChildren);
        bool have = false;
        Matrix4x4 worldToMiniRov = transform.worldToLocalMatrix;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null || !c.enabled && !includeInactiveChildren)
            {
                continue;
            }

            string path = GetHierarchyPath(c.transform).ToLowerInvariant();
            if (ignoreCableTetherAndDebugRenderers && IsCableOrDebugPath(path))
            {
                continue;
            }

            EncapsulateWorldBounds(worldToMiniRov, c.bounds, ref localBounds, ref have);
        }

        return have && IsUsableBounds(localBounds);
    }

    private bool IsRendererUsableForRovBounds(Renderer r)
    {
        string path = GetHierarchyPath(r.transform).ToLowerInvariant();
        if (ignoreCableTetherAndDebugRenderers && IsCableOrDebugPath(path))
        {
            return false;
        }

        if (path.Contains("water") || path.Contains("ocean") || path.Contains("environment") || path.Contains("heightmap"))
        {
            return false;
        }

        return true;
    }

    private static bool IsCableOrDebugPath(string lowerPath)
    {
        return lowerPath.Contains("tether") ||
               lowerPath.Contains("cable") ||
               lowerPath.Contains("rope") ||
               lowerPath.Contains("physicaltether") ||
               lowerPath.Contains("yellowtether") ||
               lowerPath.Contains("debug") ||
               lowerPath.Contains("gizmo") ||
               lowerPath.Contains("line");
    }

    private static void EncapsulateWorldBounds(Matrix4x4 worldToLocal, Bounds worldBounds, ref Bounds localBounds, ref bool have)
    {
        Vector3 c = worldBounds.center;
        Vector3 e = worldBounds.extents;

        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sy = -1; sy <= 1; sy += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 worldCorner = c + new Vector3(e.x * sx, e.y * sy, e.z * sz);
                    Vector3 localCorner = worldToLocal.MultiplyPoint3x4(worldCorner);
                    if (!have)
                    {
                        localBounds = new Bounds(localCorner, Vector3.zero);
                        have = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localCorner);
                    }
                }
            }
        }
    }

    private bool IsUsableBounds(Bounds b)
    {
        Vector3 s = b.size;
        if (s.x <= 0.0005f || s.y <= 0.0005f || s.z <= 0.0005f)
        {
            return false;
        }

        if (s.x > 3.0f || s.y > 3.0f || s.z > 3.0f)
        {
            return false;
        }

        return true;
    }

    private float DetermineFrontSign()
    {
        if (rearAxisMode == RearAxisMode.PositiveLocalZIsFront)
        {
            frontAxisEvidence = "manual_positive_local_z_front";
            return 1.0f;
        }

        if (rearAxisMode == RearAxisMode.NegativeLocalZIsFront)
        {
            frontAxisEvidence = "manual_negative_local_z_front";
            return -1.0f;
        }

        if (frontReference == null && Time.realtimeSinceStartup >= nextSlowReferenceSearchTime)
        {
            nextSlowReferenceSearchTime = Time.realtimeSinceStartup + 0.5f;
            frontReference = FindDeepChild(transform, frontCameraName);
        }

        if (frontReference != null)
        {
            Vector3 local = transform.InverseTransformPoint(frontReference.position);
            if (Mathf.Abs(local.z) > 0.002f)
            {
                frontAxisEvidence = "front_reference_" + frontReference.name + "_local_z_" + local.z.ToString("F4");
                return local.z >= 0.0f ? 1.0f : -1.0f;
            }
        }

        frontAxisEvidence = "fallback_positive_local_z_front";
        return 1.0f;
    }

    private void OnDrawGizmosSelected()
    {
        Transform a = anchor != null ? anchor : FindDeepChild(transform, anchorName);
        if (a == null)
        {
            return;
        }

        Gizmos.color = new Color(1.0f, 0.85f, 0.05f, 1.0f);
        Gizmos.DrawSphere(a.position, 0.025f);
        Gizmos.DrawLine(transform.position, a.position);

        BoxCollider rootBox = GetComponent<BoxCollider>();
        if (rootBox != null)
        {
            Vector3 rearCenter = rootBox.center;
            float frontSign = DetermineFrontSign();
            rearCenter.z = frontSign >= 0.0f ? rootBox.center.z - rootBox.size.z * 0.5f : rootBox.center.z + rootBox.size.z * 0.5f;
            Gizmos.color = new Color(1.0f, 0.55f, 0.0f, 0.8f);
            Gizmos.DrawLine(transform.TransformPoint(rearCenter), a.position);
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
}
