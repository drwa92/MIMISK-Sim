using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MIMISKDroneAquaTrackPathFollower : MonoBehaviour
{
    public enum TrackState
    {
        Idle,
        FollowingPath,
        FinalHold,
        Completed,
        Aborted
    }

    [Header("References")]
    public MIMISKDroneAquaPFObserver aquaPF;
    public MIMISKDroneAquaLocEstimator aquaLoc;
    public MIMISKDroneAquaLocPositionHold aquaHold;
    public MIMISKDroneModelController controller;
    public MIMISKDroneUdpGamepadReceiver udpReceiver;

    [Header("Keyboard")]
    public Key startKey = Key.N;
    public Key abortKey = Key.B;

    [Header("Mission")]
    public bool missionActive;
    public TrackState trackState = TrackState.Idle;

    public Vector3[] localPathOffsets = new Vector3[]
    {
        new Vector3(0.0f, 0.0f, 3.0f),
        new Vector3(2.0f, 0.0f, 3.0f),
        new Vector3(2.0f, 0.0f, 0.0f),
        new Vector3(0.0f, 0.0f, 0.0f)
    };

    public Vector3[] pathPointsWorld;
    public int currentSegmentIndex;

    [Header("AquaPF-Track Guidance")]
    public float pathSpeedMS = 0.65f;
    public float finalApproachSpeedMS = 0.22f;

    public float lookaheadM = 0.75f;
    public float minLookaheadM = 0.35f;
    public float maxLookaheadM = 1.10f;
    public float speedLookaheadGain = 0.20f;

    public float crossTrackGain = 0.95f;
    public float maxCrossTrackCorrectionMS = 0.55f;

    public float alongTrackSwitchM = 0.25f;
    public float predictedSwitchLookaheadS = 0.65f;

    public float finalApproachDistanceM = 0.70f;

    [Header("Command Output")]
    public float maxCommandMagnitude = 0.80f;
    public float commandResponseHz = 8.0f;
    public float maxCommandSlewPerSecond = 3.0f;

    [Header("Final Deployment Hold")]
    public bool useAquaHoldForFinalDeployment = true;
    public float finalHoldRadiusM = 0.22f;
    public float finalHoldSpeedMS = 0.12f;
    public float finalStableRequiredSeconds = 1.2f;
    public float finalLoiterSeconds = 3.0f;
    public bool holdAtEnd = true;

    [Header("Runtime")]
    public Vector3 missionStartWorld;
    public float missionYawDeg;

    public Vector3 currentTargetWorld;
    public Vector3 closestPointWorld;
    public Vector3 segmentStartWorld;
    public Vector3 segmentEndWorld;

    public float segmentLengthM;
    public float alongTrackM;
    public float alongTrack01;
    public float crossTrackErrorM;
    public float distanceToSegmentEndM;
    public float horizontalSpeedMS;
    public float finalStableTimerS;
    public float finalLoiterTimerS;
    public float missionTimerS;

    public Vector3 estimatedPositionWorld;
    public Vector3 estimatedVelocityWorld;
    public float estimatedYawDeg;

    public Vector3 desiredVelocityWorld;
    public Vector3 desiredVelocityBody;
    public Vector4 rawCommandForwardRightYawAlt;
    public Vector4 filteredCommandForwardRightYawAlt;

    public string lastEvent = "none";

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

        if (!HasValidState())
        {
            return;
        }

        missionTimerS += Time.fixedDeltaTime;

        if (trackState == TrackState.FollowingPath)
        {
            FollowPath(Time.fixedDeltaTime);
        }
        else if (trackState == TrackState.FinalHold)
        {
            UpdateFinalHold(Time.fixedDeltaTime);
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (aquaPF == null)
        {
            aquaPF = GetComponent<MIMISKDroneAquaPFObserver>();
        }

        if (aquaLoc == null)
        {
            aquaLoc = GetComponent<MIMISKDroneAquaLocEstimator>();
        }

        if (aquaHold == null)
        {
            aquaHold = GetComponent<MIMISKDroneAquaLocPositionHold>();
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
        if (aquaPF == null || aquaLoc == null || controller == null || udpReceiver == null)
        {
            AutoFindReferences();
        }
    }

    private bool HasValidState()
    {
        if (aquaPF != null && aquaPF.observerReady)
        {
            estimatedPositionWorld = aquaPF.pfPositionWorld;
            estimatedVelocityWorld = aquaPF.pfVelocityWorld;
            estimatedYawDeg = aquaPF.pfYawDeg;
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

    [ContextMenu("Start AquaPF-Track Mission")]
    public void StartMission()
    {
        AutoFindReferences();

        if (!HasValidState())
        {
            lastEvent = "Mission rejected: no valid localization";
            Debug.LogWarning("[MIMISK] AquaTrack rejected: no valid localization.");
            return;
        }

        missionStartWorld = estimatedPositionWorld;
        missionYawDeg = estimatedYawDeg;

        BuildPath();

        currentSegmentIndex = 0;
        missionTimerS = 0.0f;
        finalStableTimerS = 0.0f;
        finalLoiterTimerS = 0.0f;
        filteredCommandForwardRightYawAlt = Vector4.zero;

        missionActive = true;
        trackState = TrackState.FollowingPath;

        if (udpReceiver != null)
        {
            udpReceiver.enabled = false;
        }

        if (controller != null)
        {
            controller.SetMode(MIMISKDroneModelController.DroneMode.ManualAttitude);
        }

        lastEvent = "AquaPF-Track mission started";
        Debug.Log("[MIMISK] AquaPF-Track mission started.");
    }

    private void BuildPath()
    {
        if (localPathOffsets == null || localPathOffsets.Length == 0)
        {
            localPathOffsets = new Vector3[]
            {
                new Vector3(0.0f, 0.0f, 3.0f),
                new Vector3(2.0f, 0.0f, 3.0f),
                new Vector3(2.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 0.0f, 0.0f)
            };
        }

        pathPointsWorld = new Vector3[localPathOffsets.Length + 1];
        pathPointsWorld[0] = missionStartWorld;

        Quaternion yawRotation = Quaternion.Euler(0.0f, missionYawDeg, 0.0f);

        for (int i = 0; i < localPathOffsets.Length; i++)
        {
            pathPointsWorld[i + 1] = missionStartWorld + yawRotation * localPathOffsets[i];
        }
    }

    private void FollowPath(float dt)
    {
        if (pathPointsWorld == null || pathPointsWorld.Length < 2)
        {
            CompleteMission();
            return;
        }

        currentSegmentIndex = Mathf.Clamp(
            currentSegmentIndex,
            0,
            pathPointsWorld.Length - 2
        );

        segmentStartWorld = pathPointsWorld[currentSegmentIndex];
        segmentEndWorld = pathPointsWorld[currentSegmentIndex + 1];

        Vector3 segment = segmentEndWorld - segmentStartWorld;
        segment.y = 0.0f;

        segmentLengthM = Mathf.Max(0.001f, segment.magnitude);
        Vector3 segmentDir = segment / segmentLengthM;

        Vector3 position = estimatedPositionWorld;
        Vector3 velocity = new Vector3(
            estimatedVelocityWorld.x,
            0.0f,
            estimatedVelocityWorld.z
        );

        horizontalSpeedMS = velocity.magnitude;

        Vector3 fromStart = position - segmentStartWorld;
        fromStart.y = 0.0f;

        alongTrackM = Mathf.Clamp(Vector3.Dot(fromStart, segmentDir), 0.0f, segmentLengthM);
        alongTrack01 = alongTrackM / segmentLengthM;

        closestPointWorld = segmentStartWorld + segmentDir * alongTrackM;
        closestPointWorld.y = segmentEndWorld.y;

        Vector3 crossTrack = position - closestPointWorld;
        crossTrack.y = 0.0f;

        crossTrackErrorM = crossTrack.magnitude;

        Vector3 toEnd = segmentEndWorld - position;
        toEnd.y = 0.0f;
        distanceToSegmentEndM = toEnd.magnitude;

        Vector3 predictedPosition =
            position + velocity * predictedSwitchLookaheadS;

        Vector3 predictedFromStart = predictedPosition - segmentStartWorld;
        predictedFromStart.y = 0.0f;

        float predictedAlongTrackM = Vector3.Dot(predictedFromStart, segmentDir);

        bool finalSegment = currentSegmentIndex >= pathPointsWorld.Length - 2;

        if (!finalSegment)
        {
            bool switchSegment =
                alongTrackM >= segmentLengthM - alongTrackSwitchM ||
                predictedAlongTrackM >= segmentLengthM - alongTrackSwitchM ||
                distanceToSegmentEndM <= alongTrackSwitchM;

            if (switchSegment)
            {
                currentSegmentIndex++;
                lastEvent = "Switched segment " + currentSegmentIndex;
                return;
            }
        }
        else
        {
            if (distanceToSegmentEndM <= finalApproachDistanceM)
            {
                EnterFinalHold();
                return;
            }
        }

        float dynamicLookahead =
            Mathf.Clamp(
                lookaheadM + horizontalSpeedMS * speedLookaheadGain,
                minLookaheadM,
                maxLookaheadM
            );

        float targetAlong =
            Mathf.Clamp(alongTrackM + dynamicLookahead, 0.0f, segmentLengthM);

        currentTargetWorld = segmentStartWorld + segmentDir * targetAlong;
        currentTargetWorld.y = segmentEndWorld.y;

        Vector3 crossTrackCorrectionVelocity =
            -crossTrack * crossTrackGain;

        if (crossTrackCorrectionVelocity.magnitude > maxCrossTrackCorrectionMS)
        {
            crossTrackCorrectionVelocity =
                crossTrackCorrectionVelocity.normalized * maxCrossTrackCorrectionMS;
        }

        float speed = finalSegment && distanceToSegmentEndM < finalApproachDistanceM
            ? finalApproachSpeedMS
            : pathSpeedMS;

        desiredVelocityWorld =
            segmentDir * speed + crossTrackCorrectionVelocity;

        desiredVelocityWorld.y = 0.0f;

        SendVelocityCommand(dt);

        lastEvent = "Following segment " + currentSegmentIndex;
    }

    private void SendVelocityCommand(float dt)
    {
        if (controller == null)
        {
            return;
        }

        controller.targetAltitudeM = currentTargetWorld.y;
        controller.targetYawDeg = missionYawDeg;
        controller.SetMode(MIMISKDroneModelController.DroneMode.ManualAttitude);

        Quaternion yawRotation = Quaternion.Euler(0.0f, estimatedYawDeg, 0.0f);
        desiredVelocityBody = Quaternion.Inverse(yawRotation) * desiredVelocityWorld;

        float maxSpeed = Mathf.Max(0.1f, controller.maxManualHorizontalSpeedMS);

        float desiredForwardMS =
            controller.forwardVelocityAxisSign * desiredVelocityBody.z;

        float desiredRightMS =
            controller.rightVelocityAxisSign * desiredVelocityBody.x;

        rawCommandForwardRightYawAlt = new Vector4(
            Mathf.Clamp(desiredForwardMS / maxSpeed, -maxCommandMagnitude, maxCommandMagnitude),
            Mathf.Clamp(desiredRightMS / maxSpeed, -maxCommandMagnitude, maxCommandMagnitude),
            0.0f,
            0.0f
        );

        Vector4 delta = rawCommandForwardRightYawAlt - filteredCommandForwardRightYawAlt;
        float maxDelta = maxCommandSlewPerSecond * dt;

        if (delta.magnitude > maxDelta)
        {
            delta = delta.normalized * maxDelta;
        }

        Vector4 slewCommand = filteredCommandForwardRightYawAlt + delta;

        float alpha = 1.0f - Mathf.Exp(-Mathf.Max(0.001f, commandResponseHz) * dt);

        filteredCommandForwardRightYawAlt =
            Vector4.Lerp(filteredCommandForwardRightYawAlt, slewCommand, alpha);

        controller.SetExternalCommand(
            filteredCommandForwardRightYawAlt.x,
            filteredCommandForwardRightYawAlt.y,
            filteredCommandForwardRightYawAlt.z,
            filteredCommandForwardRightYawAlt.w
        );
    }

    private void EnterFinalHold()
    {
        trackState = TrackState.FinalHold;
        finalStableTimerS = 0.0f;
        finalLoiterTimerS = 0.0f;

        currentTargetWorld = pathPointsWorld[pathPointsWorld.Length - 1];

        controller.ClearExternalCommand();

        if (useAquaHoldForFinalDeployment && aquaHold != null)
        {
            aquaHold.SetNavigationTarget(
                currentTargetWorld,
                missionYawDeg,
                MIMISKDroneAquaLocPositionHold.GuidanceMode.HoldTarget
            );
        }

        lastEvent = "Final deployment hold";
        Debug.Log("[MIMISK] AquaPF-Track final deployment hold.");
    }

    private void UpdateFinalHold(float dt)
    {
        currentTargetWorld = pathPointsWorld[pathPointsWorld.Length - 1];

        Vector3 error = currentTargetWorld - estimatedPositionWorld;
        error.y = 0.0f;

        float distance = error.magnitude;
        float speed = new Vector3(
            estimatedVelocityWorld.x,
            0.0f,
            estimatedVelocityWorld.z
        ).magnitude;

        if (distance <= finalHoldRadiusM && speed <= finalHoldSpeedMS)
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

        if (!holdAtEnd)
        {
            ReleaseToManual();
        }

        lastEvent = "Mission completed";
        Debug.Log("[MIMISK] AquaPF-Track mission completed.");
    }

    [ContextMenu("Abort Mission")]
    public void AbortMission()
    {
        missionActive = false;
        trackState = TrackState.Aborted;
        ReleaseToManual();

        lastEvent = "Mission aborted";
        Debug.Log("[MIMISK] AquaPF-Track mission aborted.");
    }

    private void ReleaseToManual()
    {
        if (controller != null)
        {
            controller.ClearExternalCommand();
            controller.SetMode(MIMISKDroneModelController.DroneMode.ManualAttitude);
        }

        if (aquaHold != null)
        {
            aquaHold.DisableAquaHold();
        }

        if (udpReceiver != null)
        {
            udpReceiver.enabled = true;
        }
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
        if (pathPointsWorld == null)
        {
            return;
        }

        Gizmos.color = Color.blue;

        for (int i = 0; i < pathPointsWorld.Length; i++)
        {
            Gizmos.DrawWireSphere(pathPointsWorld[i], 0.18f);

            if (i > 0)
            {
                Gizmos.DrawLine(pathPointsWorld[i - 1], pathPointsWorld[i]);
            }
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(currentTargetWorld, 0.15f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(closestPointWorld, 0.10f);
    }
}
