#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class MIMISKFinalRearAttachedTetherV9_1Setup
{
    [MenuItem("MIMISK/Tether/V9.1 Restore Fast Safe Tether (Recommended)")]
    public static void RestoreFastSafeTether()
    {
        MIMISKFinalRearAttachedTetherV8 tether = Object.FindFirstObjectByType<MIMISKFinalRearAttachedTetherV8>();
        if (tether == null)
        {
            EditorUtility.DisplayDialog("MIMISK Tether V9.1", "No V8/V9 tether component found. Run the V8 or V9 installer first.", "OK");
            return;
        }

        Undo.RecordObject(tether, "MIMISK V9.1 Restore Fast Safe Tether");
        ConfigureFastSafe(tether, contactEnabled: true);
        tether.SuppressLegacyVisualsNow();
        tether.RebuildCableNow();
        EditorUtility.SetDirty(tether);

        MIMISKFinalRearAttachedTetherV8CsvLogger logger = tether.GetComponent<MIMISKFinalRearAttachedTetherV8CsvLogger>();
        if (logger != null)
        {
            Undo.RecordObject(logger, "MIMISK V9.1 Logger Rate");
            logger.sampleIntervalS = 0.10f;
            EditorUtility.SetDirty(logger);
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(tether.gameObject.scene);
        EditorUtility.DisplayDialog("MIMISK Tether V9.1", "Restored the performance-safe tether configuration. Contact is terrain/plane based, raycasts are off, contact is throttled, old visual scans are not repeated every frame, and the original MiniROV/drone logic remains read-only.", "OK");
    }

    [MenuItem("MIMISK/Tether/V9.1 Emergency Visual-Only Restore Navigation")]
    public static void EmergencyVisualOnlyRestoreNavigation()
    {
        MIMISKFinalRearAttachedTetherV8 tether = Object.FindFirstObjectByType<MIMISKFinalRearAttachedTetherV8>();
        if (tether == null)
        {
            EditorUtility.DisplayDialog("MIMISK Tether V9.1", "No V8/V9 tether component found.", "OK");
            return;
        }

        Undo.RecordObject(tether, "MIMISK V9.1 Emergency Visual Only");
        ConfigureFastSafe(tether, contactEnabled: false);
        tether.SuppressLegacyVisualsNow();
        tether.RebuildCableNow();
        EditorUtility.SetDirty(tether);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(tether.gameObject.scene);

        EditorUtility.DisplayDialog("MIMISK Tether V9.1", "Contact projection is now OFF. This restores V8-style navigation performance while keeping the final rear-attached runtime cable visible. Use this if planner/control responsiveness is still degraded.", "OK");
    }

    [MenuItem("MIMISK/Tether/V9.1 Diagnose Performance Safe Tether")]
    public static void DiagnoseV91()
    {
        MIMISKFinalRearAttachedTetherV8 tether = Object.FindFirstObjectByType<MIMISKFinalRearAttachedTetherV8>();
        if (tether == null)
        {
            Debug.LogWarning("[MIMISK Tether V9.1] No V8/V9 tether component found.");
            return;
        }

        Debug.Log("[MIMISK Tether V9.1] endpoint=" + tether.endpointModeText +
                  " visualLength=" + tether.visualCableLengthM.ToString("F3") +
                  " geomLength=" + tether.geometricCableLengthM.ToString("F3") +
                  " slack=" + tether.slackM.ToString("F3") +
                  " nodes=" + tether.runtimeNodeCount +
                  " iterations=" + tether.constraintIterations +
                  " substeps=" + tether.physicsSubsteps +
                  " radial=" + tether.radialSegments +
                  " renderSubdiv=" + tether.renderSubdivisionsPerSegment +
                  " perfSafe=" + tether.performanceSafeMode +
                  " contact=" + tether.enableContactProjection +
                  " raycast=" + tether.usePhysicsRaycastContact +
                  " terrain=" + tether.useTerrainHeightContact +
                  " fallbackPlane=" + tether.useFallbackSeabedPlane +
                  " contactStride=" + tether.contactFixedUpdateStride +
                  " contactDuringIterations=" + tether.projectContactDuringConstraintIterations +
                  " suppressEveryFrame=" + tether.suppressEveryFrame +
                  " suppressOnceOnly=" + tether.suppressLegacyVisualsOnceOnly +
                  " applyForces=" + tether.applyForces +
                  " readOnly=" + tether.readOnlyDoNotChangeOriginalLogic +
                  " contactSource=" + tether.contactSurfaceSource);
    }

    private static void ConfigureFastSafe(MIMISKFinalRearAttachedTetherV8 tether, bool contactEnabled)
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
        tether.verletVelocityDamping = 0.965f;
        tether.nodeMaxSpeedMS = 3.0f;
        tether.internalDrag = 0.60f;

        tether.performanceSafeMode = true;
        tether.enableContactProjection = contactEnabled;
        tether.projectContactDuringConstraintIterations = false;
        tether.contactFixedUpdateStride = 4;
        tether.usePhysicsRaycastContact = false;
        tether.useTerrainHeightContact = true;
        tether.useFallbackSeabedPlane = true;
        tether.fallbackSeabedY = -2.05f;
        tether.contactSkinM = 0.035f;
        tether.contactActiveClearanceM = 0.025f;
        tether.contactOnlyBelowWater = true;
        tether.contactVelocityDamping = 0.50f;

        tether.minimumVisualSlackM = 0.08f;
        tether.maximumVisualSlackM = 0.45f;
        tether.allowVisualLengthExtensionForEndpointSync = true;
        tether.useWeakCatenaryGuide = true;
        tether.catenaryGuideStrength = 0.12f;
        tether.slackToSagScale = 0.30f;
        tether.maximumSagM = 0.30f;
        tether.maximumLateralCurrentBendM = 0.05f;
        tether.currentBendPerSlackM = 0.035f;

        tether.renderCable = true;
        tether.radialSegments = 8;
        tether.visualRadiusM = 0.0040f;
        tether.minimumVisualRadiusM = 0.0028f;
        tether.smoothRenderPath = true;
        tether.renderSubdivisionsPerSegment = 2;
        tether.renderSmoothingPasses = 1;
        tether.renderSmoothingStrength = 0.06f;

        tether.suppressLegacyVisuals = true;
        tether.suppressEveryFrame = false;
        tether.suppressLegacyVisualsOnceOnly = true;
        tether.legacyVisualSuppressionIntervalS = 2.0f;
        tether.disableLegacyVisualComponents = true;
        tether.disableLegacyRenderers = true;
        tether.keepCadWinchCableVisible = true;
        tether.keepHookVisible = true;
    }
}
#endif
