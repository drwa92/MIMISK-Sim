using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKFinalTetherSystemSetup
{
    [MenuItem("MIMISK/Tether/Setup Final Tether System")]
    public static void SetupFinalTetherSystem()
    {
        GameObject root =
            Selection.activeGameObject;

        if (root == null)
        {
            MIMISKDroneCoreTetherManager tether =
                Object.FindFirstObjectByType<MIMISKDroneCoreTetherManager>();

            if (tether != null)
            {
                root = tether.gameObject;
            }
        }

        if (root == null)
        {
            Debug.LogError("[MIMISK] Select drone/tether root, or add MIMISKDroneCoreTetherManager first.");
            return;
        }

        MIMISKDroneCoreTetherManager tetherManager =
            root.GetComponent<MIMISKDroneCoreTetherManager>();

        MIMISKMiniROVRealisticDeploymentManager deployment =
            root.GetComponent<MIMISKMiniROVRealisticDeploymentManager>();

        MIMISKDroneTetherHandoffMission handoff =
            root.GetComponent<MIMISKDroneTetherHandoffMission>();

        MIMISKMiniROVPreDeploymentSafetyGuard guard =
            root.GetComponent<MIMISKMiniROVPreDeploymentSafetyGuard>();

        MIMISKFinalTetherLineSynchronizer visualSync =
            root.GetComponent<MIMISKFinalTetherLineSynchronizer>();

        if (tetherManager == null)
        {
            tetherManager =
                root.AddComponent<MIMISKDroneCoreTetherManager>();
        }

        if (deployment == null)
        {
            deployment =
                root.AddComponent<MIMISKMiniROVRealisticDeploymentManager>();
        }

        if (handoff == null)
        {
            handoff =
                root.AddComponent<MIMISKDroneTetherHandoffMission>();
        }

        if (guard == null)
        {
            guard =
                root.AddComponent<MIMISKMiniROVPreDeploymentSafetyGuard>();
        }

        if (visualSync == null)
        {
            visualSync =
                root.AddComponent<MIMISKFinalTetherLineSynchronizer>();
        }

        tetherManager.AutoFindReferences();
        deployment.AutoFindReferences();
        handoff.AutoFindReferences();
        guard.AutoFindReferences();

        // Final authority: TetherAgent/SystemOrchestrator will own commands.
        tetherManager.acceptKeyboardCommands = false;
        deployment.acceptKeyboardCommands = false;
        handoff.acceptKeyboardCommands = false;

        // Visual/logical tether first; force model can be enabled later after validation.
        tetherManager.enableTetherForceWhenMiniRovAttached = false;
        tetherManager.tetherStiffnessNPerM = 0.0f;
        tetherManager.tetherDampingNPerMS = 0.0f;
        tetherManager.maximumSafeTensionN = 999999.0f;

        tetherManager.minimumLengthM = 0.15f;
        tetherManager.maximumLengthM = 12.0f;
        tetherManager.targetDeployLengthM = 3.0f;
        tetherManager.payoutSpeedMS = 0.25f;
        tetherManager.recoverySpeedMS = 0.30f;

        deployment.tetherManager = tetherManager;
        deployment.flightManager = root.GetComponent<MIMISKDroneCoreFlightModeManager>();
        deployment.missionManager = root.GetComponent<MIMISKDroneCoreMissionManager>();

        deployment.deploymentEnabled = true;
        deployment.requireMissionReady = true;
        deployment.allowSurfaceStableAsMissionReady = true;
        deployment.requireSurfaceStable = true;
        deployment.attachMiniRovOnStart = true;

        deployment.targetDeployLengthM = 1.25f;
        deployment.payoutSpeedMS = 0.22f;
        deployment.recoverySpeedMS = 0.25f;
        deployment.releaseDepthBelowSurfaceM = 0.08f;
        deployment.minimumPayoutBeforeReleaseM = 0.15f;
        deployment.postWaterTouchStabilizationS = 1.50f;

        deployment.autoReleaseToDynamicAtWaterDepth = true;
        deployment.stopAndHoldKinematicAtReleaseDepth = false;

        deployment.preferHookVisualAsCableEndpoint = true;
        deployment.cableEndpointWorldOffset = Vector3.zero;
        deployment.parentToDeployedWorldRootOnRelease = false;

        deployment.recordHomeOnDynamicRelease = true;
        deployment.setMiniRovHomeOnDynamicRelease = true;
        deployment.levelRovOnDynamicRelease = true;
        deployment.setRovYawZeroOnRelease = true;
        deployment.releaseYawDeg = 0.0f;
        deployment.zeroAngularVelocityOnRelease = true;
        deployment.zeroHorizontalVelocityOnRelease = true;

        deployment.disableTetherForceForNow = true;
        deployment.adaptiveSlackManagement = true;
        deployment.desiredOperationalSlackM = 0.20f;
        deployment.slackDeadbandM = 0.05f;
        deployment.allowAutoSlackRecovery = true;

        deployment.requireMiniRovRecoveryReadyBeforeKinematicRecovery = true;
        deployment.requireRovNearDeploymentHomeForRecovery = true;
        deployment.recoveryHomeDistanceToleranceM = 0.35f;
        deployment.requestMiniRovReturnHomeWhenRecoverRequested = true;

        deployment.enableTetherLocalizationEstimate = true;
        deployment.tetherLengthUncertaintyM = 0.05f;
        deployment.tetherBearingUncertaintyDeg = 20.0f;

        handoff.missionManager = root.GetComponent<MIMISKDroneCoreMissionManager>();
        handoff.flightManager = root.GetComponent<MIMISKDroneCoreFlightModeManager>();
        handoff.tetherManager = tetherManager;
        handoff.rovDeployment = deployment;
        handoff.handoffEnabled = true;
        handoff.allowManualWhenSurfaceStable = true;
        handoff.autoDeployWhenReady = false;
        handoff.targetDeployLengthM = deployment.targetDeployLengthM;
        handoff.payoutSpeedMS = deployment.payoutSpeedMS;
        handoff.recoverySpeedMS = deployment.recoverySpeedMS;
        handoff.releaseDepthBelowSurfaceM = deployment.releaseDepthBelowSurfaceM;
        handoff.minimumPayoutBeforeReleaseM = deployment.minimumPayoutBeforeReleaseM;
        handoff.stabilizationSeconds = deployment.postWaterTouchStabilizationS;

        guard.deployment = deployment;
        guard.guardEnabled = true;
        guard.enforcePassiveBeforeWaterRelease = true;
        guard.enforcePassiveDuringRecovery = true;
        guard.disableCollidersBeforeRelease = true;
        guard.disableControlBeforeRelease = true;
        guard.disableWaterPhysicsBeforeRelease = true;

        visualSync.tetherManager = tetherManager;
        visualSync.deployment = deployment;
        visualSync.synchronizerEnabled = true;
        visualSync.preferHookForKinematicEndpoint = true;
        visualSync.lineSegments = 16;
        visualSync.sagScale = 0.15f;
        visualSync.maxSagM = 0.45f;
        visualSync.AutoFindReferences();

        DisableDeprecated(root);

        EditorUtility.SetDirty(tetherManager);
        EditorUtility.SetDirty(deployment);
        EditorUtility.SetDirty(handoff);
        EditorUtility.SetDirty(guard);
        EditorUtility.SetDirty(visualSync);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Final tether system configured on " + root.name + ".");
    }

    private static void DisableDeprecated(GameObject root)
    {
        DisableIfPresent<MIMISKStandaloneTetherDeploymentMission>(root);
        DisableIfPresent<MIMISKTetherDeploymentTestMission>(root);
        DisableIfPresent<MIMISKMiniROVDeploymentManager>(root);
        DisableIfPresent<MIMISKMiniROVCableEndAttachmentManager>(root);
        DisableIfPresent<MIMISKMiniROVWaterReleaseController>(root);
    }

    private static void DisableIfPresent<T>(GameObject root)
        where T : Behaviour
    {
        T c =
            root.GetComponent<T>();

        if (c != null)
        {
            c.enabled = false;
            EditorUtility.SetDirty(c);
        }
    }
}
