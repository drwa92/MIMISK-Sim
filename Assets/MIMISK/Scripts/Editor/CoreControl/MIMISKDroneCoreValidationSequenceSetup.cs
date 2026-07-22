using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneCoreValidationSequenceSetup
{
    [MenuItem("MIMISK/Drone/Core Control/Setup Core Validation Sequence")]
    public static void SetupValidationSequence()
    {
        GameObject drone = Selection.activeGameObject;

        if (drone == null || drone.GetComponent<Rigidbody>() == null)
        {
            drone = GameObject.Find("Drone");
        }

        if (drone == null)
        {
            Debug.LogError("[MIMISK] Could not find Drone root.");
            return;
        }

        MIMISKDroneCoreValidationSequence seq =
            drone.GetComponent<MIMISKDroneCoreValidationSequence>();

        if (seq == null)
        {
            seq = drone.AddComponent<MIMISKDroneCoreValidationSequence>();
        }

        seq.core = drone.GetComponent<MIMISKDroneCoreRotorController>();
        seq.manager = drone.GetComponent<MIMISKDroneCoreFlightModeManager>();
        seq.trajectoryPlanner = drone.GetComponent<MIMISKDroneCoreTrajectoryPlanner>();
        seq.propellerBridge = drone.GetComponent<MIMISKDroneCorePropellerAnimationBridge>();

        seq.sequenceEnabled = true;
        seq.runOnStart = false;
        seq.sequenceState = MIMISKDroneCoreValidationSequence.SequenceState.Idle;

        seq.takeoffIdleSeconds = 2.0f;
        seq.holdAfterTakeoffSeconds = 4.0f;
        seq.holdAfterPathSeconds = 4.0f;

        seq.maxTakeoffWaitSeconds = 25.0f;
        seq.maxPathWaitSeconds = 80.0f;
        seq.maxLandingWaitSeconds = 45.0f;

        seq.validationPath =
            MIMISKDroneCoreFlightModeManager.PathKind.Circle;

        EditorUtility.SetDirty(seq);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Core validation sequence configured. Press V during Play to start.");
    }
}
