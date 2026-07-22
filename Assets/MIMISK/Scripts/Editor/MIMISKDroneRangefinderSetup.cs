using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneRangefinderSetup
{
    [MenuItem("MIMISK/Drone/Sensors/Add TFmini-S Downward Rangefinder")]
    public static void AddTfminiSRangefinder()
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

        Transform sensorsRoot = FindOrCreateChild(drone.transform, "SensorsAndCameras");
        Transform rangeMount = FindOrCreateChild(sensorsRoot, "TFminiS_DownwardRangefinder");

        rangeMount.localPosition = new Vector3(0.0f, -0.20f, 0.0f);
        rangeMount.localRotation = Quaternion.identity;
        rangeMount.localScale = Vector3.one;

        MIMISKDroneRangefinderDevice rangefinder =
            rangeMount.GetComponent<MIMISKDroneRangefinderDevice>();

        if (rangefinder == null)
        {
            rangefinder = rangeMount.gameObject.AddComponent<MIMISKDroneRangefinderDevice>();
        }

        rangefinder.rangefinderModel = MIMISKDroneRangefinderDevice.RangefinderModel.TFminiS;
        rangefinder.deviceName = "TFmini-S Downward Rangefinder";
        rangefinder.sensorFrame = rangeMount;
        rangefinder.droneRigidbody = drone.GetComponent<Rigidbody>();

        rangefinder.sampleRateHz = 50.0f;
        rangefinder.localRayDirection = Vector3.down;
        rangefinder.minRangeM = 0.10f;
        rangefinder.maxRangeM = 12.0f;

        rangefinder.detectWaterPlane = true;
        rangefinder.waterLevelY = 0.0f;
        rangefinder.simulateWaterReflectivityDropout = false;
        rangefinder.waterReturnProbability = 0.98f;

        rangefinder.usePhysicsRaycast = true;
        rangefinder.enableNoise = true;
        rangefinder.enableBias = true;
        rangefinder.rangeNoiseStdM = 0.03f;
        rangefinder.rangeBiasM = 0.0f;

        MIMISKDroneRangefinderLogger logger =
            rangeMount.GetComponent<MIMISKDroneRangefinderLogger>();

        if (logger == null)
        {
            logger = rangeMount.gameObject.AddComponent<MIMISKDroneRangefinderLogger>();
        }

        logger.rangefinder = rangefinder;
        logger.enableLogging = true;
        logger.logHz = 50.0f;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(rangefinder);
        EditorUtility.SetDirty(logger);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = rangeMount.gameObject;

        Debug.Log("[MIMISK] Added TFmini-S downward rangefinder under Drone/SensorsAndCameras.");
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
