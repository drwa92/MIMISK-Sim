using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneAquaNavSetup
{
    [MenuItem("MIMISK/Drone/Autonomy/Setup AquaLoc Waypoint Navigator")]
    public static void SetupAquaNav()
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

        MIMISKDroneAquaLocWaypointNavigator nav =
            drone.GetComponent<MIMISKDroneAquaLocWaypointNavigator>();

        if (nav == null)
        {
            nav = drone.AddComponent<MIMISKDroneAquaLocWaypointNavigator>();
        }

        nav.aquaLoc = drone.GetComponent<MIMISKDroneAquaLocEstimator>();
        nav.aquaHold = drone.GetComponent<MIMISKDroneAquaLocPositionHold>();
        nav.keyboardStationKeeping = drone.GetComponent<MIMISKDroneKeyboardStationKeeping>();
        nav.udpReceiver = drone.GetComponent<MIMISKDroneUdpGamepadReceiver>();

        nav.missionActive = false;
        nav.navState = MIMISKDroneAquaLocWaypointNavigator.NavState.Idle;

        nav.localWaypointOffsets = new Vector3[]
        {
            new Vector3(0.0f, 0.0f, 3.0f),
            new Vector3(2.0f, 0.0f, 3.0f),
            new Vector3(2.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 0.0f, 0.0f)
        };

        nav.lineOfSightLookaheadM = 0.90f;
        nav.segmentSwitchDistanceM = 0.18f;
        nav.segmentSwitchPredictedDistanceM = 0.25f;
        nav.predictedSwitchLookaheadS = 0.70f;
        nav.desiredCorridorRadiusM = 0.35f;

        nav.slowNearFinalWaypoint = true;
        nav.finalApproachDistanceM = 0.65f;

        nav.finalWaypointIsDeploymentPoint = true;
        nav.finalWaypointRadiusM = 0.20f;
        nav.finalWaypointSpeedThresholdMS = 0.11f;
        nav.finalStableRequiredSeconds = 1.2f;
        nav.finalLoiterSeconds = 3.0f;

        nav.holdAtEnd = true;
        nav.landAtFinalWaypoint = false;
        nav.useCurrentYawForMission = true;

        EditorUtility.SetDirty(nav);

        MIMISKDroneAquaLocWaypointNavigatorLogger logger =
            drone.GetComponent<MIMISKDroneAquaLocWaypointNavigatorLogger>();

        if (logger == null)
        {
            logger = drone.AddComponent<MIMISKDroneAquaLocWaypointNavigatorLogger>();
        }

        logger.navigator = nav;
        logger.aquaLoc = drone.GetComponent<MIMISKDroneAquaLocEstimator>();
        logger.aquaHold = drone.GetComponent<MIMISKDroneAquaLocPositionHold>();
        logger.enableLogging = true;
        logger.logHz = 50.0f;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(logger);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] AquaNav v4 path-corridor navigator configured. Press N to start, B to abort.");
    }
}
