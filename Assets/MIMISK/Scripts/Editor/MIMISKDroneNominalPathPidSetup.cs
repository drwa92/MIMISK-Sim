using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneNominalPathPidSetup
{
    [MenuItem("MIMISK/Drone/Autonomy/Setup Nominal Path PID Rotor Controller/Ground Truth")]
    public static void SetupGroundTruth()
    {
        Setup(MIMISKDroneNominalPathPidRotorController.StateSource.GroundTruth);
    }

    [MenuItem("MIMISK/Drone/Autonomy/Setup Nominal Path PID Rotor Controller/RTK AquaLoc")]
    public static void SetupRtk()
    {
        Setup(MIMISKDroneNominalPathPidRotorController.StateSource.AquaLocRTK);
    }

    private static void Setup(MIMISKDroneNominalPathPidRotorController.StateSource source)
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

        MIMISKDroneNominalPathPidRotorController c =
            drone.GetComponent<MIMISKDroneNominalPathPidRotorController>();

        if (c == null)
        {
            c = drone.AddComponent<MIMISKDroneNominalPathPidRotorController>();
        }

        Rigidbody rb = drone.GetComponent<Rigidbody>();

        c.rb = rb;
        c.aquaLoc = drone.GetComponent<MIMISKDroneAquaLocEstimator>();
        c.udpReceiver = drone.GetComponent<MIMISKDroneUdpGamepadReceiver>();
        c.modelControllerToDisable = drone.GetComponent<MIMISKDroneModelController>();

        c.stateSource = source;
        c.pathKind = MIMISKDroneNominalPathPidRotorController.PathKind.Circle;
        c.missionActive = false;

        c.disableOldModelControllerDuringMission = true;
        c.restoreGamepadOnFinish = true;

        // Python nominal outer-loop gains.
        c.kpXZ = 1.25f;
        c.kdXZ = 2.15f;
        c.kiXZ = 0.04f;

        c.kpY = 1.8f;
        c.kdY = 1.7f;
        c.kiY = 0.15f;

        c.integralLimitXYZ = new Vector3(0.8f, 0.6f, 0.8f);
        c.maxTiltDeg = 22.0f;

        // Current MIMISK Unity drone defaults. Edit in Inspector if your Rigidbody/model differs.
        c.useRigidbodyMass = true;
        c.massKg = rb != null ? rb.mass : 4.0f;
        c.gravity = 9.80665f;

        c.armX_M = 0.58f;
        c.armZ_M = 0.50f;
        c.maxThrustPerRotorN = 18.0f;
        c.motorTimeConstantS = 0.12f;
        c.yawTorqueCoeffNmPerN = 0.010f;

        c.rotorSpinSigns = new Vector4(1.0f, -1.0f, -1.0f, 1.0f);

        c.attitudeKpNmPerRad = new Vector3(8.0f, 1.2f, 8.0f);
        c.rateKdNmPerRadS = new Vector3(4.6f, 0.8f, 4.6f);
        c.torqueLimitNm = new Vector3(8.0f, 0.36f, 8.0f);

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

        c.configureRigidbodyOnStart = true;
        c.forceGravityOn = true;
        c.forceNonKinematic = true;

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

        Debug.Log("[MIMISK] Nominal Path PID rotor controller configured for " + source + ". Press N to start, B to abort.");
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
