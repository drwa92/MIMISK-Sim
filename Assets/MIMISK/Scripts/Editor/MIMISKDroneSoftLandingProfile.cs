using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneSoftLandingProfile
{
    [MenuItem("MIMISK/Drone/Apply Soft Landing Profile")]
    public static void ApplySoftLandingProfile()
    {
        GameObject drone = Selection.activeGameObject;

        if (drone == null || drone.GetComponent<MIMISKDroneModelController>() == null)
        {
            drone = GameObject.Find("Drone");
        }

        if (drone == null)
        {
            Debug.LogError("[MIMISK] Could not find Drone root.");
            return;
        }

        MIMISKDroneModelController c = drone.GetComponent<MIMISKDroneModelController>();

        if (c == null)
        {
            Debug.LogError("[MIMISK] Drone has no MIMISKDroneModelController.");
            return;
        }

        SerializedObject so = new SerializedObject(c);

        SetFloat(so, "landingDescentRateMS", 0.20f);
        SetFloat(so, "landingTouchdownVerticalSpeedMS", 0.15f);
        SetBool(so, "stopMotorsOnlyAfterBuoyancyContact", true);

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(c);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Applied soft landing profile.");
    }

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.floatValue = value;
        else Debug.LogWarning("[MIMISK] Missing float: " + name);
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.boolValue = value;
        else Debug.LogWarning("[MIMISK] Missing bool: " + name);
    }
}
