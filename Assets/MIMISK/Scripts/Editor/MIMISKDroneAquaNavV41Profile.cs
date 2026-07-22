using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneAquaNavV41Profile
{
    [MenuItem("MIMISK/Drone/Autonomy/Apply AquaNav v4.1 Vector-Field Corridor Profile")]
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

        SetFloat(so, "lineOfSightLookaheadM", 0.75f);
        SetFloat(so, "segmentSwitchDistanceM", 0.12f);
        SetFloat(so, "segmentSwitchPredictedDistanceM", 0.16f);
        SetFloat(so, "predictedSwitchLookaheadS", 0.55f);
        SetFloat(so, "desiredCorridorRadiusM", 0.25f);

        SetBool(so, "enableCrossTrackTargetCorrection", true);
        SetFloat(so, "crossTrackCorrectionGain", 0.70f);
        SetFloat(so, "maxCrossTrackCorrectionM", 0.35f);

        SetBool(so, "enableDynamicLookahead", true);
        SetFloat(so, "minLookaheadM", 0.42f);
        SetFloat(so, "maxLookaheadM", 0.85f);
        SetFloat(so, "speedLookaheadGain", 0.18f);
        SetFloat(so, "crossTrackLookaheadReductionM", 0.42f);

        SetBool(so, "enableMovingTargetSmoothing", true);
        SetFloat(so, "movingTargetResponse", 7.0f);

        SetBool(so, "slowNearFinalWaypoint", true);
        SetFloat(so, "finalApproachDistanceM", 0.65f);

        SetBool(so, "finalWaypointIsDeploymentPoint", true);
        SetFloat(so, "finalWaypointRadiusM", 0.20f);
        SetFloat(so, "finalWaypointSpeedThresholdMS", 0.11f);
        SetFloat(so, "finalStableRequiredSeconds", 1.2f);
        SetFloat(so, "finalLoiterSeconds", 3.0f);

        SetBool(so, "holdAtEnd", true);
        SetBool(so, "landAtFinalWaypoint", false);
        SetBool(so, "useCurrentYawForMission", true);

        so.ApplyModifiedProperties();

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

        Debug.Log("[MIMISK] Applied AquaNav v4.1 Vector-Field Corridor profile.");
    }

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.floatValue = value;
        else Debug.LogWarning("[AquaNav v4.1] Missing float: " + name);
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.boolValue = value;
        else Debug.LogWarning("[AquaNav v4.1] Missing bool: " + name);
    }

    private static void SetObject(SerializedObject so, string name, Object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning("[AquaNav v4.1] Missing object: " + name);
    }
}
