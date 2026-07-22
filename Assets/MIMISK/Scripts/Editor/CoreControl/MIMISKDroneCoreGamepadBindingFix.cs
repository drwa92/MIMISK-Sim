using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneCoreGamepadBindingFix
{
    [MenuItem("MIMISK/Drone/Core Control/Fix Gamepad Receiver For Core Controller")]
    public static void FixGamepadReceiverForCoreController()
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

        MIMISKDroneCoreRotorController core =
            drone.GetComponent<MIMISKDroneCoreRotorController>();

        if (core == null)
        {
            Debug.LogError("[MIMISK] Core Rotor Controller missing. Run Core Control setup first.");
            return;
        }

        MIMISKDroneUdpGamepadReceiver udp =
            drone.GetComponent<MIMISKDroneUdpGamepadReceiver>();

        if (udp == null)
        {
            Debug.LogError("[MIMISK] UDP Gamepad Receiver missing.");
            return;
        }

        core.udpReceiver = udp;
        core.legacyModelController = drone.GetComponent<MIMISKDroneModelController>();
        core.keepUdpReceiverEnabled = true;
        core.disableLegacyModelController = true;
        core.controllerEnabled = true;
        core.controlMode = MIMISKDroneCoreRotorController.ControlMode.ManualGamepad;

        EditorUtility.SetDirty(core);

        udp.enabled = true;

        SerializedObject udpSO = new SerializedObject(udp);

        SetBoolIfExists(udpSO, "suppressCommandOutput", true);
        SetBoolIfExists(udpSO, "allowModeButtonsWhileSuppressed", true);

        udpSO.ApplyModifiedProperties();
        EditorUtility.SetDirty(udp);

        DisableByClassName(drone, "MIMISKDroneModelController");
        DisableByClassName(drone, "MIMISKDroneModelGamepadInput");
        DisableByClassName(drone, "MIMISKDroneModelKeyboardInput");
        DisableByClassName(drone, "MIMISKDroneModelManualInput");
        DisableByClassName(drone, "MIMISKDroneGamepadInput");

        DisableByClassName(drone, "MIMISKDroneAquaLocPositionHold");
        DisableByClassName(drone, "MIMISKDroneKeyboardStationKeeping");
        DisableByClassName(drone, "MIMISKDroneAquaLocWaypointNavigator");
        DisableByClassName(drone, "MIMISKDroneAquaTrackPathFollower");
        DisableByClassName(drone, "MIMISKDroneAquaDynTrackController");
        DisableByClassName(drone, "MIMISKDroneRtkIKDynamicPathTracker");
        DisableByClassName(drone, "MIMISKDroneNominalPathPidRotorController");
        DisableByClassName(drone, "MIMISKDroneBaseRotorController");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] UDP Gamepad Receiver is now bound as raw input for Core Rotor Controller.");
    }

    private static void SetBoolIfExists(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);

        if (p != null)
        {
            p.boolValue = value;
        }
    }

    private static void DisableByClassName(GameObject root, string className)
    {
        Type t = FindTypeByName(className);

        if (t == null)
        {
            return;
        }

        Component[] components = root.GetComponentsInChildren(t, true);

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
