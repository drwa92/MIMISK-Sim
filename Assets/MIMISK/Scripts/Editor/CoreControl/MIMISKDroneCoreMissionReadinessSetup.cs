using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneCoreMissionReadinessSetup
{
    [MenuItem("MIMISK/Drone/Core Control/Apply Phase 1 Mission Readiness Patch")]
    public static void ApplyPhase1MissionReadiness()
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

        MIMISKDroneCoreRotorController core =
            drone.GetComponent<MIMISKDroneCoreRotorController>();

        MIMISKDroneCoreFlightModeManager manager =
            drone.GetComponent<MIMISKDroneCoreFlightModeManager>();

        MIMISKDroneCoreTrajectoryPlanner planner =
            drone.GetComponent<MIMISKDroneCoreTrajectoryPlanner>();

        if (core == null || manager == null)
        {
            Debug.LogError("[MIMISK] Core controller or flight mode manager missing. Run Clean Core Flight Stack setup first.");
            return;
        }

        manager.useSmoothTakeoffReference = true;
        manager.takeoffSmoothDurationS = 4.0f;

        manager.enablePathCompletionBrake = true;
        manager.pathCompletionBrakeSeconds = 2.0f;

        manager.useTrajectoryPlanner = planner != null;
        manager.trajectoryPlanner = planner;

        manager.stopMotorsOnlyAfterWaterContact = true;
        manager.landingTouchdownVerticalSpeedMS = 0.35f;
        manager.waterContactToleranceM = 0.04f;

        if (manager.surfaceBuoyancy == null)
        {
            manager.surfaceBuoyancy =
                drone.GetComponent<MIMISKDroneSurfaceBuoyancy>();
        }

        manager.autoFindWaterContactSensors = true;
        manager.AutoFindWaterContactSensors();

        EditorUtility.SetDirty(manager);

        MIMISKDroneCoreMissionReadinessLogger logger =
            drone.GetComponent<MIMISKDroneCoreMissionReadinessLogger>();

        if (logger == null)
        {
            logger = drone.AddComponent<MIMISKDroneCoreMissionReadinessLogger>();
        }

        logger.core = core;
        logger.manager = manager;
        logger.trajectoryPlanner = planner;
        logger.propellerBridge =
            drone.GetComponent<MIMISKDroneCorePropellerAnimationBridge>();
        logger.propellerAnimator =
            drone.GetComponent<MIMISKDronePropellerAnimator>();
        logger.surfaceBuoyancy =
            drone.GetComponent<MIMISKDroneSurfaceBuoyancy>();
        logger.rb =
            drone.GetComponent<Rigidbody>();

        logger.enableLogging = true;
        logger.logHz = 50.0f;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(logger);

        if (planner != null)
        {
            EditorUtility.SetDirty(planner);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Phase 1 mission-readiness patch applied: S-curve takeoff, path brake, readiness logger.");
    }
}
