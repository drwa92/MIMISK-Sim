using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneMagnetometerSetup
{
    [MenuItem("MIMISK/Drone/Sensors/Add IST8310 Magnetometer")]
    public static void AddIst8310Magnetometer()
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
        Transform magMount = FindOrCreateChild(avionics, "IST8310_Magnetometer");

        magMount.localPosition = new Vector3(0.06f, 0.16f, 0.03f);
        magMount.localRotation = Quaternion.identity;
        magMount.localScale = Vector3.one;

        MIMISKDroneMagnetometerDevice mag =
            magMount.GetComponent<MIMISKDroneMagnetometerDevice>();

        if (mag == null)
        {
            mag = magMount.gameObject.AddComponent<MIMISKDroneMagnetometerDevice>();
        }

        mag.magnetometerModel = MIMISKDroneMagnetometerDevice.MagnetometerModel.IST8310;
        mag.deviceName = "IST8310";
        mag.sensorFrame = magMount;
        mag.sampleRateHz = 50.0f;
        mag.enableNoise = true;
        mag.enableBias = true;
        mag.enableSoftIron = true;
        mag.fieldMagnitudeUT = 48.0f;
        mag.inclinationDeg = 60.0f;
        mag.declinationDeg = 4.0f;
        mag.ConfigureModelDefaults();
        mag.ComputeEarthFieldWorld();

        MIMISKDroneMagnetometerLogger logger =
            magMount.GetComponent<MIMISKDroneMagnetometerLogger>();

        if (logger == null)
        {
            logger = magMount.gameObject.AddComponent<MIMISKDroneMagnetometerLogger>();
        }

        logger.magnetometer = mag;
        logger.enableLogging = true;
        logger.logHz = 50.0f;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(mag);
        EditorUtility.SetDirty(logger);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = magMount.gameObject;

        Debug.Log("[MIMISK] Added IST8310 magnetometer under Drone/Avionics.");
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
