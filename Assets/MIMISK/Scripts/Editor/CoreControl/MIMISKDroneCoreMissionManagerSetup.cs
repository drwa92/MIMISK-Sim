using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneCoreMissionManagerSetup
{
    [MenuItem("MIMISK/Drone/Core Control/Setup Phase 2 MIMISK Mission Manager")]
    public static void SetupMissionManager()
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

        MIMISKDroneCoreMissionManager mission =
            drone.GetComponent<MIMISKDroneCoreMissionManager>();

        if (mission == null)
        {
            mission = drone.AddComponent<MIMISKDroneCoreMissionManager>();
        }

        mission.core =
            drone.GetComponent<MIMISKDroneCoreRotorController>();

        mission.flightManager =
            drone.GetComponent<MIMISKDroneCoreFlightModeManager>();

        mission.trajectoryPlanner =
            drone.GetComponent<MIMISKDroneCoreTrajectoryPlanner>();

        mission.propellerBridge =
            drone.GetComponent<MIMISKDroneCorePropellerAnimationBridge>();

        mission.surfaceBuoyancy =
            drone.GetComponent<MIMISKDroneSurfaceBuoyancy>();

        mission.rb =
            drone.GetComponent<Rigidbody>();

        mission.missionEnabled = true;
        mission.runOnStart = false;
        mission.missionActive = false;
        mission.missionState = MIMISKDroneCoreMissionManager.MissionState.Idle;

        mission.transitTrajectory =
            MIMISKDroneCoreTrajectoryPlanner.TrajectoryType.DeploymentApproach;

        mission.surveyTrajectory =
            MIMISKDroneCoreTrajectoryPlanner.TrajectoryType.Lawnmower;

        mission.deploymentTrajectory =
            MIMISKDroneCoreTrajectoryPlanner.TrajectoryType.DeploymentApproach;

        mission.transitDistanceM = 3.0f;
        mission.transitSpeedMS = 0.35f;

        mission.surveyLengthM = 4.0f;
        mission.surveyWidthM = 2.5f;
        mission.surveyLanes = 4;
        mission.surveySpeedMS = 0.35f;

        mission.deploymentApproachDistanceM = 1.5f;
        mission.deploymentApproachSpeedMS = 0.22f;
        mission.deploymentFinalHoldSeconds = 2.0f;

        mission.takeoffIdleSeconds = 2.0f;
        mission.holdAfterTakeoffSeconds = 3.0f;
        mission.precisionHoldSeconds = 5.0f;
        mission.readyForTetherSeconds = 5.0f;

        mission.maxTakeoffSeconds = 30.0f;
        mission.maxTransitSeconds = 80.0f;
        mission.maxSurveySeconds = 120.0f;
        mission.maxDeploymentApproachSeconds = 60.0f;
        mission.maxLandingSeconds = 60.0f;

        EditorUtility.SetDirty(mission);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Phase 2 MIMISK mission manager configured. Press P during Play to start, O to abort.");
    }
}
