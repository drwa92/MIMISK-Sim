using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMissionDashboardGadgetsSetup
{
    [MenuItem("MIMISK/UI/Setup Dashboard Gauges Timeline Map And Selectors")]
    public static void SetupDashboardGadgets()
    {
        MIMISKMissionControlDashboard dashboard =
            Object.FindFirstObjectByType<MIMISKMissionControlDashboard>();

        if (dashboard == null)
        {
            Debug.LogError("[MIMISK] No MIMISKMissionControlDashboard found. Run Setup Mission Control Dashboard first.");
            return;
        }

        GameObject root =
            dashboard.gameObject;

        MIMISKMissionDashboardGadgets gadgets =
            root.GetComponent<MIMISKMissionDashboardGadgets>();

        if (gadgets == null)
        {
            gadgets =
                root.AddComponent<MIMISKMissionDashboardGadgets>();
        }

        gadgets.dashboard = dashboard;
        gadgets.dashboardCanvas = dashboard.canvas;

        gadgets.gadgetsEnabled = true;
        gadgets.buildOnStart = true;
        gadgets.autoFindOnStart = true;
        gadgets.autoFindPeriodically = true;
        gadgets.autoFindPeriodS = 2.0f;

        gadgets.fontScale = 1.06f;
        gadgets.useTextShadow = true;

        gadgets.coverOldBottomTelemetry = true;
        gadgets.coverOldCameraGap = true;

        gadgets.enableTopDownMap = true;
        gadgets.mapTextureWidth = 768;
        gadgets.mapTextureHeight = 512;
        gadgets.mapHeightM = 35.0f;
        gadgets.mapOrthographicSizeM = 8.0f;
        gadgets.mapFollowRovAndDroneMidpoint = true;

        gadgets.AutoFindReferences();
        gadgets.BuildGadgets();

        EditorUtility.SetDirty(gadgets);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Dashboard gauges, mission timeline, map, and mission selectors configured.");
    }
}
