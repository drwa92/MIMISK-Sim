using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMiniROVRuntimeCsvLoggerSetup
{
    [MenuItem("MIMISK/MiniROV/Setup Auto Runtime CSV Logger")]
    public static void SetupAutoRuntimeCsvLogger()
    {
        GameObject miniRov = GameObject.Find("MiniROV");

        if (miniRov == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV.");
            return;
        }

        MIMISKMiniROVRuntimeCsvLogger logger =
            miniRov.GetComponent<MIMISKMiniROVRuntimeCsvLogger>();

        if (logger == null)
        {
            logger = miniRov.AddComponent<MIMISKMiniROVRuntimeCsvLogger>();
        }

        logger.rb = miniRov.GetComponent<Rigidbody>();
        logger.controlManager = miniRov.GetComponent<ControlManager>();
        logger.unityVirtualESP32 = miniRov.GetComponent<UnityVirtualESP32>();
        logger.plantBasedController = miniRov.GetComponent<MIMISKMiniROVPlantBasedController>();
        logger.directBypassInput = miniRov.GetComponent<MIMISKMiniROVDirectRaspberryBypassInput>();
        logger.pathPlanner = miniRov.GetComponent<MIMISKMiniROVPathPlanner>();
        logger.missionManager = miniRov.GetComponent<MIMISKMiniROVMissionManager>();

        logger.autoStartOnPlay = true;
        logger.logProfile = MIMISKMiniROVRuntimeCsvLogger.LogProfile.Lean;
        logger.logRateHz = 10.0f;
        logger.maxLogDurationS = 0.0f;
        logger.flushEverySamples = 200;
        logger.testName = "minirov_runtime";
        logger.logSubfolder = "Logs/MiniROV";
        logger.waterLevelY = 0.0f;

        logger.AutoFindReferences();

        EditorUtility.SetDirty(logger);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] MiniROV lean auto runtime CSV logger configured. It starts automatically on Play at 10 Hz.");
    }
}
