using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMiniROVUDPGamepadSetup
{
    [MenuItem("MIMISK/MiniROV/Setup MiniROV UDP Gamepad Control")]
    public static void SetupMiniROVUDPGamepadControl()
    {
        GameObject miniRov =
            GameObject.Find("MiniROV");

        if (miniRov == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV.");
            return;
        }

        MIMISKMiniROVCoreController core =
            miniRov.GetComponent<MIMISKMiniROVCoreController>();

        if (core == null)
        {
            core = miniRov.AddComponent<MIMISKMiniROVCoreController>();
        }

        MIMISKMiniROVMissionManager mission =
            miniRov.GetComponent<MIMISKMiniROVMissionManager>();

        if (mission == null)
        {
            mission = miniRov.AddComponent<MIMISKMiniROVMissionManager>();
        }

        mission.selectedMissionAction =
            MIMISKMiniROVMissionManager.MiniROVMissionAction.GamepadManualTest;

        mission.allowStandaloneKeyboard = false;
        mission.missionEnabled = true;

        EditorUtility.SetDirty(mission);

        MIMISKMiniROVUDPGamepadReceiver udp =
            miniRov.GetComponent<MIMISKMiniROVUDPGamepadReceiver>();

        if (udp == null)
        {
            udp = miniRov.AddComponent<MIMISKMiniROVUDPGamepadReceiver>();
        }

        udp.coreController = core;
        udp.missionManager = mission;
        udp.receiverEnabled = true;
        udp.listenAddress = "127.0.0.1";
        udp.listenPort = MIMISKNetworkPorts.MiniROVUnityDirectUdp;
        udp.inputTimeoutS = 0.35f;

        udp.requireMiniROVGamepadMissionState = true;
        udp.allowIfCoreAlreadyGamepadManual = true;
        udp.forceCoreGamepadModeWhenActive = true;
        udp.requireUnityNativeBackend = true;

        udp.useDirectCommandFields = true;
        udp.forceRawPcSenderMapping = true;
        udp.rawSurgeAxis = MIMISKMiniROVUDPGamepadReceiver.GamepadAxisSource.LeftStickY;
        udp.rawYawAxis = MIMISKMiniROVUDPGamepadReceiver.GamepadAxisSource.RightStickX;
        udp.rawDepthAxis = MIMISKMiniROVUDPGamepadReceiver.GamepadAxisSource.TriggerDifference;
        udp.invertRawSurgeAxis = true;
        udp.invertRawYawAxis = false;
        udp.invertRawDepthAxis = false;
        udp.deriveFromRawAxes = true;
        udp.invertSurge = false;
        udp.invertYaw = false;
        udp.surgeScale = 1.0f;
        udp.yawScale = 1.0f;
        udp.depthScale = 1.0f;

        EditorUtility.SetDirty(udp);

        MIMISKMiniROVGamepadReceiver local =
            miniRov.GetComponent<MIMISKMiniROVGamepadReceiver>();

        if (local != null)
        {
            local.receiverEnabled = false;
            local.enabled = false;
            local.enabled = false;
            EditorUtility.SetDirty(local);
        }

        MIMISKCommunicationBridge bridge =
            UnityEngine.Object.FindAnyObjectByType<MIMISKCommunicationBridge>();

        if (bridge != null)
        {
            bridge.miniRovMission = mission;
            bridge.startMiniRovMissionKey = UnityEngine.InputSystem.Key.M;
            EditorUtility.SetDirty(bridge);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] MiniROV UDP gamepad control configured. Run Python sender on UDP 127.0.0.1:54331, then use P/U/M.");
    }
}
