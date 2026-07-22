using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMiniROVDirectBypassSetup
{
    [MenuItem("MIMISK/MiniROV/Setup Direct Raspberry Bypass UDP Input")]
    public static void SetupDirectRaspberryBypass()
    {
        GameObject miniRov =
            GameObject.Find("MiniROV");

        if (miniRov == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV.");
            return;
        }

        Rigidbody rb =
            miniRov.GetComponent<Rigidbody>();

        ControlManager control =
            miniRov.GetComponent<ControlManager>();

        if (control == null)
        {
            control = miniRov.AddComponent<ControlManager>();
        }

        UnityVirtualESP32 esp32 =
            miniRov.GetComponent<UnityVirtualESP32>();

        if (esp32 == null)
        {
            esp32 = miniRov.AddComponent<UnityVirtualESP32>();
        }

        MIMISKMiniROVDirectRaspberryBypassInput input =
            miniRov.GetComponent<MIMISKMiniROVDirectRaspberryBypassInput>();

        if (input == null)
        {
            input = miniRov.AddComponent<MIMISKMiniROVDirectRaspberryBypassInput>();
        }

        input.controlManager = control;
        input.unityVirtualESP32 = esp32;
        input.rb = rb;

        input.receiverEnabled = true;
        input.listenAddress = "0.0.0.0";
        input.listenPort = 54341;

        input.enableInputTimeout = false;
        input.inputTimeoutS = 2.0f;

        input.stopControlManagerSerialReader = true;
        input.stopUnityVirtualESP32Bridge = true;
        input.keepUnityVirtualESP32Enabled = true;

        input.useLyForThrottle = true;
        input.useLxForYaw = true;
        input.useRtMinusLtForDc = true;

        input.invertThrottle = false;
        input.invertYaw = false;
        input.invertDc = false;

        input.throttleScale = 1.0f;
        input.yawScale = 1.0f;
        input.dcScale = 1.0f;

        input.maxThrusterPwm = 255;
        input.maxDcPwm = 255;

        input.swapLeftRight = false;
        input.invertLeftThruster = false;
        input.invertRightThruster = false;
        input.invertBothThrusters = false;

        input.AutoFindReferences();

        EditorUtility.SetDirty(input);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            EditorUtility.SetDirty(rb);
        }

        control.enabled = true;
        control.autoOpenOnStart = false;

        if (rb != null)
        {
            control.rb = rb;
        }

        if (control.leftThruster == null)
        {
            control.leftThruster =
                FindDeepChild(miniRov.transform, "propulseur_gauche");
        }

        if (control.rightThruster == null)
        {
            control.rightThruster =
                FindDeepChild(miniRov.transform, "propulseur_droite");
        }

        // Do not change ControlManager.useThrusterTransformForward.
        // Keep the exact value that works in your current Scene 13 baseline.
        EditorUtility.SetDirty(control);

        esp32.enabled = true;
        esp32.autoOpenOnStart = false;
        esp32.rb = rb;
        esp32.controlManager = control;

        EditorUtility.SetDirty(esp32);

        DisableByClassName(miniRov, "MIMISKMiniROVCoreController");
        DisableByClassName(miniRov, "MIMISKMiniROVUDPGamepadReceiver");
        DisableByClassName(miniRov, "MIMISKMiniROVGamepadReceiver");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Direct Raspberry Bypass UDP Input configured. Send pc_sender_new.py to UDP port 54341.");
    }

    private static void DisableByClassName(GameObject go, string className)
    {
        if (go == null)
        {
            return;
        }

        Type type = FindTypeByName(className);

        if (type == null)
        {
            return;
        }

        Component c =
            go.GetComponent(type);

        Behaviour b =
            c as Behaviour;

        if (b != null)
        {
            b.enabled = false;
            EditorUtility.SetDirty(b);
        }
    }

    private static Type FindTypeByName(string className)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type direct =
                asm.GetType(className);

            if (direct != null)
            {
                return direct;
            }

            Type[] types;

            try
            {
                types = asm.GetTypes();
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
