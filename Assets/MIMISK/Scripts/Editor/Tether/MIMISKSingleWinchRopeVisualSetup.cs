using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKSingleWinchRopeVisualSetup
{
    [MenuItem("MIMISK/Tether/Setup Single Yellow Winch Rope Visual")]
    public static void SetupSingleYellowWinchRopeVisual()
    {
        GameObject root =
            Selection.activeGameObject;

        if (root == null)
        {
            MIMISKDroneCoreTetherManager tether =
                Object.FindFirstObjectByType<MIMISKDroneCoreTetherManager>();

            if (tether != null)
            {
                root = tether.gameObject;
            }
        }

        if (root == null)
        {
            Debug.LogError("[MIMISK] Select the Drone / tether root first.");
            return;
        }

        // Disable old duplicate visual-only helpers. The unified tether manager stays active.
        DisableByTypeName(root, "MIMISKTetherCableModel");
        DisableByTypeName(root, "MIMISKFinalTetherEndpointRig");
        DisableByTypeName(root, "MIMISKFinalTetherLineSynchronizer");

        MIMISKWinchRopeSingleVisual visual =
            root.GetComponent<MIMISKWinchRopeSingleVisual>();

        if (visual == null)
        {
            visual =
                root.AddComponent<MIMISKWinchRopeSingleVisual>();
        }

        visual.unifiedTether =
            root.GetComponent<MIMISKUnifiedTetherManager>();

        visual.tetherManager =
            root.GetComponent<MIMISKDroneCoreTetherManager>();

        visual.visualEnabled = true;
        visual.hideDuplicateCableVisuals = true;
        visual.drivePrimaryLineRenderer = true;
        visual.useWinchRopeMaterial = true;

        visual.lineSegments = 44;
        visual.lineWidthM = 0.014f;

        visual.slackSagGain = 0.35f;
        visual.maxSagM = 0.55f;
        visual.minimumSagM = 0.015f;

        visual.enableCurrentDeflection = true;
        visual.currentDirectionWorld = new Vector3(1.0f, 0.0f, 0.20f);
        visual.currentDeflectionGain = 0.10f;
        visual.maxCurrentDeflectionM = 0.30f;

        visual.enableSmallWaveMotion = true;
        visual.waveAmplitudeM = 0.010f;
        visual.waveSpatialFrequency = 1.8f;
        visual.waveTemporalFrequency = 0.45f;

        visual.AutoFindReferences();
        visual.ConfigureAsSingleWinchRopeVisual();

        if (visual.tetherManager != null)
        {
            if (visual.fairleadStart != null)
            {
                visual.tetherManager.fairleadLineStart =
                    visual.fairleadStart;
            }

            if (visual.primaryLineRenderer != null)
            {
                visual.tetherManager.tetherLineRenderer =
                    visual.primaryLineRenderer;
            }

            if (visual.shortYellowDeploymentCable != null)
            {
                visual.tetherManager.movingTetherEndVisual =
                    visual.shortYellowDeploymentCable;
            }

            visual.tetherManager.hideStaticShortCableMeshWhenDynamic = false;
            visual.tetherManager.staticShortDeploymentCableMesh = null;
            visual.tetherManager.moveHookVisualWithTether = true;

            EditorUtility.SetDirty(visual.tetherManager);
        }

        if (visual.unifiedTether != null)
        {
            if (visual.fairleadStart != null)
            {
                visual.unifiedTether.fairleadLineStart =
                    visual.fairleadStart;
            }

            if (visual.shortYellowDeploymentCable != null)
            {
                visual.unifiedTether.yellowCableEndPoint =
                    visual.shortYellowDeploymentCable;
            }

            if (visual.hookVisual != null)
            {
                visual.unifiedTether.hookVisual =
                    visual.hookVisual;
            }

            if (visual.primaryLineRenderer != null)
            {
                visual.unifiedTether.activeYellowLine =
                    visual.primaryLineRenderer;
            }

            EditorUtility.SetDirty(visual.unifiedTether);
        }

        EditorUtility.SetDirty(visual);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Single yellow winch rope visual configured. Duplicate cable visuals disabled.");
    }

    private static void DisableByTypeName(GameObject root, string typeName)
    {
        Component[] components =
            root.GetComponents<Component>();

        for (int i = 0; i < components.Length; i++)
        {
            Component c =
                components[i];

            if (c == null)
            {
                continue;
            }

            if (c.GetType().Name == typeName)
            {
                Behaviour b =
                    c as Behaviour;

                if (b != null)
                {
                    b.enabled = false;
                    EditorUtility.SetDirty(b);
                }
            }
        }
    }
}
