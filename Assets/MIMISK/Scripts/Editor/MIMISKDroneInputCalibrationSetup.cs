using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneInputCalibrationSetup
{
    [MenuItem("MIMISK/Drone/Setup GameSir Calibration Wizard")]
    public static void SetupCalibrationWizard()
    {
        GameObject drone = Selection.activeGameObject;

        if (drone == null || drone.GetComponent<MIMISKDroneModelController>() == null)
        {
            drone = GameObject.Find("Drone");
        }

        if (drone == null)
        {
            Debug.LogError("[MIMISK] Could not find Drone root.");
            return;
        }

        MIMISKDroneModelController controller =
            drone.GetComponent<MIMISKDroneModelController>();

        if (controller != null)
        {
            controller.enableKeyboardInput = false;
            EditorUtility.SetDirty(controller);
        }

        MIMISKDroneModelGamepadInput input =
            drone.GetComponent<MIMISKDroneModelGamepadInput>();

        if (input == null)
        {
            input = drone.AddComponent<MIMISKDroneModelGamepadInput>();
        }

        input.enableGamepadInput = true;
        input.disableKeyboardInputOnStart = true;
        input.autoEnterManualMode = true;
        input.useFirstAvailableGamepad = true;
        input.allowJoystickFallback = true;
        input.forceGenericAxisMapping = true;
        input.stickDeadzone = 0.10f;
        input.triggerDeadzone = 0.05f;
        input.commandSmoothness = 10.0f;

        EditorUtility.SetDirty(input);

        MIMISKDroneInputMappingLoader loader =
            drone.GetComponent<MIMISKDroneInputMappingLoader>();

        if (loader == null)
        {
            loader = drone.AddComponent<MIMISKDroneInputMappingLoader>();
        }

        loader.loadOnStart = true;
        EditorUtility.SetDirty(loader);

        MIMISKDroneInputCalibrationWizard wizard =
            drone.GetComponent<MIMISKDroneInputCalibrationWizard>();

        if (wizard == null)
        {
            wizard = drone.AddComponent<MIMISKDroneInputCalibrationWizard>();
        }

        wizard.runCalibration = true;
        wizard.StartCalibration();

        EditorUtility.SetDirty(wizard);

        DisableIfPresent(drone, "MIMISKDroneCommandStepTester");
        DisableIfPresent(drone, "MIMISKDroneAxisTestSequence");
        DisableIfPresent(drone, "MIMISKDroneKeyboardInput");
        DisableIfPresent(drone, "MIMISKDroneGamepadInput");
        DisableIfPresent(drone, "MIMISKDroneFlightController");
        DisableIfPresent(drone, "MIMISKDroneGPSAutopilot");
        DisableIfPresent(drone, "MIMISKDronePropellerAnimator");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] GameSir calibration wizard configured. Press Play and follow the on-screen prompts.");
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
        }
    }
}
