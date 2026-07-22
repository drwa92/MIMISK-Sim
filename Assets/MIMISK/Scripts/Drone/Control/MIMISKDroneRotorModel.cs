using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MIMISKDroneRotorModel : MonoBehaviour
{
    [System.Serializable]
    public class Rotor
    {
        public string name;
        public Transform rotorTransform;

        [Header("Rotor Physics")]
        [Tooltip("Local thrust direction of the drone root. Usually +Y.")]
        public Vector3 localThrustAxis = Vector3.up;

        [Tooltip("Visual spin axis of the propeller transform. Drone V1 uses local Z.")]
        public Vector3 localSpinAxis = Vector3.forward;

        [Tooltip("+1 or -1 for yaw reaction torque direction.")]
        public float yawTorqueSign = 1.0f;

        [Header("Runtime")]
        [Range(0f, 1f)] public float targetOutput;
        [Range(0f, 1f)] public float currentOutput;
        public float currentRpm;
    }

    [Header("Rotor References")]
    public Rotor motor1_RR;
    public Rotor motor2_FR;
    public Rotor motor3_RL;
    public Rotor motor4_FL;

    [Header("Motor / Propeller Model")]
    public float maxThrustPerRotorN = 18.0f;
    public float yawTorqueCoefficient = 0.18f;

    [Tooltip("How quickly motor output follows target output.")]
    public float motorResponseRate = 8.0f;

    [Header("Visual RPM")]
    public float idleRpm = 500.0f;
    public float maxRpm = 5200.0f;

    [Header("Debug")]
    public float totalThrustN;
    public Vector4 motorOutputs;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        totalThrustN = 0.0f;

        ApplyRotor(motor1_RR);
        ApplyRotor(motor2_FR);
        ApplyRotor(motor3_RL);
        ApplyRotor(motor4_FL);

        motorOutputs = new Vector4(
            motor1_RR.currentOutput,
            motor2_FR.currentOutput,
            motor3_RL.currentOutput,
            motor4_FL.currentOutput
        );
    }

    private void Update()
    {
        AnimateRotor(motor1_RR);
        AnimateRotor(motor2_FR);
        AnimateRotor(motor3_RL);
        AnimateRotor(motor4_FL);
    }

    public void SetOutputs(float m1RR, float m2FR, float m3RL, float m4FL)
    {
        motor1_RR.targetOutput = Mathf.Clamp01(m1RR);
        motor2_FR.targetOutput = Mathf.Clamp01(m2FR);
        motor3_RL.targetOutput = Mathf.Clamp01(m3RL);
        motor4_FL.targetOutput = Mathf.Clamp01(m4FL);
    }

    public void StopMotors()
    {
        SetOutputs(0f, 0f, 0f, 0f);
    }

    public void SetIdle(float idleOutput)
    {
        idleOutput = Mathf.Clamp01(idleOutput);
        SetOutputs(idleOutput, idleOutput, idleOutput, idleOutput);
    }

    private void ApplyRotor(Rotor rotor)
    {
        if (rotor == null || rotor.rotorTransform == null)
        {
            return;
        }

        rotor.currentOutput = Mathf.MoveTowards(
            rotor.currentOutput,
            rotor.targetOutput,
            motorResponseRate * Time.fixedDeltaTime
        );

        Vector3 thrustAxisWorld = transform.TransformDirection(
            rotor.localThrustAxis.sqrMagnitude > 0.0001f
                ? rotor.localThrustAxis.normalized
                : Vector3.up
        );

        float thrustN = rotor.currentOutput * maxThrustPerRotorN;
        totalThrustN += thrustN;

        rb.AddForceAtPosition(
            thrustAxisWorld * thrustN,
            rotor.rotorTransform.position,
            ForceMode.Force
        );

        // Reaction torque for yaw.
        rb.AddTorque(
            transform.up * rotor.yawTorqueSign * rotor.currentOutput * yawTorqueCoefficient,
            ForceMode.Force
        );
    }

    private void AnimateRotor(Rotor rotor)
    {
        if (rotor == null || rotor.rotorTransform == null)
        {
            return;
        }

        rotor.currentRpm = Mathf.Lerp(idleRpm, maxRpm, rotor.currentOutput);

        if (rotor.currentOutput < 0.001f)
        {
            rotor.currentRpm = Mathf.MoveTowards(rotor.currentRpm, 0f, 3000f * Time.deltaTime);
        }

        Vector3 axis = rotor.localSpinAxis.sqrMagnitude > 0.0001f
            ? rotor.localSpinAxis.normalized
            : Vector3.forward;

        float direction = rotor.yawTorqueSign >= 0f ? 1f : -1f;
        float degPerSec = rotor.currentRpm * 360.0f / 60.0f;

        rotor.rotorTransform.Rotate(
            axis,
            degPerSec * direction * Time.deltaTime,
            Space.Self
        );
    }
}
