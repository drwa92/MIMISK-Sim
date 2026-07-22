using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKUnifiedTetherSystemSetup
{
    [MenuItem("MIMISK/Tether/Setup Unified Tether System")]
    public static void SetupUnifiedTetherSystem()
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
            Debug.LogError("[MIMISK] Select the Drone / MIMISK_AerialAquaticSystem root first.");
            return;
        }

        MIMISKDroneCoreTetherManager lowLevel =
            root.GetComponent<MIMISKDroneCoreTetherManager>();

        if (lowLevel == null)
        {
            lowLevel =
                root.AddComponent<MIMISKDroneCoreTetherManager>();
        }

        MIMISKUnifiedTetherManager unified =
            root.GetComponent<MIMISKUnifiedTetherManager>();

        if (unified == null)
        {
            unified =
                root.AddComponent<MIMISKUnifiedTetherManager>();
        }

        // Disable all old/high-level tether/deployment owners.
        DisableIfPresent<MIMISKDroneTetherHandoffMission>(root);
        DisableIfPresent<MIMISKMiniROVRealisticDeploymentManager>(root);
        DisableIfPresent<MIMISKMiniROVPreDeploymentSafetyGuard>(root);
        DisableIfPresent<MIMISKStandaloneTetherDeploymentMission>(root);
        DisableIfPresent<MIMISKTetherDeploymentTestMission>(root);
        DisableIfPresent<MIMISKMiniROVCableEndAttachmentManager>(root);
        DisableIfPresent<MIMISKMiniROVDeploymentManager>(root);
        DisableIfPresent<MIMISKMiniROVWaterReleaseController>(root);
        DisableIfPresent<MIMISKFinalTetherEndpointRig>(root);
        DisableIfPresent<MIMISKFinalTetherLineSynchronizer>(root);

        unified.tetherManager =
            lowLevel;

        unified.droneMissionManager =
            root.GetComponent<MIMISKDroneCoreMissionManager>();

        unified.droneFlightManager =
            root.GetComponent<MIMISKDroneCoreFlightModeManager>();

        unified.droneAgent =
            root.GetComponent<MIMISKDroneAgent>();

        unified.droneRigidbody =
            root.GetComponent<Rigidbody>();

        unified.managerEnabled = true;
        unified.acceptKeyboardCommands = false;

        unified.requireDroneSurfaceStable = true;
        unified.allowSurfaceStableWithoutFullMission = true;

        unified.targetDeployLengthM = 1.25f;
        unified.payoutSpeedMS = 0.22f;
        unified.recoverySpeedMS = 0.25f;
        unified.minimumLengthM = 0.05f;
        unified.maximumLengthM = 12.0f;

        unified.releaseDepthBelowSurfaceM = 0.08f;
        unified.minimumPayoutBeforeReleaseM = 0.10f;
        unified.stabilizationSeconds = 1.5f;
        unified.autoActivateRovControlAfterStabilization = false;

        unified.levelRovOnRelease = true;
        unified.setYawZeroOnRelease = true;
        unified.releaseYawDeg = 0.0f;
        unified.zeroVelocitiesOnRelease = true;

        unified.recordDeploymentHome = true;
        unified.setMiniRovHomeToDeploymentPoint = true;
        unified.requireRovRecoveryReadyBeforeWinchRecovery = true;
        unified.requestRovReturnHomeWhenRecoverRequested = true;
        unified.allowRecoveryWhenRovNearDeploymentHome = true;
        unified.recoveryHomeToleranceM = 0.35f;

        unified.adaptiveSlackManagement = true;
        unified.desiredOperationalSlackM = 0.20f;
        unified.slackDeadbandM = 0.05f;
        unified.allowAutoSlackRecovery = true;

        unified.enableTetherForce = false;
        unified.tetherStiffnessNPerM = 0.0f;
        unified.tetherDampingNPerMS = 0.0f;
        unified.maximumSafeTensionN = 999999.0f;

        unified.AutoFindReferences();
        unified.ConfigureAuthoritativeDefaults();

        if (lowLevel != null)
        {
            lowLevel.acceptKeyboardCommands = false;
            lowLevel.tetherSystemEnabled = true;

            lowLevel.requireSurfaceStable = true;
            lowLevel.allowManualDeploymentWhenSurfaceStable = true;

            lowLevel.minimumLengthM = unified.minimumLengthM;
            lowLevel.maximumLengthM = unified.maximumLengthM;
            lowLevel.targetDeployLengthM = unified.targetDeployLengthM;
            lowLevel.payoutSpeedMS = unified.payoutSpeedMS;
            lowLevel.recoverySpeedMS = unified.recoverySpeedMS;

            lowLevel.enableTetherForceWhenMiniRovAttached = unified.enableTetherForce;
            lowLevel.tetherStiffnessNPerM = unified.tetherStiffnessNPerM;
            lowLevel.tetherDampingNPerMS = unified.tetherDampingNPerMS;
            lowLevel.maximumSafeTensionN = unified.maximumSafeTensionN;

            EditorUtility.SetDirty(lowLevel);
        }

        EditorUtility.SetDirty(unified);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Unified tether system configured on " + root.name + ". Old tether/deployment owners disabled.");
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
