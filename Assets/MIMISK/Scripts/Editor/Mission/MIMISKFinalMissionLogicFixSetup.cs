using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKFinalMissionLogicFixSetup
{
    [MenuItem("MIMISK/Final Mission/Apply Final Mission Logic Fixes")]
    public static void ApplyFinalMissionLogicFixes()
    {
        GameObject drone = GameObject.Find("Drone");
        GameObject miniRov = GameObject.Find("MiniROV");

        if (drone == null)
        {
            Debug.LogError("[MIMISK] Could not find Drone.");
            return;
        }

        if (miniRov == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV.");
            return;
        }

        MIMISKFinalMissionPlanner planner =
            drone.GetComponent<MIMISKFinalMissionPlanner>();

        if (planner != null)
        {
            planner.allowDeploymentFromPhysicalSurfaceState = true;
            planner.blockDeploymentWhileDroneMissionActive = true;
            planner.requireDeploymentTargetPosition = false;
            planner.enableActiveYellowTetherLine = false;
            planner.state = MIMISKFinalMissionPlanner.FinalMissionState.WaitingForDroneMission;
            planner.lastEvent = "logic_fix_waiting_for_drone_or_surface";
            EditorUtility.SetDirty(planner);
        }

        MIMISKDroneDeploymentSurfaceAnchor anchor =
            drone.GetComponent<MIMISKDroneDeploymentSurfaceAnchor>();

        if (anchor != null)
        {
            anchor.anchorEnabled = false;
            anchor.anchorActive = false;
            anchor.enabled = false;
            anchor.lastEvent = "disabled_use_existing_drone_surface_buoyancy";
            EditorUtility.SetDirty(anchor);
        }

        MIMISKDroneCoreTetherManager tether =
            drone.GetComponent<MIMISKDroneCoreTetherManager>();

        if (tether != null)
        {
            tether.acceptKeyboardCommands = false;
            tether.enableTetherForceWhenMiniRovAttached = false;
            tether.tetherStiffnessNPerM = 0.0f;
            tether.tetherDampingNPerMS = 0.0f;
            tether.maximumSafeTensionN = 999999.0f;
            EditorUtility.SetDirty(tether);
        }

        MIMISKMiniROVCollisionIsolation isolation =
            miniRov.GetComponent<MIMISKMiniROVCollisionIsolation>();

        if (isolation == null)
        {
            isolation = miniRov.AddComponent<MIMISKMiniROVCollisionIsolation>();
        }

        isolation.miniRovRoot = miniRov.transform;
        isolation.droneRoot = drone;
        isolation.isolationEnabled = true;
        isolation.ignoreDroneCollisions = true;
        isolation.ApplyIsolation();
        EditorUtility.SetDirty(isolation);

        MIMISKMiniROVModule module =
            miniRov.GetComponent<MIMISKMiniROVModule>();

        if (module != null)
        {
            module.keyboardTestEnabled = false;
            module.enableSimpleRovBuoyancyInFinalStack = true;
            module.keepSensorManagerDisabled = true;
            module.useGravityInControl = true;

            module.correctOrientationOnHandoff = true;
            module.freeSwimWorldEuler = Vector3.zero;
            module.preserveCurrentYaw = false;
            module.keepTetherAnchorLockedDuringOrientationFix = true;

            module.enableCollidersDuringControl = true;
            module.collisionIsolation = isolation;

            EditorUtility.SetDirty(module);
        }

        MIMISKFinalContinuousTetherVisual continuous =
            drone.GetComponent<MIMISKFinalContinuousTetherVisual>();

        if (continuous != null)
        {
            continuous.activeOnlyDuringROVControl = true;
            continuous.disableOtherTetherLineRenderers = true;
            continuous.baseSagM = 0.18f;
            continuous.sagPerMeter = 0.08f;
            continuous.maxSagM = 0.85f;
            continuous.DeactivateVisual();
            EditorUtility.SetDirty(continuous);
        }

        MIMISKDroneCoreMissionManager mission =
            drone.GetComponent<MIMISKDroneCoreMissionManager>();

        if (mission != null)
        {
            mission.holdAtReadyForTetherDeployment = true;
            EditorUtility.SetDirty(mission);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Final mission logic fixes applied: surface-contact deployment allowed, surface anchor disabled, MiniROV-drone collisions ignored, recovery made safer.");
    }
}
