using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MIMISKTetherAgent))]
public class MIMISKTetherAgentInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MIMISKTetherAgent agent =
            (MIMISKTetherAgent)target;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField(
            "Tether Agent Runtime Test Panel",
            EditorStyles.boldLabel
        );

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Runtime commands are enabled only in Play Mode.",
                MessageType.Info
            );
        }

        if (GUILayout.Button("Auto Find References"))
        {
            agent.AutoFindReferences();
            EditorUtility.SetDirty(agent);
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Attach ROV To Cable End"))
            {
                agent.AgentAttachRovToCableEnd();
            }

            if (GUILayout.Button("Deploy ROV"))
            {
                agent.AgentDeployRov();
            }

            if (GUILayout.Button("Activate ROV Control"))
            {
                agent.AgentActivateRovControl();
            }

            if (GUILayout.Button("Enable Smart TMS"))
            {
                agent.AgentEnableSmartTms();
            }

            if (GUILayout.Button("Disable Smart TMS"))
            {
                agent.AgentDisableSmartTms();
            }

            if (GUILayout.Button("Recover ROV"))
            {
                agent.AgentRecoverRov();
            }

            if (GUILayout.Button("Hold Winch"))
            {
                agent.AgentHoldWinch();
            }
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField(
            "Live Agent State",
            EditorStyles.boldLabel
        );

        if (Application.isPlaying)
        {
            var state =
                agent.GetState();

            EditorGUILayout.LabelField("Mode", state.mode.ToString());
            EditorGUILayout.LabelField("Mission State", state.missionState);
            EditorGUILayout.LabelField("Tether State", state.controllerMode);
            EditorGUILayout.LabelField("Active", state.active.ToString());
            EditorGUILayout.LabelField("Recovery Ready", state.recoveryReady.ToString());
            EditorGUILayout.LabelField("Depth M", state.depthM.ToString("F3"));
            EditorGUILayout.LabelField("Winch Speed M/S", state.speedMS.ToString("F3"));
            EditorGUILayout.LabelField("Straight Distance M", state.distanceToTargetM.ToString("F3"));
            EditorGUILayout.LabelField("Distance To Deployment Home M", state.distanceToHomeM.ToString("F3"));

            EditorGUILayout.TextField("Last Event", state.lastEvent);
        }
        else
        {
            EditorGUILayout.LabelField("Mode", "Edit Mode");
        }
    }
}
