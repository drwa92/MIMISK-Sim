using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKTetherVisualAttachmentTuningSetup
{
    [MenuItem("MIMISK/Tether/Tune Visual Rear Attachment And Recovered Pose")]
    public static void TuneVisualRearAttachmentAndRecoveredPose()
    {
        GameObject root =
            Selection.activeGameObject;

        if (root == null)
        {
            MIMISKUnifiedTetherManager any =
                Object.FindFirstObjectByType<MIMISKUnifiedTetherManager>();

            if (any != null)
            {
                root = any.gameObject;
            }
        }

        if (root == null)
        {
            Debug.LogError("[MIMISK] Select the drone/tether root first.");
            return;
        }

        MIMISKUnifiedTetherManager unified =
            root.GetComponent<MIMISKUnifiedTetherManager>();

        MIMISKDroneCoreTetherManager lowLevel =
            root.GetComponent<MIMISKDroneCoreTetherManager>();

        MIMISKSingleYellowTetherVisualAuthority visual =
            root.GetComponent<MIMISKSingleYellowTetherVisualAuthority>();

        if (visual == null)
        {
            visual =
                root.AddComponent<MIMISKSingleYellowTetherVisualAuthority>();
        }

        if (unified != null)
        {
            unified.miniRovTetherAnchorName = "MiniROV_TetherPoint";

            // Keep the ROV slightly lower while attached/recovered so it is visible.
            // This is only the cable-managed pose. Dynamic ROV control is unchanged.
            unified.miniRovLocalOffsetOnCableEnd =
                new Vector3(0.0f, -0.18f, 0.0f);

            // For visual clarity, do not force the anchor to exactly cancel the local offset.
            // The visible cable authority attaches to MiniROV_TetherPoint directly.
            unified.alignRovTetherAnchorToCableEnd = false;

            // More generous tether length for visual and future TMS operation.
            unified.maximumLengthM = Mathf.Max(unified.maximumLengthM, 20.0f);
            unified.desiredOperationalSlackM = Mathf.Max(unified.desiredOperationalSlackM, 0.35f);

            unified.AutoFindReferences();
            unified.ConfigureAuthoritativeDefaults();

            EditorUtility.SetDirty(unified);
        }

        if (lowLevel != null)
        {
            lowLevel.maximumLengthM = Mathf.Max(lowLevel.maximumLengthM, 20.0f);
            lowLevel.targetDeployLengthM = Mathf.Max(lowLevel.targetDeployLengthM, 1.25f);
            lowLevel.tetherLineWidthM = Mathf.Max(lowLevel.tetherLineWidthM, 0.012f);

            EditorUtility.SetDirty(lowLevel);
        }

        visual.preferRearMiniRovTetherPoint = true;
        visual.rearMiniRovTetherPointName = "MiniROV_TetherPoint";
        visual.attachVisibleCableToMiniRovPointWheneverAvailable = true;
        visual.miniRovTetherPointLocalVisualOffset = Vector3.zero;

        visual.forceVisualCableLengthAtLeastStraightDistance = true;
        visual.visualMinimumSlackM = 0.35f;

        visual.useContinuousWaterEntryCableShape = true;
        visual.underwaterMinimumSideCurveM = Mathf.Max(visual.underwaterMinimumSideCurveM, 0.12f);
        visual.underwaterSagGain = Mathf.Max(visual.underwaterSagGain, 0.28f);

        visual.AutoFindReferences();
        visual.ConfigureSingleCableVisual();

        EditorUtility.SetDirty(visual);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Tuned tether visual rear attachment to MiniROV_TetherPoint and lowered recovered/attached MiniROV pose.");
    }
}
