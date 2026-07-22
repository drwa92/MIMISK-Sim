using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneCoreTrajectoryPlannerSetup
{
    [MenuItem("MIMISK/Drone/Core Control/Setup Core Trajectory Planner")]
    public static void SetupTrajectoryPlanner()
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

        MIMISKDroneCoreTrajectoryPlanner planner =
            drone.GetComponent<MIMISKDroneCoreTrajectoryPlanner>();

        if (planner == null)
        {
            planner = drone.AddComponent<MIMISKDroneCoreTrajectoryPlanner>();
        }

        planner.trajectoryType = MIMISKDroneCoreTrajectoryPlanner.TrajectoryType.Circle;
        planner.yawAlongPath = false;

        planner.circleRadiusM = 1.4f;
        planner.circleOmegaRadS = 0.23f;
        planner.circleDurationS = 40.0f;

        planner.spiralInitialRadiusM = 0.25f;
        planner.spiralFinalRadiusM = 1.25f;
        planner.spiralOmegaRadS = 0.28f;
        planner.spiralDurationS = 36.0f;
        planner.spiralAltitudeRiseM = 0.35f;

        planner.helixRadiusM = 1.5f;
        planner.helixOmegaRadS = 0.32f;
        planner.helixDurationS = 30.0f;
        planner.helixVerticalChangeM = -0.8f;

        planner.helixUpDownRadiusM = 1.5f;
        planner.helixUpDownOmegaRadS = 0.28f;
        planner.helixUpDownDurationS = 40.0f;
        planner.helixUpDownVerticalAmplitudeM = 0.75f;

        planner.figureEightDurationS = 32.0f;
        planner.figureEightXAmplitudeM = 1.5f;
        planner.figureEightZAmplitudeM = 0.75f;
        planner.figureEightVerticalAmplitudeM = 0.0f;

        planner.squareSideM = 2.4f;
        planner.squareSpeedMS = 0.32f;

        planner.lawnmowerLengthM = 4.0f;
        planner.lawnmowerWidthM = 2.5f;
        planner.lawnmowerLanes = 4;
        planner.lawnmowerSpeedMS = 0.35f;

        planner.deploymentForwardDistanceM = 3.0f;
        planner.deploymentSpeedMS = 0.35f;
        planner.deploymentFinalHoldSeconds = 3.0f;

        EditorUtility.SetDirty(planner);

        MIMISKDroneCoreFlightModeManager manager =
            drone.GetComponent<MIMISKDroneCoreFlightModeManager>();

        if (manager != null)
        {
            manager.useTrajectoryPlanner = true;
            manager.trajectoryPlanner = planner;
            EditorUtility.SetDirty(manager);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Core trajectory planner configured. Select trajectory type in Inspector, then press N during Play.");
    }
}
