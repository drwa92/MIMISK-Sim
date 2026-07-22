using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneAquaPFTrackSetup
{
    [MenuItem("MIMISK/Drone/Autonomy/Setup AquaPF Track Path Follower")]
    public static void SetupAquaPFTrack()
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

        MIMISKDroneAquaLocEstimator aquaLoc =
            drone.GetComponent<MIMISKDroneAquaLocEstimator>();

        if (aquaLoc == null)
        {
            Debug.LogError("[MIMISK] AquaLoc estimator missing.");
            return;
        }

        MIMISKDroneAquaPFObserver pf =
            drone.GetComponent<MIMISKDroneAquaPFObserver>();

        if (pf == null)
        {
            pf = drone.AddComponent<MIMISKDroneAquaPFObserver>();
        }

        pf.aquaLoc = aquaLoc;
        pf.observerEnabled = true;
        pf.resetOnStart = true;
        pf.particleCount = 96;

        pf.initialPositionStdM = 0.20f;
        pf.initialVelocityStdMS = 0.10f;
        pf.processAccelStdMS2 = 0.35f;
        pf.processVelocityDamping = 0.06f;
        pf.measurementPositionSigmaM = 0.32f;
        pf.measurementVelocitySigmaMS = 0.18f;
        pf.useRobustLikelihood = true;
        pf.maxPositionInnovationM = 1.25f;
        pf.maxVelocityInnovationMS = 0.65f;
        pf.resampleEssRatio = 0.55f;
        pf.rougheningPositionStdM = 0.015f;
        pf.rougheningVelocityStdMS = 0.010f;
        pf.enableOutputSmoothing = true;
        pf.outputPositionResponseHz = 8.0f;
        pf.outputVelocityResponseHz = 10.0f;

        EditorUtility.SetDirty(pf);

        MIMISKDroneAquaTrackPathFollower tracker =
            drone.GetComponent<MIMISKDroneAquaTrackPathFollower>();

        if (tracker == null)
        {
            tracker = drone.AddComponent<MIMISKDroneAquaTrackPathFollower>();
        }

        tracker.aquaPF = pf;
        tracker.aquaLoc = aquaLoc;
        tracker.aquaHold = drone.GetComponent<MIMISKDroneAquaLocPositionHold>();
        tracker.controller = drone.GetComponent<MIMISKDroneModelController>();
        tracker.udpReceiver = drone.GetComponent<MIMISKDroneUdpGamepadReceiver>();

        tracker.missionActive = false;
        tracker.trackState = MIMISKDroneAquaTrackPathFollower.TrackState.Idle;

        tracker.localPathOffsets = new Vector3[]
        {
            new Vector3(0.0f, 0.0f, 3.0f),
            new Vector3(2.0f, 0.0f, 3.0f),
            new Vector3(2.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 0.0f, 0.0f)
        };

        tracker.pathSpeedMS = 0.70f;
        tracker.finalApproachSpeedMS = 0.22f;
        tracker.lookaheadM = 0.80f;
        tracker.minLookaheadM = 0.40f;
        tracker.maxLookaheadM = 1.15f;
        tracker.speedLookaheadGain = 0.22f;
        tracker.crossTrackGain = 1.05f;
        tracker.maxCrossTrackCorrectionMS = 0.60f;
        tracker.alongTrackSwitchM = 0.25f;
        tracker.predictedSwitchLookaheadS = 0.65f;
        tracker.finalApproachDistanceM = 0.70f;
        tracker.maxCommandMagnitude = 0.82f;
        tracker.commandResponseHz = 8.0f;
        tracker.maxCommandSlewPerSecond = 3.0f;
        tracker.useAquaHoldForFinalDeployment = true;
        tracker.finalHoldRadiusM = 0.22f;
        tracker.finalHoldSpeedMS = 0.12f;
        tracker.finalStableRequiredSeconds = 1.2f;
        tracker.finalLoiterSeconds = 3.0f;
        tracker.holdAtEnd = true;

        EditorUtility.SetDirty(tracker);

        MIMISKDroneAquaTrackPathFollowerLogger logger =
            drone.GetComponent<MIMISKDroneAquaTrackPathFollowerLogger>();

        if (logger == null)
        {
            logger = drone.AddComponent<MIMISKDroneAquaTrackPathFollowerLogger>();
        }

        logger.tracker = tracker;
        logger.aquaPF = pf;
        logger.aquaLoc = aquaLoc;
        logger.enableLogging = true;
        logger.logHz = 50.0f;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(logger);

        MIMISKDroneAquaLocWaypointNavigator oldNav =
            drone.GetComponent<MIMISKDroneAquaLocWaypointNavigator>();

        if (oldNav != null)
        {
            oldNav.enabled = false;
            EditorUtility.SetDirty(oldNav);
        }

        MIMISKDroneAquaLocWaypointNavigatorLogger oldNavLogger =
            drone.GetComponent<MIMISKDroneAquaLocWaypointNavigatorLogger>();

        if (oldNavLogger != null)
        {
            oldNavLogger.enabled = false;
            EditorUtility.SetDirty(oldNavLogger);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] AquaPF Track path follower configured. Press N to start, B to abort.");
    }
}
