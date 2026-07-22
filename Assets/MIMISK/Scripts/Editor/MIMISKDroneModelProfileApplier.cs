using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneModelProfileApplier
{
    [MenuItem("MIMISK/Drone/Apply Model-Based Drone Controller From Final Package")]
    public static void ApplyProfile()
    {
        GameObject drone = Selection.activeGameObject;

        if (drone == null || drone.GetComponent<Rigidbody>() == null)
        {
            drone = GameObject.Find("Drone");
        }

        if (drone == null)
        {
            Debug.LogError("[MIMISK] Could not find Drone root. Select MIMISK_AerialAquaticSystem/Drone and run again.");
            return;
        }

        Rigidbody rb = drone.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = drone.AddComponent<Rigidbody>();
        }

        ApplyRigidbody(rb);

        MIMISKDroneModelController controller = drone.GetComponent<MIMISKDroneModelController>();
        if (controller == null)
        {
            controller = drone.AddComponent<MIMISKDroneModelController>();
        }

        ApplyController(drone, controller);

        MIMISKDroneSurfaceBuoyancy buoyancy = drone.GetComponent<MIMISKDroneSurfaceBuoyancy>();
        if (buoyancy == null)
        {
            buoyancy = drone.AddComponent<MIMISKDroneSurfaceBuoyancy>();
        }

        ApplySurfaceBuoyancy(buoyancy);

        MIMISKDroneFlightLogger logger = drone.GetComponent<MIMISKDroneFlightLogger>();
        if (logger == null)
        {
            logger = drone.AddComponent<MIMISKDroneFlightLogger>();
        }

        ApplyLogger(logger, controller, buoyancy);

        DisableOldControllers(drone);

        EditorUtility.SetDirty(drone);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Applied model-based drone controller profile safely.");
    }

    private static void ApplyRigidbody(Rigidbody rb)
    {
        rb.mass = 1.5f;
        rb.inertiaTensor = new Vector3(0.1783796f, 0.41364064f, 0.23955822f);
        rb.inertiaTensorRotation = Quaternion.identity;

        rb.useGravity = false;
        rb.isKinematic = true;

        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.02f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.constraints = RigidbodyConstraints.None;

        EditorUtility.SetDirty(rb);
    }

    private static void ApplyController(GameObject drone, MIMISKDroneModelController controller)
    {
        SerializedObject so = new SerializedObject(controller);

        SetEnum(so, "mode", 0); // Disarmed

        SetFloat(so, "massKg", 1.5f);
        SetVector3(so, "inertiaKgM2", new Vector3(0.1783796f, 0.41364064f, 0.23955822f));
        SetVector3(so, "angularDampingNmPerRadSec", new Vector3(0.02453234f, 0.03374142f, 0.02581342f));

        SetFloat(so, "motorTimeConstantS", 0.0550339f);
        SetFloat(so, "maxMotorThrustN", 8.2743609375f);
        SetFloat(so, "yawMomentCoeffM", 0.035f);
        SetFloat(so, "maxTotalThrustN", 27.21345375f);

        SetFloat(so, "rotorArmXM", 0.58f);
        SetFloat(so, "rotorArmZM", 0.50f);
        SetVector4(so, "rotorSpinSignsFL_FR_RL_RR", new Vector4(1f, -1f, -1f, 1f));

        SetVector3(so, "attitudeKpRadS2", new Vector3(11.439608f, 5.448721f, 11.439608f));
        SetVector3(so, "rateKdRadS", new Vector3(5.982596f, 5.167017f, 5.982596f));

        SetFloat(so, "altitudeKpS2", 6.102603f);
        SetFloat(so, "altitudeKdS", 5.097711f);
        SetFloat(so, "altitudeKiS3", 1.3175f);
        SetFloat(so, "altitudeIntegralLimitMS", 1.25f);

        SetVector3(so, "torqueLimitsNm", new Vector3(2.8f, 0.36f, 2.8f));

        SetFloat(so, "takeoffAltitudeM", 2.5f);
        SetFloat(so, "surfaceStableHeightM", 0.35f);
        SetFloat(so, "waterSurfaceY", 0.0f);

        SetFloat(so, "maxTiltDeg", 6.0f);
        SetFloat(so, "maxYawRateDegS", 35.0f);
        SetFloat(so, "manualAltitudeRateMS", 0.6f);

        SetFloatIfExists(so, "landingDescentRateMS", 0.35f);
        SetFloatIfExists(so, "landingTouchdownVerticalSpeedMS", 0.35f);
        SetBoolIfExists(so, "stopMotorsOnlyAfterBuoyancyContact", true);
        SetObjectIfExists(so, "surfaceBuoyancy", drone.GetComponent<MIMISKDroneSurfaceBuoyancy>());

        SetBool(so, "disableTiltCommandsNearWater", true);
        SetFloat(so, "minimumAirborneControlHeightM", 1.2f);
        SetBool(so, "useTiltCompensation", true);
        SetFloat(so, "minVerticalThrustFactor", 0.55f);

        SetBool(so, "enableKeyboardInput", true);

        SetBool(so, "invertPitchCommand", false);
        SetBool(so, "invertRollCommand", false);
        SetBool(so, "invertYawCommand", false);

        // Unity drone_v1 roll torque sign calibration.
        SetBool(so, "invertPitchTorqueSign", false);
        SetBool(so, "invertRollTorqueSign", true);
        SetBool(so, "invertYawTorqueSign", false);

        SetFloat(so, "idleRpm", 500.0f);
        SetFloat(so, "maxRpm", 5200.0f);
        SetVector3(so, "propellerSpinAxisLocal", Vector3.forward);

        SetObject(so, "rotorFL", FindDeepChild(drone.transform, "Rotor_FL_spin_pivot_Unity_rotate_local_Z"));
        SetObject(so, "rotorFR", FindDeepChild(drone.transform, "Rotor_FR_spin_pivot_Unity_rotate_local_Z"));
        SetObject(so, "rotorRL", FindDeepChild(drone.transform, "Rotor_RL_spin_pivot_Unity_rotate_local_Z"));
        SetObject(so, "rotorRR", FindDeepChild(drone.transform, "Rotor_RR_spin_pivot_Unity_rotate_local_Z"));

        so.ApplyModifiedProperties();

        controller.enabled = true;
        EditorUtility.SetDirty(controller);
    }

    private static void ApplySurfaceBuoyancy(MIMISKDroneSurfaceBuoyancy buoyancy)
    {
        SerializedObject so = new SerializedObject(buoyancy);

        SetFloatIfExists(so, "waterLevel", 0.0f);

        // Keep your existing buoyancy points if already assigned.
        SerializedProperty points = so.FindProperty("buoyancyPoints");
        if (points != null && points.isArray && points.arraySize > 0)
        {
            SetBoolIfExists(so, "autoFindBuoyancyPoints", false);
        }

        SetFloatIfExists(so, "floatRadius", 0.35f);

        // Newer surface-buoyancy version fields.
        SetBoolIfExists(so, "autoComputeForceFromMass", true);
        SetFloatIfExists(so, "buoyancyMargin", 2.2f);
        SetFloatIfExists(so, "pointVerticalDamping", 18.0f);

        // Older surface-buoyancy version field names.
        SetFloatIfExists(so, "maxBuoyancyForcePerPointN", 8.1f);
        SetFloatIfExists(so, "buoyancyForcePerPoint", 8.1f);
        SetFloatIfExists(so, "buoyancyForcePerPointN", 8.1f);
        SetFloatIfExists(so, "verticalDampingPerPoint", 18.0f);

        SetBoolIfExists(so, "stabilizeRollPitchOnWater", true);
        SetFloatIfExists(so, "surfaceLevelKp", 18.0f);
        SetFloatIfExists(so, "surfaceLevelKd", 8.0f);
        SetFloatIfExists(so, "maxSurfaceLevelTorque", 60.0f);

        SetFloatIfExists(so, "waterLinearDrag", 4.0f);
        SetFloatIfExists(so, "waterAngularDrag", 5.0f);

        // Older drag field names.
        SetFloatIfExists(so, "surfaceLinearDrag", 4.0f);
        SetFloatIfExists(so, "surfaceAngularDrag", 5.0f);

        SetBoolIfExists(so, "enableEmergencyRecovery", true);
        SetFloatIfExists(so, "maxAllowedRootSinkBelowWater", 0.75f);
        SetFloatIfExists(so, "emergencyRecoveryForceN", 200.0f);

        so.ApplyModifiedProperties();

        buoyancy.enabled = true;
        EditorUtility.SetDirty(buoyancy);
    }

    private static void ApplyLogger(
        MIMISKDroneFlightLogger logger,
        MIMISKDroneModelController controller,
        MIMISKDroneSurfaceBuoyancy buoyancy)
    {
        SerializedObject so = new SerializedObject(logger);

        SetObject(so, "controller", controller);
        SetObject(so, "surfaceBuoyancy", buoyancy);
        SetBool(so, "enableLogging", true);
        SetFloat(so, "logHz", 50.0f);
        SetBool(so, "flushEveryLine", false);

        so.ApplyModifiedProperties();

        logger.enabled = true;
        EditorUtility.SetDirty(logger);
    }

    private static void DisableOldControllers(GameObject drone)
    {
        DisableIfPresent(drone, "MIMISKDroneAutopilot");
        DisableIfPresent(drone, "MIMISKDroneRotorModel");
        DisableIfPresent(drone, "MIMISKDroneFlightController");
        DisableIfPresent(drone, "MIMISKDroneGPSAutopilot");
        DisableIfPresent(drone, "MIMISKDroneKeyboardInput");
        DisableIfPresent(drone, "MIMISKDroneGamepadInput");
        DisableIfPresent(drone, "MIMISKDronePropellerAnimator");
        DisableIfPresent(drone, "MIMISKDroneParameterEstimator");
    }

    private static void DisableIfPresent(GameObject obj, string componentName)
    {
        Component component = obj.GetComponent(componentName);

        if (component == null)
        {
            return;
        }

        Behaviour behaviour = component as Behaviour;

        if (behaviour != null)
        {
            behaviour.enabled = false;
            EditorUtility.SetDirty(behaviour);
            Debug.Log("[MIMISK] Disabled old drone component: " + componentName);
        }
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

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null)
        {
            Debug.LogWarning("[MIMISK] Missing required float property: " + name);
            return;
        }
        p.floatValue = value;
    }

    private static void SetFloatIfExists(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null)
        {
            p.floatValue = value;
        }
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null)
        {
            Debug.LogWarning("[MIMISK] Missing required bool property: " + name);
            return;
        }
        p.boolValue = value;
    }

    private static void SetBoolIfExists(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null)
        {
            p.boolValue = value;
        }
    }

    private static void SetEnum(SerializedObject so, string name, int value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null)
        {
            Debug.LogWarning("[MIMISK] Missing required enum property: " + name);
            return;
        }
        p.enumValueIndex = value;
    }

    private static void SetVector3(SerializedObject so, string name, Vector3 value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null)
        {
            Debug.LogWarning("[MIMISK] Missing required Vector3 property: " + name);
            return;
        }
        p.vector3Value = value;
    }

    private static void SetVector4(SerializedObject so, string name, Vector4 value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null)
        {
            Debug.LogWarning("[MIMISK] Missing required Vector4 property: " + name);
            return;
        }
        p.vector4Value = value;
    }


    private static void SetObjectIfExists(SerializedObject so, string name, Object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null)
        {
            p.objectReferenceValue = value;
        }
    }

    private static void SetObject(SerializedObject so, string name, Object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null)
        {
            Debug.LogWarning("[MIMISK] Missing required object property: " + name);
            return;
        }
        p.objectReferenceValue = value;
    }
}
