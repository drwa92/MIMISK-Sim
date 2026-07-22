using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMiniROVAgentSetup
{
    [MenuItem("MIMISK/MiniROV/Setup MiniROV Agent")]
    public static void SetupMiniROVAgent()
    {
        GameObject miniRov =
            GameObject.Find("MiniROV");

        if (miniRov == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV.");
            return;
        }

        MIMISKMiniROVAgent agent =
            miniRov.GetComponent<MIMISKMiniROVAgent>();

        if (agent == null)
        {
            agent =
                miniRov.AddComponent<MIMISKMiniROVAgent>();
        }

        agent.agentName = "MiniROV";
        agent.agentEnabled = true;
        agent.autoFindOnAwake = true;

        agent.missionManager =
            miniRov.GetComponent<MIMISKMiniROVMissionManager>();

        agent.pathPlanner =
            miniRov.GetComponent<MIMISKMiniROVPathPlanner>();

        agent.controller =
            miniRov.GetComponent<MIMISKMiniROVPlantBasedController>();

        agent.rb =
            miniRov.GetComponent<Rigidbody>();

        agent.AutoFindReferences();

        EditorUtility.SetDirty(agent);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] MiniROV Agent configured.");
    }
}
