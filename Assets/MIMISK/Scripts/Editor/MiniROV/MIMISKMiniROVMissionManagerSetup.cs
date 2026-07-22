using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMiniROVMissionManagerSetup
{
    [MenuItem("MIMISK/MiniROV/Setup Mission Manager")]
    public static void SetupMissionManager()
    {
        GameObject miniRov =
            GameObject.Find("MiniROV");

        if (miniRov == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV.");
            return;
        }

        MIMISKMiniROVPathPlanner planner =
            miniRov.GetComponent<MIMISKMiniROVPathPlanner>();

        if (planner == null)
        {
            planner =
                miniRov.AddComponent<MIMISKMiniROVPathPlanner>();
        }

        MIMISKMiniROVMissionManager manager =
            miniRov.GetComponent<MIMISKMiniROVMissionManager>();

        if (manager == null)
        {
            manager =
                miniRov.AddComponent<MIMISKMiniROVMissionManager>();
        }

        manager.pathPlanner = planner;
        manager.controller =
            miniRov.GetComponent<MIMISKMiniROVPlantBasedController>();

        manager.directBypassInput =
            miniRov.GetComponent<MIMISKMiniROVDirectRaspberryBypassInput>();

        manager.rb =
            miniRov.GetComponent<Rigidbody>();

        manager.missionManagerEnabled = true;
        manager.usePathPlannerSelectedPath = true;
        manager.recoveryReadyRadiusM = 0.25f;
        manager.recoveryReadySpeedMS = 0.05f;

        manager.dwellSeconds = 3.0f;
        manager.dwellYawPolicy =
            MIMISKMiniROVMissionManager.DwellYawPolicy.CurrentYaw;
        manager.dwellFixedYawDeg = 0.0f;
        manager.dwellRequireSettleBeforeTimer = true;
        manager.dwellStationToleranceM = 0.08f;
        manager.dwellYawToleranceDeg = 15.0f;
        manager.dwellSpeedToleranceMS = 0.05f;
        manager.dwellMaxSettleSeconds = 20.0f;

        manager.stopLookRequireSettleBeforeDwell = true;
        manager.stopLookStationToleranceM = 0.08f;
        manager.stopLookYawToleranceDeg = 15.0f;
        manager.stopLookSpeedToleranceMS = 0.05f;
        manager.stopLookTightenStationHoldDeadband = true;
        manager.stopLookInspectionDeadbandM = 0.04f;
        manager.stopLookProceedAfterSettleTimeout = false;
        manager.stopLookSkipUnsettledWaypointOnTimeout = true;
        manager.stopLookPauseDwellIfUnsettled = false;
        manager.stopLookMaxSettleSeconds = 20.0f;
        manager.stopLookMaxDwellPauseSeconds = 8.0f;

        manager.AutoFindReferences();

        EditorUtility.SetDirty(manager);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] MiniROV mission manager configured.");
    }
}
