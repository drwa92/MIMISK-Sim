using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneRefinedManualProfile
{
    [MenuItem("MIMISK/Drone/Apply Refined Manual Braking Profile")]
    public static void ApplyRefinedManualProfile()
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

        // Keep validated model/controller package values.
        SetFloat(so, "massKg", 1.5f);
        SetVector3(so, "inertiaKgM2", new Vector3(0.1783796f, 0.41364064f, 0.23955822f));
        SetFloat(so, "motorTimeConstantS", 0.0550339f);
        SetFloat(so, "maxMotorThrustN", 8.2743609375f);
        SetFloat(so, "yawMomentCoeffM", 0.035f);
        SetFloat(so, "maxTotalThrustN", 27.21345375f);

        SetVector3(so, "attitudeKpRadS2", new Vector3(11.439608f, 5.448721f, 11.439608f));
        SetVector3(so, "rateKdRadS", new Vector3(5.982596f, 5.167017f, 5.982596f));

        SetFloat(so, "altitudeKpS2", 6.102603f);
        SetFloat(so, "altitudeKdS", 5.097711f);
        SetFloat(so, "altitudeKiS3", 1.3175f);
        SetFloat(so, "altitudeIntegralLimitMS", 1.25f);

        SetVector3(so, "torqueLimitsNm", new Vector3(2.8f, 0.36f, 2.8f));

        // Refined manual velocity/braking profile.
        SetBool(so, "enableManualVelocityHold", true);
        SetFloat(so, "maxManualHorizontalSpeedMS", 1.0f);
        SetFloat(so, "horizontalVelocityToTiltDegPerMS", 6.5f);
        SetFloat(so, "maxBrakeTiltDeg", 7.0f);

        // Sign convention confirmed by logs.
        SetFloat(so, "forwardVelocityAxisSign", -1.0f);
        SetFloat(so, "rightVelocityAxisSign", 1.0f);

        // Keep near-water safety.
        SetBool(so, "disableTiltCommandsNearWater", true);
        SetFloat(so, "minimumAirborneControlHeightM", 1.2f);
        SetBool(so, "useTiltCompensation", true);
        SetFloat(so, "minVerticalThrustFactor", 0.55f);

        // Motion limits.
        SetFloat(so, "maxTiltDeg", 6.0f);
        SetFloat(so, "maxYawRateDegS", 45.0f);
        SetFloat(so, "manualAltitudeRateMS", 0.6f);

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(c);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Applied refined manual braking profile.");
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

    private static void SetVector3(SerializedObject so, string name, Vector3 value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.vector3Value = value;
        else Debug.LogWarning("[MIMISK] Missing Vector3: " + name);
    }
}
