using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMiniROVRealisticDeploymentSetup
{
    [MenuItem("MIMISK/Drone/Tether/Setup Phase 3C Realistic MiniROV Deployment")]
    public static void SetupRealisticMiniRovDeployment()
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
            Transform found = FindDeepChild(drone.transform.root, "MiniROV");

            if (found != null)
            {
                miniRovGo = found.gameObject;
            }
        }

        if (miniRovGo == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV GameObject.");
            return;
        }

        // Disable older partial MiniROV deployment managers to avoid conflicting keyboard ownership.
        DisableByClassName(drone, "MIMISKMiniROVDeploymentManager");
        DisableByClassName(drone, "MIMISKMiniROVCableEndAttachmentManager");
        DisableByClassName(drone, "MIMISKMiniROVWaterReleaseController");
        DisableByClassName(drone, "MIMISKMiniROVWaterReleaseLogger");
        DisableByClassName(drone, "MIMISKMiniROVCableEndAttachmentLogger");
        DisableByClassName(drone, "MIMISKMiniROVDeploymentLogger");

        MIMISKDroneCoreTetherManager tether =
            drone.GetComponent<MIMISKDroneCoreTetherManager>();

        if (tether == null)
        {
            tether = drone.AddComponent<MIMISKDroneCoreTetherManager>();
        }

        tether.acceptKeyboardCommands = false;
        tether.useVirtualEndpointWhenNoMiniRov = true;
        tether.enableTetherForceWhenMiniRovAttached = false;
        tether.tetherStiffnessNPerM = 0.0f;
        tether.tetherDampingNPerMS = 0.0f;
        tether.maximumSafeTensionN = 999999.0f;
        tether.targetDeployLengthM = 3.0f;
        tether.payoutSpeedMS = 0.22f;
        tether.recoverySpeedMS = 0.25f;

        Transform yellowCable =
            FindDeepChild(drone.transform, "real_mesh_short_yellow_deployment_cable_to_hook");

        Transform hook =
            FindDeepChild(drone.transform, "small_dark_open_deployment_hook_for_miniROV");

        Transform followRoot =
            FindDeepChild(drone.transform, "MiniROV_CableEndFollowRoot");

        if (followRoot == null)
        {
            GameObject go = new GameObject("MiniROV_CableEndFollowRoot");
            go.transform.SetParent(drone.transform, false);
            followRoot = go.transform;
        }

        if (yellowCable != null)
        {
            followRoot.SetPositionAndRotation(yellowCable.position, yellowCable.rotation);
        }

        followRoot.localScale = Vector3.one;

        tether.movingTetherEndVisual = yellowCable;
        tether.staticShortDeploymentCableMesh = null;
        tether.hideStaticShortCableMeshWhenDynamic = false;

        EditorUtility.SetDirty(tether);

        Rigidbody rovRb =
            miniRovGo.GetComponent<Rigidbody>();

        if (rovRb == null)
        {
            rovRb = miniRovGo.AddComponent<Rigidbody>();
        }

        rovRb.mass = 0.60f;
        rovRb.isKinematic = true;
        rovRb.useGravity = false;

        Transform rovAnchor =
            FindDeepChild(miniRovGo.transform, "ROV_TetherAnchor");

        if (rovAnchor == null)
        {
            rovAnchor = FindDeepChild(miniRovGo.transform, "MiniROV_TetherPoint");
        }

        if (rovAnchor == null)
        {
            rovAnchor = FindDeepChild(miniRovGo.transform, "TetherPoint");
        }

        MIMISKMiniROVRealisticDeploymentManager deploy =
            drone.GetComponent<MIMISKMiniROVRealisticDeploymentManager>();

        if (deploy == null)
        {
            deploy = drone.AddComponent<MIMISKMiniROVRealisticDeploymentManager>();
        }

        deploy.tetherManager = tether;
        deploy.missionManager = drone.GetComponent<MIMISKDroneCoreMissionManager>();
        deploy.flightManager = drone.GetComponent<MIMISKDroneCoreFlightModeManager>();

        deploy.miniRovRoot = miniRovGo.transform;
        deploy.miniRovRigidbody = rovRb;
        deploy.miniRovTetherAnchor = rovAnchor;
        deploy.miniRovColliders = miniRovGo.GetComponentsInChildren<Collider>(true);

        deploy.yellowCableEndPoint = yellowCable;
        deploy.hookVisual = hook;
        deploy.cableEndFollowRoot = followRoot;

        deploy.deploymentEnabled = true;
        deploy.requireMissionReady = true;
        deploy.requireSurfaceStable = true;
        deploy.attachMiniRovOnStart = true;

        deploy.alignRovTetherAnchorToCableEnd = true;
        deploy.miniRovLocalOffsetOnCableEnd = Vector3.zero;
        deploy.miniRovLocalEulerOnCableEnd = Vector3.zero;
        deploy.miniRovLocalScaleOnCableEnd = Vector3.one;
        deploy.forceMiniRovScaleOnAttach = true;

        deploy.targetDeployLengthM = 3.0f;
        deploy.payoutSpeedMS = 0.22f;
        deploy.recoverySpeedMS = 0.25f;

        deploy.waterSurfaceY = 0.0f;
        deploy.waterTouchMarginM = 0.03f;
        deploy.releaseDepthBelowSurfaceM = 0.25f;
        deploy.minimumPayoutBeforeReleaseM = 0.45f;
        deploy.stopReelAtWaterTouch = true;
        deploy.autoReleaseToDynamicAtWaterDepth = false;
        deploy.stopAndHoldKinematicAtReleaseDepth = true;
        deploy.postWaterTouchStabilizationS = 1.50f;

        deploy.keepRovKinematicBeforeWaterTouch = true;
        deploy.disableRovControlBeforeWaterTouch = true;
        deploy.enableRovWaterPhysicsAtTouch = true;
        deploy.enableRovControlAfterStabilization = true;
        deploy.enableCollidersAfterWaterTouch = true;
        deploy.ignoreMiniRovDroneCollisions = true;
        deploy.disableMiniRovCollidersDuringCableFollow = true;
        deploy.disableMiniRovCollidersDuringRecovery = true;
        deploy.applySmallDownwardVelocityAtRelease = true;
        deploy.initialDownwardVelocityMS = 0.10f;

        deploy.disableTetherForceForNow = true;
        deploy.adaptiveSlackManagement = true;
        deploy.desiredOperationalSlackM = 0.20f;
        deploy.slackDeadbandM = 0.05f;
        deploy.allowAutoSlackRecovery = true;

        deploy.kinematicRecoveryForNow = true;
        deploy.recoveredLengthToleranceM = 0.01f;

        EditorUtility.SetDirty(deploy);

        deploy.AttachRovToCableEndpoint();

        MIMISKMiniROVRealisticDeploymentLogger logger =
            drone.GetComponent<MIMISKMiniROVRealisticDeploymentLogger>();

        if (logger == null)
        {
            logger = drone.AddComponent<MIMISKMiniROVRealisticDeploymentLogger>();
        }

        logger.deployment = deploy;
        logger.tether = tether;
        logger.missionManager = deploy.missionManager;
        logger.flightManager = deploy.flightManager;
        logger.enableLogging = true;
        logger.logHz = 50.0f;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(logger);
        EditorUtility.SetDirty(rovRb);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Phase 3C realistic MiniROV deployment configured. Use U deploy, R recover, K stop, D reattach.");
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
            Transform found = FindDeepChild(root.GetChild(i), childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void DisableByClassName(GameObject root, string className)
    {
        Type t = FindTypeByName(className);

        if (t == null)
        {
            return;
        }

        Component[] components =
            root.GetComponentsInChildren(t, true);

        if (components == null)
        {
            return;
        }

        for (int i = 0; i < components.Length; i++)
        {
            Behaviour b = components[i] as Behaviour;

            if (b != null)
            {
                b.enabled = false;
                EditorUtility.SetDirty(b);
            }
        }
    }

    private static Type FindTypeByName(string className)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = assembly.GetType(className);

            if (t != null)
            {
                return t;
            }

            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch
            {
                continue;
            }

            for (int i = 0; i < types.Length; i++)
            {
                if (types[i].Name == className)
                {
                    return types[i];
                }
            }
        }

        return null;
    }
}
