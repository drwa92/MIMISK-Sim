using System;
using UnityEngine;

public class MIMISKDroneAquaPFObserver : MonoBehaviour
{
    [Header("References")]
    public MIMISKDroneAquaLocEstimator aquaLoc;

    [Header("Particle Filter")]
    public bool observerEnabled = true;
    public bool resetOnStart = true;
    public int particleCount = 96;
    public int randomSeed = 42;

    [Header("Noise Model")]
    public float initialPositionStdM = 0.20f;
    public float initialVelocityStdMS = 0.10f;

    public float processAccelStdMS2 = 0.35f;
    public float processVelocityDamping = 0.06f;

    public float measurementPositionSigmaM = 0.32f;
    public float measurementVelocitySigmaMS = 0.18f;

    [Header("Robust Likelihood")]
    public bool useRobustLikelihood = true;
    public float maxPositionInnovationM = 1.25f;
    public float maxVelocityInnovationMS = 0.65f;

    [Header("Resampling")]
    public float resampleEssRatio = 0.55f;
    public float rougheningPositionStdM = 0.015f;
    public float rougheningVelocityStdMS = 0.010f;

    [Header("Output Smoothing")]
    public bool enableOutputSmoothing = true;
    public float outputPositionResponseHz = 8.0f;
    public float outputVelocityResponseHz = 10.0f;

    [Header("Output State")]
    public bool observerReady;
    public Vector3 pfPositionWorld;
    public Vector3 pfVelocityWorld;
    public float pfYawDeg;

    public Vector3 measuredPositionWorld;
    public Vector3 measuredVelocityWorld;
    public float effectiveSampleSize;
    public float normalizedEss;
    public int resampleCount;

    [Header("Truth Debug - Evaluation Only")]
    public Vector3 truePositionWorld;
    public Vector3 trueVelocityWorld;
    public float positionErrorM;

    private Vector2[] p;
    private Vector2[] v;
    private float[] w;
    private Vector2[] pNew;
    private Vector2[] vNew;

    private System.Random rng;
    private bool outputInitialized;

    private void Awake()
    {
        if (aquaLoc == null)
        {
            aquaLoc = GetComponent<MIMISKDroneAquaLocEstimator>();
        }

        AllocateParticles();
    }

    private void Start()
    {
        if (resetOnStart)
        {
            ResetObserver();
        }
    }

    private void FixedUpdate()
    {
        if (!observerEnabled)
        {
            return;
        }

        if (aquaLoc == null || !aquaLoc.estimatorReady)
        {
            observerReady = false;
            return;
        }

        if (!observerReady)
        {
            ResetObserver();
            return;
        }

        float dt = Time.fixedDeltaTime;

        measuredPositionWorld = aquaLoc.estimatedPositionWorld;
        measuredVelocityWorld = aquaLoc.estimatedVelocityWorld;
        pfYawDeg = aquaLoc.estimatedYawDeg;

        Predict(dt);
        UpdateWeights();
        EstimateState(dt);
        MaybeResample();

        truePositionWorld = aquaLoc.truePositionWorld;
        trueVelocityWorld = aquaLoc.trueVelocityWorld;
        positionErrorM = Vector3.Distance(pfPositionWorld, truePositionWorld);
    }

    [ContextMenu("Reset AquaPF Observer")]
    public void ResetObserver()
    {
        if (aquaLoc == null)
        {
            aquaLoc = GetComponent<MIMISKDroneAquaLocEstimator>();
        }

        AllocateParticles();

        if (aquaLoc == null || !aquaLoc.estimatorReady)
        {
            observerReady = false;
            return;
        }

        measuredPositionWorld = aquaLoc.estimatedPositionWorld;
        measuredVelocityWorld = aquaLoc.estimatedVelocityWorld;

        Vector2 pos0 = new Vector2(measuredPositionWorld.x, measuredPositionWorld.z);
        Vector2 vel0 = new Vector2(measuredVelocityWorld.x, measuredVelocityWorld.z);

        for (int i = 0; i < particleCount; i++)
        {
            p[i] = pos0 + new Vector2(
                Normal() * initialPositionStdM,
                Normal() * initialPositionStdM
            );

            v[i] = vel0 + new Vector2(
                Normal() * initialVelocityStdMS,
                Normal() * initialVelocityStdMS
            );

            w[i] = 1.0f / particleCount;
        }

        pfPositionWorld = measuredPositionWorld;
        pfVelocityWorld = measuredVelocityWorld;
        pfYawDeg = aquaLoc.estimatedYawDeg;
        outputInitialized = false;

        resampleCount = 0;
        observerReady = true;
    }

    private void AllocateParticles()
    {
        particleCount = Mathf.Clamp(particleCount, 16, 512);

        if (p != null && p.Length == particleCount)
        {
            return;
        }

        p = new Vector2[particleCount];
        v = new Vector2[particleCount];
        w = new float[particleCount];
        pNew = new Vector2[particleCount];
        vNew = new Vector2[particleCount];

        rng = new System.Random(randomSeed);
    }

