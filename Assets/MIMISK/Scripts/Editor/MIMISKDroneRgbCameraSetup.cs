using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneRgbCameraSetup
{
    [MenuItem("MIMISK/Drone/Sensors/Add Drone RGB Camera")]
    public static void AddDroneRgbCamera()
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
        Transform camMount = FindOrCreateChild(sensorsRoot, "RaspberryPiCamera3_DroneRGB");

        camMount.localPosition = new Vector3(0.0f, 0.22f, 0.72f);
        camMount.localRotation = Quaternion.identity;
        camMount.localScale = Vector3.one;

        Camera unityCamera = camMount.GetComponent<Camera>();

        if (unityCamera == null)
        {
            unityCamera = camMount.gameObject.AddComponent<Camera>();
        }

        MIMISKDroneRgbCameraDevice cameraDevice =
            camMount.GetComponent<MIMISKDroneRgbCameraDevice>();

        if (cameraDevice == null)
        {
            cameraDevice = camMount.gameObject.AddComponent<MIMISKDroneRgbCameraDevice>();
        }

        cameraDevice.cameraModel = MIMISKDroneRgbCameraDevice.CameraModel.RaspberryPiCameraModule3Wide;
        cameraDevice.deviceName = "Raspberry Pi Camera Module 3 Wide";
        cameraDevice.unityCamera = unityCamera;
        cameraDevice.imageWidth = 1280;
        cameraDevice.imageHeight = 720;
        cameraDevice.frameRateHz = 30.0f;
        cameraDevice.horizontalFovDeg = 102.0f;
        cameraDevice.renderOverlayInGameView = true;
        cameraDevice.overlayViewport = new Rect(0.70f, 0.70f, 0.28f, 0.28f);
        cameraDevice.cameraDepth = 80;
        cameraDevice.nearClipM = 0.05f;
        cameraDevice.farClipM = 500.0f;
        cameraDevice.ConfigureModelDefaults();
        cameraDevice.ApplyCameraSettings();

        MIMISKDroneRgbCameraLogger logger =
            camMount.GetComponent<MIMISKDroneRgbCameraLogger>();

        if (logger == null)
        {
            logger = camMount.gameObject.AddComponent<MIMISKDroneRgbCameraLogger>();
        }

        logger.cameraDevice = cameraDevice;
        logger.enableLogging = true;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(camMount);
        EditorUtility.SetDirty(unityCamera);
        EditorUtility.SetDirty(cameraDevice);
        EditorUtility.SetDirty(logger);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = camMount.gameObject;

        Debug.Log("[MIMISK] Added drone RGB camera under Drone/SensorsAndCameras.");
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
