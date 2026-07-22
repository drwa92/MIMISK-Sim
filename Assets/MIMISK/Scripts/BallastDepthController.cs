using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BallastDepthController : MonoBehaviour
{
    [Header("Enable")]
    public bool enableDepthHold = true;

    [Header("References")]
    public UnityVirtualESP32 virtualESP32;
    public SimpleROVBuoyancy buoyancy;

    [Header("Depth Target")]
    public float targetDepthMeters = 5.0f;
    public float waterLevel = 0.0f;
    public float depthDeadbandMeters = 0.03f;

    [Header("PID Controller")]
    [Tooltip("Main depth correction. Positive error means target is deeper.")]
    public float kp = 0.08f;

    [Tooltip("Usually keep small or zero at first.")]
    public float ki = 0.00f;

    [Tooltip("Damps vertical speed.")]
    public float kd = 0.04f;

    public float integralLimit = 2.0f;

    [Header("Ballast Model")]
    [Tooltip("Maximum ballast buoyancy trim in Newtons.")]
    public float maxTrimBuoyancyN = 0.12f;

    [Range(0, 500)]
    public int neutralBallastRaw = 250;

    [Range(0, 500)]
    public int initialBallastRaw = 250;

    [Tooltip("How fast the simulated syringe ballast can move.")]
    public float ballastRawRatePerSecond = 80.0f;

    [Header("Keyboard Target Control")]
    public bool enableKeyboardTargetControl = true;
    public KeyCode increaseDepthKey = KeyCode.PageDown;
    public KeyCode decreaseDepthKey = KeyCode.PageUp;
    public float targetDepthStepMeters = 0.10f;

    [Header("Debug")]
    public float measuredDepthMeters;
    public float depthErrorMeters;
    public float depthRateMetersPerSecond;
    public float desiredTrimBuoyancyN;
    public float appliedTrimBuoyancyN;
    public int ballastPortRaw;
    public int ballastStarboardRaw;

    private Rigidbody rb;
    private float lastDepthMeters;
    private float integral;
    private bool hasLastDepth;

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

        if (virtualESP32 != null)
        {
            measuredDepthMeters = virtualESP32.depthMeters;
        }
        else
        {
            measuredDepthMeters = Mathf.Max(0.0f, waterLevel - transform.position.y);
        }

        lastDepthMeters = measuredDepthMeters;
        hasLastDepth = true;
    }

    private void Update()
    {
        if (!enableKeyboardTargetControl)
        {
            return;
        }

        if (Input.GetKeyDown(increaseDepthKey))
        {
            targetDepthMeters += targetDepthStepMeters;
        }

        if (Input.GetKeyDown(decreaseDepthKey))
        {
            targetDepthMeters = Mathf.Max(0.0f, targetDepthMeters - targetDepthStepMeters);
        }
    }

    private void FixedUpdate()
    {
        UpdateMeasuredDepth();

        if (!enableDepthHold)
        {
            ApplyTrim(0.0f);
            ReturnBallastToNeutral();
            return;
        }

        RunDepthController();
    }

    private void UpdateMeasuredDepth()
    {
        if (virtualESP32 != null)
        {
            measuredDepthMeters = virtualESP32.depthMeters;
        }
        else
        {
            measuredDepthMeters = Mathf.Max(0.0f, waterLevel - transform.position.y);
        }

        if (hasLastDepth && Time.fixedDeltaTime > 0.0001f)
        {
            depthRateMetersPerSecond =
                (measuredDepthMeters - lastDepthMeters) / Time.fixedDeltaTime;
        }
        else
        {
            depthRateMetersPerSecond = 0.0f;
            hasLastDepth = true;
        }

        lastDepthMeters = measuredDepthMeters;
    }

    private void RunDepthController()
    {
        depthErrorMeters = targetDepthMeters - measuredDepthMeters;

        float errorForIntegral = depthErrorMeters;

        if (Mathf.Abs(depthErrorMeters) < depthDeadbandMeters &&
            Mathf.Abs(depthRateMetersPerSecond) < 0.02f)
        {
            depthErrorMeters = 0.0f;
            errorForIntegral = 0.0f;
        }

        integral += errorForIntegral * Time.fixedDeltaTime;
        integral = Mathf.Clamp(integral, -integralLimit, integralLimit);

        // Convention:
        // positive trim = more upward buoyancy
        // negative trim = heavier / go down
        //
        // If target depth is deeper, error is positive,
        // so controller must create negative trim.
        desiredTrimBuoyancyN =
            -kp * depthErrorMeters
            -ki * integral
            +kd * depthRateMetersPerSecond;

        desiredTrimBuoyancyN = Mathf.Clamp(
            desiredTrimBuoyancyN,
            -maxTrimBuoyancyN,
            maxTrimBuoyancyN
        );

        MoveBallastTowardTrim(desiredTrimBuoyancyN);
    }

    private void MoveBallastTowardTrim(float trimN)
    {
        float desiredCentered = 0.0f;

        if (maxTrimBuoyancyN > 0.0001f)
        {
            // raw > neutral means heavier/down.
            // positive trim means lighter/up.
            desiredCentered = -trimN / maxTrimBuoyancyN;
        }

        desiredCentered = Mathf.Clamp(desiredCentered, -1.0f, 1.0f);

        float desiredRaw = neutralBallastRaw + desiredCentered * 250.0f;
        desiredRaw = Mathf.Clamp(desiredRaw, 0.0f, 500.0f);

        ballastPortRaw = Mathf.RoundToInt(
            Mathf.MoveTowards(
                ballastPortRaw,
                desiredRaw,
                ballastRawRatePerSecond * Time.fixedDeltaTime
            )
        );

        ballastStarboardRaw = Mathf.RoundToInt(
            Mathf.MoveTowards(
                ballastStarboardRaw,
                desiredRaw,
                ballastRawRatePerSecond * Time.fixedDeltaTime
            )
        );

        float actualAverageRaw = 0.5f * (ballastPortRaw + ballastStarboardRaw);
        float actualCentered = (actualAverageRaw - neutralBallastRaw) / 250.0f;
        actualCentered = Mathf.Clamp(actualCentered, -1.0f, 1.0f);

        appliedTrimBuoyancyN = -actualCentered * maxTrimBuoyancyN;

        ApplyTrim(appliedTrimBuoyancyN);
        PushBallastToVirtualESP32();
    }

    private void ReturnBallastToNeutral()
    {
        ballastPortRaw = Mathf.RoundToInt(
            Mathf.MoveTowards(
                ballastPortRaw,
                neutralBallastRaw,
                ballastRawRatePerSecond * Time.fixedDeltaTime
            )
        );

        ballastStarboardRaw = Mathf.RoundToInt(
            Mathf.MoveTowards(
                ballastStarboardRaw,
                neutralBallastRaw,
                ballastRawRatePerSecond * Time.fixedDeltaTime
            )
        );

        PushBallastToVirtualESP32();
    }

    private void ApplyTrim(float trim)
    {
        appliedTrimBuoyancyN = trim;

        if (buoyancy != null)
        {
            buoyancy.trimBuoyancyN = trim;
        }
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
}
