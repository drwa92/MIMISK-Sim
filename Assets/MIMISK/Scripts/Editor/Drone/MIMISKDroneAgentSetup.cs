using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneAgentSetup
{
    [MenuItem("MIMISK/Drone/Setup Drone Agent")]
    public static void SetupDroneAgent()
    {
        GameObject root =
            Selection.activeGameObject;

        if (root == null)
        {
            MIMISKDroneCoreRotorController anyCore =
                Object.FindFirstObjectByType<MIMISKDroneCoreRotorController>();

            if (anyCore != null)
            {
                root =
                    anyCore.gameObject;
            }
        }

        if (root == null)
        {
            Debug.LogError("[MIMISK] No selected drone and no MIMISKDroneCoreRotorController found.");
            return;
        }

        MIMISKDroneAgent agent =
            root.GetComponent<MIMISKDroneAgent>();

        if (agent == null)
        {
            agent =
                root.AddComponent<MIMISKDroneAgent>();
        }

        agent.agentName = "Drone";
        agent.agentEnabled = true;
        agent.autoFindOnAwake = true;
        agent.surfaceStableImpliesReadyForTether = true;

        agent.missionManager =
            root.GetComponent<MIMISKDroneCoreMissionManager>();

        agent.flightManager =
            root.GetComponent<MIMISKDroneCoreFlightModeManager>();

        agent.core =
            root.GetComponent<MIMISKDroneCoreRotorController>();

        agent.trajectoryPlanner =
            root.GetComponent<MIMISKDroneCoreTrajectoryPlanner>();

        agent.rb =
            root.GetComponent<Rigidbody>();

        agent.blockDroneMotionWhenMiniRovDeployed = true;
        agent.miniRovDeployment =
            root.GetComponent<MIMISKMiniROVRealisticDeploymentManager>();
        agent.tetherHandoff =
            root.GetComponent<MIMISKDroneTetherHandoffMission>();

        agent.AutoFindReferences();

        EditorUtility.SetDirty(agent);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Drone Agent configured on " + root.name + ".");
    }
}
