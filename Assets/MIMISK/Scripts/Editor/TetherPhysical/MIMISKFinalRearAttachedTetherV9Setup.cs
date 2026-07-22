#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class MIMISKFinalRearAttachedTetherV9Setup
{
    [MenuItem("MIMISK/Tether/V9 Install Contact-Safe Runtime Cable + Logger")]
    public static void InstallV9()
    {
        GameObject drone = GameObject.Find("Drone");
        if (drone == null && Selection.activeGameObject != null)
        {
            drone = Selection.activeGameObject;
        }
        if (drone == null)
        {
            EditorUtility.DisplayDialog("MIMISK Tether V9", "Could not find Drone. Select the Drone object or ensure it is named Drone.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(drone, "Install MIMISK Tether V9");

        MIMISKFinalRearAttachedTetherV8 tether = drone.GetComponent<MIMISKFinalRearAttachedTetherV8>();
        if (tether == null)
        {
            tether = drone.AddComponent<MIMISKFinalRearAttachedTetherV8>();
        }

        ConfigureContact(tether);
        tether.AutoFindReferences();
        tether.EnsureRearAnchor();
        tether.CopyMaterialFromWinchCableNow();
        tether.SuppressLegacyVisualsNow();
        tether.RebuildCableNow();

        MIMISKFinalRearAttachedTetherV8CsvLogger logger = drone.GetComponent<MIMISKFinalRearAttachedTetherV8CsvLogger>();
        if (logger == null)
        {
            logger = drone.AddComponent<MIMISKFinalRearAttachedTetherV8CsvLogger>();
        }
        logger.tether = tether;
        logger.unifiedTether = drone.GetComponent<MIMISKUnifiedTetherManager>();
        logger.droneCoreTether = drone.GetComponent<MIMISKDroneCoreTetherManager>();
        logger.droneRigidbody = drone.GetComponent<Rigidbody>();
        GameObject rov = GameObject.Find("MiniROV");
        if (rov != null) logger.miniRovRigidbody = rov.GetComponent<Rigidbody>();
        logger.enableLogging = true;
        logger.sampleIntervalS = 0.10f;
        logger.createLogsFolderInProjectRoot = true;
        logger.logsFolderName = "Logs";
        logger.filePrefix = "mimisk_v9_tether";

        EditorUtility.SetDirty(tether);
        EditorUtility.SetDirty(logger);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(drone.scene);

        EditorUtility.DisplayDialog("MIMISK Tether V9",
            "Installed the contact-safe V9 configuration. Original deployment, mission, home, agent, ROS, and gRPC logic are still read-only. A V8/V9 logger will write contact-aware CSV logs under the project Logs folder during Play Mode.",
            "OK");
    }

    [MenuItem("MIMISK/Tether/V9 Diagnose Contact Runtime Cable")]
    public static void DiagnoseV9()
    {
        MIMISKFinalRearAttachedTetherV8 tether = Object.FindFirstObjectByType<MIMISKFinalRearAttachedTetherV8>();
        if (tether == null)
        {
            Debug.LogWarning("[MIMISK Tether V9] No V8/V9 tether component found.");
            return;
        }

        Debug.Log("[MIMISK Tether V9] endpoint=" + tether.endpointModeText +
                  " start=" + F(tether.startWorld) +
                  " end=" + F(tether.endWorld) +
                  " visualLength=" + tether.visualCableLengthM.ToString("F3") +
                  " geometricLength=" + tether.geometricCableLengthM.ToString("F3") +
                  " slack=" + tether.slackM.ToString("F3") +
                  " sag=" + tether.sagDepthM.ToString("F3") +
                  " nodes=" + tether.runtimeNodeCount +
                  " solverHealthy=" + tether.solverHealthy +
                  " contactEnabled=" + tether.enableContactProjection +
                  " contactActive=" + tether.contactProjectionActive +
                  " contactNodes=" + tether.contactNodeCount +
                  " contactLength=" + tether.contactLengthM.ToString("F3") +
                  " maxPenetration=" + tether.maxContactPenetrationM.ToString("F4") +
                  " lowestClearance=" + tether.lowestNodeClearanceM.ToString("F4") +
                  " source=" + tether.contactSurfaceSource +
                  " applyForces=" + tether.applyForces +
                  " readOnly=" + tether.readOnlyDoNotChangeOriginalLogic);

        MIMISKFinalRearAttachedTetherV8CsvLogger logger = Object.FindFirstObjectByType<MIMISKFinalRearAttachedTetherV8CsvLogger>();
        if (logger != null)
        {
            Debug.Log("[MIMISK Tether V9] Logger enabled=" + logger.enableLogging + " file=" + logger.currentFilePath);
        }
    }

    [MenuItem("MIMISK/Tether/V9 Contact OFF - Visual Only")]
    public static void DisableContact()
    {
        MIMISKFinalRearAttachedTetherV8 tether = Object.FindFirstObjectByType<MIMISKFinalRearAttachedTetherV8>();
        if (tether == null) return;
        Undo.RecordObject(tether, "Disable V9 Tether Contact");
        tether.enableContactProjection = false;
        EditorUtility.SetDirty(tether);
    }

    [MenuItem("MIMISK/Tether/V9 Contact ON - Anti Cut")]
    public static void EnableContact()
    {
        MIMISKFinalRearAttachedTetherV8 tether = Object.FindFirstObjectByType<MIMISKFinalRearAttachedTetherV8>();
        if (tether == null) return;
        Undo.RecordObject(tether, "Enable V9 Tether Contact");
        ConfigureContact(tether);
        EditorUtility.SetDirty(tether);
    }

    private static void ConfigureContact(MIMISKFinalRearAttachedTetherV8 tether)
    {
        tether.readOnlyDoNotChangeOriginalLogic = true;
        tether.applyForces = false;
        tether.autoFindReferences = true;

        tether.simulateCable = true;
        tether.minimumNodeCount = 8;
        tether.maximumNodeCount = 64;
        tether.targetSegmentLengthM = 0.18f;
        tether.constraintIterations = 32;
        tether.physicsSubsteps = 1;
        tether.performanceSafeMode = true;
        tether.projectContactDuringConstraintIterations = false;
        tether.contactFixedUpdateStride = 4;
        tether.suppressLegacyVisualsOnceOnly = true;
        tether.legacyVisualSuppressionIntervalS = 2.0f;
        tether.verletVelocityDamping = 0.965f;
        tether.nodeMaxSpeedMS = 3.0f;
        tether.internalDrag = 0.60f;

        tether.enableContactProjection = true;
        tether.usePhysicsRaycastContact = false;
        tether.useTerrainHeightContact = true;
        tether.useFallbackSeabedPlane = true;
        tether.fallbackSeabedY = -2.05f;
        tether.contactSkinM = 0.035f;
        tether.contactActiveClearanceM = 0.025f;
        tether.contactOnlyBelowWater = true;
        tether.contactWaterSurfaceMarginM = 0.05f;
        tether.contactRaycastAboveM = 6.0f;
        tether.contactRaycastBelowM = 12.0f;
        tether.contactLayerMask = -1;
        tether.contactTriggerInteraction = QueryTriggerInteraction.Ignore;
        tether.useContactNameIgnoreFilter = true;
        tether.contactIgnoredNameKeywords = "water,ocean,surface,global volume,tether,cable,rope,winch,hook,drone,minirov,camera,light,kelp,plant,grass,leaf,algae,coral";
        tether.contactVelocityDamping = 0.85f;

        tether.minimumVisualSlackM = 0.08f;
        tether.maximumVisualSlackM = 0.45f;
        tether.allowVisualLengthExtensionForEndpointSync = true;
        tether.useWeakCatenaryGuide = true;
        tether.catenaryGuideStrength = 0.14f;
        tether.slackToSagScale = 0.30f;
        tether.maximumSagM = 0.32f;
        tether.maximumLateralCurrentBendM = 0.06f;
        tether.currentBendPerSlackM = 0.04f;

        tether.renderCable = true;
        tether.radialSegments = 8;
        tether.visualRadiusM = 0.0040f;
        tether.minimumVisualRadiusM = 0.0028f;
        tether.smoothRenderPath = true;
        tether.renderSubdivisionsPerSegment = 2;
        tether.renderSmoothingPasses = 1;
        tether.renderSmoothingStrength = 0.08f;
        tether.copyWinchCableMaterial = true;
        tether.forceMatteMaterial = true;
        tether.disableEmission = true;
        tether.suppressLegacyVisuals = true;
        tether.suppressEveryFrame = false;
        tether.disableLegacyVisualComponents = true;
        tether.disableLegacyRenderers = true;
        tether.keepCadWinchCableVisible = true;
        tether.keepHookVisible = true;
    }

    private static string F(Vector3 v)
    {
        return "(" + v.x.ToString("F3") + "," + v.y.ToString("F3") + "," + v.z.ToString("F3") + ")";
    }
}
#endif
