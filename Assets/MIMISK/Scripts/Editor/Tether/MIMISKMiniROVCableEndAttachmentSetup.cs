using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMiniROVCableEndAttachmentSetup
{
    [MenuItem("MIMISK/Drone/Tether/Setup Phase 3B Cable-End MiniROV Attachment")]
    public static void SetupCableEndMiniRovAttachment()
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

        GameObject miniRovGo = GameObject.Find("MiniROV");

        if (miniRovGo == null)
        {
            Transform found = FindDeepChild(drone.transform.root, "MiniROV");

            if (found != null)
            {
                miniRovGo = found.gameObject;
            }
        }

        if (miniRovGo == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV GameObject.");
            return;
        }

        // Disable previous carry-slot deployment manager to avoid conflict.
        DisableByClassName(drone, "MIMISKMiniROVDeploymentManager");

        MIMISKDroneCoreTetherManager tether =
            drone.GetComponent<MIMISKDroneCoreTetherManager>();

        if (tether == null)
        {
            tether = drone.AddComponent<MIMISKDroneCoreTetherManager>();
        }

        Transform yellowCableEnd =
            FindDeepChild(drone.transform, "real_mesh_short_yellow_deployment_cable_to_hook");

        Transform hook =
            FindDeepChild(drone.transform, "small_dark_open_deployment_hook_for_miniROV");

        if (yellowCableEnd == null)
        {
            Debug.LogError("[MIMISK] Could not find real_mesh_short_yellow_deployment_cable_to_hook.");
            return;
        }

        // Create a clean unscaled follow root under the Drone.
        Transform followRoot =
            FindDeepChild(drone.transform, "MiniROV_CableEndFollowRoot");

        if (followRoot == null)
        {
            GameObject followRootGo = new GameObject("MiniROV_CableEndFollowRoot");
            followRootGo.transform.SetParent(drone.transform, false);
            followRoot = followRootGo.transform;
        }

        followRoot.localScale = Vector3.one;
        followRoot.SetPositionAndRotation(yellowCableEnd.position, yellowCableEnd.rotation);

        tether.acceptKeyboardCommands = false;
        tether.movingTetherEndVisual = yellowCableEnd;
        tether.staticShortDeploymentCableMesh = null;
        tether.hideStaticShortCableMeshWhenDynamic = false;
        tether.useVirtualEndpointWhenNoMiniRov = true;

        tether.miniRovRigidbody = null;
        tether.miniRovTetherPoint = null;

        tether.targetDeployLengthM = 3.0f;
        tether.targetLengthM = tether.minimumLengthM;

        EditorUtility.SetDirty(tether);

        Rigidbody rovRb =
            miniRovGo.GetComponent<Rigidbody>();

        if (rovRb == null)
        {
            rovRb = miniRovGo.AddComponent<Rigidbody>();
        }

        rovRb.mass = 0.60f;
        rovRb.useGravity = false;
        rovRb.isKinematic = true;

        EditorUtility.SetDirty(rovRb);

        MIMISKMiniROVCableEndAttachmentManager attach =
            drone.GetComponent<MIMISKMiniROVCableEndAttachmentManager>();

        if (attach == null)
        {
            attach = drone.AddComponent<MIMISKMiniROVCableEndAttachmentManager>();
        }

        attach.tetherManager = tether;
        attach.missionManager = drone.GetComponent<MIMISKDroneCoreMissionManager>();
        attach.flightManager = drone.GetComponent<MIMISKDroneCoreFlightModeManager>();

        attach.miniRovRoot = miniRovGo.transform;
        attach.miniRovRigidbody = rovRb;
        attach.miniRovColliders = miniRovGo.GetComponentsInChildren<Collider>(true);

        attach.yellowCableEndPoint = yellowCableEnd;
        attach.hookVisual = hook;
        attach.cableEndFollowRoot = followRoot;

        attach.deploymentEnabled = true;
        attach.attachMiniRovOnStart = true;
        attach.requireMissionReady = true;
        attach.requireSurfaceStable = true;
        attach.keepMiniRovKinematicWhileCableAttached = true;
        attach.disableMiniRovCollidersWhileAttached = true;
        attach.forceMiniRovScaleOnAttach = true;
        attach.targetDeployLengthM = 3.0f;

        // Safe default. Adjust in Inspector only if the MiniROV visual orientation needs refinement.
        attach.miniRovLocalOffsetOnCableEnd = Vector3.zero;
        attach.miniRovLocalEulerOnCableEnd = Vector3.zero;
        attach.miniRovLocalScaleOnCableEnd = Vector3.one;

        EditorUtility.SetDirty(attach);

        attach.AttachMiniRovToCableEnd();

        MIMISKMiniROVCableEndAttachmentLogger logger =
            drone.GetComponent<MIMISKMiniROVCableEndAttachmentLogger>();

        if (logger == null)
        {
            logger = drone.AddComponent<MIMISKMiniROVCableEndAttachmentLogger>();
        }

        logger.attachment = attach;
        logger.tether = tether;
        logger.missionManager = attach.missionManager;
        logger.flightManager = attach.flightManager;
        logger.enableLogging = true;
        logger.logHz = 50.0f;
        logger.flushEveryLine = false;

        EditorUtility.SetDirty(logger);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MIMISK] Safe cable-end MiniROV attachment configured. " +
            "MiniROV follows real_mesh_short_yellow_deployment_cable_to_hook through MiniROV_CableEndFollowRoot. " +
            "Use U deploy, R recover, K stop, D reattach."
        );
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
