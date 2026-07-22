using UnityEditor;
using UnityEngine;

public static class MIMISKDroneSetup
{
    [MenuItem("MIMISK/Drone/Create Drone Mothership Prefab In Scene")]
    public static void CreateDroneMothership()
    {
        string fbxPath = "Assets/MIMISK/Models/Drone/mimisk_drone.fbx";
        GameObject droneAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);

        if (droneAsset == null)
        {
            Debug.LogError("[MIMISK] Drone FBX not found at: " + fbxPath);
            return;
        }

        GameObject existing = GameObject.Find("MIMISK_Drone");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject root = new GameObject("MIMISK_Drone");
        root.transform.position = new Vector3(0f, 2.0f, -10f);
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(droneAsset);
        body.name = "Body_Model";
        body.transform.SetParent(root.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = Vector3.one;

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 4.0f;
        rb.useGravity = false;
        rb.linearDamping = 1.0f;
        rb.angularDamping = 1.0f;
        rb.isKinematic = true;

        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = Vector3.zero;
        collider.size = new Vector3(1.2f, 0.25f, 1.2f);
        collider.isTrigger = false;

        CreateChild(root.transform, "PayloadMount", new Vector3(0f, -0.25f, 0f));
        CreateChild(root.transform, "WinchPoint", new Vector3(0f, -0.35f, 0f));
        CreateChild(root.transform, "TetherAnchor", new Vector3(0f, -0.35f, 0f));
        CreateChild(root.transform, "MiniROV_CarrySlot", new Vector3(0f, -0.65f, 0f));

        CreateChild(root.transform, "Buoy_FL", new Vector3(-0.45f, -0.15f, 0.45f));
        CreateChild(root.transform, "Buoy_FR", new Vector3(0.45f, -0.15f, 0.45f));
        CreateChild(root.transform, "Buoy_RL", new Vector3(-0.45f, -0.15f, -0.45f));
        CreateChild(root.transform, "Buoy_RR", new Vector3(0.45f, -0.15f, -0.45f));

        GameObject camObj = new GameObject("DroneCamera");
        camObj.transform.SetParent(root.transform);
        camObj.transform.localPosition = new Vector3(0f, 0.25f, 0.6f);
        camObj.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);

        Camera cam = camObj.AddComponent<Camera>();
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 500f;
        cam.enabled = false;

        string prefabPath = "Assets/MIMISK/Prefabs/MIMISK_Drone.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);

        Selection.activeGameObject = root;

        Debug.Log("[MIMISK] Drone mothership created and saved as: " + prefabPath);
    }

    private static GameObject CreateChild(Transform parent, string name, Vector3 localPosition)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.localPosition = localPosition;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;
        return obj;
    }
}
