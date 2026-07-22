using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMissionControlCameraSwitchingSetup
{
    [MenuItem("MIMISK/UI/Setup Dashboard Camera Switching")]
    public static void SetupDashboardCameraSwitching()
    {
        MIMISKMissionControlDashboard dashboard =
            Object.FindFirstObjectByType<MIMISKMissionControlDashboard>();

        if (dashboard == null)
        {
            Debug.LogError("[MIMISK] No MIMISKMissionControlDashboard found. Run Setup Mission Control Dashboard first.");
            return;
        }

        dashboard.droneFollowCameraName = "DroneFollowCamera";
        dashboard.dronePayloadCameraName = "DroneCamera";
        dashboard.rovFollowCameraName = "FollowCamera";
        dashboard.rovFrontCameraName = "FrontCamera";

        dashboard.droneFollowCamera = FindCamera("DroneFollowCamera");
        dashboard.dronePayloadCamera = FindCamera("DroneCamera");
        dashboard.rovFollowCamera = FindCamera("FollowCamera");

        if (dashboard.rovFollowCamera == null)
        {
            dashboard.rovFollowCamera = FindCamera("MiniROVFollowCamera");
        }

        dashboard.rovFrontCamera = FindCamera("FrontCamera");

        if (dashboard.rovFrontCamera == null)
        {
            dashboard.rovFrontCamera = FindCamera("MiniROVFrontCamera");
        }

        dashboard.switchUnityMainCamera = true;
        dashboard.manageAudioListeners = true;
        dashboard.enableCameraPreviewPanel = true;
        dashboard.previewTextureWidth = 640;
        dashboard.previewTextureHeight = 360;
        dashboard.currentCameraView = "Drone Follow / Preview: DroneCamera";

        EditorUtility.SetDirty(dashboard);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MIMISK] Dashboard camera switching configured. " +
            "Main Drone=" + NameOf(dashboard.droneFollowCamera) +
            ", Preview Drone=" + NameOf(dashboard.dronePayloadCamera) +
            ", Main ROV=" + NameOf(dashboard.rovFollowCamera) +
            ", Preview ROV=" + NameOf(dashboard.rovFrontCamera)
        );
    }

    private static Camera FindCamera(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        Camera[] cameras =
            Resources.FindObjectsOfTypeAll<Camera>();

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera c = cameras[i];

            if (c == null ||
                c.gameObject == null ||
                !c.gameObject.scene.IsValid())
            {
                continue;
            }

            if (c.gameObject.name == objectName)
            {
                return c;
            }
        }

        return null;
    }

    private static string NameOf(Camera c)
    {
        return c != null ? c.gameObject.name : "missing";
    }
}
