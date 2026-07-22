using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKTetherCableModelSetup
{
    [MenuItem("MIMISK/Tether/Setup Realistic Cable Model")]
    public static void SetupRealisticCableModel()
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

        MIMISKTetherCableModel model =
            root.GetComponent<MIMISKTetherCableModel>();

        if (model == null)
        {
            model =
                root.AddComponent<MIMISKTetherCableModel>();
        }

        model.unifiedTether =
            root.GetComponent<MIMISKUnifiedTetherManager>();

        model.tetherManager =
            root.GetComponent<MIMISKDroneCoreTetherManager>();

        if (model.tetherManager != null)
        {
            model.debugLineRenderer =
                model.tetherManager.tetherLineRenderer;
        }

        model.cableModelEnabled = true;
        model.curveSegments = 40;
        model.radialSegments = 8;
        model.cableRadiusM = 0.009f;

        model.slackSagGain = 0.45f;
        model.maxSagM = 0.65f;
        model.minimumVisualSagM = 0.015f;

        model.enableCurrentDeflection = true;
        model.currentDirectionWorld = new Vector3(1.0f, 0.0f, 0.25f);
        model.currentDeflectionGain = 0.12f;

        model.enableSmallWaveMotion = true;
        model.waveAmplitudeM = 0.012f;
        model.waveSpatialFrequency = 2.0f;
        model.waveTemporalFrequency = 0.50f;

        model.driveDebugLineRenderer = true;
        model.colorByTetherState = true;

        model.AutoFindReferences();

        if (model.cableMeshRenderer != null && model.cableMeshRenderer.sharedMaterial == null)
        {
            string materialDir =
                "Assets/MIMISK/Materials";

            if (!AssetDatabase.IsValidFolder("Assets/MIMISK"))
            {
                AssetDatabase.CreateFolder("Assets", "MIMISK");
            }

            if (!AssetDatabase.IsValidFolder(materialDir))
            {
                AssetDatabase.CreateFolder("Assets/MIMISK", "Materials");
            }

            string materialPath =
                materialDir + "/MIMISK_RealisticTetherCable.mat";

            Material mat =
                AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (mat == null)
            {
                Shader shader =
                    Shader.Find("Universal Render Pipeline/Lit");

                if (shader == null)
                {
                    shader =
                        Shader.Find("Standard");
                }

                mat =
                    new Material(shader);

                mat.name =
                    "MIMISK_RealisticTetherCable";

                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", model.semiTautColor);
                }

                if (mat.HasProperty("_Color"))
                {
                    mat.SetColor("_Color", model.semiTautColor);
                }

                AssetDatabase.CreateAsset(mat, materialPath);
            }

            model.cableMeshRenderer.sharedMaterial =
                mat;
        }

        EditorUtility.SetDirty(model);

        if (model.cableMeshRenderer != null)
        {
            EditorUtility.SetDirty(model.cableMeshRenderer);
        }

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Realistic tether cable model configured on " + root.name + ".");
    }
}
