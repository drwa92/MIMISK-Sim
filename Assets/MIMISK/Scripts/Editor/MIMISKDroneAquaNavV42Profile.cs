using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneAquaNavV42Profile
{
    [MenuItem("MIMISK/Drone/Autonomy/Apply AquaNav v4.2 Fast Vector-Field Corridor Profile")]
    public static void ApplyProfile()
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

        SerializedObject so = new SerializedObject(nav);

        SetObject(so, "aquaLoc", drone.GetComponent<MIMISKDroneAquaLocEstimator>());
        SetObject(so, "aquaHold", drone.GetComponent<MIMISKDroneAquaLocPositionHold>());
        SetObject(so, "keyboardStationKeeping", drone.GetComponent<MIMISKDroneKeyboardStationKeeping>());
        SetObject(so, "udpReceiver", drone.GetComponent<MIMISKDroneUdpGamepadReceiver>());

        SetBool(so, "missionActive", false);

        nav.localWaypointOffsets = new Vector3[]
        {
            new Vector3(0.0f, 0.0f, 3.0f),
            new Vector3(2.0f, 0.0f, 3.0f),
            new Vector3(2.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 0.0f, 0.0f)
        };

        // Faster corridor following.
        SetFloat(so, "lineOfSightLookaheadM", 1.35f);
        SetFloat(so, "segmentSwitchDistanceM", 0.32f);
        SetFloat(so, "segmentSwitchPredictedDistanceM", 0.45f);
        SetFloat(so, "predictedSwitchLookaheadS", 0.65f);
        SetFloat(so, "desiredCorridorRadiusM", 0.25f);

        // Stronger but bounded cross-track correction.
        SetBool(so, "enableCrossTrackTargetCorrection", true);
        SetFloat(so, "crossTrackCorrectionGain", 0.90f);
        SetFloat(so, "maxCrossTrackCorrectionM", 0.50f);

        // Dynamic lookahead: farther target for speed, shorter target when off-corridor.
        SetBool(so, "enableDynamicLookahead", true);
        SetFloat(so, "minLookaheadM", 0.75f);
        SetFloat(so, "maxLookaheadM", 1.50f);
        SetFloat(so, "speedLookaheadGain", 0.30f);
        SetFloat(so, "crossTrackLookaheadReductionM", 0.35f);

        SetBool(so, "enableMovingTargetSmoothing", true);
        SetFloat(so, "movingTargetResponse", 9.0f);

        SetBool(so, "slowNearFinalWaypoint", true);
        SetFloat(so, "finalApproachDistanceM", 0.80f);

        SetBool(so, "finalWaypointIsDeploymentPoint", true);
        SetFloat(so, "finalWaypointRadiusM", 0.22f);
        SetFloat(so, "finalWaypointSpeedThresholdMS", 0.13f);
        SetFloat(so, "finalStableRequiredSeconds", 0.8f);
        SetFloat(so, "finalLoiterSeconds", 2.0f);

        SetBool(so, "holdAtEnd", true);
        SetBool(so, "landAtFinalWaypoint", false);
        SetBool(so, "useCurrentYawForMission", true);

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(nav);

        MIMISKDroneAquaLocWaypointNavigatorLogger logger =
            drone.GetComponent<MIMISKDroneAquaLocWaypointNavigatorLogger>();

        if (logger != null)
        {
            logger.navigator = nav;
            logger.aquaLoc = drone.GetComponent<MIMISKDroneAquaLocEstimator>();
            logger.aquaHold = drone.GetComponent<MIMISKDroneAquaLocPositionHold>();
            logger.enableLogging = true;
            logger.logHz = 50.0f;
            EditorUtility.SetDirty(logger);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Applied AquaNav v4.2 fast vector-field corridor profile.");
    }

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.floatValue = value;
        else Debug.LogWarning("[AquaNav v4.2] Missing float: " + name);
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.boolValue = value;
        else Debug.LogWarning("[AquaNav v4.2] Missing bool: " + name);
    }

    private static void SetObject(SerializedObject so, string name, Object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning("[AquaNav v4.2] Missing object: " + name);
    }
}
