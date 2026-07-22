using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKRestoreStandaloneTetherBaselineSetup
{
    [MenuItem("MIMISK/Drone/Tether/RESTORE Accepted Standalone Tether Baseline")]
    public static void RestoreStandaloneTetherBaseline()
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

        // Disable full integration handoff and experimental gates.
        DisableByClassName(drone, "MIMISKDroneTetherHandoffMission");
        DisableByClassName(drone, "MIMISKTetherDeploymentTestMission");
        DisableByClassName(drone, "MIMISKDroneSurfaceIdleAntiVibrationGate");
        DisableByClassName(drone, "MIMISKMiniROVPreDeploymentSafetyGuard");

        // Disable older conflicting partial managers.
        DisableByClassName(drone, "MIMISKMiniROVDeploymentManager");
        DisableByClassName(drone, "MIMISKMiniROVCableEndAttachmentManager");
        DisableByClassName(drone, "MIMISKMiniROVWaterReleaseController");

        MIMISKDroneCoreMissionManager mission =
            drone.GetComponent<MIMISKDroneCoreMissionManager>();

        if (mission != null)
        {
            mission.missionEnabled = false;
            mission.missionActive = false;
            mission.missionState =
                MIMISKDroneCoreMissionManager.MissionState.ReadyForTetherDeployment;
            mission.lastMissionEvent =
                "standalone_tether_baseline_ready";
            EditorUtility.SetDirty(mission);
        }

        MIMISKDroneCoreFlightModeManager flight =
            drone.GetComponent<MIMISKDroneCoreFlightModeManager>();

        if (flight != null)
        {
            flight.flightMode =
                MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceStable;
            flight.lastModeEvent =
                "standalone_tether_baseline_surface_stable";

            // Restore accepted landing parameters for future full mission use.
            flight.surfaceHoldHeightM = 0.20f;
            flight.landingDescentSpeedMS = 0.35f;
            flight.landingTouchdownVerticalSpeedMS = 0.35f;
            flight.requireSettledBeforeSurfaceStable = false;
            flight.requireMeasuredWaterContactForSurfaceStable = false;
            flight.descendBelowSurfaceUntilMeasuredContact = false;

            EditorUtility.SetDirty(flight);
        }

        MIMISKDroneCoreRotorController core =
            drone.GetComponent<MIMISKDroneCoreRotorController>();

        if (core != null)
        {
            core.controlMode =
                MIMISKDroneCoreRotorController.ControlMode.Disabled;
            core.CutMotors();
            EditorUtility.SetDirty(core);
        }

        MIMISKDroneCoreTetherManager tether =
            drone.GetComponent<MIMISKDroneCoreTetherManager>();

        if (tether == null)
        {
            tether = drone.AddComponent<MIMISKDroneCoreTetherManager>();
        }

        tether.acceptKeyboardCommands = false;
        tether.tetherSystemEnabled = true;
        tether.requireSurfaceStable = true;
        tether.allowManualDeploymentWhenSurfaceStable = true;

        tether.deployedLengthM = tether.minimumLengthM;
        tether.targetLengthM = tether.minimumLengthM;
        tether.targetDeployLengthM = 3.0f;

        tether.payoutSpeedMS = 0.22f;
        tether.recoverySpeedMS = 0.25f;
        tether.tetherState = MIMISKDroneCoreTetherManager.TetherState.Ready;

        tether.enableTetherForceWhenMiniRovAttached = false;
        tether.tetherStiffnessNPerM = 0.0f;
        tether.tetherDampingNPerMS = 0.0f;
        tether.maximumSafeTensionN = 999999.0f;

        tether.AutoFindReferences();
        EditorUtility.SetDirty(tether);

        MIMISKMiniROVRealisticDeploymentManager deploy =
            drone.GetComponent<MIMISKMiniROVRealisticDeploymentManager>();

        if (deploy == null)
        {
            deploy = drone.AddComponent<MIMISKMiniROVRealisticDeploymentManager>();
        }

        deploy.tetherManager = tether;
        deploy.missionManager = mission;
        deploy.flightManager = flight;

        deploy.acceptKeyboardCommands = false;
        deploy.deploymentEnabled = true;

        deploy.targetDeployLengthM = 3.0f;
        deploy.payoutSpeedMS = 0.22f;
        deploy.recoverySpeedMS = 0.25f;

        deploy.releaseDepthBelowSurfaceM = 0.25f;
        deploy.minimumPayoutBeforeReleaseM = 0.45f;
        deploy.postWaterTouchStabilizationS = 1.50f;

        deploy.stopReelAtWaterTouch = true;
        deploy.autoReleaseToDynamicAtWaterDepth = false;
        deploy.stopAndHoldKinematicAtReleaseDepth = true;
        deploy.disableTetherForceForNow = true;
        deploy.adaptiveSlackManagement = true;
        deploy.enableRovControlAfterStabilization = true;

        deploy.ignoreMiniRovDroneCollisions = true;
        deploy.usePhysicsIgnoreCollision = false;
        deploy.disableMiniRovCollidersDuringCableFollow = true;
        deploy.disableMiniRovCollidersDuringRecovery = true;

        deploy.AutoFindReferences();
        deploy.AttachRovToCableEndpoint();

        EditorUtility.SetDirty(deploy);

        MIMISKStandaloneTetherDeploymentMission standalone =
            drone.GetComponent<MIMISKStandaloneTetherDeploymentMission>();

        if (standalone == null)
        {
            standalone = drone.AddComponent<MIMISKStandaloneTetherDeploymentMission>();
        }

        standalone.core = core;
        standalone.flightManager = flight;
        standalone.missionManager = mission;
        standalone.tetherManager = tether;
        standalone.rovDeployment = deploy;
        standalone.droneRigidbody = drone.GetComponent<Rigidbody>();

        standalone.standaloneEnabled = true;
        standalone.disableFullMissionDuringStandaloneTest = true;
        standalone.forceSurfaceStableForStandaloneTest = true;

        standalone.targetDeployLengthM = 3.0f;
        standalone.payoutSpeedMS = 0.22f;
        standalone.recoverySpeedMS = 0.25f;
        standalone.releaseDepthBelowSurfaceM = 0.25f;
        standalone.minimumPayoutBeforeReleaseM = 0.45f;
        standalone.stabilizationSeconds = 1.50f;

        standalone.state =
            MIMISKStandaloneTetherDeploymentMission.StandaloneState.ReadyForDeploy;
        standalone.lastEvent =
            "restored_accepted_standalone_tether_baseline";

        EditorUtility.SetDirty(standalone);

        MIMISKMiniROVRealisticDeploymentLogger logger =
            drone.GetComponent<MIMISKMiniROVRealisticDeploymentLogger>();

        if (logger == null)
        {
            logger = drone.AddComponent<MIMISKMiniROVRealisticDeploymentLogger>();
        }

        logger.deployment = deploy;
        logger.tether = tether;
        logger.missionManager = mission;
        logger.flightManager = flight;
        logger.enableLogging = true;
        logger.logHz = 50.0f;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(logger);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MIMISK] Restored accepted standalone tether baseline. " +
            "Use P to prepare, U deploy, K hold, R recover, D reattach. " +
            "Full integrated handoff is disabled."
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
