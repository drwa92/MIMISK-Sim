using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKDroneCoreTrajectoryPlanner : MonoBehaviour
{
    public enum TrajectoryType
    {
        Circle,
        SpiralOut,
        HelixDown,
        HelixUpDown,
        FigureEight,
        SmoothSquare,
        Lawnmower,
        DeploymentApproach
    }

    [System.Serializable]
    public struct TrajectorySample
    {
        public Vector3 positionWorld;
        public Vector3 velocityWorld;
        public Vector3 accelerationWorld;
        public float yawDeg;
        public bool completed;
    }

    [Header("Selection")]
    public TrajectoryType trajectoryType = TrajectoryType.Circle;

    [Tooltip("If ON, yaw follows the path velocity direction. If OFF, yaw remains at mission start yaw.")]
    public bool yawAlongPath = false;

    [Header("Circle")]
    public float circleRadiusM = 1.4f;
    public float circleOmegaRadS = 0.23f;
    public float circleDurationS = 40.0f;

    [Header("Spiral Out")]
    public float spiralInitialRadiusM = 0.25f;
    public float spiralFinalRadiusM = 1.25f;
    public float spiralOmegaRadS = 0.28f;
    public float spiralDurationS = 36.0f;
    public float spiralAltitudeRiseM = 0.35f;

    [Header("Helix Down")]
    public float helixRadiusM = 1.5f;
    public float helixOmegaRadS = 0.32f;
    public float helixDurationS = 30.0f;
    public float helixVerticalChangeM = -0.8f;

    [Header("Helix Up Down")]
    public float helixUpDownRadiusM = 1.5f;
    public float helixUpDownOmegaRadS = 0.28f;
    public float helixUpDownDurationS = 40.0f;
    public float helixUpDownVerticalAmplitudeM = 0.75f;

    [Header("Figure Eight")]
    public float figureEightDurationS = 32.0f;
    public float figureEightXAmplitudeM = 1.5f;
    public float figureEightZAmplitudeM = 0.75f;
    public float figureEightVerticalAmplitudeM = 0.0f;

    [Header("Smooth Square / Polyline")]
    public float squareSideM = 2.4f;
    public float squareSpeedMS = 0.32f;

    [Header("Lawnmower Search")]
    public float lawnmowerLengthM = 4.0f;
    public float lawnmowerWidthM = 2.5f;
    public int lawnmowerLanes = 4;
    public float lawnmowerSpeedMS = 0.35f;

    [Header("Deployment Approach")]
    public float deploymentForwardDistanceM = 3.0f;
    public float deploymentSpeedMS = 0.35f;
    public float deploymentFinalHoldSeconds = 3.0f;

    [Header("Runtime")]
    public Vector3 originWorld;
    public float originYawDeg;
    public bool initialized;

    public float lastPathTimeS;
    public TrajectorySample lastSample;

    public void Begin(Vector3 startWorld, float yawDeg)
    {
        originWorld = startWorld;
        originYawDeg = yawDeg;
        initialized = true;
        lastPathTimeS = 0.0f;
    }

    public float GetDuration()
    {
        switch (trajectoryType)
        {
            case TrajectoryType.Circle:
                return circleDurationS;

            case TrajectoryType.SpiralOut:
                return spiralDurationS;

            case TrajectoryType.HelixDown:
                return helixDurationS;

            case TrajectoryType.HelixUpDown:
                return helixUpDownDurationS;

            case TrajectoryType.FigureEight:
                return figureEightDurationS;

            case TrajectoryType.SmoothSquare:
                return 4.0f * squareSideM / Mathf.Max(0.05f, squareSpeedMS);

            case TrajectoryType.Lawnmower:
                return GetLawnmowerDuration();

            case TrajectoryType.DeploymentApproach:
                return deploymentForwardDistanceM / Mathf.Max(0.05f, deploymentSpeedMS) +
                       deploymentFinalHoldSeconds;
        }

        return circleDurationS;
    }

    public TrajectorySample Evaluate(float t)
    {
        if (!initialized)
        {
            Begin(transform.position, transform.eulerAngles.y);
        }

        lastPathTimeS = t;

        TrajectorySample sample;

        switch (trajectoryType)
        {
            case TrajectoryType.SpiralOut:
                sample = EvaluateSpiralOut(t);
                break;

            case TrajectoryType.HelixDown:
                sample = EvaluateHelixDown(t);
                break;

            case TrajectoryType.HelixUpDown:
                sample = EvaluateHelixUpDown(t);
                break;

            case TrajectoryType.FigureEight:
                sample = EvaluateFigureEight(t);
                break;

            case TrajectoryType.SmoothSquare:
                sample = EvaluateSmoothSquare(t);
                break;

            case TrajectoryType.Lawnmower:
                sample = EvaluateLawnmower(t);
                break;

            case TrajectoryType.DeploymentApproach:
                sample = EvaluateDeploymentApproach(t);
                break;

            default:
                sample = EvaluateCircle(t);
                break;
        }

        if (yawAlongPath &&
            sample.velocityWorld.sqrMagnitude > 0.0025f)
        {
            Vector3 v = sample.velocityWorld;
            v.y = 0.0f;

            if (v.sqrMagnitude > 0.0001f)
            {
                sample.yawDeg =
                    Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            }
        }

        lastSample = sample;
        return sample;
    }

    private TrajectorySample EvaluateCircle(float t)
    {
        float T = Mathf.Max(0.1f, circleDurationS);
        float tt = Mathf.Clamp(t, 0.0f, T);

        float R = circleRadiusM;
        float w = circleOmegaRadS;
        float wt = w * tt;

        Vector3 pLocal = new Vector3(
            R * (Mathf.Cos(wt) - 1.0f),
            0.0f,
            R * Mathf.Sin(wt)
        );

        Vector3 vLocal = new Vector3(
            -R * w * Mathf.Sin(wt),
            0.0f,
            R * w * Mathf.Cos(wt)
        );

        Vector3 aLocal = new Vector3(
            -R * w * w * Mathf.Cos(wt),
            0.0f,
            -R * w * w * Mathf.Sin(wt)
        );

        return MakeSample(pLocal, vLocal, aLocal, tt >= T);
    }

    private TrajectorySample EvaluateSpiralOut(float t)
    {
        float T = Mathf.Max(0.1f, spiralDurationS);
        float tt = Mathf.Clamp(t, 0.0f, T);
        float s = Mathf.Clamp01(tt / T);

        float r = Mathf.Lerp(spiralInitialRadiusM, spiralFinalRadiusM, s);
        float rDot = tt < T ? (spiralFinalRadiusM - spiralInitialRadiusM) / T : 0.0f;

        float w = spiralOmegaRadS;
        float th = w * tt;

        Vector3 pLocal = new Vector3(
            r * Mathf.Cos(th) - spiralInitialRadiusM,
            spiralAltitudeRiseM * s,
            r * Mathf.Sin(th)
        );

        Vector3 vLocal = new Vector3(
            rDot * Mathf.Cos(th) - r * w * Mathf.Sin(th),
            spiralAltitudeRiseM / T,
            rDot * Mathf.Sin(th) + r * w * Mathf.Cos(th)
        );

        Vector3 aLocal = new Vector3(
            -2.0f * rDot * w * Mathf.Sin(th) - r * w * w * Mathf.Cos(th),
            0.0f,
            2.0f * rDot * w * Mathf.Cos(th) - r * w * w * Mathf.Sin(th)
        );

        return MakeSample(pLocal, vLocal, aLocal, tt >= T);
    }

    private TrajectorySample EvaluateHelixDown(float t)
    {
        float T = Mathf.Max(0.1f, helixDurationS);
        float tt = Mathf.Clamp(t, 0.0f, T);

        float s = Mathf.Clamp01(tt / T);
        float R = helixRadiusM;
        float w = helixOmegaRadS;
        float wt = w * tt;

        float yRate = helixVerticalChangeM / T;

        Vector3 pLocal = new Vector3(
            R * (Mathf.Cos(wt) - 1.0f),
            helixVerticalChangeM * s,
            R * Mathf.Sin(wt)
        );

        Vector3 vLocal = new Vector3(
            -R * w * Mathf.Sin(wt),
            yRate,
            R * w * Mathf.Cos(wt)
        );

        Vector3 aLocal = new Vector3(
            -R * w * w * Mathf.Cos(wt),
            0.0f,
            -R * w * w * Mathf.Sin(wt)
        );

        return MakeSample(pLocal, vLocal, aLocal, tt >= T);
    }

    private TrajectorySample EvaluateHelixUpDown(float t)
    {
        float T = Mathf.Max(0.1f, helixUpDownDurationS);
        float tt = Mathf.Clamp(t, 0.0f, T);

        float R = helixUpDownRadiusM;
        float w = helixUpDownOmegaRadS;
        float wt = w * tt;

        // Starts at current altitude, descends to -amplitude at half duration,
        // then returns to the start altitude.
        float q = 2.0f * Mathf.PI * tt / T;
        float y = -0.5f * helixUpDownVerticalAmplitudeM * (1.0f - Mathf.Cos(q));
        float yDot = -0.5f * helixUpDownVerticalAmplitudeM * (2.0f * Mathf.PI / T) * Mathf.Sin(q);
        float yDDot = -0.5f * helixUpDownVerticalAmplitudeM *
                      (2.0f * Mathf.PI / T) *
                      (2.0f * Mathf.PI / T) *
                      Mathf.Cos(q);

        Vector3 pLocal = new Vector3(
            R * (Mathf.Cos(wt) - 1.0f),
            y,
            R * Mathf.Sin(wt)
        );

        Vector3 vLocal = new Vector3(
            -R * w * Mathf.Sin(wt),
            yDot,
            R * w * Mathf.Cos(wt)
        );

        Vector3 aLocal = new Vector3(
            -R * w * w * Mathf.Cos(wt),
            yDDot,
            -R * w * w * Mathf.Sin(wt)
        );

        return MakeSample(pLocal, vLocal, aLocal, tt >= T);
    }

    private TrajectorySample EvaluateFigureEight(float t)
    {
        float T = Mathf.Max(0.1f, figureEightDurationS);
        float tt = Mathf.Clamp(t, 0.0f, T);

        float w = 2.0f * Mathf.PI / T;

        float ax = figureEightXAmplitudeM;
        float az = figureEightZAmplitudeM;
        float ay = figureEightVerticalAmplitudeM;

        Vector3 pLocal = new Vector3(
            ax * Mathf.Sin(w * tt),
            ay * Mathf.Sin(w * tt),
            az * Mathf.Sin(2.0f * w * tt)
        );

        Vector3 vLocal = new Vector3(
            ax * w * Mathf.Cos(w * tt),
            ay * w * Mathf.Cos(w * tt),
            2.0f * az * w * Mathf.Cos(2.0f * w * tt)
        );

        Vector3 aLocal = new Vector3(
            -ax * w * w * Mathf.Sin(w * tt),
            -ay * w * w * Mathf.Sin(w * tt),
            -4.0f * az * w * w * Mathf.Sin(2.0f * w * tt)
        );

        return MakeSample(pLocal, vLocal, aLocal, tt >= T);
    }

    private TrajectorySample EvaluateSmoothSquare(float t)
    {
        float side = Mathf.Max(0.1f, squareSideM);
        float speed = Mathf.Max(0.05f, squareSpeedMS);
        float segTime = side / speed;
        float total = 4.0f * segTime;
        float tt = Mathf.Clamp(t, 0.0f, total);

        Vector3[] pts =
        {
            Vector3.zero,
            new Vector3(side, 0.0f, 0.0f),
            new Vector3(side, 0.0f, side),
            new Vector3(0.0f, 0.0f, side),
            Vector3.zero
        };

        return EvaluatePolyline(pts, speed, tt, total);
    }

    private TrajectorySample EvaluateLawnmower(float t)
    {
        int lanes = Mathf.Max(2, lawnmowerLanes);
        float length = Mathf.Max(0.1f, lawnmowerLengthM);
        float width = Mathf.Max(0.1f, lawnmowerWidthM);
        float speed = Mathf.Max(0.05f, lawnmowerSpeedMS);

        Vector3[] pts = new Vector3[lanes * 2];

        float dx = width / Mathf.Max(1, lanes - 1);

        for (int i = 0; i < lanes; i++)
        {
            float x = i * dx;

            if (i % 2 == 0)
            {
                pts[i * 2] = new Vector3(x, 0.0f, 0.0f);
                pts[i * 2 + 1] = new Vector3(x, 0.0f, length);
            }
            else
            {
                pts[i * 2] = new Vector3(x, 0.0f, length);
                pts[i * 2 + 1] = new Vector3(x, 0.0f, 0.0f);
            }
        }

        float total = GetPolylineDuration(pts, speed);
        float tt = Mathf.Clamp(t, 0.0f, total);

        return EvaluatePolyline(pts, speed, tt, total);
    }

    private TrajectorySample EvaluateDeploymentApproach(float t)
    {
        float speed = Mathf.Max(0.05f, deploymentSpeedMS);
        float transitTime = deploymentForwardDistanceM / speed;
        float total = transitTime + deploymentFinalHoldSeconds;
        float tt = Mathf.Clamp(t, 0.0f, total);

        float d = Mathf.Min(deploymentForwardDistanceM, speed * tt);

        Vector3 pLocal = new Vector3(0.0f, 0.0f, d);
        Vector3 vLocal = tt < transitTime ? new Vector3(0.0f, 0.0f, speed) : Vector3.zero;
        Vector3 aLocal = Vector3.zero;

        return MakeSample(pLocal, vLocal, aLocal, tt >= total);
    }

    private TrajectorySample EvaluatePolyline(
        Vector3[] pts,
        float speed,
        float t,
        float totalDuration)
    {
        if (pts == null || pts.Length < 2)
        {
            return MakeSample(Vector3.zero, Vector3.zero, Vector3.zero, true);
        }

        float remaining = t;

        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector3 a = pts[i];
            Vector3 b = pts[i + 1];

            float len = Vector3.Distance(a, b);
            float segTime = len / Mathf.Max(0.05f, speed);

            if (remaining <= segTime || i == pts.Length - 2)
            {
                float u = segTime > 0.001f ? Mathf.Clamp01(remaining / segTime) : 1.0f;

                Vector3 pLocal = Vector3.Lerp(a, b, u);
                Vector3 dir = len > 0.001f ? (b - a) / len : Vector3.zero;

                Vector3 vLocal = (t >= totalDuration) ? Vector3.zero : dir * speed;

                return MakeSample(
                    pLocal,
                    vLocal,
                    Vector3.zero,
                    t >= totalDuration
                );
            }

            remaining -= segTime;
        }

        return MakeSample(pts[pts.Length - 1], Vector3.zero, Vector3.zero, true);
    }

    private float GetPolylineDuration(Vector3[] pts, float speed)
    {
        if (pts == null || pts.Length < 2)
        {
            return 0.0f;
        }

        float total = 0.0f;

        for (int i = 0; i < pts.Length - 1; i++)
        {
            total += Vector3.Distance(pts[i], pts[i + 1]) / Mathf.Max(0.05f, speed);
        }

        return total;
    }

    private float GetLawnmowerDuration()
    {
        int lanes = Mathf.Max(2, lawnmowerLanes);
        float length = Mathf.Max(0.1f, lawnmowerLengthM);
        float width = Mathf.Max(0.1f, lawnmowerWidthM);
        float speed = Mathf.Max(0.05f, lawnmowerSpeedMS);

        float laneDistance = lanes * length;
        float crossDistance = Mathf.Max(0, lanes - 1) * (width / Mathf.Max(1, lanes - 1));

        return (laneDistance + crossDistance) / speed;
    }

    private TrajectorySample MakeSample(
        Vector3 pLocal,
        Vector3 vLocal,
        Vector3 aLocal,
        bool completed)
    {
        Quaternion yawRotation =
            Quaternion.Euler(0.0f, originYawDeg, 0.0f);

        TrajectorySample sample = new TrajectorySample();

        sample.positionWorld = originWorld + yawRotation * pLocal;
        sample.velocityWorld = yawRotation * vLocal;
        sample.accelerationWorld = yawRotation * aLocal;
        sample.yawDeg = originYawDeg;
        sample.completed = completed;

        return sample;
    }
}
