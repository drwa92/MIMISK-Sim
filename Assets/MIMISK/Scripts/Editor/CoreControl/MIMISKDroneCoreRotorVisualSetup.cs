using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneCoreRotorVisualSetup
{
    [MenuItem("MIMISK/Drone/Core Control/Setup Core Rotor Visual Animation")]
    public static void SetupRotorVisualAnimation()
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

        if (core == null)
        {
            Debug.LogError("[MIMISK] Core Rotor Controller missing. Run Clean Core Flight Stack setup first.");
            return;
        }

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

        anim.pivotNameFL = "Rotor_FL_spin_pivot_Unity_rotate_local_Z";
        anim.pivotNameFR = "Rotor_FR_spin_pivot_Unity_rotate_local_Z";
        anim.pivotNameRL = "Rotor_RL_spin_pivot_Unity_rotate_local_Z";
        anim.pivotNameRR = "Rotor_RR_spin_pivot_Unity_rotate_local_Z";

        anim.idleOutput = 0.06f;
        anim.idleRpm = 600.0f;
        anim.maxRpm = 6800.0f;
        anim.visualResponseRate = 10.0f;
        anim.spinDownRpmPerSecond = 4500.0f;

        SetRotorSpinAxes(anim, Vector3.forward);

        anim.rotorFL.spinSign = 1.0f;
        anim.rotorFR.spinSign = -1.0f;
        anim.rotorRL.spinSign = -1.0f;
        anim.rotorRR.spinSign = 1.0f;

        anim.AutoBindRotorVisuals();

        EditorUtility.SetDirty(anim);

        // Keep the old rotor model disabled. It is used only as a visual reference source.
        MIMISKDroneRotorModel legacyRotorModel =
            drone.GetComponentInChildren<MIMISKDroneRotorModel>(true);

        if (legacyRotorModel != null)
        {
            legacyRotorModel.enabled = false;
            EditorUtility.SetDirty(legacyRotorModel);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MIMISK] Core rotor visual animation configured. " +
            "FL=" + anim.boundFL + ", FR=" + anim.boundFR +
            ", RL=" + anim.boundRL + ", RR=" + anim.boundRR
        );
    }

    private static void SetRotorSpinAxes(
        MIMISKDroneCoreRotorAnimator anim,
        Vector3 axis)
    {
        if (anim == null)
        {
            return;
        }

        if (anim.rotorFL != null) anim.rotorFL.localSpinAxis = axis;
        if (anim.rotorFR != null) anim.rotorFR.localSpinAxis = axis;
        if (anim.rotorRL != null) anim.rotorRL.localSpinAxis = axis;
        if (anim.rotorRR != null) anim.rotorRR.localSpinAxis = axis;
    }
}
