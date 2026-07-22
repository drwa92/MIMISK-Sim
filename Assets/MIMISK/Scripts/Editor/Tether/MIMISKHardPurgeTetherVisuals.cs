using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKHardPurgeTetherVisuals
{
    [MenuItem("MIMISK/Tether/Hard Purge Duplicate Tether Visuals")]
    public static void HardPurgeDuplicateTetherVisuals()
    {
        string[] duplicateObjectNames =
        {
            "MIMISK_RealisticTetherCableMesh",
            "MIMISK_FinalActiveYellowTether",
            "MIMISK_Final_ActiveYellowTether",
            "MIMISK_Final_ContinuousYellowTether",
            "MIMISK_Final_ActiveYellowTetherLine",
            "MIMISK_Final_ContinuousYellowTetherLine",
            "MIMISK_Final_TetherCable",
            "MIMISK_FinalCable",
            "MIMISK_ContinuousYellowTether",
            "MIMISK_FinalDeploymentPoint"
        };

        string[] duplicateComponentTypeNames =
        {
            "MIMISKWinchRopeSingleVisual",
            "MIMISKTetherCableModel",
            "MIMISKFinalTetherEndpointRig",
            "MIMISKFinalTetherLineSynchronizer",
            "MIMISKMiniROVTetherVisualBridge",
            "MIMISKFinalContinuousTetherVisual",
            "MIMISKFinalTetherVisualBridge"
        };

        int destroyed = 0;
        int disabled = 0;

        GameObject[] allObjects =
            Resources.FindObjectsOfTypeAll<GameObject>();

        for (int i = allObjects.Length - 1; i >= 0; i--)
        {
            GameObject go = allObjects[i];

            if (go == null)
            {
                continue;
            }

            if (!go.scene.IsValid())
            {
                continue;
            }

            for (int j = 0; j < duplicateObjectNames.Length; j++)
            {
                if (go.name == duplicateObjectNames[j])
                {
                    Undo.DestroyObjectImmediate(go);
                    destroyed++;
                    break;
                }
            }
        }

        MonoBehaviour[] behaviours =
            Resources.FindObjectsOfTypeAll<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour b = behaviours[i];

            if (b == null || !b.gameObject.scene.IsValid())
            {
                continue;
            }

            string typeName = b.GetType().Name;

            for (int j = 0; j < duplicateComponentTypeNames.Length; j++)
            {
                if (typeName == duplicateComponentTypeNames[j])
                {
                    b.enabled = false;
                    EditorUtility.SetDirty(b);
                    disabled++;
                    break;
                }
            }
        }

        MIMISKDroneCoreTetherManager tether =
            Object.FindFirstObjectByType<MIMISKDroneCoreTetherManager>();

        if (tether != null)
        {
            GameObject root = tether.gameObject;

            MIMISKSingleYellowTetherVisualAuthority authority =
                root.GetComponent<MIMISKSingleYellowTetherVisualAuthority>();

            if (authority == null)
            {
                authority =
                    root.AddComponent<MIMISKSingleYellowTetherVisualAuthority>();
            }

            authority.visualAuthorityEnabled = true;
            authority.disableDuplicateVisualComponents = true;
            authority.deactivateDuplicateCableObjects = true;
            authority.hideOtherTetherLineRenderers = true;
            authority.hideShortYellowCableMeshRenderer = true;

            authority.unifiedTether =
                root.GetComponent<MIMISKUnifiedTetherManager>();

            authority.tetherManager =
                tether;

            authority.AutoFindReferences();
            authority.ConfigureSingleCableVisual();

            if (authority.primaryLine != null)
            {
                tether.tetherLineRenderer =
                    authority.primaryLine;
            }

            if (authority.fairlead != null)
            {
                tether.fairleadLineStart =
                    authority.fairlead;
            }

            if (authority.yellowCableEnd != null)
            {
                tether.movingTetherEndVisual =
                    authority.yellowCableEnd;
            }

            tether.hideStaticShortCableMeshWhenDynamic = false;
            tether.staticShortDeploymentCableMesh = null;

            EditorUtility.SetDirty(tether);
            EditorUtility.SetDirty(authority);
        }

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MIMISK] Hard purge completed. Destroyed duplicate visual objects=" +
            destroyed +
            ", disabled duplicate visual components=" +
            disabled +
            ". Final visible deployed cable is MiniROV_ActiveYellowTetherLine only."
        );
    }
}
