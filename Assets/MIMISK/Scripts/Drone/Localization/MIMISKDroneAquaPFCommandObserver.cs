using System;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKDroneAquaPFCommandObserver : MonoBehaviour
{
    [Header("References")]
    public MIMISKDroneAquaLocEstimator aquaLoc;

    [Header("Enable")]
    public bool observerEnabled = true;
    public bool resetOnStart = true;

    [Header("Particles")]
    public int particleCount = 64;
    public int randomSeed = 42;

    [Header("Initial Spread")]
    public float initialPositionStdM = 0.18f;
    public float initialVelocityStdMS = 0.10f;

    [Header("Command Prediction")]
    public bool useCommandPrediction = true;
    public Vector3 commandedVelocityWorld;
    public float commandVelocityResponseHz = 2.5f;
    public float processAccelStdMS2 = 0.22f;
    public float processVelocityRandomWalkMS = 0.015f;
    public float processVelocityDamping = 0.02f;

    [Header("AquaLoc Measurement Update")]
    public float measurementPositionSigmaM = 0.28f;
    public float measurementVelocitySigmaMS = 0.18f;
    public bool useRobustLikelihood = true;
    public float maxPositionInnovationM = 1.10f;
    public float maxVelocityInnovationMS = 0.60f;

    [Header("Resampling")]
    public float resampleEssRatio = 0.55f;
    public float rougheningPositionStdM = 0.010f;
    public float rougheningVelocityStdMS = 0.008f;

    [Header("Output Smoothing")]
    public bool enableOutputSmoothing = true;
    public float outputPositionResponseHz = 9.0f;
    public float outputVelocityResponseHz = 10.0f;

    [Header("Output State")]
    public bool observerReady;
    public Vector3 pfPositionWorld;
    public Vector3 pfVelocityWorld;
    public float pfYawDeg;

    [Header("Debug")]
    public Vector3 measuredPositionWorld;
    public Vector3 measuredVelocityWorld;
    public float effectiveSampleSize;
    public float normalizedEss;
    public int resampleCount;
    public float positionErrorM;

    private Vector2[] particlesP;
    private Vector2[] particlesV;
    private float[] weights;

    private Vector2[] resampledP;
    private Vector2[] resampledV;

    private System.Random rng;
    private bool outputInitialized;

    private void Awake()
    {
        if (aquaLoc == null)
        {
            aquaLoc = GetComponent<MIMISKDroneAquaLocEstimator>();
        }

        Allocate();
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
        Estimate(dt);
        MaybeResample();

        positionErrorM = Vector3.Distance(pfPositionWorld, aquaLoc.truePositionWorld);
    }

    public void SetCommandedVelocityWorld(Vector3 velocityWorld)
    {
        commandedVelocityWorld = velocityWorld;
    }

    [ContextMenu("Reset Observer")]
    public void ResetObserver()
    {
        if (aquaLoc == null)
        {
            aquaLoc = GetComponent<MIMISKDroneAquaLocEstimator>();
        }

        Allocate();

        if (aquaLoc == null || !aquaLoc.estimatorReady)
        {
            observerReady = false;
            return;
        }

        measuredPositionWorld = aquaLoc.estimatedPositionWorld;
        measuredVelocityWorld = aquaLoc.estimatedVelocityWorld;

        Vector2 p0 = new Vector2(measuredPositionWorld.x, measuredPositionWorld.z);
        Vector2 v0 = new Vector2(measuredVelocityWorld.x, measuredVelocityWorld.z);

        float invN = 1.0f / Mathf.Max(1, particleCount);

        for (int i = 0; i < particleCount; i++)
        {
            particlesP[i] = p0 + new Vector2(
                Normal() * initialPositionStdM,
                Normal() * initialPositionStdM
            );

            particlesV[i] = v0 + new Vector2(
                Normal() * initialVelocityStdMS,
                Normal() * initialVelocityStdMS
            );

            weights[i] = invN;
        }

        pfPositionWorld = measuredPositionWorld;
        pfVelocityWorld = measuredVelocityWorld;
        pfYawDeg = aquaLoc.estimatedYawDeg;

        outputInitialized = false;
        resampleCount = 0;
        observerReady = true;
    }

    private void Allocate()
    {
        particleCount = Mathf.Clamp(particleCount, 16, 256);

        if (particlesP != null && particlesP.Length == particleCount)
        {
            if (rng == null)
            {
                rng = new System.Random(randomSeed);
            }

            return;
        }

        particlesP = new Vector2[particleCount];
        particlesV = new Vector2[particleCount];
        weights = new float[particleCount];

        resampledP = new Vector2[particleCount];
        resampledV = new Vector2[particleCount];

        rng = new System.Random(randomSeed);
    }

    private void Predict(float dt)
    {
        Vector2 commandV = new Vector2(commandedVelocityWorld.x, commandedVelocityWorld.z);
        Vector2 measuredV = new Vector2(measuredVelocityWorld.x, measuredVelocityWorld.z);

        if (!useCommandPrediction)
        {
            commandV = measuredV;
        }

        float commandAlpha =
            1.0f - Mathf.Exp(-Mathf.Max(0.001f, commandVelocityResponseHz) * dt);

        float accelStdStep =
            processAccelStdMS2 * Mathf.Sqrt(Mathf.Max(dt, 0.0001f));

        for (int i = 0; i < particleCount; i++)
        {
            particlesV[i] =
                Vector2.Lerp(particlesV[i], commandV, commandAlpha);

            particlesV[i] += new Vector2(
                Normal() * accelStdStep,
                Normal() * accelStdStep
            );

            particlesV[i] += new Vector2(
                Normal() * processVelocityRandomWalkMS,
                Normal() * processVelocityRandomWalkMS
            );

            particlesV[i] *= Mathf.Exp(-processVelocityDamping * dt);

            particlesP[i] += particlesV[i] * dt;
        }
    }

    private void UpdateWeights()
    {
        Vector2 zP = new Vector2(measuredPositionWorld.x, measuredPositionWorld.z);
        Vector2 zV = new Vector2(measuredVelocityWorld.x, measuredVelocityWorld.z);

        float posSigma = Mathf.Max(0.02f, measurementPositionSigmaM);
        float velSigma = Mathf.Max(0.02f, measurementVelocitySigmaMS);

        float sum = 0.0f;

        for (int i = 0; i < particleCount; i++)
        {
            float posErr = (zP - particlesP[i]).magnitude;
            float velErr = (zV - particlesV[i]).magnitude;

            if (useRobustLikelihood)
            {
                posErr = Mathf.Min(posErr, maxPositionInnovationM);
                velErr = Mathf.Min(velErr, maxVelocityInnovationMS);
            }

            float exponent =
                (posErr * posErr) / (posSigma * posSigma) +
                (velErr * velErr) / (velSigma * velSigma);

            float likelihood = Mathf.Exp(-0.5f * exponent) + 1e-12f;

            weights[i] *= likelihood;
            sum += weights[i];
        }

        if (sum <= 1e-20f || float.IsNaN(sum) || float.IsInfinity(sum))
        {
            float uniform = 1.0f / Mathf.Max(1, particleCount);

            for (int i = 0; i < particleCount; i++)
            {
                weights[i] = uniform;
            }

            return;
        }

        float inv = 1.0f / sum;

        for (int i = 0; i < particleCount; i++)
        {
            weights[i] *= inv;
        }
    }

    private void Estimate(float dt)
    {
        Vector2 meanP = Vector2.zero;
        Vector2 meanV = Vector2.zero;

        for (int i = 0; i < particleCount; i++)
        {
            meanP += particlesP[i] * weights[i];
            meanV += particlesV[i] * weights[i];
        }

        Vector3 rawP = new Vector3(meanP.x, measuredPositionWorld.y, meanP.y);
        Vector3 rawV = new Vector3(meanV.x, measuredVelocityWorld.y, meanV.y);

        if (!enableOutputSmoothing || !outputInitialized)
        {
            pfPositionWorld = rawP;
            pfVelocityWorld = rawV;
            outputInitialized = true;
        }
        else
        {
            float pa = 1.0f - Mathf.Exp(-Mathf.Max(0.001f, outputPositionResponseHz) * dt);
            float va = 1.0f - Mathf.Exp(-Mathf.Max(0.001f, outputVelocityResponseHz) * dt);

            pfPositionWorld = Vector3.Lerp(pfPositionWorld, rawP, pa);
            pfVelocityWorld = Vector3.Lerp(pfVelocityWorld, rawV, va);
        }
    }

    private void MaybeResample()
    {
        float sumSq = 0.0f;

        for (int i = 0; i < particleCount; i++)
        {
            sumSq += weights[i] * weights[i];
        }

        effectiveSampleSize = 1.0f / Mathf.Max(sumSq, 1e-12f);
        normalizedEss = effectiveSampleSize / Mathf.Max(1, particleCount);

        if (normalizedEss > resampleEssRatio)
        {
            return;
        }

        ResampleSystematic();
    }

    private void ResampleSystematic()
    {
        float step = 1.0f / Mathf.Max(1, particleCount);
        float u = (float)rng.NextDouble() * step;
        float c = weights[0];
        int i = 0;

        for (int j = 0; j < particleCount; j++)
        {
            float uj = u + j * step;

            while (uj > c && i < particleCount - 1)
            {
                i++;
                c += weights[i];
            }

            resampledP[j] = particlesP[i] + new Vector2(
                Normal() * rougheningPositionStdM,
                Normal() * rougheningPositionStdM
            );

            resampledV[j] = particlesV[i] + new Vector2(
                Normal() * rougheningVelocityStdMS,
                Normal() * rougheningVelocityStdMS
            );
        }

        float uniform = 1.0f / Mathf.Max(1, particleCount);

        for (int j = 0; j < particleCount; j++)
        {
            particlesP[j] = resampledP[j];
            particlesV[j] = resampledV[j];
            weights[j] = uniform;
        }

        resampleCount++;
    }

    private float Normal()
    {
        if (rng == null)
        {
            rng = new System.Random(randomSeed);
        }

        double u1 = Math.Max(1e-12, rng.NextDouble());
        double u2 = Math.Max(1e-12, rng.NextDouble());

        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }
}
