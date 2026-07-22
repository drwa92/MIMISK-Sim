using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class MIMISKDronePropellerAutoBinder
{
    [MenuItem("MIMISK/Drone/Auto Bind Propellers On Selected Drone")]
    public static void AutoBindPropellers()
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

        Transform[] transforms = selected.GetComponentsInChildren<Transform>(true);

        List<Transform> propellers = new List<Transform>();

        foreach (Transform t in transforms)
        {
            string n = t.name.ToLower();

            if (n.Contains("prop") ||
                n.Contains("rotor") ||
                n.Contains("blade") ||
                n.Contains("fan"))
            {
                if (t.GetComponent<MeshRenderer>() != null ||
                    t.GetComponent<SkinnedMeshRenderer>() != null)
                {
                    propellers.Add(t);
                }
            }
        }

        if (propellers.Count == 0)
        {
            Debug.LogWarning("[MIMISK] No propeller-like transforms found. Rename propeller objects to include prop/rotor/blade, or assign manually.");
            return;
        }

        animator.rotors = new MIMISKDronePropellerAnimator.Rotor[propellers.Count];

        for (int i = 0; i < propellers.Count; i++)
        {
            animator.rotors[i] = new MIMISKDronePropellerAnimator.Rotor();
            animator.rotors[i].name = propellers[i].name;
            animator.rotors[i].rotorTransform = propellers[i];
            animator.rotors[i].localSpinAxis = Vector3.up;
            animator.rotors[i].clockwise = (i % 2 == 0);
        }

        EditorUtility.SetDirty(animator);

        Debug.Log("[MIMISK] Bound " + propellers.Count + " propeller transform(s).");
    }
}
