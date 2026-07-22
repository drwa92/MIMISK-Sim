using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKTetherPhase2To4Setup
{
    [MenuItem("MIMISK/Tether/Setup Smart TMS Logger And Tether Agent")]
    public static void SetupSmartTmsLoggerAndTetherAgent()
    {
        GameObject root =
            Selection.activeGameObject;

        if (root == null)
        {
            MIMISKUnifiedTetherManager unified =
                Object.FindFirstObjectByType<MIMISKUnifiedTetherManager>();

            if (unified != null)
            {
                root = unified.gameObject;
            }
        }

        if (root == null)
        {
            Debug.LogError("[MIMISK] Select the Drone / MIMISK_AerialAquaticSystem root first.");
            return;
        }

        MIMISKUnifiedTetherManager unifiedTether =
            root.GetComponent<MIMISKUnifiedTetherManager>();

        MIMISKDroneCoreTetherManager tether =
            root.GetComponent<MIMISKDroneCoreTetherManager>();

        if (unifiedTether == null || tether == null)
        {
            Debug.LogError("[MIMISK] Missing UnifiedTetherManager or DroneCoreTetherManager. Run Setup Unified Tether System first.");
            return;
        }

        MIMISKTetherSmartWinchController smart =
            root.GetComponent<MIMISKTetherSmartWinchController>();

        if (smart == null)
        {
            smart =
                root.AddComponent<MIMISKTetherSmartWinchController>();
        }

        MIMISKUnifiedTetherResearchLogger logger =
            root.GetComponent<MIMISKUnifiedTetherResearchLogger>();

        if (logger == null)
        {
            logger =
                root.AddComponent<MIMISKUnifiedTetherResearchLogger>();
        }

        MIMISKTetherAgent agent =
            root.GetComponent<MIMISKTetherAgent>();

        if (agent == null)
        {
            agent =
                root.AddComponent<MIMISKTetherAgent>();
        }

        // Smart TMS.
        smart.unifiedTether = unifiedTether;
        smart.tetherManager = tether;
        smart.droneRigidbody = root.GetComponent<Rigidbody>();
        smart.controllerEnabled = true;
        smart.mode = MIMISKTetherSmartWinchController.SmartWinchMode.HybridFeedforward;
        smart.activeOnlyWhenRovControlActive = true;
        smart.takeOwnershipFromUnifiedSimpleSlack = true;

        smart.desiredSlackM = 0.10f;
        smart.slackDeadbandM = 0.03f;
        smart.stretchEmergencyThresholdM = 0.03f;
        smart.emergencyPayoutSlackM = 0.20f;

        smart.kpLength = 0.85f;
        smart.kdLength = 0.18f;
        smart.rovVelocityFeedforward = 0.65f;
        smart.minCommandSpeedMS = 0.015f;
        smart.maxPayoutSpeedMS = 0.25f;
        smart.maxRecoverySpeedMS = 0.25f;
        smart.AutoFindReferences();

        // Prevent the old simple adaptive slack from fighting smart TMS.
        unifiedTether.adaptiveSlackManagement = false;
        unifiedTether.desiredOperationalSlackM = 0.10f;
        unifiedTether.slackDeadbandM = 0.03f;

        // Logger.
        logger.unifiedTether = unifiedTether;
        logger.tetherManager = tether;
        logger.smartWinch = smart;
        logger.cableVisual = root.GetComponent<MIMISKSingleYellowTetherVisualAuthority>();
        logger.droneFlightManager = root.GetComponent<MIMISKDroneCoreFlightModeManager>();
        logger.droneMissionManager = root.GetComponent<MIMISKDroneCoreMissionManager>();
        logger.droneRigidbody = root.GetComponent<Rigidbody>();
        logger.enableLogging = true;
        logger.logHz = 20.0f;
        logger.flushEveryLine = false;
        logger.AutoFindReferences();

        // Agent.
        agent.agentName = "Tether";
        agent.agentEnabled = true;
        agent.autoFindOnAwake = true;
        agent.unifiedTether = unifiedTether;
        agent.tetherManager = tether;
        agent.smartWinch = smart;
        agent.researchLogger = logger;
        agent.visualAuthority = root.GetComponent<MIMISKSingleYellowTetherVisualAuthority>();
        agent.AutoFindReferences();

        EditorUtility.SetDirty(smart);
        EditorUtility.SetDirty(logger);
        EditorUtility.SetDirty(agent);
        EditorUtility.SetDirty(unifiedTether);
        EditorUtility.SetDirty(tether);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Smart TMS, research logger, and TetherAgent configured.");
    }
}
