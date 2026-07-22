using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneCorePropellerAnimationBridgeSetup
{
    [MenuItem("MIMISK/Drone/Core Control/Setup Legacy-Style Core Propeller Animation")]
    public static void SetupLegacyStyleCorePropellerAnimation()
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

        DisableByClassName(drone, "MIMISKDroneCoreRotorAnimator");
        DisableByClassName(drone, "MIMISKDroneCoreRotorVisualAnimator");

        MIMISKDroneRotorModel oldRotorModel =
            drone.GetComponentInChildren<MIMISKDroneRotorModel>(true);

        if (oldRotorModel != null)
        {
            oldRotorModel.enabled = false;
            EditorUtility.SetDirty(oldRotorModel);
        }

        MIMISKDronePropellerAnimator propellerAnimator =
            drone.GetComponent<MIMISKDronePropellerAnimator>();

        if (propellerAnimator == null)
        {
            propellerAnimator = drone.AddComponent<MIMISKDronePropellerAnimator>();
        }

        propellerAnimator.enabled = true;
        propellerAnimator.enableKeyboardDebug = false;

        EditorUtility.SetDirty(propellerAnimator);

        MIMISKDroneCorePropellerAnimationBridge bridge =
            drone.GetComponent<MIMISKDroneCorePropellerAnimationBridge>();

        if (bridge == null)
        {
            bridge = drone.AddComponent<MIMISKDroneCorePropellerAnimationBridge>();
        }

        bridge.enabled = true;
        bridge.core = drone.GetComponent<MIMISKDroneCoreRotorController>();
        bridge.modeManager = drone.GetComponent<MIMISKDroneCoreFlightModeManager>();
        bridge.propellerAnimator = propellerAnimator;

        bridge.rotorFLName = "Rotor_FL_spin_pivot_Unity_rotate_local_Z";
        bridge.rotorFRName = "Rotor_FR_spin_pivot_Unity_rotate_local_Z";
        bridge.rotorRLName = "Rotor_RL_spin_pivot_Unity_rotate_local_Z";
        bridge.rotorRRName = "Rotor_RR_spin_pivot_Unity_rotate_local_Z";

        bridge.idleRpm = 600.0f;
        bridge.minFlyingRpm = 2400.0f;
        bridge.maxFlyingRpm = 6800.0f;
        bridge.spinUpRate = 3500.0f;
        bridge.spinDownRate = 2500.0f;

        bridge.BindDroneV1Propellers();

        EditorUtility.SetDirty(bridge);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MIMISK] Legacy-style core propeller animation configured. " +
            "Bound=" + bridge.boundRotorCount + "/4, " +
            "FL=" + bridge.boundFL +
            ", FR=" + bridge.boundFR +
            ", RL=" + bridge.boundRL +
            ", RR=" + bridge.boundRR
        );
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
