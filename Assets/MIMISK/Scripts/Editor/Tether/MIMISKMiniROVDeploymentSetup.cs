using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMiniROVDeploymentSetup
{
    [MenuItem("MIMISK/Drone/Tether/Setup Phase 3B MiniROV Deployment")]
    public static void SetupMiniRovDeployment()
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

        GameObject miniRovGo = GameObject.Find("MiniROV");

        if (miniRovGo == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV GameObject in the scene.");
            return;
        }

        MIMISKDroneCoreTetherManager tether =
            drone.GetComponent<MIMISKDroneCoreTetherManager>();

        if (tether == null)
        {
            tether = drone.AddComponent<MIMISKDroneCoreTetherManager>();
        }

        tether.acceptKeyboardCommands = false;
        EditorUtility.SetDirty(tether);

        MIMISKMiniROVDeploymentManager deployment =
            drone.GetComponent<MIMISKMiniROVDeploymentManager>();

        if (deployment == null)
        {
            deployment = drone.AddComponent<MIMISKMiniROVDeploymentManager>();
        }

        deployment.tetherManager = tether;
        deployment.missionManager = drone.GetComponent<MIMISKDroneCoreMissionManager>();
        deployment.flightManager = drone.GetComponent<MIMISKDroneCoreFlightModeManager>();

        deployment.miniRovRoot = miniRovGo.transform;

        Rigidbody rovRb =
            miniRovGo.GetComponent<Rigidbody>();

        if (rovRb == null)
        {
            rovRb = miniRovGo.AddComponent<Rigidbody>();
        }

        rovRb.mass = 0.60f;
        rovRb.useGravity = false;
        rovRb.isKinematic = true;
        rovRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        deployment.miniRovRigidbody = rovRb;

        Transform tetherPoint =
            FindDeepChild(miniRovGo.transform, "MiniROV_TetherPoint");

        if (tetherPoint == null)
        {
            tetherPoint = FindDeepChild(miniRovGo.transform, "TetherPoint");
        }

        if (tetherPoint == null)
        {
            tetherPoint = FindDeepChild(miniRovGo.transform, "TetherAnchor");
        }

        if (tetherPoint == null)
        {
            GameObject tp = new GameObject("MiniROV_TetherPoint");
            tp.transform.SetParent(miniRovGo.transform, false);
            tp.transform.localPosition = Vector3.zero;
            tp.transform.localRotation = Quaternion.identity;
            tetherPoint = tp.transform;
        }

        deployment.miniRovTetherPoint = tetherPoint;
        deployment.miniRovColliders = miniRovGo.GetComponentsInChildren<Collider>(true);

        deployment.miniRovCarrySlot = FindDeepChild(drone.transform, "MiniROV_CarrySlot");
        deployment.hookVisual = tether.movingTetherEndVisual;

        deployment.deploymentEnabled = true;
        deployment.stowOnStart = true;
        deployment.snapToCarrySlotOnSetup = true;
        deployment.disableCollidersWhenStowed = true;
        deployment.requireTetherReady = true;

        deployment.miniRovMassKg = 0.60f;
        deployment.useGravityWhenReleased = true;
        deployment.unparentWhenReleased = true;

        deployment.dockingCaptureDistanceM = 0.35f;
        deployment.dockingCaptureSpeedMS = 0.35f;
        deployment.autoDockWhenRecovered = true;

        EditorUtility.SetDirty(deployment);
        EditorUtility.SetDirty(rovRb);

        if (deployment.snapToCarrySlotOnSetup)
        {
            deployment.StowMiniRov();
        }

        MIMISKMiniROVDeploymentLogger logger =
            drone.GetComponent<MIMISKMiniROVDeploymentLogger>();

        if (logger == null)
        {
            logger = drone.AddComponent<MIMISKMiniROVDeploymentLogger>();
        }

        logger.deployment = deployment;
        logger.tether = tether;
        logger.missionManager = deployment.missionManager;
        logger.enableLogging = true;
        logger.logHz = 50.0f;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(logger);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Phase 3B MiniROV deployment configured. Use U deploy, R recover, K stop, D dock.");
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
