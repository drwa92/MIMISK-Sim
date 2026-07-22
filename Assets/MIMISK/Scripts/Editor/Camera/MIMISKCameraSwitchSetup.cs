using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKCameraSwitchSetup
{
    [MenuItem("MIMISK/Camera/Setup Drone MiniROV Camera Switcher")]
    public static void SetupCameraSwitcher()
    {
        GameObject drone = GameObject.Find("Drone");
        GameObject miniRov = GameObject.Find("MiniROV");

        if (drone == null)
        {
            Debug.LogError("[MIMISK] Could not find Drone.");
            return;
        }

        if (miniRov == null)
        {
            Debug.LogWarning("[MIMISK] Could not find MiniROV. MiniROV camera will be assigned later.");
        }

        Camera droneFollowCamera =
            EnsureCameraObject(
                "DroneFollowCamera",
                drone.transform,
                new Vector3(0.0f, 2.5f, -5.0f),
                new Vector3(0.0f, 0.4f, 0.0f)
            );

        Camera miniRovFollowCamera =
            miniRov != null
                ? EnsureCameraObject(
                    "MiniROVFollowCamera",
                    miniRov.transform,
                    new Vector3(0.0f, 1.2f, -2.5f),
                    new Vector3(0.0f, 0.1f, 0.0f)
                  )
                : null;

        Camera miniRovFrontCamera = null;

        if (miniRov != null)
        {
            Transform front =
                FindDeepChild(miniRov.transform, "FrontCamera");

            if (front != null)
            {
                miniRovFrontCamera = front.GetComponent<Camera>();

                if (miniRovFrontCamera == null)
                {
                    miniRovFrontCamera = front.gameObject.AddComponent<Camera>();
                }
            }
        }

        MIMISKCameraViewSwitcher switcher =
            drone.GetComponent<MIMISKCameraViewSwitcher>();

        if (switcher == null)
        {
            switcher = drone.AddComponent<MIMISKCameraViewSwitcher>();
        }

        switcher.droneTarget = drone.transform;
        switcher.miniRovTarget = miniRov != null ? miniRov.transform : null;

        switcher.droneFollowCamera = droneFollowCamera;
        switcher.miniRovFollowCamera = miniRovFollowCamera;
        switcher.miniRovFrontCamera = miniRovFrontCamera;

        switcher.currentView = MIMISKCameraViewSwitcher.ViewMode.DroneFollow;
        switcher.ApplyView(switcher.currentView);

        EditorUtility.SetDirty(switcher);

        if (droneFollowCamera != null) EditorUtility.SetDirty(droneFollowCamera);
        if (miniRovFollowCamera != null) EditorUtility.SetDirty(miniRovFollowCamera);
        if (miniRovFrontCamera != null) EditorUtility.SetDirty(miniRovFrontCamera);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Camera switcher configured. C cycles views, 1 drone, 2 MiniROV follow, 3 MiniROV front.");
    }

    private static Camera EnsureCameraObject(
        string objectName,
        Transform target,
        Vector3 offset,
        Vector3 lookAtOffset)
    {
        GameObject go = GameObject.Find(objectName);

        if (go == null)
        {
            go = new GameObject(objectName);
        }

        Camera cam = go.GetComponent<Camera>();

        if (cam == null)
        {
            cam = go.AddComponent<Camera>();
        }

        MIMISKSimpleFollowCameraRig rig =
            go.GetComponent<MIMISKSimpleFollowCameraRig>();

        if (rig == null)
        {
            rig = go.AddComponent<MIMISKSimpleFollowCameraRig>();
        }

        rig.target = target;
        rig.offsetWorld = offset;
        rig.lookAtOffset = lookAtOffset;
        rig.rotateOffsetWithTargetYaw = true;
        rig.positionResponseHz = 6.0f;
        rig.rotationResponseHz = 8.0f;
        rig.SnapToTarget();

        AudioListener listener =
            go.GetComponent<AudioListener>();

        if (listener == null)
        {
            go.AddComponent<AudioListener>();
        }

        EditorUtility.SetDirty(go);
        EditorUtility.SetDirty(rig);
        EditorUtility.SetDirty(cam);

        return cam;
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found =
                FindDeepChild(root.GetChild(i), childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
