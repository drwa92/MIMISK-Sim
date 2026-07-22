using UnityEditor;
using UnityEngine;

public static class MIMISKDroneV1PropellerBinder
{
    [MenuItem("MIMISK/Drone/Bind Drone V1 Propeller Pivots")]
    public static void BindPropellers()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            Debug.LogWarning("[MIMISK] Select the Drone root object first.");
            return;
        }

        MIMISKDronePropellerAnimator animator =
            selected.GetComponent<MIMISKDronePropellerAnimator>();

        if (animator == null)
        {
            animator = selected.AddComponent<MIMISKDronePropellerAnimator>();
        }

        string[] rotorNames =
        {
            "Rotor_FL_spin_pivot_Unity_rotate_local_Z",
            "Rotor_FR_spin_pivot_Unity_rotate_local_Z",
            "Rotor_RL_spin_pivot_Unity_rotate_local_Z",
            "Rotor_RR_spin_pivot_Unity_rotate_local_Z"
        };

        bool[] clockwise =
        {
            true,   // FL
            false,  // FR
            false,  // RL
            true    // RR
        };

        animator.rotors = new MIMISKDronePropellerAnimator.Rotor[rotorNames.Length];

        for (int i = 0; i < rotorNames.Length; i++)
        {
            Transform rotor = FindDeepChild(selected.transform, rotorNames[i]);

            animator.rotors[i] = new MIMISKDronePropellerAnimator.Rotor();
            animator.rotors[i].name = rotorNames[i];
            animator.rotors[i].rotorTransform = rotor;
            animator.rotors[i].localSpinAxis = Vector3.forward; // local Z
            animator.rotors[i].clockwise = clockwise[i];

            if (rotor == null)
            {
                Debug.LogWarning("[MIMISK] Rotor not found: " + rotorNames[i]);
            }
            else
            {
                Debug.Log("[MIMISK] Bound rotor: " + rotorNames[i]);
            }
        }

        animator.armedIdleRpm = 600f;
        animator.flyingRpm = 4200f;
        animator.waterSurfaceStableRpm = 0f;
        animator.spinUpRate = 2500f;
        animator.spinDownRate = 1800f;
        animator.enableKeyboardDebug = false;

        EditorUtility.SetDirty(animator);

        Debug.Log("[MIMISK] Drone V1 propeller pivots bound. Axis = local Z.");
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent;
        }

        foreach (Transform child in parent)
        {
            Transform result = FindDeepChild(child, name);

            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
