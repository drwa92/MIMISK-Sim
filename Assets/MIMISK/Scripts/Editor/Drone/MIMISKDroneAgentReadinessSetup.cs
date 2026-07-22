using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneAgentReadinessSetup
{
    [MenuItem("MIMISK/Drone/Prepare Selected Drone For Agent")]
    public static void PrepareSelectedDroneForAgent()
    {
        GameObject root =
            Selection.activeGameObject;

        if (root == null)
        {
            MIMISKDroneCoreRotorController anyCore =
                Object.FindFirstObjectByType<MIMISKDroneCoreRotorController>();

            if (anyCore != null)
            {
                root =
                    anyCore.gameObject;
            }
        }

        if (root == null)
        {
            Debug.LogError("[MIMISK] No selected drone and no MIMISKDroneCoreRotorController found.");
            return;
        }

        MIMISKDroneCoreMissionManager mission =
            root.GetComponent<MIMISKDroneCoreMissionManager>();

        MIMISKDroneCoreFlightModeManager flight =
            root.GetComponent<MIMISKDroneCoreFlightModeManager>();

        MIMISKDroneCoreRotorController core =
            root.GetComponent<MIMISKDroneCoreRotorController>();

        MIMISKDroneCoreGamepadReceiver gamepad =
            root.GetComponent<MIMISKDroneCoreGamepadReceiver>();

        MIMISKDroneCoreTrajectoryPlanner planner =
            root.GetComponent<MIMISKDroneCoreTrajectoryPlanner>();

        if (mission != null)
        {
            mission.AutoFindReferences();
            mission.missionEnabled = true;
            mission.runOnStart = false;
            mission.acceptKeyboardMissionCommands = false;
            EditorUtility.SetDirty(mission);
        }

        if (flight != null)
        {
            flight.AutoFindReferences();
            flight.managerEnabled = true;
            flight.acceptKeyboardModeCommands = false;
            flight.acceptGamepadModeButtons = false;
            EditorUtility.SetDirty(flight);
        }

        if (core != null)
        {
            core.AutoFindReferences();
            core.controllerEnabled = true;
            core.acceptKeyboardShortcuts = false;
            core.disableLegacyModelController = true;
            core.keepUdpReceiverEnabled = true;
            EditorUtility.SetDirty(core);
        }

        if (gamepad != null)
        {
            // Keep receiver configuration unchanged.
            // FlightModeManager.acceptGamepadModeButtons prevents button-driven mode changes.
            EditorUtility.SetDirty(gamepad);
        }

        if (planner != null)
        {
            EditorUtility.SetDirty(planner);
        }

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MIMISK] Drone prepared for Agent control on object: " +
            root.name +
            ". Keyboard/mode-button shortcuts disabled; validated public methods remain available."
        );
    }
}
