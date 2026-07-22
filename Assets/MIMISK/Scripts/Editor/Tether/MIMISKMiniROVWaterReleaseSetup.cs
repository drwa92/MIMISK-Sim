using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMiniROVWaterReleaseSetup
{
    [MenuItem("MIMISK/Drone/Tether/Setup Phase 3C MiniROV Water Release And Control")]
    public static void SetupWaterReleaseAndControl()
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
            Debug.LogError("[MIMISK] Could not find MiniROV GameObject.");
            return;
        }

        MIMISKDroneCoreTetherManager tether =
            drone.GetComponent<MIMISKDroneCoreTetherManager>();

        MIMISKMiniROVCableEndAttachmentManager cableAttachment =
            drone.GetComponent<MIMISKMiniROVCableEndAttachmentManager>();

        if (tether == null || cableAttachment == null)
        {
            Debug.LogError("[MIMISK] Run Phase 3 Core Tether Manager and Phase 3B Cable-End MiniROV Attachment first.");
            return;
        }

        Transform yellowCable =
            FindDeepChild(drone.transform, "real_mesh_short_yellow_deployment_cable_to_hook");

        Transform followRoot =
            FindDeepChild(drone.transform, "MiniROV_CableEndFollowRoot");

        Transform rovTether =
            FindDeepChild(miniRovGo.transform, "ROV_TetherAnchor");

        if (rovTether == null)
        {
            rovTether = FindDeepChild(miniRovGo.transform, "MiniROV_TetherPoint");
        }

        Rigidbody rovRb =
            miniRovGo.GetComponent<Rigidbody>();

        if (rovRb == null)
        {
            rovRb = miniRovGo.AddComponent<Rigidbody>();
        }

        rovRb.mass = 0.60f;
        rovRb.isKinematic = true;
        rovRb.useGravity = false;

        MIMISKMiniROVWaterReleaseController release =
            drone.GetComponent<MIMISKMiniROVWaterReleaseController>();

        if (release == null)
        {
            release = drone.AddComponent<MIMISKMiniROVWaterReleaseController>();
        }

        release.tetherManager = tether;
        release.cableAttachmentManager = cableAttachment;
        release.miniRovRoot = miniRovGo.transform;
        release.miniRovRigidbody = rovRb;
        release.miniRovTetherAnchor = rovTether;
        release.miniRovColliders = miniRovGo.GetComponentsInChildren<Collider>(true);
        release.yellowCableEndPoint = yellowCable;
        release.cableEndFollowRoot = followRoot;

        release.releaseEnabled = true;
        release.waterSurfaceY = 0.0f;
        release.waterTouchMarginM = 0.03f;
        release.postReleaseStabilizationSeconds = 1.50f;

        release.applyInitialDownwardVelocity = true;
        release.initialDownwardVelocityMS = 0.15f;

        release.enableGravityWhenReleased = true;
        release.enableCollidersWhenReleased = true;
        release.disableTetherForcesForNow = true;

        release.disableRovControlWhileCableFollowing = true;
        release.enableRovControlAfterStabilization = true;
        release.enableKinematicRecoveryWhenWinchRecovers = true;

        EditorUtility.SetDirty(release);

        release.ConfigureInitialCableFollowState();

        MIMISKMiniROVWaterReleaseLogger logger =
            drone.GetComponent<MIMISKMiniROVWaterReleaseLogger>();

        if (logger == null)
        {
            logger = drone.AddComponent<MIMISKMiniROVWaterReleaseLogger>();
        }

        logger.waterRelease = release;
        logger.tether = tether;
        logger.missionManager = drone.GetComponent<MIMISKDroneCoreMissionManager>();
        logger.flightManager = drone.GetComponent<MIMISKDroneCoreFlightModeManager>();
        logger.enableLogging = true;
        logger.logHz = 50.0f;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(logger);

        // Ensure tether is passive/logical only for this phase.
        tether.enableTetherForceWhenMiniRovAttached = false;
        tether.tetherStiffnessNPerM = 0.0f;
        tether.tetherDampingNPerMS = 0.0f;
        tether.maximumSafeTensionN = 999999.0f;

        EditorUtility.SetDirty(tether);
        EditorUtility.SetDirty(rovRb);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Phase 3C MiniROV water release/control configured. Deploy with U after ReadyForTetherDeployment.");
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