    private void Predict(float dt)
    {
        float accelStdStep = processAccelStdMS2 * Mathf.Sqrt(Mathf.Max(dt, 0.0001f));

        for (int i = 0; i < particleCount; i++)
        {
            Vector2 accelNoise = new Vector2(
                Normal() * accelStdStep,
                Normal() * accelStdStep
            );

            v[i] += accelNoise;
            v[i] *= Mathf.Exp(-processVelocityDamping * dt);
            p[i] += v[i] * dt;
        }
    }

    private void UpdateWeights()
    {
        Vector2 zPos = new Vector2(measuredPositionWorld.x, measuredPositionWorld.z);
        Vector2 zVel = new Vector2(measuredVelocityWorld.x, measuredVelocityWorld.z);

        float posSigma = Mathf.Max(0.02f, measurementPositionSigmaM);
        float velSigma = Mathf.Max(0.02f, measurementVelocitySigmaMS);

        float sum = 0.0f;

        for (int i = 0; i < particleCount; i++)
        {
            Vector2 posInnov = zPos - p[i];
            Vector2 velInnov = zVel - v[i];

            float posErr = posInnov.magnitude;
            float velErr = velInnov.magnitude;

            if (useRobustLikelihood)
            {
                posErr = Mathf.Min(posErr, maxPositionInnovationM);
                velErr = Mathf.Min(velErr, maxVelocityInnovationMS);
            }

            float e =
                (posErr * posErr) / (posSigma * posSigma) +
                (velErr * velErr) / (velSigma * velSigma);

            float likelihood = Mathf.Exp(-0.5f * e) + 1e-12f;

            w[i] *= likelihood;
            sum += w[i];
        }

        if (sum <= 1e-20f || float.IsNaN(sum) || float.IsInfinity(sum))
        {
            float uniform = 1.0f / particleCount;

            for (int i = 0; i < particleCount; i++)
            {
                w[i] = uniform;
            }

            return;
        }

        float inv = 1.0f / sum;

        for (int i = 0; i < particleCount; i++)
        {
            w[i] *= inv;
        }
    }

    private void EstimateState(float dt)
    {
        Vector2 meanP = Vector2.zero;
        Vector2 meanV = Vector2.zero;

        for (int i = 0; i < particleCount; i++)
        {
            meanP += p[i] * w[i];
            meanV += v[i] * w[i];
        }

        Vector3 rawPosition = new Vector3(
            meanP.x,
            measuredPositionWorld.y,
            meanP.y
        );

        Vector3 rawVelocity = new Vector3(
            meanV.x,
            measuredVelocityWorld.y,
            meanV.y
        );

        if (!enableOutputSmoothing || !outputInitialized)
        {
            pfPositionWorld = rawPosition;
            pfVelocityWorld = rawVelocity;
            outputInitialized = true;
        }
        else
        {
            float pa = 1.0f - Mathf.Exp(-Mathf.Max(0.001f, outputPositionResponseHz) * dt);
            float va = 1.0f - Mathf.Exp(-Mathf.Max(0.001f, outputVelocityResponseHz) * dt);

            pfPositionWorld = Vector3.Lerp(pfPositionWorld, rawPosition, pa);
            pfVelocityWorld = Vector3.Lerp(pfVelocityWorld, rawVelocity, va);
        }
    }

    private void MaybeResample()
    {
        float sumSq = 0.0f;

        for (int i = 0; i < particleCount; i++)
        {
            sumSq += w[i] * w[i];
        }

        effectiveSampleSize = 1.0f / Mathf.Max(sumSq, 1e-12f);
        normalizedEss = effectiveSampleSize / particleCount;

        if (normalizedEss > resampleEssRatio)
        {
            return;
        }

        ResampleSystematic();
    }

    private void ResampleSystematic()
    {
        float step = 1.0f / particleCount;
        float u = (float)rng.NextDouble() * step;
        float c = w[0];
        int i = 0;

        for (int j = 0; j < particleCount; j++)
        {
            float uj = u + j * step;

            while (uj > c && i < particleCount - 1)
            {
                i++;
                c += w[i];
            }

            pNew[j] = p[i] + new Vector2(
                Normal() * rougheningPositionStdM,
                Normal() * rougheningPositionStdM
            );

            vNew[j] = v[i] + new Vector2(
                Normal() * rougheningVelocityStdMS,
                Normal() * rougheningVelocityStdMS
            );
        }

        for (int j = 0; j < particleCount; j++)
        {
            p[j] = pNew[j];
            v[j] = vNew[j];
            w[j] = 1.0f / particleCount;
        }

        resampleCount++;
    }

    private float Normal()
    {
        double u1 = Math.Max(1e-12, rng.NextDouble());
        double u2 = Math.Max(1e-12, rng.NextDouble());

        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }
}
