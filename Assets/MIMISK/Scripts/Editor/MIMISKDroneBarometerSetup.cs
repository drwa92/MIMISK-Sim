using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneBarometerSetup
{
    [MenuItem("MIMISK/Drone/Sensors/Add MS5611 Barometer")]
    public static void AddMs5611Barometer()
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
        Transform baroMount = FindOrCreateChild(avionics, "MS5611_Barometer");

        baroMount.localPosition = new Vector3(0.0f, 0.14f, 0.05f);
        baroMount.localRotation = Quaternion.identity;
        baroMount.localScale = Vector3.one;

        MIMISKDroneBarometerDevice baro =
            baroMount.GetComponent<MIMISKDroneBarometerDevice>();

        if (baro == null)
        {
            baro = baroMount.gameObject.AddComponent<MIMISKDroneBarometerDevice>();
        }

        baro.barometerModel = MIMISKDroneBarometerDevice.BarometerModel.MS5611;
        baro.deviceName = "MS5611";
        baro.sensorFrame = baroMount;
        baro.droneRigidbody = drone.GetComponent<Rigidbody>();
        baro.sampleRateHz = 50.0f;
        baro.enableNoise = true;
        baro.enableBias = true;
        baro.setHomeOnStart = true;
        baro.ConfigureModelDefaults();

        MIMISKDroneBarometerLogger logger =
            baroMount.GetComponent<MIMISKDroneBarometerLogger>();

        if (logger == null)
        {
            logger = baroMount.gameObject.AddComponent<MIMISKDroneBarometerLogger>();
        }

        logger.barometer = baro;
        logger.enableLogging = true;
        logger.logHz = 50.0f;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(baro);
        EditorUtility.SetDirty(logger);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = baroMount.gameObject;

        Debug.Log("[MIMISK] Added MS5611 barometer under Drone/Avionics.");
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
