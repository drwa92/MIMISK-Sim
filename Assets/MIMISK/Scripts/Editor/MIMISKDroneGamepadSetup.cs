using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneGamepadSetup
{
    [MenuItem("MIMISK/Drone/Setup Drone Gamepad Control")]
    public static void SetupDroneGamepadControl()
    {
        GameObject drone = Selection.activeGameObject;

        if (drone == null || drone.GetComponent<MIMISKDroneModelController>() == null)
        {
            drone = GameObject.Find("Drone");
        }

        if (drone == null)
        {
            Debug.LogError("[MIMISK] Could not find Drone root. Select MIMISK_AerialAquaticSystem/Drone.");
            return;
        }

        MIMISKDroneModelController controller = drone.GetComponent<MIMISKDroneModelController>();

        if (controller == null)
        {
            Debug.LogError("[MIMISK] Drone has no MIMISKDroneModelController. Apply model-based controller first.");
            return;
        }

        controller.enableKeyboardInput = false;
        EditorUtility.SetDirty(controller);

        MIMISKDroneModelGamepadInput gamepadInput =
            drone.GetComponent<MIMISKDroneModelGamepadInput>();

        if (gamepadInput == null)
        {
            gamepadInput = drone.AddComponent<MIMISKDroneModelGamepadInput>();
        }

        gamepadInput.controller = controller;
        gamepadInput.enableGamepadInput = true;
        gamepadInput.disableKeyboardInputOnStart = true;
        gamepadInput.autoEnterManualMode = true;

        gamepadInput.stickDeadzone = 0.10f;
        gamepadInput.triggerDeadzone = 0.05f;
        gamepadInput.commandSmoothness = 8.0f;

        gamepadInput.invertForward = false;
        gamepadInput.invertRight = false;
        gamepadInput.invertYaw = false;
        gamepadInput.invertAltitude = false;

        gamepadInput.enabled = true;
        EditorUtility.SetDirty(gamepadInput);

        DisableIfPresent(drone, "MIMISKDroneCommandStepTester");
        DisableIfPresent(drone, "MIMISKDroneAxisTestSequence");
        DisableIfPresent(drone, "MIMISKDroneKeyboardInput");
        DisableIfPresent(drone, "MIMISKDroneGamepadInput");
        DisableIfPresent(drone, "MIMISKDroneFlightController");
        DisableIfPresent(drone, "MIMISKDroneGPSAutopilot");
        DisableIfPresent(drone, "MIMISKDronePropellerAnimator");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Drone gamepad control configured. Keyboard input disabled; model controller remains active.");
    }

    private static void DisableIfPresent(GameObject obj, string typeName)
    {
        Component component = obj.GetComponent(typeName);

        if (component == null)
        {
            return;
        }

        Behaviour behaviour = component as Behaviour;

        if (behaviour != null)
        {
            behaviour.enabled = false;
            EditorUtility.SetDirty(behaviour);
            Debug.Log("[MIMISK] Disabled component: " + typeName);
        }
    }
}
