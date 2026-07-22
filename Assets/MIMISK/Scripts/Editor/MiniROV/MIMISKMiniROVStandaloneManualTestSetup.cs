using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMiniROVStandaloneManualTestSetup
{
    [MenuItem("MIMISK/MiniROV/Setup Standalone Manual Control Test")]
    public static void SetupStandaloneMiniRovManualTest()
    {
        GameObject miniRov = GameObject.Find("MiniROV");

        if (miniRov == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV.");
            return;
        }

        GameObject drone = GameObject.Find("Drone");

        if (drone != null)
        {
            // Disable drone/tether/deployment owners while testing MiniROV alone.
            DisableByClassName(drone, "MIMISKDroneTetherHandoffMission");
            DisableByClassName(drone, "MIMISKStandaloneTetherDeploymentMission");
            DisableByClassName(drone, "MIMISKTetherDeploymentTestMission");

            DisableByClassName(drone, "MIMISKMiniROVRealisticDeploymentManager");
            DisableByClassName(drone, "MIMISKMiniROVDeploymentManager");
            DisableByClassName(drone, "MIMISKMiniROVCableEndAttachmentManager");
            DisableByClassName(drone, "MIMISKMiniROVWaterReleaseController");

            DisableByClassName(drone, "MIMISKMiniROVRealisticDeploymentLogger");
            DisableByClassName(drone, "MIMISKMiniROVDeploymentLogger");
            DisableByClassName(drone, "MIMISKMiniROVCableEndAttachmentLogger");
            DisableByClassName(drone, "MIMISKMiniROVWaterReleaseLogger");
        }

        Rigidbody rb = miniRov.GetComponent<Rigidbody>();

        if (rb == null)
        {
            rb = miniRov.AddComponent<Rigidbody>();
        }

        rb.mass = 0.60f;
        rb.isKinematic = true;
        rb.useGravity = false;

        EditorUtility.SetDirty(rb);

        MIMISKMiniROVStandaloneManualTest test =
            miniRov.GetComponent<MIMISKMiniROVStandaloneManualTest>();

        if (test == null)
        {
            test = miniRov.AddComponent<MIMISKMiniROVStandaloneManualTest>();
        }

        test.miniRovRoot = miniRov.transform;
        test.miniRovRigidbody = rb;
        test.miniRovColliders = miniRov.GetComponentsInChildren<Collider>(true);

        test.testEnabled = true;
        test.detachFromDroneOrCableOnPrepare = true;
        test.placeAtTestPoseOnPrepare = true;
        test.testWorldPosition = new Vector3(0.0f, -0.50f, -3.0f);
        test.testWorldEuler = Vector3.zero;
        test.forceLocalScaleOnPrepare = true;
        test.localScaleOnPrepare = Vector3.one;

        test.miniRovMassKg = 0.60f;
        test.enableCollidersWhenDynamic = false;
        test.useGravityWithWaterInteraction = true;

        test.requireExternalSerialBeforeControl = true;
        test.unityEsp32SerialDevice = "/dev/unity_esp32";

        EditorUtility.SetDirty(test);

        // Initial final profile: all MiniROV active scripts OFF.
        ApplyFinalMiniRovComponentProfile(miniRov);

        test.PrepareStandaloneMiniROV();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MIMISK] MiniROV standalone manual test configured. " +
            "In Play: P prepare, J dynamic-only, O water-only, I external control, K kill/passive, L reset pose."
        );
    }

    private static void ApplyFinalMiniRovComponentProfile(GameObject miniRov)
    {
        Behaviour[] behaviours =
            miniRov.GetComponentsInChildren<Behaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour b = behaviours[i];

            if (b == null)
            {
                continue;
            }

            string typeName = b.GetType().Name;

            bool managed =
                ContainsIgnoreCase(typeName, "UnityVirtualESP32") ||
                ContainsIgnoreCase(typeName, "ControlManager") ||
                ContainsIgnoreCase(typeName, "MIMISKWaterInteraction") ||
                ContainsIgnoreCase(typeName, "SensorManager") ||
                ContainsIgnoreCase(typeName, "SimpleROVBuoyancy");

            if (managed)
            {
                b.enabled = false;
                EditorUtility.SetDirty(b);
            }
        }
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

    private static bool ContainsIgnoreCase(string text, string pattern)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        return text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
