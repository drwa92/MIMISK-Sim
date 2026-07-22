using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

public static class MIMISKPhase2B2CFinalRuntimeSetup
{
    [MenuItem("MIMISK/Communication/Phase 2B-2C Setup Final Public Runtime")]
    public static void SetupFinalPublicRuntime()
    {
        GameObject common = GameObject.Find("MIMISK_CommonInterface");

        if (common == null)
        {
            common = new GameObject("MIMISK_CommonInterface");
        }

        MIMISKCommonBus bus = common.GetComponent<MIMISKCommonBus>();

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

        // Phase 2B public command ownership.
        bridge.keyboardCommandsEnabled = true;
        bridge.startDroneMissionKey = Key.P;
        bridge.deployTetherKey = Key.U;
        bridge.enableMiniRovControlKey = Key.I;
        bridge.recoverMiniRovKey = Key.R;
        bridge.holdTetherKey = Key.K;
        bridge.resetBridgeKey = Key.F8;

        bridge.allowDeploymentFromPhysicalSurfaceContact = true;
        bridge.blockDeploymentWhileDroneMissionActive = true;

        bridge.requireMiniRovBackendReadyForControl = true;
        bridge.requireRovNearRecoveryPointWhenControlActive = true;
        bridge.recoveryRadiusM = 0.60f;
        bridge.allowEmergencyRecoveryOverride = false;


        GameObject rovForMission =
            GameObject.Find("MiniROV");

        if (rovForMission != null)
        {
            MIMISKMiniROVMissionManager rovMission =
                rovForMission.GetComponent<MIMISKMiniROVMissionManager>();

            if (rovMission != null)
            {
                bridge.miniRovMission = rovMission;
            }
        }

        bridge.AutoFindReferences();
        bridge.RefreshState();

        EditorUtility.SetDirty(bridge);

        // Old common command chain is now deprecated in the final public runtime.
        DisableByClassName(common, "MIMISKCommonCommandFrontend");
        DisableByClassName(common, "MIMISKCommandSafetyGate");
        DisableByClassName(common, "MIMISKCommandBridgeToExistingModules");
        DisableByClassName(common, "MIMISKUnifiedKeyboardFrontend");
        DisableByClassName(common, "MIMISKFinalPlannerAdapter");
        DisableByClassName(common, "MIMISKRuntimeSceneManifest");

        MIMISKCommonInterfaceReadOnlyProbe probe =
            common.GetComponent<MIMISKCommonInterfaceReadOnlyProbe>();

        if (probe != null)
        {
            probe.probeEnabled = true;
            probe.publishToBus = true;
            probe.publishHz = 10.0f;
            EditorUtility.SetDirty(probe);
        }

        MIMISKCommonInterfaceDashboard dashboard =
            common.GetComponent<MIMISKCommonInterfaceDashboard>();

        if (dashboard != null)
        {
            dashboard.dashboardEnabled = true;
            EditorUtility.SetDirty(dashboard);
        }

        MIMISKFinalRuntimeManifest manifest =
            common.GetComponent<MIMISKFinalRuntimeManifest>();

        if (manifest == null)
        {
            manifest = common.AddComponent<MIMISKFinalRuntimeManifest>();
        }

        manifest.manifestEnabled = true;
        manifest.validateOnStart = true;
        manifest.logReport = true;
        EditorUtility.SetDirty(manifest);

        GameObject drone = GameObject.Find("Drone");

        if (drone != null)
        {
            // Disable old overlapping runtime owners.
            DisableByClassName(drone, "MIMISKDroneTetherHandoffMission");
            DisableByClassName(drone, "MIMISKTetherDeploymentTestMission");
            DisableByClassName(drone, "MIMISKStandaloneTetherDeploymentMission");
            DisableByClassName(drone, "MIMISKMiniROVRealisticDeploymentManager");
            DisableByClassName(drone, "MIMISKMiniROVDeploymentManager");
            DisableByClassName(drone, "MIMISKMiniROVCableEndAttachmentManager");
            DisableByClassName(drone, "MIMISKMiniROVWaterReleaseController");
            DisableByClassName(drone, "MIMISKMiniROVPreDeploymentSafetyGuard");
            DisableByClassName(drone, "MIMISKDroneSurfaceIdleAntiVibrationGate");

            MIMISKDroneCoreMissionManager mission =
                drone.GetComponent<MIMISKDroneCoreMissionManager>();

            if (mission != null)
            {
                mission.missionEnabled = true;
                mission.runOnStart = false;
                mission.holdAtReadyForTetherDeployment = true;

                SetBoolFieldOrProperty(mission, "acceptKeyboardMissionCommands", false);

                EditorUtility.SetDirty(mission);
            }

            MIMISKFinalMissionPlanner planner =
                drone.GetComponent<MIMISKFinalMissionPlanner>();

            if (planner != null)
            {
                planner.plannerEnabled = true;
                planner.requireDeploymentTargetPosition = false;
                planner.allowDeploymentFromPhysicalSurfaceState = true;
                planner.blockDeploymentWhileDroneMissionActive = true;
                planner.enableActiveYellowTetherLine = false;

                SetBoolFieldOrProperty(planner, "acceptKeyboardCommands", false);

                EditorUtility.SetDirty(planner);
            }

            MIMISKDroneCoreTetherManager tether =
                drone.GetComponent<MIMISKDroneCoreTetherManager>();

            if (tether != null)
            {
                tether.acceptKeyboardCommands = false;
                tether.tetherSystemEnabled = true;

                tether.enableTetherForceWhenMiniRovAttached = false;
                tether.tetherStiffnessNPerM = 0.0f;
                tether.tetherDampingNPerMS = 0.0f;
                tether.maximumSafeTensionN = 999999.0f;

                EditorUtility.SetDirty(tether);
            }

            MIMISKFinalContinuousTetherVisual tetherVisual =
                drone.GetComponent<MIMISKFinalContinuousTetherVisual>();

            if (tetherVisual != null)
            {
                tetherVisual.activeOnlyDuringROVControl = true;
                tetherVisual.disableOtherTetherLineRenderers = true;
                EditorUtility.SetDirty(tetherVisual);
            }
        }

        GameObject miniRov = GameObject.Find("MiniROV");

        if (miniRov != null)
        {
            DisableByClassName(miniRov, "MIMISKMiniROVStandaloneManualTest");
            DisableByClassName(miniRov, "MIMISKMiniROVManualControlHandoff");



            MIMISKMiniROVTrajectoryPlanner planner =
                miniRov.GetComponent<MIMISKMiniROVTrajectoryPlanner>();

            if (planner == null)
            {
                planner = miniRov.AddComponent<MIMISKMiniROVTrajectoryPlanner>();
            }

            if (planner.trajectoryFile == null)
            {
                TextAsset defaultTrajectory =
                    AssetDatabase.LoadAssetAtPath<TextAsset>(
                        "Assets/MIMISK/Missions/Trajectories/minirov_kelp_inspection_demo.csv"
                    );

                if (defaultTrajectory != null)
                {
                    planner.trajectoryFile = defaultTrajectory;
                }
            }

            planner.trajectoryMode =
                MIMISKMiniROVTrajectoryPlanner.TrajectoryMode.TextAssetTrajectory;

            planner.trajectoryZIsUnityVerticalY = true;
            planner.trajectoryRelativeToMissionOrigin = true;
            planner.LoadTrajectory();

            EditorUtility.SetDirty(planner);

            MIMISKMiniROVCoreController controller =
                miniRov.GetComponent<MIMISKMiniROVCoreController>();

            if (controller == null)
            {
                controller = miniRov.AddComponent<MIMISKMiniROVCoreController>();
            }

            controller.rb =
                miniRov.GetComponent<Rigidbody>();

            controller.controlManager =
                miniRov.GetComponent<ControlManager>();

            controller.backendMode =
                MIMISKMiniROVCoreController.BackendMode.UnityNative;

            controller.controllerEnabled = true;
            controller.injectMotorFramesToControlManager = true;
            controller.disableESPBridgeInUnityNative = true;
            controller.disableControlManagerSerialReaderInUnityNative = true;

            controller.useBallastDepthController = true;
            controller.defaultWaterLevel = 0.0f;

            controller.surgeKp = 0.85f;
            controller.surgeKd = 1.15f;
            controller.maxSurgeCommand = 0.70f;

            controller.yawKp = 0.018f;
            controller.yawKd = 0.010f;
            controller.maxYawCommand = 0.55f;

            controller.maxThrusterPwm = 170;

            controller.AutoFindReferences();

            EditorUtility.SetDirty(controller);

            MIMISKMiniROVMissionManager missionManager =
                miniRov.GetComponent<MIMISKMiniROVMissionManager>();

            if (missionManager != null)
            {
                missionManager.allowStandaloneKeyboard = false;
                missionManager.missionEnabled = true;
                missionManager.releaseToWorldOnStart = true;
                missionManager.useRecoveryPointAsMissionOrigin = true;
                missionManager.trajectoryPlanner = planner;
                missionManager.coreController = controller;
                missionManager.LoadTrajectory();
                EditorUtility.SetDirty(missionManager);
            }


            MIMISKMiniROVGamepadReceiver localReceiver =
                miniRov.GetComponent<MIMISKMiniROVGamepadReceiver>();

            if (localReceiver != null)
            {
                localReceiver.receiverEnabled = false;
                localReceiver.enabled = false;
                localReceiver.enabled = false;
                EditorUtility.SetDirty(localReceiver);
            }

            MIMISKMiniROVUDPGamepadReceiver udpReceiver =
                miniRov.GetComponent<MIMISKMiniROVUDPGamepadReceiver>();

            if (udpReceiver != null)
            {
                udpReceiver.receiverEnabled = true;
                udpReceiver.requireMiniROVGamepadMissionState = true;
                udpReceiver.forceCoreGamepadModeWhenActive = true;
                udpReceiver.requireUnityNativeBackend = true;
                udpReceiver.useDirectCommandFields = true;
                udpReceiver.deriveFromRawAxes = true;
                EditorUtility.SetDirty(udpReceiver);
            }

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
                module.freeSwimWorldEuler = Vector3.zero;
                module.preserveCurrentYaw = false;
                module.keepTetherAnchorLockedDuringOrientationFix = true;

                module.enableCollidersDuringControl = true;

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

        manifest.ValidateScene();

        Selection.activeGameObject = common;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Phase 2B-2C final public runtime configured.");
        Debug.Log("[MIMISK] Public keys now route through MIMISKCommunicationBridge: P=start, U=deploy, I=ROV control, R=recover, K=hold, F8=reset.");
    }

    private static void DisableByClassName(GameObject root, string className)
    {
        if (root == null)
        {
            return;
        }

        Type type = FindTypeByName(className);

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
            Type direct = assembly.GetType(className);

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

    private static void SetBoolFieldOrProperty(object target, string name, bool value)
    {
        if (target == null)
        {
            return;
        }

        Type t = target.GetType();

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        FieldInfo f = t.GetField(name, flags);

        if (f != null && f.FieldType == typeof(bool))
        {
            f.SetValue(target, value);
            return;
        }

        PropertyInfo p = t.GetProperty(name, flags);

        if (p != null && p.PropertyType == typeof(bool) && p.CanWrite)
        {
            p.SetValue(target, value, null);
        }
    }
}
