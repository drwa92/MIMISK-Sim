using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MIMISKDroneRtkIKDynamicPathTracker : MonoBehaviour
{
    public enum TrackState
    {
        Idle,
        Tracking,
        FinalHold,
        Completed,
        Aborted
    }

    [System.Serializable]
    public class AxisCommandModel
    {
        [Header("Model: accel = commandGain * u - damping * velocity + bias")]
        public float commandGain = 1.8f;
        public float damping = 1.2f;
        public float bias = 0.0f;

        [Header("Recursive Least Squares")]
        public bool enableLearning = true;
        public float forgetting = 0.995f;
        public float minExcitation = 0.025f;
        public float maxAcceptedAccel = 5.0f;

        public float minCommandGain = 0.25f;
        public float maxCommandGain = 8.0f;
        public float minDamping = 0.05f;
        public float maxDamping = 8.0f;
        public float maxBias = 1.5f;

        public float p00 = 10.0f;
        public float p01 = 0.0f;
        public float p02 = 0.0f;
        public float p11 = 10.0f;
        public float p12 = 0.0f;
        public float p22 = 1.0f;

        public float lastPrediction;
        public float lastError;

        public void Reset(float initialGain, float initialDamping)
        {
            commandGain = Mathf.Clamp(initialGain, minCommandGain, maxCommandGain);
            damping = Mathf.Clamp(initialDamping, minDamping, maxDamping);
            bias = 0.0f;

            p00 = 10.0f;
            p01 = 0.0f;
            p02 = 0.0f;
            p11 = 10.0f;
            p12 = 0.0f;
            p22 = 1.0f;

            lastPrediction = 0.0f;
            lastError = 0.0f;
        }

        public float PredictAccel(float command, float velocity)
        {
            return commandGain * command - damping * velocity + bias;
        }

        public void Update(float command, float velocity, float measuredAccel)
        {
            if (!enableLearning)
            {
                return;
            }

            if (Mathf.Abs(command) < minExcitation &&
                Mathf.Abs(velocity) < minExcitation)
            {
                return;
            }

            if (Mathf.Abs(measuredAccel) > maxAcceptedAccel)
            {
                return;
            }

            // Regressor for accel = gain*u + damping*(-v) + bias.
            float phi0 = command;
            float phi1 = -velocity;
            float phi2 = 1.0f;

            float pPhi0 = p00 * phi0 + p01 * phi1 + p02 * phi2;
            float pPhi1 = p01 * phi0 + p11 * phi1 + p12 * phi2;
            float pPhi2 = p02 * phi0 + p12 * phi1 + p22 * phi2;

            float denom =
                forgetting +
                phi0 * pPhi0 +
                phi1 * pPhi1 +
                phi2 * pPhi2;

            if (denom < 1e-6f)
            {
                return;
            }

            float k0 = pPhi0 / denom;
            float k1 = pPhi1 / denom;
            float k2 = pPhi2 / denom;

            lastPrediction = PredictAccel(command, velocity);
            lastError = measuredAccel - lastPrediction;

            commandGain += k0 * lastError;
            damping += k1 * lastError;
            bias += k2 * lastError;

            commandGain = Mathf.Clamp(commandGain, minCommandGain, maxCommandGain);
            damping = Mathf.Clamp(damping, minDamping, maxDamping);
            bias = Mathf.Clamp(bias, -maxBias, maxBias);

            float invLambda = 1.0f / Mathf.Max(0.90f, forgetting);

            float np00 = (p00 - k0 * pPhi0) * invLambda;
            float np01 = (p01 - k0 * pPhi1) * invLambda;
            float np02 = (p02 - k0 * pPhi2) * invLambda;
            float np11 = (p11 - k1 * pPhi1) * invLambda;
            float np12 = (p12 - k1 * pPhi2) * invLambda;
            float np22 = (p22 - k2 * pPhi2) * invLambda;

            p00 = Mathf.Clamp(np00, 1e-4f, 100.0f);
            p01 = Mathf.Clamp(np01, -100.0f, 100.0f);
            p02 = Mathf.Clamp(np02, -100.0f, 100.0f);
            p11 = Mathf.Clamp(np11, 1e-4f, 100.0f);
            p12 = Mathf.Clamp(np12, -100.0f, 100.0f);
            p22 = Mathf.Clamp(np22, 1e-4f, 100.0f);
        }

        public float InverseCommand(float desiredAccel, float currentVelocity)
        {
            float gain = Mathf.Max(minCommandGain, commandGain);

            return (desiredAccel + damping * currentVelocity - bias) / gain;
        }
    }

    [Header("References")]
    public MIMISKDroneAquaLocEstimator aquaLoc;
    public MIMISKDroneModelController controller;
    public MIMISKDroneUdpGamepadReceiver udpReceiver;

    [Header("Keyboard")]
    public Key startKey = Key.N;
    public Key abortKey = Key.B;

    [Header("Mission")]
    public bool missionActive;
    public TrackState trackState = TrackState.Idle;

    [Tooltip("Smooth path control points relative to mission start. Final point is deployment/hold point.")]
    public Vector3[] localControlPointOffsets = new Vector3[]
    {
        new Vector3(0.0f, 0.0f, 3.0f),
        new Vector3(2.0f, 0.0f, 3.0f),
        new Vector3(2.0f, 0.0f, 0.0f),
        new Vector3(0.0f, 0.0f, 0.0f)
    };

    public Vector3[] controlPointsWorld;

    [Header("Trajectory Timing")]
    public float nominalPathSpeedMS = 0.42f;
    public float finalApproachSpeedMS = 0.16f;
    public float finalApproachDistanceM = 0.70f;

    [Tooltip("If tracking error grows, slow the virtual trajectory so the drone can catch up.")]
    public bool enableErrorAdaptiveProgress = true;

    public float progressSlowdownErrorM = 0.55f;
    public float minProgressSpeedScale = 0.30f;

    [Header("Inverse Kinematics Outer Loop")]
    public float ikPositionGainL1 = 0.80f;
    public float ikErrorScaleL2 = 1.70f;
    public float maxDesiredVelocityMS = 0.75f;

    [Header("Outer-Loop Velocity Damping")]
    public float outerVelocityDamping = 0.45f;
    public float finalOuterVelocityDamping = 1.25f;

    [Header("Inverse Dynamics Inner Loop")]
    public bool enableInverseDynamics = true;
    public bool freezeLearningDuringFinalHold = true;
    public float velocityErrorGainKD = 2.8f;
    public float maxDesiredAccelMS2 = 2.0f;

    public AxisCommandModel forwardAxis = new AxisCommandModel();
    public AxisCommandModel rightAxis = new AxisCommandModel();

    [Header("Command Output")]
    public float maxCommandMagnitude = 0.80f;
    public float commandResponseHz = 8.0f;
    public float maxCommandSlewPerSecond = 3.0f;

    [Header("Final Hold")]
    public float finalHoldRadiusM = 0.26f;
    public float finalHoldSpeedMS = 0.16f;
    public float finalStableRequiredSeconds = 0.8f;
    public float finalLoiterSeconds = 2.0f;
    public bool restoreGamepadOnComplete = true;

    [Header("Runtime State")]
    public Vector3 missionStartWorld;
    public float missionYawDeg;

    public float trajectoryProgress;
    public float closestProgress;
    public int currentSegmentIndex;
    public float currentSegmentU;

    public Vector3 estimatedPositionWorld;
    public Vector3 estimatedVelocityWorld;
    public float estimatedYawDeg;

    public Vector3 referencePositionWorld;
    public Vector3 referenceVelocityWorld;
    public Vector3 desiredVelocityWorld;
    public Vector3 desiredVelocityBody;
    public Vector3 positionErrorWorld;

    public float trackingErrorM;
    public float distanceToFinalM;
    public float horizontalSpeedMS;
    public float progressSpeedScale;
    public float missionTimerS;
    public float finalStableTimerS;
    public float finalLoiterTimerS;

    public Vector4 rawCommandForwardRightYawAlt;
    public Vector4 filteredCommandForwardRightYawAlt;

    public float forwardAxisVelocity;
    public float rightAxisVelocity;
    public float forwardAxisAccel;
    public float rightAxisAccel;

    public string lastEvent = "none";

    private Vector3 previousBodyVelocity;
    private Vector3 previousDesiredVelocityBody;
    private bool previousVelocityReady;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Update()
    {
        AutoFindReferencesIfNeeded();

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[startKey].wasPressedThisFrame)
        {
            StartMission();
        }

        if (Keyboard.current[abortKey].wasPressedThisFrame)
        {
            AbortMission();
        }
    }

    private void FixedUpdate()
    {
        if (!missionActive)
        {
            return;
        }

        if (!UpdateEstimatedState())
        {
            return;
        }

        missionTimerS += Time.fixedDeltaTime;

        if (trackState == TrackState.Tracking)
        {
            TrackTrajectory(Time.fixedDeltaTime);
        }
        else if (trackState == TrackState.FinalHold)
        {
            TrackFinalHold(Time.fixedDeltaTime);
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (aquaLoc == null)
        {
            aquaLoc = GetComponent<MIMISKDroneAquaLocEstimator>();
        }

        if (controller == null)
        {
            controller = GetComponent<MIMISKDroneModelController>();
        }

        if (udpReceiver == null)
        {
            udpReceiver = GetComponent<MIMISKDroneUdpGamepadReceiver>();
        }
    }

    private void AutoFindReferencesIfNeeded()
    {
        if (aquaLoc == null || controller == null || udpReceiver == null)
        {
            AutoFindReferences();
        }
    }

    private bool UpdateEstimatedState()
    {
        if (aquaLoc == null || !aquaLoc.estimatorReady)
        {
            return false;
        }

        estimatedPositionWorld = aquaLoc.estimatedPositionWorld;
        estimatedVelocityWorld = aquaLoc.estimatedVelocityWorld;
        estimatedYawDeg = aquaLoc.estimatedYawDeg;

        return true;
    }

    [ContextMenu("Start RTK IK-Dynamic Mission")]
    public void StartMission()
    {
        AutoFindReferences();

        if (!UpdateEstimatedState())
        {
            lastEvent = "Mission rejected: RTK/AquaLoc not ready";
            Debug.LogWarning("[MIMISK] RTK trajectory mission rejected: AquaLoc is not ready.");
            return;
        }

        BuildControlPoints();

        trajectoryProgress = 0.0f;
        closestProgress = 0.0f;
        currentSegmentIndex = 0;
        currentSegmentU = 0.0f;

        missionTimerS = 0.0f;
        finalStableTimerS = 0.0f;
        finalLoiterTimerS = 0.0f;

        rawCommandForwardRightYawAlt = Vector4.zero;
        filteredCommandForwardRightYawAlt = Vector4.zero;

        previousVelocityReady = false;
        previousBodyVelocity = Vector3.zero;
        previousDesiredVelocityBody = Vector3.zero;

        forwardAxis.Reset(0.75f, 0.45f);
        rightAxis.Reset(0.78f, 0.40f);

        if (udpReceiver != null)
        {
            udpReceiver.enabled = false;
        }

        if (controller != null)
        {
            controller.ClearExternalCommand();
            controller.SetMode(MIMISKDroneModelController.DroneMode.ManualAttitude);
        }

        missionActive = true;
        trackState = TrackState.Tracking;

        lastEvent = "RTK IK-Dynamic trajectory tracking started";
        Debug.Log("[MIMISK] RTK IK-Dynamic trajectory mission started.");
    }

    private void BuildControlPoints()
    {
        missionStartWorld = estimatedPositionWorld;
        missionYawDeg = estimatedYawDeg;

        controlPointsWorld = new Vector3[localControlPointOffsets.Length + 1];
        controlPointsWorld[0] = missionStartWorld;

        Quaternion yawRotation = Quaternion.Euler(0.0f, missionYawDeg, 0.0f);

        for (int i = 0; i < localControlPointOffsets.Length; i++)
        {
            controlPointsWorld[i + 1] =
                missionStartWorld + yawRotation * localControlPointOffsets[i];
        }
    }

    private void TrackTrajectory(float dt)
    {
        float finalProgress = controlPointsWorld.Length - 1.0f;
        Vector3 finalPoint = controlPointsWorld[controlPointsWorld.Length - 1];

        Vector3 finalError = finalPoint - estimatedPositionWorld;
        finalError.y = 0.0f;

        distanceToFinalM = finalError.magnitude;
        horizontalSpeedMS =
            new Vector3(estimatedVelocityWorld.x, 0.0f, estimatedVelocityWorld.z).magnitude;

        if (trajectoryProgress >= finalProgress - 0.15f &&
            distanceToFinalM <= finalApproachDistanceM)
        {
            EnterFinalHold();
            return;
        }

        closestProgress = FindClosestProgressNear(
            estimatedPositionWorld,
            Mathf.Max(0.0f, trajectoryProgress - 0.8f),
            Mathf.Min(finalProgress, trajectoryProgress + 1.2f),
            32
        );

        if (closestProgress > trajectoryProgress)
        {
            trajectoryProgress = closestProgress;
        }

        Vector3 closestPoint = EvaluateSpline(closestProgress);
        Vector3 closestError = closestPoint - estimatedPositionWorld;
        closestError.y = 0.0f;
        trackingErrorM = closestError.magnitude;

        float pathSpeed =
            distanceToFinalM < finalApproachDistanceM
                ? finalApproachSpeedMS
                : nominalPathSpeedMS;

        if (enableErrorAdaptiveProgress)
        {
            float severity =
                Mathf.Clamp01(trackingErrorM / Mathf.Max(0.001f, progressSlowdownErrorM));

            progressSpeedScale =
                Mathf.Lerp(1.0f, minProgressSpeedScale, severity);
        }
        else
        {
            progressSpeedScale = 1.0f;
        }

        Vector3 derivative = EvaluateSplineDerivative(trajectoryProgress);
        derivative.y = 0.0f;

        float derivativeMag = Mathf.Max(0.05f, derivative.magnitude);

        trajectoryProgress +=
            (pathSpeed * progressSpeedScale * dt) / derivativeMag;

        trajectoryProgress =
            Mathf.Clamp(trajectoryProgress, 0.0f, finalProgress);

        currentSegmentIndex =
            Mathf.Clamp(Mathf.FloorToInt(trajectoryProgress), 0, controlPointsWorld.Length - 2);

        currentSegmentU =
            Mathf.Clamp01(trajectoryProgress - currentSegmentIndex);

        referencePositionWorld = EvaluateSpline(trajectoryProgress);

        Vector3 referenceDerivative = EvaluateSplineDerivative(trajectoryProgress);
        referenceDerivative.y = 0.0f;

        if (referenceDerivative.sqrMagnitude > 0.0001f)
        {
            referenceVelocityWorld =
                referenceDerivative.normalized * pathSpeed * progressSpeedScale;
        }
        else
        {
            referenceVelocityWorld = Vector3.zero;
        }

        ComputeDesiredVelocity();
        SendCommand(dt);

        lastEvent = "Tracking RTK spline trajectory";
    }

    private void TrackFinalHold(float dt)
    {
        referencePositionWorld = controlPointsWorld[controlPointsWorld.Length - 1];
        referenceVelocityWorld = Vector3.zero;

        ComputeDesiredVelocity();

        if (desiredVelocityWorld.magnitude > finalApproachSpeedMS)
        {
            desiredVelocityWorld =
                desiredVelocityWorld.normalized * finalApproachSpeedMS;
        }

        SendCommand(dt);

        distanceToFinalM = positionErrorWorld.magnitude;
        horizontalSpeedMS =
            new Vector3(estimatedVelocityWorld.x, 0.0f, estimatedVelocityWorld.z).magnitude;

        if (distanceToFinalM <= finalHoldRadiusM &&
            horizontalSpeedMS <= finalHoldSpeedMS)
        {
            finalStableTimerS += dt;
        }
        else
        {
            finalStableTimerS = 0.0f;
        }

        if (finalStableTimerS >= finalStableRequiredSeconds)
        {
            finalLoiterTimerS += dt;
        }

        if (finalLoiterTimerS >= finalLoiterSeconds)
        {
            CompleteMission();
        }

        lastEvent = "Final RTK hold";
    }

    private void ComputeDesiredVelocity()
    {
        positionErrorWorld = referencePositionWorld - estimatedPositionWorld;
        positionErrorWorld.y = 0.0f;

        Vector3 nonlinearCorrection = new Vector3(
            ikPositionGainL1 * SafeTanh(ikErrorScaleL2 * positionErrorWorld.x),
            0.0f,
            ikPositionGainL1 * SafeTanh(ikErrorScaleL2 * positionErrorWorld.z)
        );

        Vector3 horizontalVelocity =
            new Vector3(
                estimatedVelocityWorld.x,
                0.0f,
                estimatedVelocityWorld.z
            );

        float damping =
            trackState == TrackState.FinalHold
                ? finalOuterVelocityDamping
                : outerVelocityDamping;

        desiredVelocityWorld =
            referenceVelocityWorld
            + nonlinearCorrection
            + damping * (referenceVelocityWorld - horizontalVelocity);

        desiredVelocityWorld.y = 0.0f;

        if (desiredVelocityWorld.magnitude > maxDesiredVelocityMS)
        {
            desiredVelocityWorld =
                desiredVelocityWorld.normalized * maxDesiredVelocityMS;
        }

        trackingErrorM = positionErrorWorld.magnitude;
    }

    private void SendCommand(float dt)
    {
        if (controller == null)
        {
            return;
        }

        controller.SetMode(MIMISKDroneModelController.DroneMode.ManualAttitude);
        controller.targetAltitudeM = referencePositionWorld.y;
        controller.targetYawDeg = missionYawDeg;

        Quaternion yawRotation =
            Quaternion.Euler(0.0f, estimatedYawDeg, 0.0f);

        Vector3 currentBodyVelocity =
            Quaternion.Inverse(yawRotation) *
            new Vector3(estimatedVelocityWorld.x, 0.0f, estimatedVelocityWorld.z);

        desiredVelocityBody =
            Quaternion.Inverse(yawRotation) *
            new Vector3(desiredVelocityWorld.x, 0.0f, desiredVelocityWorld.z);

        forwardAxisVelocity =
            controller.forwardVelocityAxisSign * currentBodyVelocity.z;

        rightAxisVelocity =
            controller.rightVelocityAxisSign * currentBodyVelocity.x;

        float desiredForwardVelocity =
            controller.forwardVelocityAxisSign * desiredVelocityBody.z;

        float desiredRightVelocity =
            controller.rightVelocityAxisSign * desiredVelocityBody.x;

        if (previousVelocityReady)
        {
            forwardAxisAccel =
                (forwardAxisVelocity -
                 controller.forwardVelocityAxisSign * previousBodyVelocity.z) /
                Mathf.Max(0.001f, dt);

            rightAxisAccel =
                (rightAxisVelocity -
                 controller.rightVelocityAxisSign * previousBodyVelocity.x) /
                Mathf.Max(0.001f, dt);

            bool allowIdentification =
                !(freezeLearningDuringFinalHold && trackState == TrackState.FinalHold);

            if (allowIdentification)
            {
                forwardAxis.Update(
                    filteredCommandForwardRightYawAlt.x,
                    controller.forwardVelocityAxisSign * previousBodyVelocity.z,
                    forwardAxisAccel
                );

                rightAxis.Update(
                    filteredCommandForwardRightYawAlt.y,
                    controller.rightVelocityAxisSign * previousBodyVelocity.x,
                    rightAxisAccel
                );
            }
        }

        float previousDesiredForward =
            controller.forwardVelocityAxisSign * previousDesiredVelocityBody.z;

        float previousDesiredRight =
            controller.rightVelocityAxisSign * previousDesiredVelocityBody.x;

        float desiredForwardAccel =
            (desiredForwardVelocity - previousDesiredForward) /
            Mathf.Max(0.001f, dt);

        float desiredRightAccel =
            (desiredRightVelocity - previousDesiredRight) /
            Mathf.Max(0.001f, dt);

        desiredForwardAccel +=
            velocityErrorGainKD * (desiredForwardVelocity - forwardAxisVelocity);

        desiredRightAccel +=
            velocityErrorGainKD * (desiredRightVelocity - rightAxisVelocity);

        desiredForwardAccel =
            Mathf.Clamp(desiredForwardAccel, -maxDesiredAccelMS2, maxDesiredAccelMS2);

        desiredRightAccel =
            Mathf.Clamp(desiredRightAccel, -maxDesiredAccelMS2, maxDesiredAccelMS2);

        float uForward;
        float uRight;

        if (enableInverseDynamics)
        {
            uForward =
                forwardAxis.InverseCommand(
                    desiredForwardAccel,
                    forwardAxisVelocity
                );

            uRight =
                rightAxis.InverseCommand(
                    desiredRightAccel,
                    rightAxisVelocity
                );
        }
        else
        {
            float maxSpeed =
                Mathf.Max(0.1f, controller.maxManualHorizontalSpeedMS);

            uForward = desiredForwardVelocity / maxSpeed;
            uRight = desiredRightVelocity / maxSpeed;
        }

        rawCommandForwardRightYawAlt = new Vector4(
            Mathf.Clamp(uForward, -maxCommandMagnitude, maxCommandMagnitude),
            Mathf.Clamp(uRight, -maxCommandMagnitude, maxCommandMagnitude),
            0.0f,
            0.0f
        );

        Vector4 delta =
            rawCommandForwardRightYawAlt - filteredCommandForwardRightYawAlt;

        float maxDelta =
            maxCommandSlewPerSecond * dt;

        if (delta.magnitude > maxDelta)
        {
            delta = delta.normalized * maxDelta;
        }

        Vector4 slewCommand =
            filteredCommandForwardRightYawAlt + delta;

        float alpha =
            1.0f - Mathf.Exp(-Mathf.Max(0.001f, commandResponseHz) * dt);

        filteredCommandForwardRightYawAlt =
            Vector4.Lerp(filteredCommandForwardRightYawAlt, slewCommand, alpha);

        controller.SetExternalCommand(
            filteredCommandForwardRightYawAlt.x,
            filteredCommandForwardRightYawAlt.y,
            filteredCommandForwardRightYawAlt.z,
            filteredCommandForwardRightYawAlt.w
        );

        previousBodyVelocity = currentBodyVelocity;
        previousDesiredVelocityBody = desiredVelocityBody;
        previousVelocityReady = true;
    }

    private void EnterFinalHold()
    {
        trackState = TrackState.FinalHold;
        finalStableTimerS = 0.0f;
        finalLoiterTimerS = 0.0f;

        referencePositionWorld = controlPointsWorld[controlPointsWorld.Length - 1];
        referenceVelocityWorld = Vector3.zero;

        lastEvent = "Entered final hold";
        Debug.Log("[MIMISK] RTK trajectory tracker entered final hold.");
    }

    private void CompleteMission()
    {
        missionActive = false;
        trackState = TrackState.Completed;

        if (controller != null)
        {
            controller.ClearExternalCommand();
        }

        if (restoreGamepadOnComplete && udpReceiver != null)
        {
            udpReceiver.enabled = true;
        }

        lastEvent = "Mission completed";
        Debug.Log("[MIMISK] RTK IK-Dynamic mission completed.");
    }

    [ContextMenu("Abort Mission")]
    public void AbortMission()
    {
        missionActive = false;
        trackState = TrackState.Aborted;

        if (controller != null)
        {
            controller.ClearExternalCommand();
            controller.SetMode(MIMISKDroneModelController.DroneMode.ManualAttitude);
        }

        if (udpReceiver != null)
        {
            udpReceiver.enabled = true;
        }

        lastEvent = "Mission aborted";
        Debug.Log("[MIMISK] RTK IK-Dynamic mission aborted.");
    }

    private float FindClosestProgressNear(
        Vector3 position,
        float minProgress,
        float maxProgress,
        int samples)
    {
        float bestProgress = minProgress;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i <= samples; i++)
        {
            float t =
                Mathf.Lerp(minProgress, maxProgress, i / Mathf.Max(1.0f, (float)samples));

            Vector3 p = EvaluateSpline(t);
            Vector3 e = p - position;
            e.y = 0.0f;

            float d = e.sqrMagnitude;

            if (d < bestDistance)
            {
                bestDistance = d;
                bestProgress = t;
            }
        }

        return bestProgress;
    }

    private Vector3 EvaluateSpline(float progress)
    {
        float finalProgress = controlPointsWorld.Length - 1.0f;

        if (progress >= finalProgress)
        {
            return controlPointsWorld[controlPointsWorld.Length - 1];
        }

        int maxSegment = controlPointsWorld.Length - 2;

        int i =
            Mathf.Clamp(Mathf.FloorToInt(progress), 0, maxSegment);

        float u =
            Mathf.Clamp01(progress - i);

        Vector3 p0 = controlPointsWorld[Mathf.Clamp(i - 1, 0, controlPointsWorld.Length - 1)];
        Vector3 p1 = controlPointsWorld[Mathf.Clamp(i, 0, controlPointsWorld.Length - 1)];
        Vector3 p2 = controlPointsWorld[Mathf.Clamp(i + 1, 0, controlPointsWorld.Length - 1)];
        Vector3 p3 = controlPointsWorld[Mathf.Clamp(i + 2, 0, controlPointsWorld.Length - 1)];

        return CatmullRom(p0, p1, p2, p3, u);
    }

    private Vector3 EvaluateSplineDerivative(float progress)
    {
        float finalProgress = controlPointsWorld.Length - 1.0f;

        if (progress >= finalProgress)
        {
            progress = finalProgress - 0.001f;
        }

        int maxSegment = controlPointsWorld.Length - 2;

        int i =
            Mathf.Clamp(Mathf.FloorToInt(progress), 0, maxSegment);

        float u =
            Mathf.Clamp01(progress - i);

        Vector3 p0 = controlPointsWorld[Mathf.Clamp(i - 1, 0, controlPointsWorld.Length - 1)];
        Vector3 p1 = controlPointsWorld[Mathf.Clamp(i, 0, controlPointsWorld.Length - 1)];
        Vector3 p2 = controlPointsWorld[Mathf.Clamp(i + 1, 0, controlPointsWorld.Length - 1)];
        Vector3 p3 = controlPointsWorld[Mathf.Clamp(i + 2, 0, controlPointsWorld.Length - 1)];

        return CatmullRomDerivative(p0, p1, p2, p3, u);
    }

    private Vector3 CatmullRom(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            2.0f * p1 +
            (-p0 + p2) * t +
            (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2 +
            (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t3
        );
    }

    private Vector3 CatmullRomDerivative(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        float t)
    {
        float t2 = t * t;

        return 0.5f * (
            (-p0 + p2) +
            2.0f * (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t +
            3.0f * (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t2
        );
    }

    private float SafeTanh(float x)
    {
        x = Mathf.Clamp(x, -10.0f, 10.0f);

        float e2x =
            Mathf.Exp(2.0f * x);

        return (e2x - 1.0f) / (e2x + 1.0f);
    }

    private void OnDisable()
    {
        if (missionActive)
        {
            AbortMission();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (controlPointsWorld == null)
        {
            return;
        }

        Gizmos.color = Color.blue;

        for (int i = 0; i < controlPointsWorld.Length; i++)
        {
            Gizmos.DrawWireSphere(controlPointsWorld[i], 0.15f);

            if (i > 0)
            {
                Gizmos.DrawLine(controlPointsWorld[i - 1], controlPointsWorld[i]);
            }
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(referencePositionWorld, 0.18f);
    }
}
