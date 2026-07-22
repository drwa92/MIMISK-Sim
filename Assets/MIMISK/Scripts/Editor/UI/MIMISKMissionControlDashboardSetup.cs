using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMissionControlDashboardSetup
{
    [MenuItem("MIMISK/UI/Setup Mission Control Dashboard")]
    public static void SetupMissionControlDashboard()
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
            Debug.LogError("[MIMISK] Select the MIMISK_AerialAquaticSystem / Drone root first.");
            return;
        }

        MIMISKMissionControlDashboard dashboard =
            root.GetComponent<MIMISKMissionControlDashboard>();

        if (dashboard == null)
        {
            dashboard =
                root.AddComponent<MIMISKMissionControlDashboard>();
        }

        dashboard.dashboardEnabled = true;
        dashboard.buildOnStart = true;
        dashboard.autoFindOnStart = true;
        dashboard.autoFindPeriodically = true;
        dashboard.autoFindPeriodS = 2.0f;

        dashboard.AutoFindReferences();
        dashboard.RebuildDashboard();

        EditorUtility.SetDirty(dashboard);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Mission Control Dashboard configured on " + root.name + ".");
    }
}
