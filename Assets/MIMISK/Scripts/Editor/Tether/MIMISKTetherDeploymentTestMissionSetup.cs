using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKTetherDeploymentTestMissionSetup
{
    [MenuItem("MIMISK/Drone/Tether/Setup Tether Deployment Test Mission")]
    public static void SetupTetherDeploymentTestMission()
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

        // Disable older partial deployment managers and their loggers.
        DisableByClassName(drone, "MIMISKMiniROVDeploymentManager");
        DisableByClassName(drone, "MIMISKMiniROVCableEndAttachmentManager");
        DisableByClassName(drone, "MIMISKMiniROVWaterReleaseController");

        DisableByClassName(drone, "MIMISKMiniROVDeploymentLogger");
        DisableByClassName(drone, "MIMISKMiniROVCableEndAttachmentLogger");
        DisableByClassName(drone, "MIMISKMiniROVWaterReleaseLogger");

        MIMISKDroneCoreTetherManager tether =
            drone.GetComponent<MIMISKDroneCoreTetherManager>();

        if (tether == null)
        {
            tether = drone.AddComponent<MIMISKDroneCoreTetherManager>();
        }

        MIMISKMiniROVRealisticDeploymentManager deploy =
            drone.GetComponent<MIMISKMiniROVRealisticDeploymentManager>();

        if (deploy == null)
        {
            deploy = drone.AddComponent<MIMISKMiniROVRealisticDeploymentManager>();
        }

        MIMISKTetherDeploymentTestMission test =
            drone.GetComponent<MIMISKTetherDeploymentTestMission>();

        if (test == null)
        {
            test = drone.AddComponent<MIMISKTetherDeploymentTestMission>();
        }

        test.core =
            drone.GetComponent<MIMISKDroneCoreRotorController>();

        test.flightManager =
            drone.GetComponent<MIMISKDroneCoreFlightModeManager>();

        test.missionManager =
            drone.GetComponent<MIMISKDroneCoreMissionManager>();

        test.tetherManager = tether;
        test.rovDeployment = deploy;
        test.droneRigidbody = drone.GetComponent<Rigidbody>();

        test.testMissionEnabled = true;
        test.disableFullMissionManagerDuringTest = true;
        test.forceReadyForTetherDeployment = true;
        test.forceSurfaceStableModeForTest = true;
        test.autoDeployAfterPrepare = false;

        test.targetDeployLengthM = 3.0f;
        test.payoutSpeedMS = 0.22f;
        test.recoverySpeedMS = 0.25f;
        test.releaseDepthBelowSurfaceM = 0.25f;
        test.minimumPayoutBeforeReleaseM = 0.45f;
        test.stabilizationSeconds = 1.5f;

        test.enableLogging = true;
        test.logHz = 50.0f;
        test.flushEveryLine = false;

        EditorUtility.SetDirty(test);

        // Configure existing realistic deployment manager.
        deploy.tetherManager = tether;
        deploy.missionManager = test.missionManager;
        deploy.flightManager = test.flightManager;
        deploy.acceptKeyboardCommands = false;

        deploy.targetDeployLengthM = test.targetDeployLengthM;
        deploy.payoutSpeedMS = test.payoutSpeedMS;
        deploy.recoverySpeedMS = test.recoverySpeedMS;
        deploy.releaseDepthBelowSurfaceM = test.releaseDepthBelowSurfaceM;
        deploy.minimumPayoutBeforeReleaseM = test.minimumPayoutBeforeReleaseM;
        deploy.postWaterTouchStabilizationS = test.stabilizationSeconds;

        deploy.stopReelAtWaterTouch = true;
        deploy.enableRovControlAfterStabilization = true;
        deploy.disableTetherForceForNow = true;
        deploy.adaptiveSlackManagement = true;

        EditorUtility.SetDirty(deploy);

        // Configure tether keyboard ownership.
        tether.acceptKeyboardCommands = false;
        tether.targetDeployLengthM = test.targetDeployLengthM;
        tether.payoutSpeedMS = test.payoutSpeedMS;
        tether.recoverySpeedMS = test.recoverySpeedMS;
        tether.enableTetherForceWhenMiniRovAttached = false;
        tether.tetherStiffnessNPerM = 0.0f;
        tether.tetherDampingNPerMS = 0.0f;
        tether.maximumSafeTensionN = 999999.0f;

        EditorUtility.SetDirty(tether);

        // Disable full mission manager keyboard so P starts only the tether test.
        MIMISKDroneCoreMissionManager mission =
            drone.GetComponent<MIMISKDroneCoreMissionManager>();

        if (mission != null)
        {
            mission.missionEnabled = false;
            mission.missionActive = false;
            mission.missionState =
                MIMISKDroneCoreMissionManager.MissionState.ReadyForTetherDeployment;

            mission.lastMissionEvent =
                "tether_test_setup_ready";

            EditorUtility.SetDirty(mission);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MIMISK] Tether deployment test mission configured. " +
            "Press P to prepare ReadyForTetherDeployment, U deploy, R recover, K hold, D reattach."
        );
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
