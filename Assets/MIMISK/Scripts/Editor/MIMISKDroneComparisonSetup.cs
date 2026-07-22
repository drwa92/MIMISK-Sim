using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneComparisonSetup
{
    [MenuItem("MIMISK/Drone/Create Drone Model Comparison")]
    public static void CreateDroneComparison()
    {
        string modelAPath = "Assets/MIMISK/Models/Drone/mimisk_drone.fbx";
        string modelBPath = "Assets/MIMISK/Models/Drone/mimisk_drone_v1.fbx";

        GameObject modelA = AssetDatabase.LoadAssetAtPath<GameObject>(modelAPath);
        GameObject modelB = AssetDatabase.LoadAssetAtPath<GameObject>(modelBPath);

        if (modelA == null)
        {
            Debug.LogWarning("[MIMISK] Model A not found: " + modelAPath);
        }

        if (modelB == null)
        {
            Debug.LogWarning("[MIMISK] Model B not found: " + modelBPath);
        }

        GameObject old = GameObject.Find("MIMISK_Drone_Model_Comparison");
        if (old != null)
        {
            Object.DestroyImmediate(old);
        }

        GameObject root = new GameObject("MIMISK_Drone_Model_Comparison");
        root.transform.position = Vector3.zero;

        if (modelA != null)
        {
            GameObject candidateA = CreateCandidate(
                "DroneCandidate_A_Original",
                modelA,
                new Vector3(-3.0f, 2.0f, -10.0f)
            );

            candidateA.transform.SetParent(root.transform);
            PrefabUtility.SaveAsPrefabAsset(candidateA, "Assets/MIMISK/Prefabs/MIMISK_Drone_Candidate_A.prefab");
        }

        if (modelB != null)
        {
            GameObject candidateB = CreateCandidate(
                "DroneCandidate_B_V1",
                modelB,
                new Vector3(3.0f, 2.0f, -10.0f)
            );

            candidateB.transform.SetParent(root.transform);
            PrefabUtility.SaveAsPrefabAsset(candidateB, "Assets/MIMISK/Prefabs/MIMISK_Drone_Candidate_B_V1.prefab");
        }

        PositionMiniROVForComparison();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Drone comparison setup created.");
    }

    private static GameObject CreateCandidate(string name, GameObject modelAsset, Vector3 position)
    {
        GameObject root = new GameObject(name);
        root.transform.position = position;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
        body.name = "Body_Model";
        body.transform.SetParent(root.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = Vector3.one;

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 4.0f;
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.linearDamping = 1.0f;
        rb.angularDamping = 1.0f;

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = Vector3.zero;
        box.size = new Vector3(1.5f, 0.4f, 1.5f);

        CreateChild(root.transform, "FlightController", new Vector3(0f, 0.05f, 0f));
        CreateChild(root.transform, "CompanionComputer", new Vector3(0f, 0.10f, 0f));
        CreateChild(root.transform, "PayloadMount", new Vector3(0f, -0.25f, 0f));
        CreateChild(root.transform, "WinchPoint", new Vector3(0f, -0.35f, 0f));
        CreateChild(root.transform, "TetherAnchor", new Vector3(0f, -0.35f, 0f));
        CreateChild(root.transform, "MiniROV_CarrySlot", new Vector3(0f, -0.65f, 0f));

        CreateChild(root.transform, "Buoy_FL", new Vector3(-0.55f, -0.15f, 0.55f));
        CreateChild(root.transform, "Buoy_FR", new Vector3(0.55f, -0.15f, 0.55f));
        CreateChild(root.transform, "Buoy_RL", new Vector3(-0.55f, -0.15f, -0.55f));
        CreateChild(root.transform, "Buoy_RR", new Vector3(0.55f, -0.15f, -0.55f));

        GameObject camObj = new GameObject("DroneCamera");
        camObj.transform.SetParent(root.transform);
        camObj.transform.localPosition = new Vector3(0f, 0.25f, 0.7f);
        camObj.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);

        Camera cam = camObj.AddComponent<Camera>();
        cam.enabled = false;
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 500f;

        Bounds bounds = CalculateBounds(root);
        Debug.Log(
            "[MIMISK] " + name +
            " bounds size: " + bounds.size.ToString("F3") +
            " center: " + bounds.center.ToString("F3")
        );

        CreateLabel(root.transform, name + "_Label", name, new Vector3(0f, 1.0f, 0f));

        return root;
    }

    private static void CreateChild(Transform parent, string name, Vector3 localPosition)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
    }

    private static void CreateLabel(Transform parent, string name, string text, Vector3 localPosition)
    {
        GameObject label = new GameObject(name);
        label.transform.SetParent(parent);
        label.transform.localPosition = localPosition;
        label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        TextMesh tm = label.AddComponent<TextMesh>();
        tm.text = text;
        tm.characterSize = 0.15f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.black;
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            return new Bounds(root.transform.position, Vector3.zero);
        }

        Bounds b = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            b.Encapsulate(renderers[i].bounds);
        }

        return b;
    }

    private static void PositionMiniROVForComparison()
    {
        GameObject rov = GameObject.Find("MiniROV");

        if (rov == null)
        {
            return;
        }

        rov.transform.position = new Vector3(0f, -1.5f, -10f);
        rov.transform.rotation = Quaternion.identity;
    }
}
