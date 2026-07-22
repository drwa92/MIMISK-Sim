using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKAerialAquaticSystemGenerator
{
    private const string SystemRootName = "MIMISK_AerialAquaticSystem";
    private const string FinalDroneName = "Drone";
    private const string DronePrefabPath = "Assets/MIMISK/Prefabs/MIMISK_Drone_Final.prefab";
    private const string TetherMaterialPath = "Assets/MIMISK/Materials/TetherLine_Mat.mat";

    [MenuItem("MIMISK/Drone/Generate Final Aerial-Aquatic System")]
    public static void GenerateFinalAerialAquaticSystem()
    {
        GameObject miniRov = GameObject.Find("MiniROV");

        if (miniRov == null)
        {
            Debug.LogWarning("[MIMISK] MiniROV not found in the scene. The structure will be created without attaching the ROV.");
        }

        PreserveMiniROVBeforeDeletingOldRoot(miniRov);
        DeleteOldRoot();

        GameObject root = new GameObject(SystemRootName);
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        GameObject drone = CreateDrone(root.transform);
        GameObject tetherSystem = CreateTetherSystem(root.transform);
        GameObject missionManager = CreateMissionManager(root.transform);

        if (miniRov != null)
        {
            SetupMiniROV(miniRov, root.transform);
            ConnectTether(tetherSystem, drone, miniRov);
        }

        PrefabUtility.SaveAsPrefabAsset(drone, DronePrefabPath);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Generated final aerial-aquatic system structure.");
    }

    private static void PreserveMiniROVBeforeDeletingOldRoot(GameObject miniRov)
    {
        if (miniRov == null)
        {
            return;
        }

        miniRov.transform.SetParent(null, true);
    }

    private static void DeleteOldRoot()
    {
        GameObject oldRoot = GameObject.Find(SystemRootName);

        if (oldRoot != null)
        {
            Object.DestroyImmediate(oldRoot);
        }
    }

    private static GameObject CreateDrone(Transform parent)
    {
        GameObject droneRoot = new GameObject(FinalDroneName);
        droneRoot.transform.SetParent(parent, false);
        droneRoot.transform.position = new Vector3(0f, 1.2f, -10f);
        droneRoot.transform.rotation = Quaternion.identity;
        droneRoot.transform.localScale = Vector3.one;

        GameObject bodyModel = InstantiateDroneBody();
        bodyModel.name = "Body_Model";
        bodyModel.transform.SetParent(droneRoot.transform, false);
        bodyModel.transform.localPosition = Vector3.zero;
        bodyModel.transform.localRotation = Quaternion.identity;
        bodyModel.transform.localScale = Vector3.one;

        Rigidbody rb = droneRoot.AddComponent<Rigidbody>();
        rb.mass = 4.0f;
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.linearDamping = 1.0f;
        rb.angularDamping = 1.0f;

        BoxCollider box = droneRoot.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0f, 0f);
        box.size = new Vector3(1.6f, 0.35f, 1.6f);

        CreateDroneAvionics(droneRoot.transform);
        CreateDronePayloadSystem(droneRoot.transform);
        CreateDroneSurfaceLandingSystem(droneRoot.transform);
        CreateDroneSensorsAndCameras(droneRoot.transform);

        return droneRoot;
    }

    private static GameObject InstantiateDroneBody()
    {
        string preferredPath = "Assets/MIMISK/Models/Drone/mimisk_drone_v1.fbx";
        GameObject droneAsset = AssetDatabase.LoadAssetAtPath<GameObject>(preferredPath);

        if (droneAsset == null)
        {
            string[] guids = AssetDatabase.FindAssets("mimisk_drone_v1 t:Model");

            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                droneAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Debug.Log("[MIMISK] Found drone_v1 model at: " + path);
            }
        }

        if (droneAsset == null)
        {
            Debug.LogError("[MIMISK] Could not find mimisk_drone_v1.fbx under Assets/MIMISK/Models/Drone.");
            GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            placeholder.name = "Missing_Drone_Model_Placeholder";
            placeholder.transform.localScale = new Vector3(1.5f, 0.2f, 1.5f);
            return placeholder;
        }

        return (GameObject)PrefabUtility.InstantiatePrefab(droneAsset);
    }

    private static void CreateDroneAvionics(Transform drone)
    {
        GameObject avionics = CreateEmpty(drone, "Avionics", Vector3.zero);

        CreateEmpty(avionics.transform, "PX4FlightController", new Vector3(0f, 0.10f, 0f));
        CreateEmpty(avionics.transform, "JetsonOrinNano_CompanionComputer", new Vector3(0f, 0.16f, 0.10f));
        CreateEmpty(avionics.transform, "MAVLink_ROS2_Bridge", new Vector3(0f, 0.16f, -0.10f));
        CreateEmpty(avionics.transform, "DronePowerDistribution", new Vector3(0f, 0.07f, 0f));
        CreateEmpty(avionics.transform, "TelemetryRadio", new Vector3(0.20f, 0.10f, 0f));
    }

    private static void CreateDronePayloadSystem(Transform drone)
    {
        GameObject payload = CreateEmpty(drone, "PayloadAndWinchSystem", Vector3.zero);

        CreateEmpty(payload.transform, "PayloadMount", new Vector3(0f, -0.20f, 0f));
        CreateEmpty(payload.transform, "WinchPoint", new Vector3(0f, -0.35f, 0f));
        CreateEmpty(payload.transform, "TetherAnchor", new Vector3(0f, -0.35f, 0f));
        CreateEmpty(payload.transform, "MiniROV_CarrySlot", new Vector3(0f, -0.75f, 0f));

        GameObject winchDrum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        winchDrum.name = "WinchDrum_Visual";
        winchDrum.transform.SetParent(payload.transform, false);
        winchDrum.transform.localPosition = new Vector3(0f, -0.22f, -0.18f);
        winchDrum.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        winchDrum.transform.localScale = new Vector3(0.12f, 0.10f, 0.12f);

        Collider c = winchDrum.GetComponent<Collider>();
        if (c != null)
        {
            Object.DestroyImmediate(c);
        }
    }

    private static void CreateDroneSurfaceLandingSystem(Transform drone)
    {
        GameObject landing = CreateEmpty(drone, "SurfaceLandingSystem", Vector3.zero);

        GameObject buoyancyPoints = CreateEmpty(landing.transform, "BuoyancyPoints", Vector3.zero);

        CreateEmpty(buoyancyPoints.transform, "Buoy_FL", new Vector3(-0.55f, -0.15f, 0.55f));
        CreateEmpty(buoyancyPoints.transform, "Buoy_FR", new Vector3(0.55f, -0.15f, 0.55f));
        CreateEmpty(buoyancyPoints.transform, "Buoy_RL", new Vector3(-0.55f, -0.15f, -0.55f));
        CreateEmpty(buoyancyPoints.transform, "Buoy_RR", new Vector3(0.55f, -0.15f, -0.55f));

        CreateEmpty(landing.transform, "WaterContactSensor_FL", new Vector3(-0.65f, -0.30f, 0.65f));
        CreateEmpty(landing.transform, "WaterContactSensor_FR", new Vector3(0.65f, -0.30f, 0.65f));
        CreateEmpty(landing.transform, "WaterContactSensor_RL", new Vector3(-0.65f, -0.30f, -0.65f));
        CreateEmpty(landing.transform, "WaterContactSensor_RR", new Vector3(0.65f, -0.30f, -0.65f));
    }

    private static void CreateDroneSensorsAndCameras(Transform drone)
    {
        GameObject sensors = CreateEmpty(drone, "SensorsAndCameras", Vector3.zero);

        CreateEmpty(sensors.transform, "DroneIMU", new Vector3(0f, 0.12f, 0f));
        CreateEmpty(sensors.transform, "DroneGPS", new Vector3(0f, 0.25f, 0f));
        CreateEmpty(sensors.transform, "DroneBarometer", new Vector3(0.10f, 0.14f, 0f));

        GameObject cameraObject = new GameObject("DroneCamera");
        cameraObject.transform.SetParent(sensors.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0.25f, 0.70f);
        cameraObject.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);

        Camera cam = cameraObject.AddComponent<Camera>();
        cam.enabled = false;
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 500f;
    }

    private static GameObject CreateTetherSystem(Transform parent)
    {
        GameObject tetherSystem = CreateEmpty(parent, "TetherSystem", Vector3.zero);

        GameObject tetherLine = new GameObject("TetherLine");
        tetherLine.transform.SetParent(tetherSystem.transform, false);

        LineRenderer lr = tetherLine.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.widthMultiplier = 0.01f;
        lr.useWorldSpace = true;
        lr.material = GetOrCreateTetherMaterial();

        tetherLine.AddComponent<MIMISKTetherVisual>();

        CreateEmpty(tetherSystem.transform, "WinchController", Vector3.zero);
        CreateEmpty(tetherSystem.transform, "TetherPhysics", Vector3.zero);
        CreateEmpty(tetherSystem.transform, "TetherContactModel", Vector3.zero);
        CreateEmpty(tetherSystem.transform, "TetherTensionSensor", Vector3.zero);

        return tetherSystem;
    }

    private static GameObject CreateMissionManager(Transform parent)
    {
        GameObject manager = CreateEmpty(parent, "MissionManager", Vector3.zero);

        CreateEmpty(manager.transform, "DeploymentStateMachine", Vector3.zero);
        CreateEmpty(manager.transform, "InspectionMission", Vector3.zero);
        CreateEmpty(manager.transform, "RecoveryMission", Vector3.zero);
        CreateEmpty(manager.transform, "MissionMetricsLogger", Vector3.zero);

        return manager;
    }

    private static void SetupMiniROV(GameObject miniRov, Transform parent)
    {
        miniRov.transform.SetParent(parent, true);

        if (miniRov.transform.Find("ROV_TetherAnchor") == null)
        {
            GameObject rovAnchor = new GameObject("ROV_TetherAnchor");
            rovAnchor.transform.SetParent(miniRov.transform, false);
            rovAnchor.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            rovAnchor.transform.localRotation = Quaternion.identity;
            rovAnchor.transform.localScale = Vector3.one;
        }

        // Do not change MiniROV control scripts here.
        // We only ensure its deployment test pose.
        miniRov.transform.position = new Vector3(0f, -1.5f, -10f);
        miniRov.transform.rotation = Quaternion.identity;
    }

    private static void ConnectTether(GameObject tetherSystem, GameObject drone, GameObject miniRov)
    {
        Transform droneAnchor = drone.transform.Find("PayloadAndWinchSystem/TetherAnchor");
        Transform rovAnchor = miniRov.transform.Find("ROV_TetherAnchor");
        Transform tetherLine = tetherSystem.transform.Find("TetherLine");

        if (droneAnchor == null || rovAnchor == null || tetherLine == null)
        {
            Debug.LogWarning("[MIMISK] Could not connect tether anchors.");
            return;
        }

        MIMISKTetherVisual tetherVisual = tetherLine.GetComponent<MIMISKTetherVisual>();

        if (tetherVisual != null)
        {
            tetherVisual.droneAnchor = droneAnchor;
            tetherVisual.rovAnchor = rovAnchor;
            tetherVisual.lineWidth = 0.01f;
        }
    }

    private static GameObject CreateEmpty(Transform parent, string name, Vector3 localPosition)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;
        return obj;
    }

    private static Material GetOrCreateTetherMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(TetherMaterialPath);

        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, TetherMaterialPath);
        }
        else
        {
            mat.shader = shader;
        }

        if (mat.HasProperty("_UnlitColor"))
        {
            mat.SetColor("_UnlitColor", new Color(0.02f, 0.02f, 0.02f, 1f));
        }

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.02f, 1f));
        }

        return mat;
    }
}
