using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneGnssSetup
{
    [MenuItem("MIMISK/Drone/Sensors/Add u-blox M10 GNSS")]
    public static void AddUbloxM10Gnss()
    {
        AddGnss(MIMISKDroneGnssDevice.GnssModel.UBloxM10, "uBlox_M10_GNSS");
    }

    [MenuItem("MIMISK/Drone/Sensors/Add ZED-F9P RTK GNSS")]
    public static void AddZedF9pGnss()
    {
        AddGnss(MIMISKDroneGnssDevice.GnssModel.ZedF9pRtkFixed, "ZED_F9P_RTK_GNSS");
    }

    private static void AddGnss(MIMISKDroneGnssDevice.GnssModel model, string objectName)
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

        Transform avionics = FindOrCreateChild(drone.transform, "Avionics");
        Transform gnssMount = FindOrCreateChild(avionics, objectName);

        gnssMount.localPosition = new Vector3(0.0f, 0.30f, 0.0f);
        gnssMount.localRotation = Quaternion.identity;
        gnssMount.localScale = Vector3.one;

        MIMISKDroneGnssDevice gnss =
            gnssMount.GetComponent<MIMISKDroneGnssDevice>();

        if (gnss == null)
        {
            gnss = gnssMount.gameObject.AddComponent<MIMISKDroneGnssDevice>();
        }

        gnss.gnssModel = model;
        gnss.antennaFrame = gnssMount;
        gnss.droneRigidbody = drone.GetComponent<Rigidbody>();
        gnss.setHomeOnStart = true;
        gnss.enableNoise = true;
        gnss.enableDropout = false;
        gnss.ConfigureModelDefaults();

        MIMISKDroneGnssLogger logger =
            gnssMount.GetComponent<MIMISKDroneGnssLogger>();

        if (logger == null)
        {
            logger = gnssMount.gameObject.AddComponent<MIMISKDroneGnssLogger>();
        }

        logger.gnss = gnss;
        logger.enableLogging = true;
        logger.logHz = gnss.sampleRateHz;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(gnss);
        EditorUtility.SetDirty(logger);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = gnssMount.gameObject;

        Debug.Log("[MIMISK] Added " + gnss.deviceName + " under Drone/Avionics.");
    }

    private static Transform FindOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);

        if (child != null)
        {
            return child;
        }

        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;

        return obj.transform;
    }
}
