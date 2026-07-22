using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKFinalMissionStartResetSetup
{
    [MenuItem("MIMISK/Final Mission/Reset Final Mission Start State")]
    public static void ResetFinalMissionStartState()
    {
        GameObject drone = GameObject.Find("Drone");
        GameObject miniRov = GameObject.Find("MiniROV");

        if (drone == null)
        {
            Debug.LogError("[MIMISK] Could not find Drone.");
            return;
        }

        MIMISKFinalMissionPlanner planner =
            drone.GetComponent<MIMISKFinalMissionPlanner>();

        if (planner != null)
        {
            planner.state =
                MIMISKFinalMissionPlanner.FinalMissionState.WaitingForDroneMission;

            planner.lastEvent =
                "clean_start_waiting_for_drone_mission";

            planner.stateTimerS = 0.0f;
            planner.missionTimerS = 0.0f;

            if (planner.activeYellowTetherLine != null)
            {
                planner.activeYellowTetherLine.enabled = false;
            }

            EditorUtility.SetDirty(planner);
        }

        MIMISKDroneDeploymentSurfaceAnchor anchor =
            drone.GetComponent<MIMISKDroneDeploymentSurfaceAnchor>();

        if (anchor != null)
        {
            anchor.anchorActive = false;
            anchor.anchorCaptured = false;
            anchor.lastEvent = "clean_start_anchor_inactive";
            EditorUtility.SetDirty(anchor);
        }

        MIMISKFinalContinuousTetherVisual visual =
            drone.GetComponent<MIMISKFinalContinuousTetherVisual>();

        if (visual != null)
        {
            visual.DeactivateVisual();
            visual.visualActive = false;
            visual.lastEvent = "clean_start_visual_inactive";
            EditorUtility.SetDirty(visual);
        }

        MIMISKDroneCoreMissionManager mission =
            drone.GetComponent<MIMISKDroneCoreMissionManager>();

        if (mission != null)
        {
            mission.missionEnabled = true;
            mission.missionActive = false;
            mission.missionState =
                MIMISKDroneCoreMissionManager.MissionState.Idle;

            mission.lastMissionEvent =
                "clean_final_mission_start_idle";

            mission.holdAtReadyForTetherDeployment = true;

            EditorUtility.SetDirty(mission);
        }

        MIMISKDroneCoreTetherManager tether =
            drone.GetComponent<MIMISKDroneCoreTetherManager>();

        if (tether != null)
        {
            tether.acceptKeyboardCommands = false;
            tether.tetherState =
                MIMISKDroneCoreTetherManager.TetherState.Locked;

            tether.deployedLengthM = tether.minimumLengthM;
            tether.targetLengthM = tether.minimumLengthM;
            tether.winchCommandRateMS = 0.0f;

            tether.enableTetherForceWhenMiniRovAttached = false;
            tether.tetherStiffnessNPerM = 0.0f;
            tether.tetherDampingNPerMS = 0.0f;
            tether.maximumSafeTensionN = 999999.0f;

            tether.lastEvent = "clean_start_tether_locked";

            EditorUtility.SetDirty(tether);
        }

        if (miniRov != null)
        {
            MIMISKMiniROVModule module =
                miniRov.GetComponent<MIMISKMiniROVModule>();

            if (module != null)
            {
                module.keyboardTestEnabled = false;
                module.AutoFindReferences();
                module.SetPassiveKinematic();
                EditorUtility.SetDirty(module);
            }

            Rigidbody rb =
                miniRov.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                EditorUtility.SetDirty(rb);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Final mission start state reset. Press Play, then P for full mission.");
    }
}
