using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKCommunicationBridgeSetup
{
    [MenuItem("MIMISK/Communication/Setup Final Communication Bridge")]
    public static void SetupFinalCommunicationBridge()
    {
        GameObject common =
            GameObject.Find("MIMISK_CommonInterface");

        if (common == null)
        {
            common = new GameObject("MIMISK_CommonInterface");
        }

        MIMISKCommonBus bus =
            common.GetComponent<MIMISKCommonBus>();

        if (bus == null)
        {
            bus = common.AddComponent<MIMISKCommonBus>();
        }

        bus.busEnabled = true;
        bus.logCommands = true;
        bus.logStateTransitions = false;

        EditorUtility.SetDirty(bus);

        MIMISKCommunicationBridge bridge =
            common.GetComponent<MIMISKCommunicationBridge>();

        if (bridge == null)
        {
            bridge = common.AddComponent<MIMISKCommunicationBridge>();
        }

        bridge.bus = bus;
        bridge.subscribeToCommonBus = true;
        bridge.publishBridgeStateToBus = true;
        bridge.statePublishHz = 20.0f;
        bridge.keyboardCommandsEnabled = true;

        bridge.allowDeploymentFromPhysicalSurfaceContact = true;
        bridge.blockDeploymentWhileDroneMissionActive = true;

        bridge.requireMiniRovBackendReadyForControl = true;
        bridge.requireRovNearRecoveryPointWhenControlActive = true;
        bridge.recoveryRadiusM = 0.60f;
        bridge.allowEmergencyRecoveryOverride = false;

        bridge.AutoFindReferences();
        bridge.RefreshState();

        EditorUtility.SetDirty(bridge);

        // Disable old common command chain. The final bridge owns command routing now.
        DisableByClassName(common, "MIMISKCommonCommandFrontend");
        DisableByClassName(common, "MIMISKCommandSafetyGate");
        DisableByClassName(common, "MIMISKCommandBridgeToExistingModules");
        DisableByClassName(common, "MIMISKUnifiedKeyboardFrontend");
        DisableByClassName(common, "MIMISKFinalPlannerAdapter");

        GameObject drone =
            GameObject.Find("Drone");

        if (drone != null)
        {
            // Disable older deployment/handoff owners that overlap the final bridge.
            DisableByClassName(drone, "MIMISKDroneTetherHandoffMission");
            DisableByClassName(drone, "MIMISKTetherDeploymentTestMission");
            DisableByClassName(drone, "MIMISKStandaloneTetherDeploymentMission");

            DisableByClassName(drone, "MIMISKMiniROVRealisticDeploymentManager");
            DisableByClassName(drone, "MIMISKMiniROVDeploymentManager");
            DisableByClassName(drone, "MIMISKMiniROVCableEndAttachmentManager");
            DisableByClassName(drone, "MIMISKMiniROVWaterReleaseController");
            DisableByClassName(drone, "MIMISKMiniROVPreDeploymentSafetyGuard");

            MIMISKDroneCoreTetherManager tether =
                drone.GetComponent<MIMISKDroneCoreTetherManager>();

            if (tether != null)
            {
                tether.acceptKeyboardCommands = false;
                tether.enableTetherForceWhenMiniRovAttached = false;
                tether.tetherStiffnessNPerM = 0.0f;
                tether.tetherDampingNPerMS = 0.0f;
                tether.maximumSafeTensionN = 999999.0f;
                EditorUtility.SetDirty(tether);
            }

            MIMISKDroneCoreMissionManager mission =
                drone.GetComponent<MIMISKDroneCoreMissionManager>();

            if (mission != null)
            {
                mission.holdAtReadyForTetherDeployment = true;
                EditorUtility.SetDirty(mission);
            }
        }

        GameObject miniRov =
            GameObject.Find("MiniROV");

        if (miniRov != null)
        {
            DisableByClassName(miniRov, "MIMISKMiniROVStandaloneManualTest");
            DisableByClassName(miniRov, "MIMISKMiniROVManualControlHandoff");

            MIMISKMiniROVModule module =
                miniRov.GetComponent<MIMISKMiniROVModule>();

            if (module != null)
            {
                module.keyboardTestEnabled = false;
                module.requireExternalSerialBeforeControl = true;
                module.useGravityInControl = true;
                module.enableSimpleRovBuoyancyInFinalStack = true;
                module.keepSensorManagerDisabled = true;

                module.correctOrientationOnHandoff = true;
                module.preserveCurrentYaw = false;
                module.keepTetherAnchorLockedDuringOrientationFix = true;

                EditorUtility.SetDirty(module);
            }

            MIMISKMiniROVCollisionIsolation isolation =
                miniRov.GetComponent<MIMISKMiniROVCollisionIsolation>();

            if (isolation != null)
            {
                isolation.isolationEnabled = true;
                isolation.ignoreDroneCollisions = true;
                isolation.ApplyIsolation();
                EditorUtility.SetDirty(isolation);
            }
        }

        MIMISKRuntimeSceneManifest manifest =
            common.GetComponent<MIMISKRuntimeSceneManifest>();

        if (manifest != null)
        {
            manifest.ValidateScene();
            EditorUtility.SetDirty(manifest);
        }

        Selection.activeGameObject = common;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Final Communication Bridge configured.");
        Debug.Log("[MIMISK] Commands: 1=start drone, 2=deploy tether, 3=enable ROV, 4=recover, 5=hold, F8=reset.");
    }

    private static void DisableByClassName(
        GameObject root,
        string className)
    {
        if (root == null)
        {
            return;
        }

        Type type =
            FindTypeByName(className);

        if (type == null)
        {
            return;
        }

        Component[] components =
            root.GetComponentsInChildren(type, true);

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
            Type direct =
                assembly.GetType(className);

            if (direct != null)
            {
                return direct;
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
