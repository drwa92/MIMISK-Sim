using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneCoreRotorAnimatorRebind
{
    [MenuItem("MIMISK/Drone/Core Control/Rebind Core Propeller Visuals")]
    public static void RebindCorePropellerVisuals()
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

        MIMISKDroneCoreRotorController core =
            drone.GetComponent<MIMISKDroneCoreRotorController>();

        MIMISKDroneCoreFlightModeManager manager =
            drone.GetComponent<MIMISKDroneCoreFlightModeManager>();

        MIMISKDroneCoreRotorAnimator anim =
            drone.GetComponent<MIMISKDroneCoreRotorAnimator>();

        if (anim == null)
        {
            anim = drone.AddComponent<MIMISKDroneCoreRotorAnimator>();
        }

        anim.core = core;
        anim.modeManager = manager;
        anim.legacyRotorModel =
            drone.GetComponentInChildren<MIMISKDroneRotorModel>(true);

        anim.animate = true;
        anim.idleOutput = 0.06f;
        anim.idleRpm = 500.0f;
        anim.maxRpm = 5200.0f;
        anim.visualResponseRate = 8.0f;
        anim.spinDownRpmPerSecond = 3000.0f;

        anim.AutoBindRotorVisuals();

        EditorUtility.SetDirty(anim);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MIMISK] Rebound core propeller visuals. " +
            "FL=" + anim.boundFL + ", FR=" + anim.boundFR +
            ", RL=" + anim.boundRL + ", RR=" + anim.boundRR
        );
    }
}
