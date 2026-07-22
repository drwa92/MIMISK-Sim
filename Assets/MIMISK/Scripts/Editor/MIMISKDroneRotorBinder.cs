using UnityEditor;
using UnityEngine;

public static class MIMISKDroneRotorBinder
{
    [MenuItem("MIMISK/Drone/Bind Drone Rotor Model")]
    public static void BindResearchRotors()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            Debug.LogWarning("[MIMISK] Select the Drone root object first.");
            return;
        }

        MIMISKDroneRotorModel rotorModel =
            selected.GetComponent<MIMISKDroneRotorModel>();

        if (rotorModel == null)
        {
            rotorModel = selected.AddComponent<MIMISKDroneRotorModel>();
        }

        rotorModel.motor1_RR = MakeRotor(
            "M1_RR",
            FindDeepChild(selected.transform, "Rotor_RR_spin_pivot_Unity_rotate_local_Z"),
            -1f
        );

        rotorModel.motor2_FR = MakeRotor(
            "M2_FR",
            FindDeepChild(selected.transform, "Rotor_FR_spin_pivot_Unity_rotate_local_Z"),
            1f
        );

        rotorModel.motor3_RL = MakeRotor(
            "M3_RL",
            FindDeepChild(selected.transform, "Rotor_RL_spin_pivot_Unity_rotate_local_Z"),
            1f
        );

        rotorModel.motor4_FL = MakeRotor(
            "M4_FL",
            FindDeepChild(selected.transform, "Rotor_FL_spin_pivot_Unity_rotate_local_Z"),
            -1f
        );

        rotorModel.maxThrustPerRotorN = 18.0f;
        rotorModel.yawTorqueCoefficient = 0.18f;
        rotorModel.motorResponseRate = 8.0f;
        rotorModel.idleRpm = 500.0f;
        rotorModel.maxRpm = 5200.0f;

        EditorUtility.SetDirty(rotorModel);
        Debug.Log("[MIMISK] Bound drone rotor model using Drone V1 rotor pivots.");
    }

    private static MIMISKDroneRotorModel.Rotor MakeRotor(string name, Transform transform, float yawSign)
    {
        MIMISKDroneRotorModel.Rotor rotor = new MIMISKDroneRotorModel.Rotor();
        rotor.name = name;
        rotor.rotorTransform = transform;
        rotor.localThrustAxis = Vector3.up;
        rotor.localSpinAxis = Vector3.forward;
        rotor.yawTorqueSign = yawSign;

        if (transform == null)
        {
            Debug.LogWarning("[MIMISK] Missing rotor transform for " + name);
        }

        return rotor;
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
