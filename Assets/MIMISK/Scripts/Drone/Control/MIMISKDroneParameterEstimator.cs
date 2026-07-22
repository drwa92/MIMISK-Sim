using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MIMISKDroneParameterEstimator : MonoBehaviour
{
    [Header("References")]
    public MIMISKDroneRotorModel rotorModel;

    [Header("Design Targets")]
    public float altitudeNaturalFrequency = 1.2f;
    public float altitudeDampingRatio = 0.9f;

    public float attitudeNaturalFrequency = 3.0f;
    public float attitudeDampingRatio = 0.8f;

    [Header("Estimated Physical Parameters")]
    public float massKg;
    public float weightN;
    public float totalMaxThrustN;
    public float hoverOutput;

    public Vector3 inertiaTensor;
    public Vector3 centerOfMassLocal;

    public float averageRotorArmX;
    public float averageRotorArmZ;

    public float rollTorqueAuthorityPerOutput;
    public float pitchTorqueAuthorityPerOutput;
    public float yawTorqueAuthorityPerOutput;

    [Header("Recommended Altitude Gains")]
    public float recommendedAltitudeKp;
    public float recommendedAltitudeKd;

    [Header("Recommended Attitude Gains")]
    public float recommendedRollKp;
    public float recommendedRollKd;
    public float recommendedPitchKp;
    public float recommendedPitchKd;
    public float recommendedYawRateKp;

    [Header("Debug")]
    public bool printReportOnStart = true;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rotorModel == null)
        {
            rotorModel = GetComponent<MIMISKDroneRotorModel>();
        }
    }

    private void Start()
    {
        Estimate();

        if (printReportOnStart)
        {
            PrintReport();
        }
    }

    [ContextMenu("Estimate Drone Parameters")]
    public void Estimate()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rotorModel == null)
        {
            rotorModel = GetComponent<MIMISKDroneRotorModel>();
        }

        massKg = rb.mass;
        weightN = massKg * Mathf.Abs(Physics.gravity.y);
        centerOfMassLocal = rb.centerOfMass;
        inertiaTensor = rb.inertiaTensor;

        if (rotorModel == null)
        {
            Debug.LogWarning("[MIMISKDroneParameterEstimator] Rotor model missing.");
            return;
        }

        totalMaxThrustN = 4.0f * rotorModel.maxThrustPerRotorN;

        if (totalMaxThrustN > 0.001f)
        {
            hoverOutput = weightN / totalMaxThrustN;
        }
        else
        {
            hoverOutput = 0.5f;
        }

        Transform[] rotors =
        {
            rotorModel.motor1_RR != null ? rotorModel.motor1_RR.rotorTransform : null,
            rotorModel.motor2_FR != null ? rotorModel.motor2_FR.rotorTransform : null,
            rotorModel.motor3_RL != null ? rotorModel.motor3_RL.rotorTransform : null,
            rotorModel.motor4_FL != null ? rotorModel.motor4_FL.rotorTransform : null
        };

        float sumAbsX = 0f;
        float sumAbsZ = 0f;
        int count = 0;

        foreach (Transform rotor in rotors)
        {
            if (rotor == null)
            {
                continue;
            }

            Vector3 local = transform.InverseTransformPoint(rotor.position) - centerOfMassLocal;

            sumAbsX += Mathf.Abs(local.x);
            sumAbsZ += Mathf.Abs(local.z);
            count++;
        }

        if (count > 0)
        {
            averageRotorArmX = sumAbsX / count;
            averageRotorArmZ = sumAbsZ / count;
        }

        // Approximate authority for one unit of normalized mixer correction.
        rollTorqueAuthorityPerOutput =
            Mathf.Max(0.001f, 4.0f * rotorModel.maxThrustPerRotorN * averageRotorArmX);

        pitchTorqueAuthorityPerOutput =
            Mathf.Max(0.001f, 4.0f * rotorModel.maxThrustPerRotorN * averageRotorArmZ);

        yawTorqueAuthorityPerOutput =
            Mathf.Max(0.001f, 4.0f * rotorModel.yawTorqueCoefficient);

        // Altitude controller:
        // collectiveOutput = hover + Kp*error - Kd*verticalVelocity
        // thrustChange = totalMaxThrust * collectiveCorrection
        recommendedAltitudeKp =
            massKg * altitudeNaturalFrequency * altitudeNaturalFrequency / totalMaxThrustN;

        recommendedAltitudeKd =
            2.0f * altitudeDampingRatio * altitudeNaturalFrequency * massKg / totalMaxThrustN;

        // Attitude controller gains are in correction per degree.
        // torque = authority * gain * errorDeg
        float degPerRad = Mathf.Rad2Deg;

        float ix = Mathf.Max(0.0001f, inertiaTensor.x);
        float iz = Mathf.Max(0.0001f, inertiaTensor.z);
        float iy = Mathf.Max(0.0001f, inertiaTensor.y);

        recommendedPitchKp =
            ix * attitudeNaturalFrequency * attitudeNaturalFrequency /
            (pitchTorqueAuthorityPerOutput * degPerRad);

        recommendedPitchKd =
            2.0f * attitudeDampingRatio * attitudeNaturalFrequency * ix /
            (pitchTorqueAuthorityPerOutput * degPerRad);

        recommendedRollKp =
            iz * attitudeNaturalFrequency * attitudeNaturalFrequency /
            (rollTorqueAuthorityPerOutput * degPerRad);

        recommendedRollKd =
            2.0f * attitudeDampingRatio * attitudeNaturalFrequency * iz /
            (rollTorqueAuthorityPerOutput * degPerRad);

        recommendedYawRateKp =
            iy * attitudeNaturalFrequency /
            (yawTorqueAuthorityPerOutput * degPerRad);
    }

    [ContextMenu("Apply Recommended Gains To Autopilot")]
    public void ApplyRecommendedGains()
    {
        Estimate();

        MIMISKDroneAutopilot autopilot = GetComponent<MIMISKDroneAutopilot>();

        if (autopilot == null)
        {
            Debug.LogWarning("[MIMISKDroneParameterEstimator] MIMISKDroneAutopilot missing.");
            return;
        }

        autopilot.altitudeKp = recommendedAltitudeKp;
        autopilot.altitudeKd = recommendedAltitudeKd;

        autopilot.rollKp = recommendedRollKp;
        autopilot.rollKd = recommendedRollKd;

        autopilot.pitchKp = recommendedPitchKp;
        autopilot.pitchKd = recommendedPitchKd;

        autopilot.yawRateKp = recommendedYawRateKp;

        Debug.Log("[MIMISKDroneParameterEstimator] Applied recommended gains to MIMISKDroneAutopilot.");
    }

    [ContextMenu("Print Parameter Report")]
    public void PrintReport()
    {
        Estimate();

        Debug.Log(
            "[MIMISK Drone Parameter Report]\n" +
            "Mass kg: " + massKg.ToString("F3") + "\n" +
            "Weight N: " + weightN.ToString("F3") + "\n" +
            "Total max thrust N: " + totalMaxThrustN.ToString("F3") + "\n" +
            "Hover output: " + hoverOutput.ToString("F3") + "\n" +
            "Inertia tensor: " + inertiaTensor.ToString("F4") + "\n" +
            "COM local: " + centerOfMassLocal.ToString("F4") + "\n" +
            "Average rotor arm X: " + averageRotorArmX.ToString("F3") + "\n" +
            "Average rotor arm Z: " + averageRotorArmZ.ToString("F3") + "\n" +
            "Roll authority: " + rollTorqueAuthorityPerOutput.ToString("F3") + "\n" +
            "Pitch authority: " + pitchTorqueAuthorityPerOutput.ToString("F3") + "\n" +
            "Yaw authority: " + yawTorqueAuthorityPerOutput.ToString("F3") + "\n" +
            "Recommended altitude Kp/Kd: " +
            recommendedAltitudeKp.ToString("F4") + " / " +
            recommendedAltitudeKd.ToString("F4") + "\n" +
            "Recommended roll Kp/Kd: " +
            recommendedRollKp.ToString("F4") + " / " +
            recommendedRollKd.ToString("F4") + "\n" +
            "Recommended pitch Kp/Kd: " +
            recommendedPitchKp.ToString("F4") + " / " +
            recommendedPitchKd.ToString("F4") + "\n" +
            "Recommended yaw rate Kp: " +
            recommendedYawRateKp.ToString("F4")
        );
    }
}
