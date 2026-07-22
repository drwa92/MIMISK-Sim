using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMiniROVControllerLoggerSetup
{
    [MenuItem("MIMISK/MiniROV/Setup Controller Logger")]
    public static void SetupControllerLogger()
    {
        GameObject miniRov =
            GameObject.Find("MiniROV");

        if (miniRov == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV.");
            return;
        }

        MIMISKMiniROVControllerLogger logger =
            miniRov.GetComponent<MIMISKMiniROVControllerLogger>();

        if (logger == null)
        {
            logger =
                miniRov.AddComponent<MIMISKMiniROVControllerLogger>();
        }

        logger.controller =
            miniRov.GetComponent<MIMISKMiniROVPlantBasedController>();

        logger.controlManager =
            miniRov.GetComponent<ControlManager>();

        logger.rb =
            miniRov.GetComponent<Rigidbody>();

        logger.autoStartOnPlay = false;
        logger.logOnlyWhenControllerEnabled = true;
        logger.logRateHz = 50.0f;
        logger.testName = "minirov_unity_controller_test";
        logger.logSubfolder = "Logs/MiniROV";

        EditorUtility.SetDirty(logger);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] MiniROV controller logger configured.");
    }
}
