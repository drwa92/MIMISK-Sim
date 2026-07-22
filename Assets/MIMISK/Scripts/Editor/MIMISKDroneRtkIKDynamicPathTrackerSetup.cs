using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneRtkIKDynamicPathTrackerSetup
{
    [MenuItem("MIMISK/Drone/Autonomy/Setup RTK IK-Dynamic Path Tracker")]
    public static void SetupTracker()
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

        MIMISKDroneRtkIKDynamicPathTracker tracker =
            drone.GetComponent<MIMISKDroneRtkIKDynamicPathTracker>();

        if (tracker == null)
        {
            tracker = drone.AddComponent<MIMISKDroneRtkIKDynamicPathTracker>();
        }

        tracker.aquaLoc = drone.GetComponent<MIMISKDroneAquaLocEstimator>();
        tracker.controller = drone.GetComponent<MIMISKDroneModelController>();
        tracker.udpReceiver = drone.GetComponent<MIMISKDroneUdpGamepadReceiver>();

        tracker.missionActive = false;
        tracker.trackState = MIMISKDroneRtkIKDynamicPathTracker.TrackState.Idle;

        tracker.localControlPointOffsets = new Vector3[]
        {
            new Vector3(0.0f, 0.0f, 3.0f),
            new Vector3(2.0f, 0.0f, 3.0f),
            new Vector3(2.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 0.0f, 0.0f)
        };

        tracker.nominalPathSpeedMS = 0.42f;
        tracker.finalApproachSpeedMS = 0.16f;
        tracker.finalApproachDistanceM = 0.70f;

        tracker.enableErrorAdaptiveProgress = true;
        tracker.progressSlowdownErrorM = 0.55f;
        tracker.minProgressSpeedScale = 0.30f;

        tracker.ikPositionGainL1 = 0.80f;
        tracker.ikErrorScaleL2 = 1.70f;
        tracker.maxDesiredVelocityMS = 0.75f;

        tracker.outerVelocityDamping = 0.45f;
        tracker.finalOuterVelocityDamping = 1.25f;

        tracker.enableInverseDynamics = true;
        tracker.freezeLearningDuringFinalHold = true;
        tracker.velocityErrorGainKD = 2.8f;
        tracker.maxDesiredAccelMS2 = 2.0f;

        tracker.maxCommandMagnitude = 0.80f;
        tracker.commandResponseHz = 8.0f;
        tracker.maxCommandSlewPerSecond = 3.0f;

        tracker.finalHoldRadiusM = 0.26f;
        tracker.finalHoldSpeedMS = 0.16f;
        tracker.finalStableRequiredSeconds = 0.8f;
        tracker.finalLoiterSeconds = 2.0f;
        tracker.restoreGamepadOnComplete = true;

        tracker.forwardAxis.Reset(0.75f, 0.45f);
        tracker.forwardAxis.enableLearning = false;
        tracker.rightAxis.Reset(0.78f, 0.40f);
        tracker.rightAxis.enableLearning = false;

        EditorUtility.SetDirty(tracker);

        MIMISKDroneRtkIKDynamicPathTrackerLogger logger =
            drone.GetComponent<MIMISKDroneRtkIKDynamicPathTrackerLogger>();

        if (logger == null)
        {
            logger = drone.AddComponent<MIMISKDroneRtkIKDynamicPathTrackerLogger>();
        }

        logger.tracker = tracker;
        logger.aquaLoc = tracker.aquaLoc;
        logger.enableLogging = true;
        logger.logHz = 50.0f;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(logger);

        DisableComponentByClassName(drone, "MIMISKDroneAquaLocWaypointNavigator");
        DisableComponentByClassName(drone, "MIMISKDroneAquaLocWaypointNavigatorLogger");
        DisableComponentByClassName(drone, "MIMISKDroneAquaTrackPathFollower");
        DisableComponentByClassName(drone, "MIMISKDroneAquaTrackPathFollowerLogger");
        DisableComponentByClassName(drone, "MIMISKDroneAquaSplinePFPathFollower");
        DisableComponentByClassName(drone, "MIMISKDroneAquaSplinePFPathFollowerLogger");
        DisableComponentByClassName(drone, "MIMISKDroneAquaDynTrackController");
        DisableComponentByClassName(drone, "MIMISKDroneAquaDynTrackLogger");

        MIMISKDroneUdpGamepadReceiver udp =
            drone.GetComponent<MIMISKDroneUdpGamepadReceiver>();

        if (udp != null)
        {
            udp.enabled = true;
            EditorUtility.SetDirty(udp);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] RTK IK-Dynamic path tracker configured. Press N to start, B to abort.");
    }

    private static void DisableComponentByClassName(GameObject go, string className)
    {
        Type t = FindTypeByName(className);

        if (t == null)
        {
            return;
        }

        Component c = go.GetComponent(t);

        if (c == null)
        {
            return;
        }

        Behaviour b = c as Behaviour;

        if (b != null)
        {
            b.enabled = false;
            EditorUtility.SetDirty(b);
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
