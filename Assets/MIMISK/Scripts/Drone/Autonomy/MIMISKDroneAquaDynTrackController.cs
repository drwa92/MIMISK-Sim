using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MIMISKDroneAquaDynTrackController : MonoBehaviour
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
    public class AxisRlsIdentifier
    {
        [Header("Identified model: v_dot = a*u + b*v + d")]
        public float a = 1.4f;
        public float b = -1.2f;
        public float d = 0.0f;

        [Header("RLS")]
        public bool enableLearning = true;
        public float forgetting = 0.996f;
        public float minInputForLearning = 0.03f;
        public float minVelocityForLearning = 0.03f;
        public float maxObservedAccel = 6.0f;
        public float minA = 0.25f;
        public float maxA = 8.0f;

        public float p00 = 8.0f;
        public float p01 = 0.0f;
        public float p02 = 0.0f;
        public float p11 = 8.0f;
        public float p12 = 0.0f;
        public float p22 = 1.0f;

        public float lastPredictionError;

        public void Reset(float initialA, float initialB)
        {
            a = Mathf.Max(0.25f, initialA);
            b = initialB;
            d = 0.0f;

            p00 = 8.0f;
            p01 = 0.0f;
            p02 = 0.0f;
            p11 = 8.0f;
            p12 = 0.0f;
            p22 = 1.0f;

            lastPredictionError = 0.0f;
        }

        public void Update(float u, float v, float measuredAccel)
        {
            if (!enableLearning)
            {
                return;
            }

            if (Mathf.Abs(u) < minInputForLearning &&
                Mathf.Abs(v) < minVelocityForLearning)
            {
                return;
            }

            if (Mathf.Abs(measuredAccel) > maxObservedAccel)
            {
                return;
            }

            float phi0 = u;
            float phi1 = v;
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

            float predictedAccel = a * u + b * v + d;
            float err = measuredAccel - predictedAccel;
            lastPredictionError = err;

            a += k0 * err;
            b += k1 * err;
            d += k2 * err;

            a = Mathf.Clamp(a, minA, maxA);
            b = Mathf.Clamp(b, -8.0f, 2.0f);
            d = Mathf.Clamp(d, -2.0f, 2.0f);

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
            float denom = Mathf.Max(minA, Mathf.Abs(a));
            return (desiredAccel - b * currentVelocity - d) / denom;
        }
    }

    [Header("References")]
    public MIMISKDroneAquaLocEstimator aquaLoc;
    public MIMISKDroneAquaPFCommandObserver commandPfObserver;
    public MIMISKDroneModelController controller;
    public MIMISKDroneUdpGamepadReceiver udpReceiver;

    [Header("Keyboard")]
    public Key startKey = Key.N;
    public Key abortKey = Key.B;

    [Header("Mission")]
    public bool missionActive;
    public TrackState trackState = TrackState.Idle;

    [Tooltip("Smooth trajectory control points relative to the mission start. Final point is deployment/hold point.")]
    public Vector3[] localControlPointOffsets = new Vector3[]
    {
        new Vector3(0.0f, 0.0f, 3.0f),
        new Vector3(2.0f, 0.0f, 3.0f),
        new Vector3(2.0f, 0.0f, 0.0f),
        new Vector3(0.0f, 0.0f, 0.0f)
    };

    public Vector3[] controlPointsWorld;

    [Header("Reference Governor")]
    public float nominalPathSpeedMS = 0.45f;
    public float finalApproachSpeedMS = 0.18f;
    public float finalApproachDistanceM = 0.75f;

    public float lookaheadSeconds = 1.15f;
    public float minLookaheadM = 0.35f;
    public float maxLookaheadM = 0.90f;

    [Tooltip("When tracking error grows, slow virtual progress so the drone can catch the trajectory.")]
    public bool enableErrorAdaptiveProgress = true;

    public float progressSlowdownErrorM = 0.65f;
    public float minProgressSpeedScale = 0.35f;

    [Header("Inverse-Kinematic Tracking Law")]
    public float ikPositionGainL1 = 0.78f;
    public float ikErrorScaleL2 = 1.65f;
    public float velocityErrorDamping = 0.65f;
    public float maxDesiredVelocityMS = 0.82f;

    [Header("Adaptive Inverse-Dynamics Compensation")]
    public bool enableAdaptiveInverseDynamics = true;
    public AxisRlsIdentifier forwardAxis = new AxisRlsIdentifier();
    public AxisRlsIdentifier rightAxis = new AxisRlsIdentifier();

    public float desiredVelocityResponse = 3.0f;
    public float maxDesiredAccelMS2 = 2.2f;

    [Header("Command Output")]
    public float maxCommandMagnitude = 0.82f;
    public float commandResponseHz = 8.0f;
    public float maxCommandSlewPerSecond = 3.2f;

    [Header("Final Deployment Hold")]
    public float finalHoldRadiusM = 0.22f;
    public float finalHoldSpeedMS = 0.12f;
    public float finalStableRequiredSeconds = 1.20f;
    public float finalLoiterSeconds = 3.0f;
    public bool holdAtEnd = true;

    [Header("Runtime State")]
    public Vector3 missionStartWorld;
    public float missionYawDeg;

    public float trajectoryProgress;
    public float closestProgress;
    public float desiredProgress;
    public int currentSegmentIndex;
    public float currentSegmentU;

    public Vector3 estimatedPositionWorld;
    public Vector3 estimatedVelocityWorld;
    public float estimatedYawDeg;

    public Vector3 closestPointWorld;
    public Vector3 desiredPositionWorld;
    public Vector3 referenceVelocityWorld;
    public Vector3 desiredVelocityWorld;
    public Vector3 desiredVelocityBody;
    public Vector3 positionErrorWorld;
    public Vector3 velocityErrorWorld;

    public float trackingErrorM;
    public float distanceToFinalM;
    public float horizontalSpeedMS;
    public float missionTimerS;
    public float finalStableTimerS;
    public float finalLoiterTimerS;
    public float progressSpeedScale;

    public Vector4 rawCommandForwardRightYawAlt;
    public Vector4 filteredCommandForwardRightYawAlt;

    public float forwardAxisVelocity;
    public float rightAxisVelocity;
    public float forwardAxisAccel;
    public float rightAxisAccel;

    public string lastEvent = "none";

    private Vector3 previousEstimatedVelocityWorld;
    private Vector3 previousDesiredVelocityBody;
    private float previousForwardAxisVelocity;
    private float previousRightAxisVelocity;
    private bool previousVelocityValid;

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
            UpdateFinalHold(Time.fixedDeltaTime);
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (aquaLoc == null)
        {
            aquaLoc = GetComponent<MIMISKDroneAquaLocEstimator>();
        }

        if (commandPfObserver == null)
        {
            commandPfObserver = GetComponent<MIMISKDroneAquaPFCommandObserver>();
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
        if (aquaLoc == null || commandPfObserver == null || controller == null || udpReceiver == null)
        {
            AutoFindReferences();
        }
    }

    private bool UpdateEstimatedState()
    {
        if (commandPfObserver != null && commandPfObserver.observerReady)
        {
            estimatedPositionWorld = commandPfObserver.pfPositionWorld;
            estimatedVelocityWorld = commandPfObserver.pfVelocityWorld;
            estimatedYawDeg = commandPfObserver.pfYawDeg;
            return true;
        }

        if (aquaLoc != null && aquaLoc.estimatorReady)
        {
            estimatedPositionWorld = aquaLoc.estimatedPositionWorld;
            estimatedVelocityWorld = aquaLoc.estimatedVelocityWorld;
            estimatedYawDeg = aquaLoc.estimatedYawDeg;
            return true;
        }

        return false;
    }

    [ContextMenu("Start AquaDynTrack Mission")]
    public void StartMission()
    {
        AutoFindReferences();

        if (!UpdateEstimatedState())
        {
            lastEvent = "Mission rejected: localization not ready";
            Debug.LogWarning("[MIMISK] AquaDynTrack rejected: localization not ready.");
            return;
        }

        BuildTrajectory();

        trajectoryProgress = 0.0f;
        closestProgress = 0.0f;
        desiredProgress = 0.0f;
        currentSegmentIndex = 0;
        currentSegmentU = 0.0f;

        missionTimerS = 0.0f;
        finalStableTimerS = 0.0f;
        finalLoiterTimerS = 0.0f;

        filteredCommandForwardRightYawAlt = Vector4.zero;
        rawCommandForwardRightYawAlt = Vector4.zero;

        previousVelocityValid = false;
        previousDesiredVelocityBody = Vector3.zero;
        previousEstimatedVelocityWorld = estimatedVelocityWorld;

        forwardAxis.Reset(1.4f, -1.2f);
        rightAxis.Reset(1.4f, -1.2f);

        if (commandPfObserver != null)
        {
            commandPfObserver.ResetObserver();
            commandPfObserver.SetCommandedVelocityWorld(Vector3.zero);
        }

        if (udpReceiver != null)
        {
            udpReceiver.enabled = false;
        }

        if (controller != null)
        {
            controller.SetMode(MIMISKDroneModelController.DroneMode.ManualAttitude);
        }

        missionActive = true;
        trackState = TrackState.Tracking;

        lastEvent = "AquaDynTrack mission started";
        Debug.Log("[MIMISK] AquaDynTrack mission started.");
    }

    private void BuildTrajectory()
    {
        missionStartWorld = estimatedPositionWorld;
        missionYawDeg = estimatedYawDeg;

        if (localControlPointOffsets == null || localControlPointOffsets.Length == 0)
        {
            localControlPointOffsets = new Vector3[]
            {
                new Vector3(0.0f, 0.0f, 3.0f),
                new Vector3(2.0f, 0.0f, 3.0f),
                new Vector3(2.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 0.0f, 0.0f)
            };
        }

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
        int maxSegment = controlPointsWorld.Length - 2;

        // Important:
        // Segment index max is Length - 2, but spline progress must reach Length - 1
        // so that EvaluateSpline(progress) reaches the final control point.
        float finalProgress = controlPointsWorld.Length - 1.0f;

        Vector3 finalPoint = controlPointsWorld[controlPointsWorld.Length - 1];

        Vector3 toFinal = finalPoint - estimatedPositionWorld;
        toFinal.y = 0.0f;

        distanceToFinalM = toFinal.magnitude;
        horizontalSpeedMS =
            new Vector3(estimatedVelocityWorld.x, 0.0f, estimatedVelocityWorld.z).magnitude;

        if (trajectoryProgress >= finalProgress - 0.25f &&
            distanceToFinalM <= finalApproachDistanceM)
        {
            EnterFinalHold();
            return;
        }

        closestProgress =
            FindClosestProgressNear(
                estimatedPositionWorld,
                Mathf.Max(0.0f, trajectoryProgress - 0.8f),
                Mathf.Min(finalProgress, trajectoryProgress + 1.3f),
                32
            );

        if (closestProgress > trajectoryProgress)
        {
            trajectoryProgress = closestProgress;
        }

        closestPointWorld = EvaluateSpline(closestProgress);
        closestPointWorld.y = estimatedPositionWorld.y;

        Vector3 closestError = closestPointWorld - estimatedPositionWorld;
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

        Vector3 derivNow = EvaluateSplineDerivative(trajectoryProgress);
        derivNow.y = 0.0f;

        float derivMag = Mathf.Max(0.05f, derivNow.magnitude);

        trajectoryProgress +=
            (pathSpeed * progressSpeedScale * dt) / derivMag;

        trajectoryProgress = Mathf.Clamp(trajectoryProgress, 0.0f, finalProgress);

        float lookaheadDistance =
            Mathf.Clamp(
                pathSpeed * lookaheadSeconds,
                minLookaheadM,
                maxLookaheadM
            );

        desiredProgress =
            AdvanceProgressByDistance(trajectoryProgress, lookaheadDistance);

        desiredPositionWorld = EvaluateSpline(desiredProgress);

        Vector3 desiredDeriv = EvaluateSplineDerivative(desiredProgress);
        desiredDeriv.y = 0.0f;

        if (desiredDeriv.sqrMagnitude > 0.0001f)
        {
            referenceVelocityWorld =
                desiredDeriv.normalized * pathSpeed * progressSpeedScale;
        }
        else
        {
            referenceVelocityWorld = Vector3.zero;
        }

        positionErrorWorld = desiredPositionWorld - estimatedPositionWorld;
        positionErrorWorld.y = 0.0f;

        velocityErrorWorld = referenceVelocityWorld - new Vector3(
            estimatedVelocityWorld.x,
            0.0f,
            estimatedVelocityWorld.z
        );

        Vector3 nonlinearCorrection =
            new Vector3(
                ikPositionGainL1 * SafeTanh(ikErrorScaleL2 * positionErrorWorld.x),
                0.0f,
                ikPositionGainL1 * SafeTanh(ikErrorScaleL2 * positionErrorWorld.z)
            );

        desiredVelocityWorld =
            referenceVelocityWorld
            + nonlinearCorrection
            + velocityErrorDamping * velocityErrorWorld;

        desiredVelocityWorld.y = 0.0f;

        if (desiredVelocityWorld.magnitude > maxDesiredVelocityMS)
        {
            desiredVelocityWorld =
                desiredVelocityWorld.normalized * maxDesiredVelocityMS;
        }

        if (commandPfObserver != null)
        {
            commandPfObserver.SetCommandedVelocityWorld(desiredVelocityWorld);
        }

        SendAdaptiveVelocityCommand(dt);

        currentSegmentIndex = Mathf.Clamp(Mathf.FloorToInt(trajectoryProgress), 0, maxSegment);
        currentSegmentU = Mathf.Clamp01(trajectoryProgress - currentSegmentIndex);

        lastEvent = "AquaDynTrack trajectory tracking";
    }

    private void SendAdaptiveVelocityCommand(float dt)
    {
        if (controller == null)
        {
            return;
        }

        controller.targetAltitudeM = desiredPositionWorld.y;
        controller.targetYawDeg = missionYawDeg;
        controller.SetMode(MIMISKDroneModelController.DroneMode.ManualAttitude);

        Quaternion yawRotation = Quaternion.Euler(0.0f, estimatedYawDeg, 0.0f);

        Vector3 bodyVelocity =
            Quaternion.Inverse(yawRotation) *
            new Vector3(estimatedVelocityWorld.x, 0.0f, estimatedVelocityWorld.z);

        desiredVelocityBody =
            Quaternion.Inverse(yawRotation) *
            new Vector3(desiredVelocityWorld.x, 0.0f, desiredVelocityWorld.z);

        forwardAxisVelocity =
            controller.forwardVelocityAxisSign * bodyVelocity.z;

        rightAxisVelocity =
            controller.rightVelocityAxisSign * bodyVelocity.x;

        float desiredForwardAxisVelocity =
            controller.forwardVelocityAxisSign * desiredVelocityBody.z;

        float desiredRightAxisVelocity =
            controller.rightVelocityAxisSign * desiredVelocityBody.x;

        if (previousVelocityValid)
        {
            forwardAxisAccel =
                (forwardAxisVelocity - previousForwardAxisVelocity) /
                Mathf.Max(0.001f, dt);

            rightAxisAccel =
                (rightAxisVelocity - previousRightAxisVelocity) /
                Mathf.Max(0.001f, dt);

            forwardAxis.Update(
                filteredCommandForwardRightYawAlt.x,
                previousForwardAxisVelocity,
                forwardAxisAccel
            );

            rightAxis.Update(
                filteredCommandForwardRightYawAlt.y,
                previousRightAxisVelocity,
                rightAxisAccel
            );
        }

        previousForwardAxisVelocity = forwardAxisVelocity;
        previousRightAxisVelocity = rightAxisVelocity;
        previousVelocityValid = true;

        float desiredForwardAxisAccel =
            (desiredForwardAxisVelocity -
             controller.forwardVelocityAxisSign * previousDesiredVelocityBody.z) /
            Mathf.Max(0.001f, dt);

        float desiredRightAxisAccel =
            (desiredRightAxisVelocity -
             controller.rightVelocityAxisSign * previousDesiredVelocityBody.x) /
            Mathf.Max(0.001f, dt);

        desiredForwardAxisAccel +=
            desiredVelocityResponse *
            (desiredForwardAxisVelocity - forwardAxisVelocity);

        desiredRightAxisAccel +=
            desiredVelocityResponse *
            (desiredRightAxisVelocity - rightAxisVelocity);

        desiredForwardAxisAccel =
            Mathf.Clamp(desiredForwardAxisAccel, -maxDesiredAccelMS2, maxDesiredAccelMS2);

        desiredRightAxisAccel =
            Mathf.Clamp(desiredRightAxisAccel, -maxDesiredAccelMS2, maxDesiredAccelMS2);

        float uForward;
        float uRight;

        if (enableAdaptiveInverseDynamics)
        {
            uForward =
                forwardAxis.InverseCommand(
                    desiredForwardAxisAccel,
                    forwardAxisVelocity
                );

            uRight =
                rightAxis.InverseCommand(
                    desiredRightAxisAccel,
                    rightAxisVelocity
                );
        }
        else
        {
            float maxSpeed = Mathf.Max(0.1f, controller.maxManualHorizontalSpeedMS);

            uForward = desiredForwardAxisVelocity / maxSpeed;
            uRight = desiredRightAxisVelocity / maxSpeed;
        }

        rawCommandForwardRightYawAlt = new Vector4(
            Mathf.Clamp(uForward, -maxCommandMagnitude, maxCommandMagnitude),
            Mathf.Clamp(uRight, -maxCommandMagnitude, maxCommandMagnitude),
            0.0f,
            0.0f
        );

        Vector4 delta =
            rawCommandForwardRightYawAlt - filteredCommandForwardRightYawAlt;

        float maxDelta = maxCommandSlewPerSecond * dt;

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

        previousDesiredVelocityBody = desiredVelocityBody;
    }

    private void EnterFinalHold()
    {
        trackState = TrackState.FinalHold;
        finalStableTimerS = 0.0f;
        finalLoiterTimerS = 0.0f;

        desiredPositionWorld = controlPointsWorld[controlPointsWorld.Length - 1];
        referenceVelocityWorld = Vector3.zero;
        desiredVelocityWorld = Vector3.zero;

        if (commandPfObserver != null)
        {
            commandPfObserver.SetCommandedVelocityWorld(Vector3.zero);
        }

        lastEvent = "Final trajectory hold";
        Debug.Log("[MIMISK] AquaDynTrack final hold.");
    }

    private void UpdateFinalHold(float dt)
    {
        desiredPositionWorld = controlPointsWorld[controlPointsWorld.Length - 1];
        referenceVelocityWorld = Vector3.zero;

        positionErrorWorld = desiredPositionWorld - estimatedPositionWorld;
        positionErrorWorld.y = 0.0f;

        distanceToFinalM = positionErrorWorld.magnitude;
        horizontalSpeedMS =
            new Vector3(estimatedVelocityWorld.x, 0.0f, estimatedVelocityWorld.z).magnitude;

        Vector3 nonlinearCorrection =
            new Vector3(
                ikPositionGainL1 * SafeTanh(ikErrorScaleL2 * positionErrorWorld.x),
                0.0f,
                ikPositionGainL1 * SafeTanh(ikErrorScaleL2 * positionErrorWorld.z)
            );

        desiredVelocityWorld =
            nonlinearCorrection
            - 0.8f * new Vector3(estimatedVelocityWorld.x, 0.0f, estimatedVelocityWorld.z);

        if (desiredVelocityWorld.magnitude > finalApproachSpeedMS)
        {
            desiredVelocityWorld =
                desiredVelocityWorld.normalized * finalApproachSpeedMS;
        }

        if (commandPfObserver != null)
        {
            commandPfObserver.SetCommandedVelocityWorld(desiredVelocityWorld);
        }

        SendAdaptiveVelocityCommand(dt);

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
    }

    private void CompleteMission()
    {
        missionActive = false;
        trackState = TrackState.Completed;

        if (commandPfObserver != null)
        {
            commandPfObserver.SetCommandedVelocityWorld(Vector3.zero);
        }

        if (holdAtEnd)
        {
            filteredCommandForwardRightYawAlt = Vector4.zero;
            rawCommandForwardRightYawAlt = Vector4.zero;

            if (controller != null)
            {
                controller.ClearExternalCommand();
            }
        }
        else
        {
            ReleaseToManual();
        }

        lastEvent = "Mission completed";
        Debug.Log("[MIMISK] AquaDynTrack mission completed.");
    }

    [ContextMenu("Abort AquaDynTrack")]
    public void AbortMission()
    {
        missionActive = false;
        trackState = TrackState.Aborted;

        ReleaseToManual();

        lastEvent = "Mission aborted";
        Debug.Log("[MIMISK] AquaDynTrack mission aborted.");
    }

    private void ReleaseToManual()
    {
        if (controller != null)
        {
            controller.ClearExternalCommand();
            controller.SetMode(MIMISKDroneModelController.DroneMode.ManualAttitude);
        }

        if (commandPfObserver != null)
        {
            commandPfObserver.SetCommandedVelocityWorld(Vector3.zero);
        }

        if (udpReceiver != null)
        {
            udpReceiver.enabled = true;
        }
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
            float t = Mathf.Lerp(minProgress, maxProgress, i / Mathf.Max(1.0f, (float)samples));

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

    private float AdvanceProgressByDistance(float progress, float distance)
    {
        float p = progress;
        float remaining = distance;
        int guard = 0;
        float maxProgress = controlPointsWorld.Length - 1.0f;

        while (remaining > 0.0f && p < maxProgress && guard < 64)
        {
            Vector3 d = EvaluateSplineDerivative(p);
            d.y = 0.0f;

            float mag = Mathf.Max(0.05f, d.magnitude);
            float step = Mathf.Min(0.05f, remaining / mag);

            p += step;
            remaining -= mag * step;
            guard++;
        }

        return Mathf.Clamp(p, 0.0f, maxProgress);
    }

    private Vector3 EvaluateSpline(float progress)
    {
        int maxSegment = controlPointsWorld.Length - 2;

        int i = Mathf.Clamp(Mathf.FloorToInt(progress), 0, maxSegment);
        float u = Mathf.Clamp01(progress - i);

        Vector3 p0 = controlPointsWorld[Mathf.Clamp(i - 1, 0, controlPointsWorld.Length - 1)];
        Vector3 p1 = controlPointsWorld[Mathf.Clamp(i, 0, controlPointsWorld.Length - 1)];
        Vector3 p2 = controlPointsWorld[Mathf.Clamp(i + 1, 0, controlPointsWorld.Length - 1)];
        Vector3 p3 = controlPointsWorld[Mathf.Clamp(i + 2, 0, controlPointsWorld.Length - 1)];

        return CatmullRom(p0, p1, p2, p3, u);
    }

    private Vector3 EvaluateSplineDerivative(float progress)
    {
        int maxSegment = controlPointsWorld.Length - 2;

        int i = Mathf.Clamp(Mathf.FloorToInt(progress), 0, maxSegment);
        float u = Mathf.Clamp01(progress - i);

        Vector3 p0 = controlPointsWorld[Mathf.Clamp(i - 1, 0, controlPointsWorld.Length - 1)];
        Vector3 p1 = controlPointsWorld[Mathf.Clamp(i, 0, controlPointsWorld.Length - 1)];
        Vector3 p2 = controlPointsWorld[Mathf.Clamp(i + 1, 0, controlPointsWorld.Length - 1)];
        Vector3 p3 = controlPointsWorld[Mathf.Clamp(i + 2, 0, controlPointsWorld.Length - 1)];

        return CatmullRomDerivative(p0, p1, p2, p3, u);
    }

    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
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

    private Vector3 CatmullRomDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
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

        float e2x = Mathf.Exp(2.0f * x);

        return (e2x - 1.0f) / (e2x + 1.0f);
    }

    private void OnDisable()
    {
        if (missionActive)
        {
            ReleaseToManual();
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
        Gizmos.DrawWireSphere(desiredPositionWorld, 0.18f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(closestPointWorld, 0.12f);
    }
}
