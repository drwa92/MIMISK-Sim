using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MIMISKMiniROVAgent))]
public class MIMISKMiniROVAgentInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MIMISKMiniROVAgent agent =
            (MIMISKMiniROVAgent)target;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField(
            "MiniROV Agent Runtime Test Panel",
            EditorStyles.boldLabel
        );

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Runtime commands are enabled only in Play Mode. " +
                "Use Auto Find References in Edit Mode if needed.",
                MessageType.Info
            );
        }

        if (GUILayout.Button("Auto Find References"))
        {
            agent.AutoFindReferences();
            EditorUtility.SetDirty(agent);
        }

        EditorGUILayout.Space(6);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Set Home"))
            {
                agent.AgentSetHome();
            }

            if (GUILayout.Button("Hold Current Pose"))
            {
                agent.AgentHold();
            }

            if (GUILayout.Button("Start Planner Selected Mission"))
            {
                agent.AgentStartSelectedMission();
            }

            if (GUILayout.Button("Dwell Current Yaw 3s"))
            {
                agent.AgentDwellCurrentYaw3s();
            }

            if (GUILayout.Button("Return Home"))
            {
                agent.AgentReturnHome();
            }

            if (GUILayout.Button("Abort To Hold"))
            {
                agent.AgentAbortToHold();
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
            EditorGUILayout.LabelField("Controller Mode", state.controllerMode);
            EditorGUILayout.LabelField("Selected Path", state.selectedPathType);
            EditorGUILayout.LabelField("Yaw Policy", state.selectedYawPolicy);
            EditorGUILayout.LabelField("Active", state.active.ToString());
            EditorGUILayout.LabelField("Recovery Ready", state.recoveryReady.ToString());
            EditorGUILayout.LabelField("Depth M", state.depthM.ToString("F3"));
            EditorGUILayout.LabelField("Yaw Deg", state.yawDeg.ToString("F2"));
            EditorGUILayout.LabelField("Speed M/S", state.speedMS.ToString("F3"));
            EditorGUILayout.LabelField("Distance To Home M", state.distanceToHomeM.ToString("F3"));
            EditorGUILayout.LabelField("Distance To Target M", state.distanceToTargetM.ToString("F3"));

            EditorGUILayout.TextField("Last Event", state.lastEvent);
        }
        else
        {
            EditorGUILayout.LabelField("Mode", "Edit Mode");
        }
    }
}
