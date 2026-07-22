using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneImuSetup
{
    [MenuItem("MIMISK/Drone/Sensors/Add ICM-42688-P IMU")]
    public static void AddIcm42688pImu()
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
        Transform imuMount = FindOrCreateChild(avionics, "ICM42688P_IMU");

        imuMount.localPosition = new Vector3(0.0f, 0.12f, 0.0f);
        imuMount.localRotation = Quaternion.identity;
        imuMount.localScale = Vector3.one;

        MIMISKDroneImuDevice imu = imuMount.GetComponent<MIMISKDroneImuDevice>();

        if (imu == null)
        {
            imu = imuMount.gameObject.AddComponent<MIMISKDroneImuDevice>();
        }

        imu.imuModel = MIMISKDroneImuDevice.ImuModel.ICM42688P;
        imu.deviceName = "ICM-42688-P";
        imu.sensorFrame = imuMount;
        imu.droneRigidbody = drone.GetComponent<Rigidbody>();
        imu.sampleRateHz = 200.0f;
        imu.enableNoise = true;
        imu.enableBias = true;
        imu.enableQuantization = false;
        imu.ConfigureModelDefaults();

        MIMISKDroneImuLogger logger = imuMount.GetComponent<MIMISKDroneImuLogger>();

        if (logger == null)
        {
            logger = imuMount.gameObject.AddComponent<MIMISKDroneImuLogger>();
        }

        logger.imu = imu;
        logger.enableLogging = true;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(imu);
        EditorUtility.SetDirty(logger);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = imuMount.gameObject;

        Debug.Log("[MIMISK] Added ICM-42688-P IMU device under Drone/Avionics.");
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
