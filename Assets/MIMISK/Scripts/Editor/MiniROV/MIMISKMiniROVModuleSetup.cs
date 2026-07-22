using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMiniROVModuleSetup
{
    [MenuItem("MIMISK/MiniROV/Setup Module 1 MiniROV Only")]
    public static void SetupMiniROVModuleOnly()
    {
        GameObject miniRov =
            GameObject.Find("MiniROV");

        if (miniRov == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV.");
            return;
        }

        // Disable older optional MiniROV scripts without hard compile dependency.
        DisableByClassName(miniRov, "MIMISKMiniROVStandaloneManualTest");
        DisableByClassName(miniRov, "MIMISKMiniROVManualControlHandoff");

        // Disable drone-side deployment controllers while testing MiniROV module only.
        GameObject drone = GameObject.Find("Drone");

        if (drone != null)
        {
            DisableByClassName(drone, "MIMISKDroneTetherHandoffMission");
            DisableByClassName(drone, "MIMISKStandaloneTetherDeploymentMission");
            DisableByClassName(drone, "MIMISKTetherDeploymentTestMission");

            DisableByClassName(drone, "MIMISKMiniROVRealisticDeploymentManager");
            DisableByClassName(drone, "MIMISKMiniROVDeploymentManager");
            DisableByClassName(drone, "MIMISKMiniROVCableEndAttachmentManager");
            DisableByClassName(drone, "MIMISKMiniROVWaterReleaseController");
        }

        Rigidbody rb =
            miniRov.GetComponent<Rigidbody>();

        if (rb == null)
        {
            rb = miniRov.AddComponent<Rigidbody>();
        }

        rb.mass = 0.60f;
        rb.isKinematic = true;
        rb.useGravity = false;

        EditorUtility.SetDirty(rb);

        MIMISKMiniROVModule module =
            miniRov.GetComponent<MIMISKMiniROVModule>();

        if (module == null)
        {
            module = miniRov.AddComponent<MIMISKMiniROVModule>();
        }

        module.miniRovRoot = miniRov.transform;
        module.miniRovRigidbody = rb;
        module.rovTetherAnchor =
            FindDeepChild(miniRov.transform, "ROV_TetherAnchor");

        if (module.rovTetherAnchor == null)
        {
            module.rovTetherAnchor =
                FindDeepChild(miniRov.transform, "MiniROV_TetherPoint");
        }

        if (module.rovTetherAnchor == null)
        {
            module.rovTetherAnchor =
                FindDeepChild(miniRov.transform, "TetherPoint");
        }

        module.miniRovColliders =
            miniRov.GetComponentsInChildren<Collider>(true);

        module.miniRovMassKg = 0.60f;
        module.requireExternalSerialBeforeControl = true;
        module.unityEsp32SerialDevice = "/dev/unity_esp32";
        module.useGravityInControl = true;

        // Keep colliders OFF in Module 1 integration testing.
        // Scene 13 standalone can enable them later if needed.
        module.enableCollidersDuringControl = false;

        module.keyboardTestEnabled = true;

        module.AutoFindReferences();
        module.SetPassiveKinematic();

        EditorUtility.SetDirty(module);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MIMISK] Module 1 MiniROV-only setup complete. " +
            "Play test: P passive, J free dynamic passive, I external control. " +
            "Old handoff/test scripts are disabled without hard dependencies."
        );
    }

    private static void DisableByClassName(GameObject root, string className)
    {
        if (root == null)
        {
            return;
        }

        Type t =
            FindTypeByName(className);

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
            Behaviour b =
                components[i] as Behaviour;

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
            Type t =
                assembly.GetType(className);

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
            Transform found =
                FindDeepChild(root.GetChild(i), childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
