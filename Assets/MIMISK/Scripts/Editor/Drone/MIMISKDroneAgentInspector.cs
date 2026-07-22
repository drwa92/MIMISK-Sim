using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MIMISKDroneAgent))]
public class MIMISKDroneAgentInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MIMISKDroneAgent agent =
            (MIMISKDroneAgent)target;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField(
            "Drone Agent Runtime Test Panel",
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
            if (GUILayout.Button("Enter Takeoff Idle"))
            {
                agent.AgentEnterTakeoffIdle();
            }

            if (GUILayout.Button("Takeoff"))
            {
                agent.AgentTakeoff();
            }

            if (GUILayout.Button("Hold Position"))
            {
                agent.AgentHold();
            }

            if (GUILayout.Button("Start Full Drone Mission"))
            {
                agent.AgentStartDroneMission();
            }

            if (GUILayout.Button("Start Current Trajectory Path"))
            {
                agent.AgentStartCurrentPath();
            }

            if (GUILayout.Button("Land On Surface"))
            {
                agent.AgentLandOnSurface();
            }

            if (GUILayout.Button("Manual Mode"))
            {
                agent.AgentManualMode();
            }

            if (GUILayout.Button("Failsafe"))
            {
                agent.AgentFailsafe();
            }

            if (GUILayout.Button("Disarm"))
            {
                agent.AgentDisarm();
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
            EditorGUILayout.LabelField("Trajectory", state.selectedPathType);
            EditorGUILayout.LabelField("Yaw Policy", state.selectedYawPolicy);
            EditorGUILayout.LabelField("Active", state.active.ToString());
            EditorGUILayout.LabelField("Ready For Tether", state.recoveryReady.ToString());
            EditorGUILayout.LabelField("Yaw Deg", state.yawDeg.ToString("F2"));
            EditorGUILayout.LabelField("Speed M/S", state.speedMS.ToString("F3"));
            EditorGUILayout.LabelField("Tracking Error M", state.distanceToTargetM.ToString("F3"));

            EditorGUILayout.TextField("Last Event", state.lastEvent);
        }
        else
        {
            EditorGUILayout.LabelField("Mode", "Edit Mode");
        }
    }
}
