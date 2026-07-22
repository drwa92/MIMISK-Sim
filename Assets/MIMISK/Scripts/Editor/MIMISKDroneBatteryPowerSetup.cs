using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneBatteryPowerSetup
{
    [MenuItem("MIMISK/Drone/Sensors/Add 4S Battery Power Module")]
    public static void AddBatteryPowerModule()
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
        Transform powerMount = FindOrCreateChild(avionics, "MIMISK_4S_BatteryPowerModule");

        powerMount.localPosition = new Vector3(0.0f, 0.10f, -0.08f);
        powerMount.localRotation = Quaternion.identity;
        powerMount.localScale = Vector3.one;

        MIMISKDroneBatteryPowerDevice power =
            powerMount.GetComponent<MIMISKDroneBatteryPowerDevice>();

        if (power == null)
        {
            power = powerMount.gameObject.AddComponent<MIMISKDroneBatteryPowerDevice>();
        }

        power.batteryModel = MIMISKDroneBatteryPowerDevice.BatteryModel.FourSLiPo;
        power.deviceName = "4S LiPo Power Module";
        power.controller = drone.GetComponent<MIMISKDroneModelController>();
        power.ConfigureModelDefaults();
        power.initialStateOfCharge = 1.0f;
        power.avionicsCurrentA = 1.2f;
        power.motorIdleCurrentA = 0.15f;
        power.motorMaxCurrentA = 12.0f;
        power.motorCurrentExponent = 1.35f;
        power.lowCellVoltage = 3.50f;
        power.criticalCellVoltage = 3.30f;
        power.enableFailsafeOnCritical = false;

        MIMISKDroneBatteryPowerLogger logger =
            powerMount.GetComponent<MIMISKDroneBatteryPowerLogger>();

        if (logger == null)
        {
            logger = powerMount.gameObject.AddComponent<MIMISKDroneBatteryPowerLogger>();
        }

        logger.power = power;
        logger.enableLogging = true;
        logger.logHz = 10.0f;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(power);
        EditorUtility.SetDirty(logger);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = powerMount.gameObject;

        Debug.Log("[MIMISK] Added 4S battery power module under Drone/Avionics.");
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
