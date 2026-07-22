using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKTetherPhase1VisualFinalizeSetup
{
    [MenuItem("MIMISK/Tether/Phase 1 Finalize Single Yellow Cable Visual")]
    public static void FinalizeSingleYellowCableVisual()
    {
        GameObject root =
            Selection.activeGameObject;

        if (root == null)
        {
            MIMISKUnifiedTetherManager unified =
                Object.FindFirstObjectByType<MIMISKUnifiedTetherManager>();

            if (unified != null)
            {
                root = unified.gameObject;
            }
        }

        if (root == null)
        {
            Debug.LogError("[MIMISK] Select the Drone / MIMISK_AerialAquaticSystem root first.");
            return;
        }

        MIMISKSingleYellowTetherVisualAuthority visual =
            root.GetComponent<MIMISKSingleYellowTetherVisualAuthority>();

        if (visual == null)
        {
            visual =
                root.AddComponent<MIMISKSingleYellowTetherVisualAuthority>();
        }

        visual.unifiedTether =
            root.GetComponent<MIMISKUnifiedTetherManager>();

        visual.tetherManager =
            root.GetComponent<MIMISKDroneCoreTetherManager>();

        visual.visualAuthorityEnabled = true;
        visual.disableDuplicateVisualComponents = true;
        visual.deactivateDuplicateCableObjects = true;
        visual.hideOtherTetherLineRenderers = true;
        visual.hideShortYellowCableMeshRenderer = true;

        visual.preferRearMiniRovTetherPoint = true;
        visual.rearMiniRovTetherPointName = "MiniROV_TetherPoint";
        visual.attachVisibleCableToMiniRovPointWheneverAvailable = true;

        visual.forceVisualCableLengthAtLeastStraightDistance = true;
        visual.visualMinimumSlackM = 0.05f;

        visual.lineSegments = 56;
        visual.lineWidthM = 0.014f;

        visual.minimumSagM = 0.005f;
        visual.slackSagGain = 0.10f;
        visual.maxSagM = 0.18f;

        visual.useContinuousWaterEntryCableShape = true;
        visual.forceWaterEntryShapeWhenDynamic = true;
        visual.waterEntryBelowSurfaceM = 0.22f;
        visual.minimumVisibleWaterEntryDepthM = 0.22f;
        visual.surfaceEntryTowardRovFraction = 0.05f;

        visual.underwaterSagGain = 0.12f;
        visual.underwaterMaxSagM = 0.30f;
        visual.underwaterMinimumSideCurveM = 0.05f;

        visual.enableCurrentDeflection = true;
        visual.currentDirectionWorld = new Vector3(1.0f, 0.0f, 0.20f);
        visual.currentDeflectionGain = 0.06f;
        visual.maxCurrentDeflectionM = 0.18f;

        visual.enableSmallWaveMotion = true;
        visual.waveAmplitudeM = 0.006f;
        visual.waveSpatialFrequency = 1.6f;
        visual.waveTemporalFrequency = 0.35f;

        visual.forceValidYellowMaterial = true;
        visual.preferredYellowMaterialName = "yellow_winch_cable_rope";
        visual.fallbackYellowCableColor = new Color(1.0f, 0.78f, 0.05f, 1.0f);

        visual.AutoFindReferences();
        visual.ConfigureSingleCableVisual();

        MIMISKDroneCoreTetherManager tether =
            root.GetComponent<MIMISKDroneCoreTetherManager>();

        if (tether != null)
        {
            if (visual.primaryLine != null)
            {
                tether.tetherLineRenderer =
                    visual.primaryLine;
            }

            if (visual.fairlead != null)
            {
                tether.fairleadLineStart =
                    visual.fairlead;
            }

            if (visual.yellowCableEnd != null)
            {
                tether.movingTetherEndVisual =
                    visual.yellowCableEnd;
            }

            tether.hideStaticShortCableMeshWhenDynamic = false;
            tether.staticShortDeploymentCableMesh = null;

            EditorUtility.SetDirty(tether);
        }

        EditorUtility.SetDirty(visual);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Phase 1 visual finalized: single yellow winch cable, rear ROV tether point, continuous water-entry shape.");
    }
}
