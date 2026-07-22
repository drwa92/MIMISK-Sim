using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKFinalMissionPlannerSetup
{
    [MenuItem("MIMISK/Final Mission/Setup Final Drone-Tether-MiniROV Planner")]
    public static void SetupFinalMissionPlanner()
    {
        GameObject drone = GameObject.Find("Drone");
        GameObject miniRov = GameObject.Find("MiniROV");

        if (drone == null)
        {
            Debug.LogError("[MIMISK] Could not find Drone.");
            return;
        }

        if (miniRov == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV.");
            return;
        }

        // Disable previous integration/test owners. FinalMissionPlanner is now the bridge.
        DisableByClassName(drone, "MIMISKDroneTetherHandoffMission");
        DisableByClassName(drone, "MIMISKTetherDeploymentTestMission");
        DisableByClassName(drone, "MIMISKStandaloneTetherDeploymentMission");

        DisableByClassName(drone, "MIMISKMiniROVRealisticDeploymentManager");
        DisableByClassName(drone, "MIMISKMiniROVDeploymentManager");
        DisableByClassName(drone, "MIMISKMiniROVCableEndAttachmentManager");
        DisableByClassName(drone, "MIMISKMiniROVWaterReleaseController");

        DisableByClassName(miniRov, "MIMISKMiniROVStandaloneManualTest");
        DisableByClassName(miniRov, "MIMISKMiniROVManualControlHandoff");

        MIMISKDroneCoreTetherManager tether =
            drone.GetComponent<MIMISKDroneCoreTetherManager>();

        if (tether == null)
        {
            tether = drone.AddComponent<MIMISKDroneCoreTetherManager>();
        }

        tether.acceptKeyboardCommands = false;
        tether.tetherSystemEnabled = true;
        tether.targetDeployLengthM = 0.90f;
        tether.payoutSpeedMS = 0.22f;
        tether.recoverySpeedMS = 0.25f;

        tether.enableTetherForceWhenMiniRovAttached = false;
        tether.tetherStiffnessNPerM = 0.0f;
        tether.tetherDampingNPerMS = 0.0f;
        tether.maximumSafeTensionN = 999999.0f;

        EditorUtility.SetDirty(tether);

        Rigidbody rovRb =
            miniRov.GetComponent<Rigidbody>();

        if (rovRb == null)
        {
            rovRb = miniRov.AddComponent<Rigidbody>();
        }

        rovRb.mass = 0.60f;
        rovRb.isKinematic = true;
        rovRb.useGravity = false;
        rovRb.linearDamping = 4.0f;
        rovRb.angularDamping = 6.0f;

        EditorUtility.SetDirty(rovRb);

        MIMISKMiniROVModule miniRovModule =
            miniRov.GetComponent<MIMISKMiniROVModule>();

        if (miniRovModule == null)
        {
            miniRovModule = miniRov.AddComponent<MIMISKMiniROVModule>();
        }

        miniRovModule.miniRovRoot = miniRov.transform;
        miniRovModule.miniRovRigidbody = rovRb;
        miniRovModule.rovTetherAnchor =
            FindDeepChild(miniRov.transform, "ROV_TetherAnchor");

        if (miniRovModule.rovTetherAnchor == null)
        {
            miniRovModule.rovTetherAnchor =
                FindDeepChild(miniRov.transform, "MiniROV_TetherPoint");
        }

        if (miniRovModule.rovTetherAnchor == null)
        {
            miniRovModule.rovTetherAnchor =
                FindDeepChild(miniRov.transform, "TetherPoint");
        }

        miniRovModule.miniRovColliders =
            miniRov.GetComponentsInChildren<Collider>(true);

        miniRovModule.keyboardTestEnabled = false;
        miniRovModule.requireExternalSerialBeforeControl = true;
        miniRovModule.unityEsp32SerialDevice = "/dev/unity_esp32";

        miniRovModule.useGravityInControl = true;
        miniRovModule.enableSimpleRovBuoyancyInFinalStack = true;
        miniRovModule.keepSensorManagerDisabled = true;

        miniRovModule.correctOrientationOnHandoff = true;
        miniRovModule.freeSwimWorldEuler = Vector3.zero;
        miniRovModule.preserveCurrentYaw = false;
        miniRovModule.keepTetherAnchorLockedDuringOrientationFix = true;

        miniRovModule.enableCollidersDuringControl = true;

        miniRovModule.AutoFindReferences();
        miniRovModule.SetPassiveKinematic();

        EditorUtility.SetDirty(miniRovModule);

        MIMISKFinalMissionPlanner planner =
            drone.GetComponent<MIMISKFinalMissionPlanner>();

        if (planner == null)
        {
            planner = drone.AddComponent<MIMISKFinalMissionPlanner>();
        }

        planner.droneMission =
            drone.GetComponent<MIMISKDroneCoreMissionManager>();

        planner.flightManager =
            drone.GetComponent<MIMISKDroneCoreFlightModeManager>();

        planner.tetherManager = tether;
        planner.miniRovModule = miniRovModule;
        planner.droneRigidbody = drone.GetComponent<Rigidbody>();
        planner.droneRoot = drone.transform;
        planner.miniRovRoot = miniRov.transform;
        planner.rovTetherAnchor = miniRovModule.rovTetherAnchor;

        planner.tetherMovingEndpoint =
            FindDeepChild(drone.transform, "real_mesh_short_yellow_deployment_cable_to_hook");

        if (planner.tetherMovingEndpoint == null)
        {
            planner.tetherMovingEndpoint =
                FindDeepChild(drone.transform, "small_dark_open_deployment_hook_for_miniROV");
        }

        planner.tetherStart =
            FindDeepChild(drone.transform, "WinchFairlead_for_Unity_LineRenderer_Start");

        if (planner.tetherStart == null)
        {
            planner.tetherStart =
                FindDeepChild(drone.transform, "TetherAnchor");
        }

        if (planner.tetherStart == null)
        {
            planner.tetherStart =
                FindDeepChild(drone.transform, "WinchPoint");
        }

        planner.hookVisual =
            FindDeepChild(drone.transform, "small_dark_open_deployment_hook_for_miniROV");

        Transform followRoot =
            FindDeepChild(drone.transform, "MiniROV_CableEndFollowRoot");

        if (followRoot == null)
        {
            GameObject go = new GameObject("MiniROV_CableEndFollowRoot");
            go.transform.SetParent(drone.transform, false);
            followRoot = go.transform;
        }

        planner.cableEndFollowRoot = followRoot;

        GameObject targetObject =
            GameObject.Find("MIMISK_FinalDeploymentPoint");

        if (targetObject == null)
        {
            targetObject = new GameObject("MIMISK_FinalDeploymentPoint");
            targetObject.transform.position = drone.transform.position;
            targetObject.transform.rotation = drone.transform.rotation;
        }

        planner.deploymentTarget = targetObject.transform;
        planner.requireDeploymentTargetPosition = false;
        planner.allowDeploymentFromPhysicalSurfaceState = true;
        planner.blockDeploymentWhileDroneMissionActive = true;
        planner.deploymentHorizontalToleranceM = 0.75f;
        planner.deploymentYawToleranceDeg = 30.0f;

        planner.plannerEnabled = true;
        planner.state = MIMISKFinalMissionPlanner.FinalMissionState.WaitingForDroneMission;

        planner.targetDeployLengthM = 0.90f;
        planner.payoutSpeedMS = 0.22f;
        planner.recoverySpeedMS = 0.25f;

        planner.autoAttachMiniRovWhenReady = true;
        planner.autoDeployWhenSurfaceReady = false;
        planner.autoEnableMiniRovControl = false;

        planner.enableActiveYellowTetherLine = true;
        planner.disableOtherTetherLineRenderersOnROVControl = true;

        planner.ConfigureSafeDefaults();

        EditorUtility.SetDirty(planner);
        EditorUtility.SetDirty(targetObject);

        MIMISKDroneCoreMissionManager mission =
            drone.GetComponent<MIMISKDroneCoreMissionManager>();

        if (mission != null)
        {
            mission.missionEnabled = true;
            mission.holdAtReadyForTetherDeployment = true;
            EditorUtility.SetDirty(mission);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MIMISK] Final mission planner configured. " +
            "P runs drone mission. U deploys tether. I releases/enables MiniROV. R recovers."
        );
    }

    private static void DisableByClassName(GameObject root, string className)
    {
        if (root == null)
        {
            return;
        }

        Type t = FindTypeByName(className);

        if (t == null)
        {
            return;
        }

        Component[] components =
            root.GetComponentsInChildren(t, true);

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
