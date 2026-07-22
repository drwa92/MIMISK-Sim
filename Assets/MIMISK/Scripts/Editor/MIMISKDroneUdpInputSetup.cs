using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneUdpInputSetup
{
    [MenuItem("MIMISK/Drone/Setup UDP GameSir Input")]
    public static void SetupUdpInput()
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

        if (controller == null)
        {
            Debug.LogError("[MIMISK] Drone has no MIMISKDroneModelController.");
            return;
        }

        controller.enableKeyboardInput = false;
        EditorUtility.SetDirty(controller);

        MIMISKDroneUdpGamepadReceiver receiver =
            drone.GetComponent<MIMISKDroneUdpGamepadReceiver>();

        if (receiver == null)
        {
            receiver = drone.AddComponent<MIMISKDroneUdpGamepadReceiver>();
        }

        receiver.controller = controller;
        receiver.enableReceiver = true;
        receiver.listenPort = 54331;

        receiver.forwardAxis = MIMISKDroneUdpGamepadReceiver.AxisSource.Ly;
        receiver.forwardSign = -1.0f;

        receiver.rightAxis = MIMISKDroneUdpGamepadReceiver.AxisSource.Lx;
        receiver.rightSign = 1.0f;

        receiver.yawAxis = MIMISKDroneUdpGamepadReceiver.AxisSource.Rx;
        receiver.yawSign = 1.0f;

        receiver.altitudeMode = MIMISKDroneUdpGamepadReceiver.AltitudeMode.RtMinusLt;
        receiver.altitudeSign = 1.0f;

        receiver.armButtons = "BTN_SOUTH";
        receiver.takeoffButtons = "BTN_NORTH";
        receiver.altitudeHoldButtons = "BTN_WEST";
        receiver.manualModeButtons = "BTN_TL,BTN_TR";
        receiver.landButtons = "BTN_EAST";
        receiver.disarmButtons = "BTN_SELECT,BTN_BACK";
        receiver.failsafeButtons = "BTN_START";

        receiver.deadzone = 0.05f;
        receiver.commandSmoothness = 10.0f;
        receiver.commandTimeoutSeconds = 0.35f;
        receiver.autoEnterManualMode = true;

        receiver.enabled = true;
        EditorUtility.SetDirty(receiver);

        DisableIfPresent(drone, "MIMISKDroneModelGamepadInput");
        DisableIfPresent(drone, "MIMISKDroneInputCalibrationWizard");
        DisableIfPresent(drone, "MIMISKDroneInputMappingLoader");
        DisableIfPresent(drone, "MIMISKInputDeviceProbe");
        DisableIfPresent(drone, "MIMISKDroneCommandStepTester");
        DisableIfPresent(drone, "MIMISKDroneAxisTestSequence");
        DisableIfPresent(drone, "MIMISKDroneKeyboardInput");
        DisableIfPresent(drone, "MIMISKDroneGamepadInput");
        DisableIfPresent(drone, "MIMISKDroneFlightController");
        DisableIfPresent(drone, "MIMISKDroneGPSAutopilot");
        DisableIfPresent(drone, "MIMISKDronePropellerAnimator");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] UDP GameSir input configured on Drone. Run Python sender on UDP port 54331.");
    }

    private static void DisableIfPresent(GameObject obj, string componentName)
    {
        Component c = obj.GetComponent(componentName);

        if (c == null)
        {
            return;
        }

        Behaviour b = c as Behaviour;

        if (b != null)
        {
            b.enabled = false;
            EditorUtility.SetDirty(b);
            Debug.Log("[MIMISK] Disabled: " + componentName);
        }
    }
}
