using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneAquaDynTrackSetup
{
    [MenuItem("MIMISK/Drone/Autonomy/Setup AquaDynTrack Trajectory Controller")]
    public static void SetupAquaDynTrack()
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

        MIMISKDroneAquaPFCommandObserver pf =
            drone.GetComponent<MIMISKDroneAquaPFCommandObserver>();

        if (pf == null)
        {
            pf = drone.AddComponent<MIMISKDroneAquaPFCommandObserver>();
        }

        pf.aquaLoc = aquaLoc;
        pf.observerEnabled = true;
        pf.resetOnStart = true;
        pf.particleCount = 64;

        pf.initialPositionStdM = 0.18f;
        pf.initialVelocityStdMS = 0.10f;

        pf.useCommandPrediction = true;
        pf.commandVelocityResponseHz = 2.5f;
        pf.processAccelStdMS2 = 0.22f;
        pf.processVelocityRandomWalkMS = 0.015f;
        pf.processVelocityDamping = 0.02f;

        pf.measurementPositionSigmaM = 0.28f;
        pf.measurementVelocitySigmaMS = 0.18f;
        pf.useRobustLikelihood = true;
        pf.maxPositionInnovationM = 1.10f;
        pf.maxVelocityInnovationMS = 0.60f;

        pf.resampleEssRatio = 0.55f;
        pf.rougheningPositionStdM = 0.010f;
        pf.rougheningVelocityStdMS = 0.008f;

        pf.enableOutputSmoothing = true;
        pf.outputPositionResponseHz = 9.0f;
        pf.outputVelocityResponseHz = 10.0f;

        EditorUtility.SetDirty(pf);

        MIMISKDroneAquaDynTrackController tracker =
            drone.GetComponent<MIMISKDroneAquaDynTrackController>();

        if (tracker == null)
        {
            tracker = drone.AddComponent<MIMISKDroneAquaDynTrackController>();
        }

        tracker.aquaLoc = aquaLoc;
        tracker.commandPfObserver = pf;
        tracker.controller = drone.GetComponent<MIMISKDroneModelController>();
        tracker.udpReceiver = drone.GetComponent<MIMISKDroneUdpGamepadReceiver>();

        tracker.missionActive = false;
        tracker.trackState = MIMISKDroneAquaDynTrackController.TrackState.Idle;

        tracker.localControlPointOffsets = new Vector3[]
        {
            new Vector3(0.0f, 0.0f, 3.0f),
            new Vector3(2.0f, 0.0f, 3.0f),
            new Vector3(2.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 0.0f, 0.0f)
        };

        tracker.nominalPathSpeedMS = 0.45f;
        tracker.finalApproachSpeedMS = 0.18f;
        tracker.finalApproachDistanceM = 0.75f;

        tracker.lookaheadSeconds = 1.15f;
        tracker.minLookaheadM = 0.35f;
        tracker.maxLookaheadM = 0.90f;

        tracker.enableErrorAdaptiveProgress = true;
        tracker.progressSlowdownErrorM = 0.65f;
        tracker.minProgressSpeedScale = 0.35f;

        tracker.ikPositionGainL1 = 0.78f;
        tracker.ikErrorScaleL2 = 1.65f;
        tracker.velocityErrorDamping = 0.65f;
        tracker.maxDesiredVelocityMS = 0.82f;

        tracker.enableAdaptiveInverseDynamics = true;
        tracker.desiredVelocityResponse = 3.0f;
        tracker.maxDesiredAccelMS2 = 2.2f;

        tracker.maxCommandMagnitude = 0.82f;
        tracker.commandResponseHz = 8.0f;
        tracker.maxCommandSlewPerSecond = 3.2f;

        tracker.finalHoldRadiusM = 0.22f;
        tracker.finalHoldSpeedMS = 0.12f;
        tracker.finalStableRequiredSeconds = 1.20f;
        tracker.finalLoiterSeconds = 3.0f;
        tracker.holdAtEnd = true;

        tracker.forwardAxis.Reset(1.4f, -1.2f);
        tracker.rightAxis.Reset(1.4f, -1.2f);

        EditorUtility.SetDirty(tracker);

        MIMISKDroneAquaDynTrackLogger logger =
            drone.GetComponent<MIMISKDroneAquaDynTrackLogger>();

        if (logger == null)
        {
            logger = drone.AddComponent<MIMISKDroneAquaDynTrackLogger>();
        }

        logger.tracker = tracker;
        logger.pfObserver = pf;
        logger.aquaLoc = aquaLoc;
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

        MIMISKDroneUdpGamepadReceiver udp =
            drone.GetComponent<MIMISKDroneUdpGamepadReceiver>();

        if (udp != null)
        {
            udp.enabled = true;

            SerializedObject udpSO = new SerializedObject(udp);
            SetBoolIfExists(udpSO, "suppressCommandOutput", false);
            SetBoolIfExists(udpSO, "allowModeButtonsWhileSuppressed", false);
            udpSO.ApplyModifiedProperties();

            EditorUtility.SetDirty(udp);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] AquaDynTrack trajectory controller configured. Press N to start, B to abort.");
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

    private static void SetBoolIfExists(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);

        if (p != null)
        {
            p.boolValue = value;
        }
    }
}
