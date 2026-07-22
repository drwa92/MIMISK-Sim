using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMiniROVUnityVirtualESP32UDPSetup
{
    [MenuItem("MIMISK/MiniROV/Setup Raspberry-less ESP32 UDP Input")]
    public static void SetupRaspberrylessEsp32UdpInput()
    {
        GameObject miniRov = GameObject.Find("MiniROV");

        if (miniRov == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV.");
            return;
        }

        UnityVirtualESP32 esp32 =
            miniRov.GetComponent<UnityVirtualESP32>();

        if (esp32 == null)
        {
            esp32 = miniRov.AddComponent<UnityVirtualESP32>();
        }

        ControlManager control =
            miniRov.GetComponent<ControlManager>();

        if (control == null)
        {
            control = miniRov.AddComponent<ControlManager>();
        }

        Rigidbody rb =
            miniRov.GetComponent<Rigidbody>();

        esp32.rb = rb;
        esp32.controlManager = control;
        esp32.autoOpenOnStart = false;
        esp32.enabled = true;
        esp32.StopBridge();

        EditorUtility.SetDirty(esp32);

        control.autoOpenOnStart = false;
        control.enabled = true;

        if (rb != null)
        {
            control.rb = rb;
        }

        if (control.leftThruster == null)
        {
            control.leftThruster = FindDeepChild(miniRov.transform, "propulseur_gauche");
        }

        if (control.rightThruster == null)
        {
            control.rightThruster = FindDeepChild(miniRov.transform, "propulseur_droite");
        }

        // Do not change useThrusterTransformForward here.
        // Keep the same value that works in Scene 13.
        EditorUtility.SetDirty(control);

        MIMISKMiniROVUnityVirtualESP32UDPInput udp =
            miniRov.GetComponent<MIMISKMiniROVUnityVirtualESP32UDPInput>();

        if (udp == null)
        {
            udp = miniRov.AddComponent<MIMISKMiniROVUnityVirtualESP32UDPInput>();
        }

        udp.unityVirtualESP32 = esp32;
        udp.controlManager = control;
        udp.rb = rb;
        udp.missionManager = miniRov.GetComponent<MIMISKMiniROVMissionManager>();

        udp.receiverEnabled = true;
        udp.listenAddress = "0.0.0.0";
        udp.listenPort = MIMISKNetworkPorts.MiniROVUnityDirectUdp;
        udp.inputTimeoutS = 0.75f;
        udp.stopUnityVirtualESP32SerialBridge = true;
        udp.requireGamepadMissionState = true;

        udp.rawSurgeAxis = MIMISKMiniROVUnityVirtualESP32UDPInput.GamepadAxisSource.LeftStickY;
        udp.rawYawAxis = MIMISKMiniROVUnityVirtualESP32UDPInput.GamepadAxisSource.RightStickX;
        udp.rawDepthAxis = MIMISKMiniROVUnityVirtualESP32UDPInput.GamepadAxisSource.TriggerDifference;

        udp.invertRawSurgeAxis = true;
        udp.invertRawYawAxis = false;
        udp.invertRawDepthAxis = false;

        udp.surgeScale = 1.0f;
        udp.yawScale = 1.0f;
        udp.depthScale = 1.0f;

        udp.maxThrusterPwm = 190;
        udp.maxDcPwm = 160;
        udp.mapDepthToDc = false;

        udp.swapLeftRight = false;
        udp.invertLeftThruster = false;
        udp.invertRightThruster = false;
        udp.invertBothThrusters = false;

        EditorUtility.SetDirty(udp);

        // Disable old Unity-native gamepad paths so this adapter is the only manual source.
        MIMISKMiniROVUDPGamepadReceiver oldUdp =
            miniRov.GetComponent<MIMISKMiniROVUDPGamepadReceiver>();

        if (oldUdp != null)
        {
            oldUdp.receiverEnabled = false;
            oldUdp.enabled = false;
            EditorUtility.SetDirty(oldUdp);
        }

        MIMISKMiniROVGamepadReceiver oldLocal =
            miniRov.GetComponent<MIMISKMiniROVGamepadReceiver>();

        if (oldLocal != null)
        {
            oldLocal.receiverEnabled = false;
            oldLocal.enabled = false;
            EditorUtility.SetDirty(oldLocal);
        }

        MIMISKMiniROVCoreController core =
            miniRov.GetComponent<MIMISKMiniROVCoreController>();

        if (core != null)
        {
            core.controllerEnabled = false;
            EditorUtility.SetDirty(core);
        }

        MIMISKMiniROVMissionManager mission =
            miniRov.GetComponent<MIMISKMiniROVMissionManager>();

        if (mission != null)
        {
            mission.selectedMissionAction =
                MIMISKMiniROVMissionManager.MiniROVMissionAction.GamepadManualTest;
            mission.missionEnabled = true;
            mission.allowStandaloneKeyboard = false;
            EditorUtility.SetDirty(mission);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Raspberry-less ESP32 UDP input configured. Use pc_sender_new.py or minirov_unity_udp_sender.py on port 54331, then P/U/M.");
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
