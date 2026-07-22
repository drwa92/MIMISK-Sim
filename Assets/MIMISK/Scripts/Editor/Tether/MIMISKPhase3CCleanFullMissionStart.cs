using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKPhase3CCleanFullMissionStart
{
    [MenuItem("MIMISK/Drone/Tether/Apply Phase 3C Clean Full Mission Start")]
    public static void ApplyCleanFullMissionStart()
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

        // Disable old partial/test systems. They should not own state at Play.
        DisableByClassName(drone, "MIMISKMiniROVDeploymentManager");
        DisableByClassName(drone, "MIMISKMiniROVCableEndAttachmentManager");
        DisableByClassName(drone, "MIMISKMiniROVWaterReleaseController");
        DisableByClassName(drone, "MIMISKTetherDeploymentTestMission");

        DisableByClassName(drone, "MIMISKMiniROVDeploymentLogger");
        DisableByClassName(drone, "MIMISKMiniROVCableEndAttachmentLogger");
        DisableByClassName(drone, "MIMISKMiniROVWaterReleaseLogger");

        MIMISKDroneCoreMissionManager mission =
            drone.GetComponent<MIMISKDroneCoreMissionManager>();

        if (mission != null)
        {
            mission.missionEnabled = true;
            mission.missionActive = false;
            mission.missionState = MIMISKDroneCoreMissionManager.MissionState.Idle;
            mission.lastMissionEvent = "clean_full_mission_start_idle";
            mission.holdAtReadyForTetherDeployment = true;
            EditorUtility.SetDirty(mission);
        }

        MIMISKDroneCoreFlightModeManager manager =
            drone.GetComponent<MIMISKDroneCoreFlightModeManager>();

        if (manager != null)
        {
            manager.flightMode = MIMISKDroneCoreFlightModeManager.FlightMode.Gamepad;
            manager.lastModeEvent = "clean_full_mission_start_gamepad";
            manager.surfaceHoldHeightM = 0.0f;
        manager.requireMeasuredWaterContactForSurfaceStable = true;
        manager.descendBelowSurfaceUntilMeasuredContact = true;
        manager.landingProbeDepthBelowSurfaceM = 0.08f;
        manager.measuredContactSettleSeconds = 0.35f;
        manager.allowInferredSurfaceContactFromHeight = false;
            manager.landingDescentSpeedMS = 0.20f;
            manager.requireSettledBeforeSurfaceStable = true;
        manager.allowSurfaceStableFallbackOnSustainedContact = true;
        manager.surfaceStableFallbackDwellSeconds = 2.00f;
        manager.allowInferredSurfaceContactFromHeight = false;
        manager.inferredSurfaceContactHeightM = 0.08f;
            manager.surfaceStableSettleSeconds = 0.60f;
            manager.surfaceStableMaxVerticalSpeedMS = 0.12f;
            manager.surfaceStableHeightToleranceM = 0.18f;
            manager.surfaceStableCandidateTimerS = 0.0f;
            EditorUtility.SetDirty(manager);
        }

        MIMISKDroneCoreRotorController core =
            drone.GetComponent<MIMISKDroneCoreRotorController>();

        if (core != null)
        {
            core.controllerEnabled = true;
            core.controlMode = MIMISKDroneCoreRotorController.ControlMode.ExternalReference;
            core.acceptKeyboardShortcuts = false;
            EditorUtility.SetDirty(core);
        }

        MIMISKDroneCoreTetherManager tether =
            drone.GetComponent<MIMISKDroneCoreTetherManager>();

        if (tether != null)
        {
            tether.acceptKeyboardCommands = false;
            tether.tetherSystemEnabled = true;
            tether.tetherState = MIMISKDroneCoreTetherManager.TetherState.Locked;
            tether.deployedLengthM = tether.minimumLengthM;
            tether.targetLengthM = tether.minimumLengthM;
            tether.targetDeployLengthM = 3.0f;
            tether.winchCommandRateMS = 0.0f;
            tether.enableTetherForceWhenMiniRovAttached = false;
            tether.tetherStiffnessNPerM = 0.0f;
            tether.tetherDampingNPerMS = 0.0f;
            tether.maximumSafeTensionN = 999999.0f;
            tether.lastEvent = "clean_full_mission_start_locked";
            EditorUtility.SetDirty(tether);
        }

        MIMISKMiniROVRealisticDeploymentManager deploy =
            drone.GetComponent<MIMISKMiniROVRealisticDeploymentManager>();

        if (deploy != null)
        {
            deploy.acceptKeyboardCommands = false;
            deploy.deploymentEnabled = true;
            deploy.deploymentState =
                MIMISKMiniROVRealisticDeploymentManager.DeploymentState.CableAttachedIdle;

            deploy.lastEvent = "clean_full_mission_start_cable_attached";
            deploy.ignoreMiniRovDroneCollisions = true;
            deploy.usePhysicsIgnoreCollision = false;
            deploy.disableMiniRovCollidersDuringCableFollow = true;
            deploy.disableMiniRovCollidersDuringRecovery = true;
            deploy.disableTetherForceForNow = true;
            deploy.AutoFindReferences();
            deploy.AttachRovToCableEndpoint();
            EditorUtility.SetDirty(deploy);
        }

        MIMISKMiniROVPreDeploymentSafetyGuard guard =
            drone.GetComponent<MIMISKMiniROVPreDeploymentSafetyGuard>();

        if (guard == null)
        {
            guard = drone.AddComponent<MIMISKMiniROVPreDeploymentSafetyGuard>();
        }

        guard.deployment = deploy;
        guard.guardEnabled = true;
        guard.enforcePassiveBeforeWaterRelease = true;
        guard.enforcePassiveDuringRecovery = true;
        guard.disableCollidersBeforeRelease = true;
        guard.disableControlBeforeRelease = true;
        guard.disableWaterPhysicsBeforeRelease = true;
        guard.AutoFindReferences();
        guard.ApplyGuard();
        EditorUtility.SetDirty(guard);

        MIMISKDroneTetherHandoffMission handoff =
            drone.GetComponent<MIMISKDroneTetherHandoffMission>();

        if (handoff != null)
        {
            handoff.handoffEnabled = true;
            handoff.autoDeployWhenReady = false;
            handoff.forceMissionHoldAtTetherReady = true;
            handoff.handoffState = MIMISKDroneTetherHandoffMission.HandoffState.WaitingForMissionReady;
            handoff.lastHandoffEvent = "clean_full_mission_start_waiting";
            EditorUtility.SetDirty(handoff);
        }

        Rigidbody rb = drone.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            EditorUtility.SetDirty(rb);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Phase 3C clean full mission start applied. At Play, mission should start from Idle, not ReadyForTetherDeployment.");
    }

    private static void DisableByClassName(GameObject root, string className)
    {
        Type t = FindTypeByName(className);

        if (t == null)
        {
            return;
        }

        Component[] components =
            root.GetComponentsInChildren(t, true);

        if (components == null)
        {
            return;
        }

        for (int i = 0; i < components.Length; i++)
        {
            Behaviour b = components[i] as Behaviour;

            if (b != null)
            {
                b.enabled = false;
                EditorUtility.SetDirty(b);
            }
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
