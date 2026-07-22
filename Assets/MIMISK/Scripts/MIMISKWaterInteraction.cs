using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MIMISKWaterInteraction : MonoBehaviour
{
    public enum BallastControlMode
    {
        PhysicalPositionStateful,
        MomentaryCommandWithAutoReturn
    }

    [Header("References")]
    public UnityVirtualESP32 virtualESP32;
    public SimpleROVBuoyancy buoyancy;

    [Header("Ballast Simulation")]
    public bool enableBallast = true;

    [Tooltip("For manual gamepad testing use MomentaryCommandWithAutoReturn. For physical syringe simulation use PhysicalPositionStateful.")]
    public BallastControlMode ballastMode = BallastControlMode.MomentaryCommandWithAutoReturn;

    [Tooltip("Raw potentiometer value change per second at full PWM. Raw range is 0..500.")]
    public float ballastRawRatePerSecondAtFullPwm = 80.0f;

    [Tooltip("How fast the simulated ballast raw value returns to neutral after command release in momentary mode.")]
    public float ballastAutoReturnRawPerSecond = 120.0f;

    [Tooltip("Neutral ballast position.")]
    [Range(0, 500)] public int neutralBallastRaw = 250;

    [Tooltip("Initial ballast raw value at Play start.")]
    [Range(0, 500)] public int initialBallastRaw = 250;

    [Tooltip("Maximum persistent buoyancy trim in physical-position mode.")]
    public float maxBallastTrimBuoyancyN = 0.12f;

    [Tooltip("Maximum temporary buoyancy trim while pressing up/down command.")]
    public float maxMomentaryTrimBuoyancyN = 0.12f;

    [Tooltip("Ignore very small DC commands around zero.")]
    [Range(0, 255)] public int dcDeadzone = 10;

    [Tooltip("If true: positive DC command means filling ballast, therefore heavier/down.")]
    public bool positiveDcCommandIncreasesBallast = true;

    [Range(0, 500)] public int ballastPortRaw = 250;
    [Range(0, 500)] public int ballastStarboardRaw = 250;

    [Header("Ballast Differential Torque")]
    public bool enableDifferentialBallastTorque = false;
    public Vector3 differentialTorqueAxisLocal = Vector3.right;
    public float maxDifferentialTorqueNm = 0.01f;

    [Header("Small Water Current")]
    public bool enableCurrent = false;
    public Vector3 currentVelocityWorld = new Vector3(0.02f, 0.0f, 0.0f);
    public float currentDragCoefficient = 0.20f;

    [Header("Tether Drag / Constraint")]
    public bool enableTether = false;
    public Vector3 tetherAnchorWorld = new Vector3(0.0f, 0.0f, 0.0f);
    public float tetherSlackLengthM = 6.0f;
    public float tetherSpringNPerM = 0.15f;
    public float tetherDampingNPerMps = 0.08f;
    public float tetherVelocityDrag = 0.05f;
    public float tetherNegativeBuoyancyN = 0.0f;

    [Header("Debug")]
    public int dcPortCommand;
    public int dcStarboardCommand;
    public float ballastAverageRaw;
    public float ballastCentered01;
    public float dcAverageCommand01;
    public float ballastTrimBuoyancyN;
    public Vector3 appliedCurrentForce;
    public Vector3 appliedTetherForce;
    public float tetherStretchM;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (virtualESP32 == null)
        {
            virtualESP32 = GetComponent<UnityVirtualESP32>();
        }

        if (buoyancy == null)
        {
            buoyancy = GetComponent<SimpleROVBuoyancy>();
        }
    }

    private void Start()
    {
        ballastPortRaw = Mathf.Clamp(initialBallastRaw, 0, 500);
        ballastStarboardRaw = Mathf.Clamp(initialBallastRaw, 0, 500);

        PushBallastToVirtualESP32();

        if (buoyancy != null)
        {
            buoyancy.trimBuoyancyN = 0.0f;
        }
    }

    private void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        UpdateBallast();
        ApplyCurrent();
        ApplyTether();
    }

    private void UpdateBallast()
    {
        if (!enableBallast)
        {
            SetBuoyancyTrim(0.0f);
            return;
        }

        if (virtualESP32 != null)
        {
            dcPortCommand = ApplyDcDeadzone(virtualESP32.lastDcPort);
            dcStarboardCommand = ApplyDcDeadzone(virtualESP32.lastDcStarboard);
        }
        else
        {
            dcPortCommand = 0;
            dcStarboardCommand = 0;
        }

        if (ballastMode == BallastControlMode.PhysicalPositionStateful)
        {
            UpdatePhysicalPositionBallast();
        }
        else
        {
            UpdateMomentaryCommandBallast();
        }

        PushBallastToVirtualESP32();

        if (enableDifferentialBallastTorque)
        {
            float differential = (ballastPortRaw - ballastStarboardRaw) / 500.0f;
            Vector3 worldAxis = transform.TransformDirection(differentialTorqueAxisLocal.normalized);
            rb.AddTorque(worldAxis * differential * maxDifferentialTorqueNm, ForceMode.Force);
        }
    }

    private void UpdatePhysicalPositionBallast()
    {
        float sign = positiveDcCommandIncreasesBallast ? 1.0f : -1.0f;

        ballastPortRaw = UpdateRawValue(ballastPortRaw, dcPortCommand, sign);
        ballastStarboardRaw = UpdateRawValue(ballastStarboardRaw, dcStarboardCommand, sign);

        ballastAverageRaw = 0.5f * (ballastPortRaw + ballastStarboardRaw);

        float maxPositiveDistance = Mathf.Max(1.0f, 500.0f - neutralBallastRaw);
        float maxNegativeDistance = Mathf.Max(1.0f, neutralBallastRaw);

        if (ballastAverageRaw >= neutralBallastRaw)
        {
            ballastCentered01 = (ballastAverageRaw - neutralBallastRaw) / maxPositiveDistance;
        }
        else
        {
            ballastCentered01 = (ballastAverageRaw - neutralBallastRaw) / maxNegativeDistance;
        }

        ballastCentered01 = Mathf.Clamp(ballastCentered01, -1.0f, 1.0f);

        // raw > neutral means more water in syringes -> heavier -> negative buoyancy.
        // raw < neutral means less water in syringes -> lighter -> positive buoyancy.
        ballastTrimBuoyancyN = -ballastCentered01 * maxBallastTrimBuoyancyN;

        SetBuoyancyTrim(ballastTrimBuoyancyN);
    }

    private void UpdateMomentaryCommandBallast()
    {
        float leftCommand01 = Mathf.Clamp(dcPortCommand / 255.0f, -1.0f, 1.0f);
        float rightCommand01 = Mathf.Clamp(dcStarboardCommand / 255.0f, -1.0f, 1.0f);

        dcAverageCommand01 = 0.5f * (leftCommand01 + rightCommand01);

        float sign = positiveDcCommandIncreasesBallast ? 1.0f : -1.0f;

        // Positive DC command means "down/heavier", so trim must be negative.
        ballastTrimBuoyancyN = -sign * dcAverageCommand01 * maxMomentaryTrimBuoyancyN;

        SetBuoyancyTrim(ballastTrimBuoyancyN);

        // Potentiometer-like display behavior:
        // while command is active, move raw value away from neutral;
        // when released, return raw values to neutral.
        if (dcPortCommand != 0)
        {
            ballastPortRaw = UpdateRawValue(ballastPortRaw, dcPortCommand, sign);
        }
        else
        {
            ballastPortRaw = MoveRawTowardNeutral(ballastPortRaw);
        }

        if (dcStarboardCommand != 0)
        {
            ballastStarboardRaw = UpdateRawValue(ballastStarboardRaw, dcStarboardCommand, sign);
        }
        else
        {
            ballastStarboardRaw = MoveRawTowardNeutral(ballastStarboardRaw);
        }

        ballastAverageRaw = 0.5f * (ballastPortRaw + ballastStarboardRaw);

        float denom = Mathf.Max(1.0f, neutralBallastRaw);
        ballastCentered01 = Mathf.Clamp((ballastAverageRaw - neutralBallastRaw) / denom, -1.0f, 1.0f);
    }

    private int ApplyDcDeadzone(int value)
    {
        if (Mathf.Abs(value) <= dcDeadzone)
        {
            return 0;
        }

        return Mathf.Clamp(value, -255, 255);
    }

    private int UpdateRawValue(int currentRaw, int dcCommand, float sign)
    {
        float command01 = Mathf.Clamp(dcCommand / 255.0f, -1.0f, 1.0f);
        float delta = sign * command01 * ballastRawRatePerSecondAtFullPwm * Time.fixedDeltaTime;
        float next = Mathf.Clamp(currentRaw + delta, 0.0f, 500.0f);
        return Mathf.RoundToInt(next);
    }

    private int MoveRawTowardNeutral(int currentRaw)
    {
        float next = Mathf.MoveTowards(
            currentRaw,
            neutralBallastRaw,
            ballastAutoReturnRawPerSecond * Time.fixedDeltaTime
        );

        return Mathf.RoundToInt(next);
    }

    private void PushBallastToVirtualESP32()
    {
        if (virtualESP32 == null)
        {
            return;
        }

        virtualESP32.ballastPortRaw = ballastPortRaw;
        virtualESP32.ballastStarboardRaw = ballastStarboardRaw;
    }

    private void SetBuoyancyTrim(float trim)
    {
        if (buoyancy == null)
        {
            return;
        }

        buoyancy.trimBuoyancyN = trim;
    }

    private void ApplyCurrent()
    {
        appliedCurrentForce = Vector3.zero;

        if (!enableCurrent)
        {
            return;
        }

        Vector3 relativeVelocity = currentVelocityWorld - rb.linearVelocity;
        appliedCurrentForce = relativeVelocity * currentDragCoefficient;
        rb.AddForce(appliedCurrentForce, ForceMode.Force);
    }

    private void ApplyTether()
    {
        appliedTetherForce = Vector3.zero;
        tetherStretchM = 0.0f;

        if (!enableTether)
        {
            return;
        }

        Vector3 toAnchor = tetherAnchorWorld - transform.position;
        float distance = toAnchor.magnitude;

        if (distance < 0.0001f)
        {
            return;
        }

        Vector3 directionToAnchor = toAnchor / distance;

        if (distance > tetherSlackLengthM)
        {
            tetherStretchM = distance - tetherSlackLengthM;

            float velocityAlongTether = Vector3.Dot(rb.linearVelocity, directionToAnchor);
            float springForce = tetherStretchM * tetherSpringNPerM;
            float dampingForce = -velocityAlongTether * tetherDampingNPerMps;

            appliedTetherForce += directionToAnchor * (springForce + dampingForce);
        }

        appliedTetherForce += -rb.linearVelocity * tetherVelocityDrag;

        if (tetherNegativeBuoyancyN > 0.0f)
        {
            appliedTetherForce += Vector3.down * tetherNegativeBuoyancyN;
        }

        rb.AddForce(appliedTetherForce, ForceMode.Force);
    }

    private void OnDrawGizmosSelected()
    {
        if (!enableTether)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(tetherAnchorWorld, 0.1f);
        Gizmos.DrawLine(transform.position, tetherAnchorWorld);
    }
}
