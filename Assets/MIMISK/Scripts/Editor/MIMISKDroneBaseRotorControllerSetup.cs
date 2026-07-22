using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneBaseRotorControllerSetup
{
    [MenuItem("MIMISK/Drone/Control/Setup Base Rotor Controller/Ground Truth")]
    public static void SetupGroundTruth()
    {
        Setup(MIMISKDroneBaseRotorController.StateSource.GroundTruth);
    }

    [MenuItem("MIMISK/Drone/Control/Setup Base Rotor Controller/AquaLoc")]
    public static void SetupAquaLoc()
    {
        Setup(MIMISKDroneBaseRotorController.StateSource.AquaLoc);
    }

    private static void Setup(MIMISKDroneBaseRotorController.StateSource stateSource)
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

        MIMISKDroneBaseRotorController c =
            drone.GetComponent<MIMISKDroneBaseRotorController>();

        if (c == null)
        {
            c = drone.AddComponent<MIMISKDroneBaseRotorController>();
        }

        Rigidbody rb = drone.GetComponent<Rigidbody>();

        c.rb = rb;
        c.aquaLoc = drone.GetComponent<MIMISKDroneAquaLocEstimator>();
        c.udpReceiver = drone.GetComponent<MIMISKDroneUdpGamepadReceiver>();
        c.legacyModelController = drone.GetComponent<MIMISKDroneModelController>();

        c.baseControllerEnabled = true;
        c.controlMode = MIMISKDroneBaseRotorController.ControlMode.ManualGamepad;
        c.stateSource = stateSource;

        c.disableLegacyModelController = true;
        c.keepUdpReceiverEnabled = true;

        c.manualMaxHorizontalSpeedMS = 0.75f;
        c.manualAltitudeRateMS = 0.45f;
        c.manualYawRateDegS = 35.0f;
        c.manualReferenceResponseHz = 5.0f;
        c.manualDeadzone = 0.05f;

        c.kpXZ = 1.25f;
        c.kdXZ = 2.15f;
        c.kiXZ = 0.04f;

        c.kpY = 1.8f;
        c.kdY = 1.7f;
        c.kiY = 0.15f;

        c.integralLimitXYZ = new Vector3(0.8f, 0.6f, 0.8f);
        c.maxTiltDeg = 22.0f;

        c.attitudeKpNmPerRad = new Vector3(8.0f, 1.2f, 8.0f);
        c.rateKdNmPerRadS = new Vector3(4.6f, 0.8f, 4.6f);
        c.torqueLimitNm = new Vector3(8.0f, 0.36f, 8.0f);

        c.useRigidbodyMass = true;
        c.massKg = rb != null ? rb.mass : 4.0f;
        c.gravity = 9.80665f;

        c.armX_M = 0.58f;
        c.armZ_M = 0.50f;
        c.maxThrustPerRotorN = 18.0f;
        c.motorTimeConstantS = 0.12f;
        c.yawTorqueCoeffNmPerN = 0.010f;
        c.rotorSpinSigns = new Vector4(1.0f, -1.0f, -1.0f, 1.0f);

        c.pathKind = MIMISKDroneBaseRotorController.PathKind.Circle;
        c.circleRadiusM = 1.4f;
        c.circleOmegaRadS = 0.23f;
        c.squareSideM = 2.4f;
        c.squareSpeedMS = 0.32f;
        c.spiralOmegaRadS = 0.28f;
        c.spiralInitialRadiusM = 0.25f;
        c.spiralFinalRadiusM = 1.25f;
        c.spiralDurationS = 36.0f;
        c.spiralAltitudeRiseM = 0.35f;
        c.missionDurationS = 40.0f;

        c.enableLogging = true;
        c.logHz = 50.0f;
        c.flushEveryLine = false;

        EditorUtility.SetDirty(c);

        MIMISKDroneUdpGamepadReceiver udp =
            drone.GetComponent<MIMISKDroneUdpGamepadReceiver>();

        if (udp != null)
        {
            udp.enabled = true;

            SerializedObject udpSO = new SerializedObject(udp);
            SetBoolIfExists(udpSO, "suppressCommandOutput", false);
            SetBoolIfExists(udpSO, "allowModeButtonsWhileSuppressed", false);
            udpSO.ApplyModifiedProperties();

            EditorUtility.SetDirty(udp);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Base rotor controller configured for " + stateSource + ".");
    }

    private static void SetBoolIfExists(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);

        if (p != null)
        {
            p.boolValue = value;
        }
    }
}
