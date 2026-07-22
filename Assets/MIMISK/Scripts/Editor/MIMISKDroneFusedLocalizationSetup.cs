using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneFusedLocalizationSetup
{
    [MenuItem("MIMISK/Drone/Localization/Setup Fused Localization")]
    public static void SetupFusedLocalization()
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

        MIMISKDroneFusedLocalization loc =
            drone.GetComponent<MIMISKDroneFusedLocalization>();

        if (loc == null)
        {
            loc = drone.AddComponent<MIMISKDroneFusedLocalization>();
        }

        loc.imu = drone.GetComponentInChildren<MIMISKDroneImuDevice>();
        loc.barometer = drone.GetComponentInChildren<MIMISKDroneBarometerDevice>();
        loc.magnetometer = drone.GetComponentInChildren<MIMISKDroneMagnetometerDevice>();
        loc.gnss = drone.GetComponentInChildren<MIMISKDroneGnssDevice>();
        loc.rangefinder = drone.GetComponentInChildren<MIMISKDroneRangefinderDevice>();

        loc.estimatorEnabled = true;
        loc.resetOnStart = true;

        loc.accelTiltCorrectionGain = 2.5f;
        loc.magnetometerYawGain = 2.0f;

        loc.gnssPositionGain = 3.0f;
        loc.gnssVelocityGain = 4.0f;

        loc.barometerAltitudeGain = 4.0f;
        loc.rangefinderAltitudeGain = 8.0f;
        loc.rangefinderMaxFusionRangeM = 6.0f;

        EditorUtility.SetDirty(loc);

        MIMISKDroneLocalizationLogger logger =
            drone.GetComponent<MIMISKDroneLocalizationLogger>();

        if (logger == null)
        {
            logger = drone.AddComponent<MIMISKDroneLocalizationLogger>();
        }

        logger.localization = loc;
        logger.enableLogging = true;
        logger.logHz = 50.0f;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(logger);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Fused localization stack configured. Controller feedback is not changed yet.");
    }
}
