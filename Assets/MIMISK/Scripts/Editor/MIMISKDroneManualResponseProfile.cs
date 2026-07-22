using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneManualResponseProfile
{
    [MenuItem("MIMISK/Drone/Apply Tuned Manual Response Profile")]
    public static void ApplyTunedManualResponseProfile()
    {
        GameObject drone = Selection.activeGameObject;

        if (drone == null || drone.GetComponent<MIMISKDroneModelController>() == null)
        {
            drone = GameObject.Find("Drone");
        }

        if (drone == null)
        {
            Debug.LogError("[MIMISK] Could not find Drone root. Select MIMISK_AerialAquaticSystem/Drone.");
            return;
        }

        MIMISKDroneModelController controller =
            drone.GetComponent<MIMISKDroneModelController>();

        if (controller == null)
        {
            Debug.LogError("[MIMISK] Drone does not have MIMISKDroneModelController.");
            return;
        }

        SerializedObject so = new SerializedObject(controller);

        // Keep the validated model package values.
        SetFloatIfExists(so, "massKg", 1.5f);
        SetFloatIfExists(so, "motorTimeConstantS", 0.0550339f);
        SetFloatIfExists(so, "maxMotorThrustN", 8.2743609375f);
        SetFloatIfExists(so, "yawMomentCoeffM", 0.035f);
        SetFloatIfExists(so, "maxTotalThrustN", 27.21345375f);

        SetVector3IfExists(so, "inertiaKgM2",
            new Vector3(0.1783796f, 0.41364064f, 0.23955822f));

        SetVector3IfExists(so, "attitudeKpRadS2",
            new Vector3(11.439608f, 5.448721f, 11.439608f));

        SetVector3IfExists(so, "rateKdRadS",
            new Vector3(5.982596f, 5.167017f, 5.982596f));

        SetFloatIfExists(so, "altitudeKpS2", 6.102603f);
        SetFloatIfExists(so, "altitudeKdS", 5.097711f);
        SetFloatIfExists(so, "altitudeKiS3", 1.3175f);
        SetFloatIfExists(so, "altitudeIntegralLimitMS", 1.25f);

        SetVector3IfExists(so, "torqueLimitsNm",
            new Vector3(2.8f, 0.36f, 2.8f));

        // Tuned manual response: stronger command-to-velocity behavior.
        SetBoolIfExists(so, "enableManualVelocityHold", true);
        SetFloatIfExists(so, "maxManualHorizontalSpeedMS", 1.2f);
        SetFloatIfExists(so, "horizontalVelocityToTiltDegPerMS", 5.0f);
        SetFloatIfExists(so, "maxBrakeTiltDeg", 6.0f);

        // Drone v1 forward motion convention found from logs.
        SetFloatIfExists(so, "forwardVelocityAxisSign", -1.0f);
        SetFloatIfExists(so, "rightVelocityAxisSign", 1.0f);

        // Slightly stronger yaw while still safe.
        SetFloatIfExists(so, "maxYawRateDegS", 45.0f);

        // Keep surface/air safety.
        SetBoolIfExists(so, "disableTiltCommandsNearWater", true);
        SetFloatIfExists(so, "minimumAirborneControlHeightM", 1.2f);
        SetBoolIfExists(so, "useTiltCompensation", true);
        SetFloatIfExists(so, "minVerticalThrustFactor", 0.55f);

        // Keep conservative attitude limits.
        SetFloatIfExists(so, "maxTiltDeg", 6.0f);
        SetFloatIfExists(so, "manualAltitudeRateMS", 0.6f);

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Applied tuned manual response profile.");
    }

    private static void SetFloatIfExists(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null)
        {
            p.floatValue = value;
        }
        else
        {
            Debug.LogWarning("[MIMISK] Missing float property: " + name);
        }
    }

    private static void SetBoolIfExists(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null)
        {
            p.boolValue = value;
        }
        else
        {
            Debug.LogWarning("[MIMISK] Missing bool property: " + name);
        }
    }

    private static void SetVector3IfExists(SerializedObject so, string name, Vector3 value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null)
        {
            p.vector3Value = value;
        }
        else
        {
            Debug.LogWarning("[MIMISK] Missing Vector3 property: " + name);
        }
    }
}
