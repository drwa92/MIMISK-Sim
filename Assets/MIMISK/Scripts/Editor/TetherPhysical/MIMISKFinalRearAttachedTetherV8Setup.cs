#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class MIMISKFinalRearAttachedTetherV8Setup
{
    [MenuItem("MIMISK/Tether/V8 Install Final Rear-Attached Runtime Cable Safe")]
    public static void InstallV8()
    {
        GameObject drone = FindDroneGameObject();
        if (drone == null)
        {
            EditorUtility.DisplayDialog("MIMISK Tether V8", "Could not find Drone or MIMISK tether managers in the active scene.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(drone, "Install MIMISK V8 Final Rear Attached Tether");

        MIMISKFinalRearAttachedTetherV8 tether = drone.GetComponent<MIMISKFinalRearAttachedTetherV8>();
        if (tether == null)
        {
            tether = Undo.AddComponent<MIMISKFinalRearAttachedTetherV8>(drone);
        }

        ConfigureV8(tether, drone);
        tether.AutoFindReferences();
        tether.EnsureRearAnchor();
        tether.CopyMaterialFromWinchCableNow();
        tether.SuppressLegacyVisualsNow();
        tether.RebuildCableNow();

        DisableEarlierPhysicalVisualizers();
        MarkDirty(tether);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(drone.scene);

        EditorUtility.DisplayDialog(
            "MIMISK Tether V8",
            "Installed V8 as a safe visual/physics tether. It does not write to original deployment, mission, home, winch, agent, ROS, or gRPC logic. It uses the original deployment endpoint before ROV control and the rear MiniROV anchor after ROV control. Press Play and inspect MIMISK_V8_FinalRearAttachedRuntimeCable.",
            "OK");
    }

    [MenuItem("MIMISK/Tether/V8 Rebuild Rear Anchor From BoxCollider")]
    public static void RebuildRearAnchor()
    {
        MIMISKFinalRearAttachedTetherV8 tether = Object.FindFirstObjectByType<MIMISKFinalRearAttachedTetherV8>();
        if (tether == null)
        {
            EditorUtility.DisplayDialog("MIMISK Tether V8", "No V8 tether component found. Run the V8 installer first.", "OK");
            return;
        }

        Undo.RecordObject(tether, "Rebuild V8 Rear Anchor");
        tether.useManualRearAnchorLocalPosition = false;
        tether.createOrUpdateRearAnchorFromBoxCollider = true;
        tether.EnsureRearAnchor();
        tether.RebuildCableNow();
        MarkDirty(tether);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(tether.gameObject.scene);
    }

    [MenuItem("MIMISK/Tether/V8 Suppress Old Tether Visuals Now")]
    public static void SuppressOldVisuals()
    {
        MIMISKFinalRearAttachedTetherV8 tether = Object.FindFirstObjectByType<MIMISKFinalRearAttachedTetherV8>();
        if (tether == null)
        {
            EditorUtility.DisplayDialog("MIMISK Tether V8", "No V8 tether component found. Run the V8 installer first.", "OK");
            return;
        }

        tether.SuppressLegacyVisualsNow();
        MarkDirty(tether);
    }

    [MenuItem("MIMISK/Tether/V8 Diagnose Final Tether")]
    public static void DiagnoseV8()
    {
        MIMISKFinalRearAttachedTetherV8 tether = Object.FindFirstObjectByType<MIMISKFinalRearAttachedTetherV8>();
        if (tether == null)
        {
            Debug.LogWarning("[MIMISK Tether V8] No V8 tether component found.");
            return;
        }

        Debug.Log("[MIMISK Tether V8] endpointMode=" + tether.endpointModeText +
                  " start=" + Format(tether.startWorld) +
                  " end=" + Format(tether.endWorld) +
                  " commandedLength=" + tether.commandedLengthM.ToString("F3") +
                  " visualLength=" + tether.visualCableLengthM.ToString("F3") +
                  " straightDistance=" + tether.straightDistanceM.ToString("F3") +
                  " slack=" + tether.slackM.ToString("F3") +
                  " sag=" + tether.sagDepthM.ToString("F3") +
                  " geomLength=" + tether.geometricCableLengthM.ToString("F3") +
                  " nodes=" + tether.runtimeNodeCount +
                  " renderedPoints=" + tether.renderedPointCount +
                  " rearLocal=" + Format(tether.computedRearAnchorLocalPosition) +
                  " readOnly=" + tether.readOnlyDoNotChangeOriginalLogic +
                  " applyForces=" + tether.applyForces +
                  " materialSource=" + tether.materialSourcePath +
                  " disabledLegacyRenderers=" + tether.legacyRenderersDisabled +
                  " disabledStalePhysical=" + tether.stalePhysicalRenderersDisabled +
                  " disabledVisualComponents=" + tether.legacyVisualComponentsDisabled);

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            string path = GetHierarchyPath(r.transform);
            string lower = path.ToLowerInvariant();
            if (lower.Contains("tether") || lower.Contains("cable") || lower.Contains("yellow") || lower.Contains("rope") || lower.Contains("winch") || lower.Contains("runtime"))
            {
                Debug.Log("[MIMISK Tether V8] Renderer enabled=" + r.enabled + " type=" + r.GetType().Name + " path=" + path + " material=" + (r.sharedMaterial != null ? r.sharedMaterial.name : "none"));
            }
        }

        LineRenderer[] lines = Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lines.Length; i++)
        {
            LineRenderer line = lines[i];
            if (line == null) continue;
            string path = GetHierarchyPath(line.transform);
            string lower = path.ToLowerInvariant();
            if (lower.Contains("tether") || lower.Contains("cable") || lower.Contains("yellow") || lower.Contains("rope"))
            {
                Debug.Log("[MIMISK Tether V8] LineRenderer enabled=" + line.enabled + " width=" + line.widthMultiplier.ToString("F4") + " path=" + path);
            }
        }
    }

    private static void ConfigureV8(MIMISKFinalRearAttachedTetherV8 tether, GameObject drone)
    {
        tether.readOnlyDoNotChangeOriginalLogic = true;
        tether.applyForces = false;
        tether.autoFindReferences = true;
        tether.unifiedTether = drone.GetComponent<MIMISKUnifiedTetherManager>();
        tether.droneCoreTether = drone.GetComponent<MIMISKDroneCoreTetherManager>();
        tether.droneRigidbody = drone.GetComponent<Rigidbody>();

        if (tether.droneCoreTether != null)
        {
            tether.droneCoreTether.AutoFindReferences();
            if (tether.droneCoreTether.fairleadLineStart != null) tether.startAnchor = tether.droneCoreTether.fairleadLineStart;
            else if (tether.droneCoreTether.tetherAnchor != null) tether.startAnchor = tether.droneCoreTether.tetherAnchor;
            else if (tether.droneCoreTether.winchPoint != null) tether.startAnchor = tether.droneCoreTether.winchPoint;
            tether.miniRovRigidbody = tether.droneCoreTether.miniRovRigidbody;
        }

        if (tether.unifiedTether != null)
        {
            if (tether.startAnchor == null && tether.unifiedTether.fairleadLineStart != null)
            {
                tether.startAnchor = tether.unifiedTether.fairleadLineStart;
            }
            if (tether.miniRovRigidbody == null)
            {
                tether.miniRovRigidbody = tether.unifiedTether.miniRovRigidbody;
            }
        }

        GameObject rov = GameObject.Find(tether.miniRovObjectName);
        if (rov != null)
        {
            tether.miniRovRoot = rov.transform;
            if (tether.miniRovRigidbody == null)
            {
                tether.miniRovRigidbody = rov.GetComponent<Rigidbody>();
            }
        }

        tether.createOrUpdateRearAnchorFromBoxCollider = true;
        tether.useManualRearAnchorLocalPosition = false;
        tether.miniRovFrontIsLocalPositiveZ = true;
        tether.rearAnchorYFractionFromCenter = 0.0f;
        tether.rearAnchorBehindFaceOffsetM = 0.010f;
        tether.useDeploymentEndpointUntilRovControlActive = true;
        tether.useRearAnchorAfterRovControlActive = true;
        tether.fallbackUseRigidBodyDynamicState = true;

        tether.simulateCable = true;
        tether.minimumNodeCount = 8;
        tether.maximumNodeCount = 96;
        tether.targetSegmentLengthM = 0.12f;
        tether.constraintIterations = 72;
        tether.physicsSubsteps = 3;
        tether.verletVelocityDamping = 0.965f;
        tether.nodeMaxSpeedMS = 3.0f;
        tether.internalDrag = 0.60f;
        tether.cableMassPerMeterKg = 0.045f;
        tether.cableDiameterM = 0.008f;
        tether.useGravity = true;
        tether.useBuoyancy = true;
        tether.readWaterSurfaceFromUnifiedTether = true;
        tether.waterCurrentWorldMS = new Vector3(0.012f, 0.0f, 0.004f);

        tether.readCommandedLengthFromOriginalManagers = true;
        tether.minimumVisualSlackM = 0.08f;
        tether.maximumVisualSlackM = 0.45f;
        tether.allowVisualLengthExtensionForEndpointSync = true;
        tether.useWeakCatenaryGuide = true;
        tether.catenaryGuideStrength = 0.16f;
        tether.slackToSagScale = 0.32f;
        tether.maximumSagM = 0.30f;
        tether.maximumLateralCurrentBendM = 0.06f;
        tether.currentBendPerSlackM = 0.04f;

        tether.renderCable = true;
        tether.runtimeCableObjectName = "MIMISK_V8_FinalRearAttachedRuntimeCable";
        tether.radialSegments = 14;
        tether.visualRadiusM = 0.0040f;
        tether.minimumVisualRadiusM = 0.0028f;
        tether.enableSubtleJacketRidges = false;
        tether.jacketRidgeCount = 3;
        tether.jacketRidgeDepthM = 0.00004f;
        tether.smoothRenderPath = true;
        tether.renderSubdivisionsPerSegment = 3;
        tether.renderSmoothingPasses = 1;
        tether.renderSmoothingStrength = 0.08f;
        tether.copyWinchCableMaterial = true;
        tether.cloneCopiedMaterial = true;
        tether.forceMatteMaterial = true;
        tether.disableEmission = true;
        tether.fallbackSmoothness = 0.25f;
        tether.fallbackCableColor = new Color(1.0f, 0.72f, 0.08f, 1.0f);

        tether.suppressLegacyVisuals = true;
        tether.suppressEveryFrame = true;
        tether.disableLegacyVisualComponents = true;
        tether.disableLegacyRenderers = true;
        tether.keepCadWinchCableVisible = true;
        tether.keepHookVisible = true;
        tether.drawDebugGizmos = true;
    }

    private static void DisableEarlierPhysicalVisualizers()
    {
        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour b = behaviours[i];
            if (b == null) continue;
            string typeName = b.GetType().Name;
            if (typeName != "MIMISKPhysicalTetherVisualizer") continue;
            Undo.RecordObject(b, "Disable earlier physical tether visualizer");
            SetBoolFieldOrProperty(b, "visualizerEnabled", false);
            b.enabled = false;
            EditorUtility.SetDirty(b);
        }
    }

    private static void SetBoolFieldOrProperty(MonoBehaviour behaviour, string memberName, bool value)
    {
        if (behaviour == null) return;
        System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        System.Reflection.FieldInfo field = behaviour.GetType().GetField(memberName, flags);
        if (field != null && field.FieldType == typeof(bool))
        {
            field.SetValue(behaviour, value);
            return;
        }
        System.Reflection.PropertyInfo property = behaviour.GetType().GetProperty(memberName, flags);
        if (property != null && property.PropertyType == typeof(bool) && property.CanWrite)
        {
            property.SetValue(behaviour, value, null);
        }
    }

    private static GameObject FindDroneGameObject()
    {
        GameObject drone = GameObject.Find("Drone");
        if (drone != null) return drone;

        MIMISKDroneCoreTetherManager core = Object.FindFirstObjectByType<MIMISKDroneCoreTetherManager>();
        if (core != null) return core.gameObject;

        MIMISKUnifiedTetherManager unified = Object.FindFirstObjectByType<MIMISKUnifiedTetherManager>();
        if (unified != null) return unified.gameObject;

        return null;
    }

    private static void MarkDirty(Object obj)
    {
        if (obj != null)
        {
            EditorUtility.SetDirty(obj);
        }
    }

    private static string Format(Vector3 v)
    {
        return "(" + v.x.ToString("F3") + ", " + v.y.ToString("F3") + ", " + v.z.ToString("F3") + ")";
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null) return string.Empty;
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
#endif
