using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MiniROVFeedbackController : MonoBehaviour
{
    [Header("Mode")]
    public bool enableDepthHold = false;
    public bool enableHeadingHold = false;

    [Header("References")]
    public SimpleROVBuoyancy buoyancy;
    public Transform leftThruster;
    public Transform rightThruster;

    [Header("Depth Control")]
    public float waterLevel = 0.0f;
    public float targetDepthMeters = 5.0f;
    public float depthDeadbandMeters = 0.03f;

    public float depthKp = 0.08f;
    public float depthKd = 0.04f;

    [Tooltip("Maximum ballast trim force in Newtons.")]
    public float maxDepthTrimN = 0.12f;

    [Header("Heading / Yaw Control")]
    public float targetHeadingDeg = 0.0f;
    public float headingDeadbandDeg = 2.0f;

    public float headingKp = 0.015f;
    public float headingKd = 0.020f;

    [Tooltip("Maximum differential thruster force in Newtons.")]
    public float maxYawDifferentialForceN = 0.08f;

    [Tooltip("Enable if yaw correction is reversed.")]
    public bool invertYawOutput = false;

    [Header("Debug")]
    public float measuredDepthMeters;
    public float depthErrorMeters;
    public float depthRateMetersPerSecond;
    public float appliedDepthTrimN;

    public float measuredHeadingDeg;
    public float headingErrorDeg;
    public float yawRateRadPerSecond;
    public float appliedYawDifferentialForceN;

    private Rigidbody rb;
    private float lastDepthMeters;
    private bool hasLastDepth;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (buoyancy == null)
        {
            buoyancy = GetComponent<SimpleROVBuoyancy>();
        }

        if (leftThruster == null)
        {
            Transform t = transform.Find("propulseur_gauche");
            if (t != null) leftThruster = t;
        }

        if (rightThruster == null)
        {
            Transform t = transform.Find("propulseur_droite");
            if (t != null) rightThruster = t;
        }
    }

    private void Start()
    {
        measuredDepthMeters = GetDepthMeters();
        lastDepthMeters = measuredDepthMeters;
        hasLastDepth = true;

        measuredHeadingDeg = GetHeadingDeg();
        targetHeadingDeg = measuredHeadingDeg;
        targetDepthMeters = measuredDepthMeters;
    }

    private void FixedUpdate()
    {
        UpdateMeasurements();

        if (enableDepthHold)
        {
            RunDepthHold();
        }
        else
        {
            appliedDepthTrimN = 0.0f;
        }

        if (enableHeadingHold)
        {
            RunHeadingHold();
        }
        else
        {
            appliedYawDifferentialForceN = 0.0f;
        }
    }

    public void SetCurrentDepthAsTarget()
    {
        targetDepthMeters = GetDepthMeters();
    }

    public void SetCurrentHeadingAsTarget()
    {
        targetHeadingDeg = GetHeadingDeg();
    }

    private void UpdateMeasurements()
    {
        measuredDepthMeters = GetDepthMeters();

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

        measuredHeadingDeg = GetHeadingDeg();
        yawRateRadPerSecond = rb.angularVelocity.y;
    }

    private float GetDepthMeters()
    {
        return Mathf.Max(0.0f, waterLevel - transform.position.y);
    }

    private float GetHeadingDeg()
    {
        // 0 deg = Unity +Z direction, positive toward +X.
        float yawRad = Mathf.Atan2(transform.forward.x, transform.forward.z);
        float yawDeg = yawRad * Mathf.Rad2Deg;

        if (yawDeg < 0.0f)
        {
            yawDeg += 360.0f;
        }

        return yawDeg;
    }

    private void RunDepthHold()
    {
        depthErrorMeters = targetDepthMeters - measuredDepthMeters;

        if (Mathf.Abs(depthErrorMeters) < depthDeadbandMeters &&
            Mathf.Abs(depthRateMetersPerSecond) < 0.02f)
        {
            depthErrorMeters = 0.0f;
        }

        // Positive depth error means target is deeper.
        // To go deeper, trim must become negative.
        float trim =
            -depthKp * depthErrorMeters
            +depthKd * depthRateMetersPerSecond;

        trim = Mathf.Clamp(trim, -maxDepthTrimN, maxDepthTrimN);

        appliedDepthTrimN = trim;

        if (buoyancy != null)
        {
            buoyancy.trimBuoyancyN = trim;
        }
    }

    private void RunHeadingHold()
    {
        headingErrorDeg = Mathf.DeltaAngle(measuredHeadingDeg, targetHeadingDeg);

        if (Mathf.Abs(headingErrorDeg) < headingDeadbandDeg &&
            Mathf.Abs(yawRateRadPerSecond) < 0.02f)
        {
            headingErrorDeg = 0.0f;
        }

        float yawCmd =
            headingKp * headingErrorDeg
            -headingKd * yawRateRadPerSecond * Mathf.Rad2Deg;

        yawCmd = Mathf.Clamp(
            yawCmd,
            -maxYawDifferentialForceN,
            maxYawDifferentialForceN
        );

        if (invertYawOutput)
        {
            yawCmd = -yawCmd;
        }

        appliedYawDifferentialForceN = yawCmd;

        ApplyDifferentialThrusterForce(yawCmd);
    }

    private void ApplyDifferentialThrusterForce(float differentialForce)
    {
        if (leftThruster == null || rightThruster == null)
        {
            return;
        }

        Vector3 forward = transform.forward;

        // Differential thrust only for yaw:
        // one side pushes forward, the other backward.
        rb.AddForceAtPosition(forward * differentialForce, leftThruster.position, ForceMode.Force);
        rb.AddForceAtPosition(-forward * differentialForce, rightThruster.position, ForceMode.Force);
    }
}
