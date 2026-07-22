using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneTetherHandoffMissionSetup
{
    [MenuItem("MIMISK/Drone/Tether/Setup Integrated Tether Handoff Mission")]
    public static void SetupIntegratedTetherHandoffMission()
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

        // Disable the isolated test mission so P belongs to the full mission again.
        DisableByClassName(drone, "MIMISKTetherDeploymentTestMission");

        MIMISKDroneCoreMissionManager mission =
            drone.GetComponent<MIMISKDroneCoreMissionManager>();

        if (mission != null)
        {
            mission.missionEnabled = true;
            mission.holdAtReadyForTetherDeployment = true;
            EditorUtility.SetDirty(mission);
        }

        MIMISKDroneCoreTetherManager tether =
            drone.GetComponent<MIMISKDroneCoreTetherManager>();

        MIMISKMiniROVRealisticDeploymentManager rovDeploy =
            drone.GetComponent<MIMISKMiniROVRealisticDeploymentManager>();

        if (tether != null)
        {
            tether.acceptKeyboardCommands = false;
            tether.enableTetherForceWhenMiniRovAttached = false;
            tether.tetherStiffnessNPerM = 0.0f;
            tether.tetherDampingNPerMS = 0.0f;
            tether.maximumSafeTensionN = 999999.0f;
            EditorUtility.SetDirty(tether);
        }

        if (rovDeploy != null)
        {
            rovDeploy.acceptKeyboardCommands = false;
            rovDeploy.disableTetherForceForNow = true;
            rovDeploy.adaptiveSlackManagement = true;
            rovDeploy.enableRovControlAfterStabilization = true;
            EditorUtility.SetDirty(rovDeploy);
        }

        MIMISKDroneTetherHandoffMission handoff =
            drone.GetComponent<MIMISKDroneTetherHandoffMission>();

        if (handoff == null)
        {
            handoff = drone.AddComponent<MIMISKDroneTetherHandoffMission>();
        }

        handoff.missionManager = mission;
        handoff.flightManager = drone.GetComponent<MIMISKDroneCoreFlightModeManager>();
        handoff.tetherManager = tether;
        handoff.rovDeployment = rovDeploy;

        handoff.handoffEnabled = true;
        handoff.forceMissionHoldAtTetherReady = true;
        handoff.autoDeployWhenReady = false;
        handoff.allowManualWhenSurfaceStable = true;

        handoff.targetDeployLengthM = 3.0f;
        handoff.payoutSpeedMS = 0.22f;
        handoff.recoverySpeedMS = 0.25f;
        handoff.releaseDepthBelowSurfaceM = 0.25f;
        handoff.minimumPayoutBeforeReleaseM = 0.45f;
        handoff.stabilizationSeconds = 1.5f;

        handoff.handoffState =
            MIMISKDroneTetherHandoffMission.HandoffState.WaitingForMissionReady;

        handoff.ConfigureSubsystems();

        EditorUtility.SetDirty(handoff);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Integrated tether handoff configured. Full mission uses P. Deploy ROV with U after ReadyForTetherDeployment.");
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
