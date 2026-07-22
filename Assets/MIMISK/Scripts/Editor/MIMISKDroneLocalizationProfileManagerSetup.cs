using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneLocalizationProfileManagerSetup
{
    [MenuItem("MIMISK/Drone/Localization/Setup Localization Profile Manager")]
    public static void SetupManager()
    {
        GameObject drone = Selection.activeGameObject;

        if (drone == null || drone.GetComponent<Rigidbody>() == null)
        {
            drone = GameObject.Find("Drone");
        }

        if (drone == null)
        {
            Debug.LogError("[MIMISK] Could not find Drone root.");
            return;
        }

        MIMISKDroneLocalizationProfileManager manager =
            drone.GetComponent<MIMISKDroneLocalizationProfileManager>();

        if (manager == null)
        {
            manager = drone.AddComponent<MIMISKDroneLocalizationProfileManager>();
        }

        manager.aquaLoc = drone.GetComponent<MIMISKDroneAquaLocEstimator>();

        if (manager.aquaLoc == null)
        {
            manager.aquaLoc = drone.AddComponent<MIMISKDroneAquaLocEstimator>();
        }

        manager.aquaLocLogger = drone.GetComponent<MIMISKDroneAquaLocLogger>();

        if (manager.aquaLocLogger == null)
        {
            manager.aquaLocLogger = drone.AddComponent<MIMISKDroneAquaLocLogger>();
        }

        manager.gnss = drone.GetComponentInChildren<MIMISKDroneGnssDevice>();

        manager.activeProfile =
            MIMISKDroneLocalizationProfileManager.LocalizationProfile.AquaLocPaperEquivalentGPS;

        manager.applyOnStart = false;
        manager.ApplyActiveProfile();

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(manager.aquaLoc);
        EditorUtility.SetDirty(manager.aquaLocLogger);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Localization Profile Manager configured.");
    }

    [MenuItem("MIMISK/Drone/Localization/Apply Profile/Raw M10 Baseline")]
    public static void ApplyRawM10()
    {
        ApplyProfile(MIMISKDroneLocalizationProfileManager.LocalizationProfile.AquaLocRawM10Baseline);
    }

    [MenuItem("MIMISK/Drone/Localization/Apply Profile/Low-Cost Smooth M10")]
    public static void ApplySmoothM10()
    {
        ApplyProfile(MIMISKDroneLocalizationProfileManager.LocalizationProfile.AquaLocLowCostSmoothM10);
    }

    [MenuItem("MIMISK/Drone/Localization/Apply Profile/Paper-Equivalent GPS")]
    public static void ApplyPaperGps()
    {
        ApplyProfile(MIMISKDroneLocalizationProfileManager.LocalizationProfile.AquaLocPaperEquivalentGPS);
    }

    [MenuItem("MIMISK/Drone/Localization/Apply Profile/RTK High Accuracy")]
    public static void ApplyRtk()
    {
        ApplyProfile(MIMISKDroneLocalizationProfileManager.LocalizationProfile.AquaLocRtkHighAccuracy);
    }

    private static void ApplyProfile(
        MIMISKDroneLocalizationProfileManager.LocalizationProfile profile)
    {
        GameObject drone = Selection.activeGameObject;

        if (drone == null || drone.GetComponent<Rigidbody>() == null)
        {
            drone = GameObject.Find("Drone");
        }

        if (drone == null)
        {
            Debug.LogError("[MIMISK] Could not find Drone root.");
            return;
        }

        MIMISKDroneLocalizationProfileManager manager =
            drone.GetComponent<MIMISKDroneLocalizationProfileManager>();

        if (manager == null)
        {
            manager = drone.AddComponent<MIMISKDroneLocalizationProfileManager>();
        }

        manager.activeProfile = profile;
        manager.ApplyActiveProfile();

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }
}
