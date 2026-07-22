using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MIMISKDroneAquaLocWaypointNavigator : MonoBehaviour
{
    public enum NavState
    {
        Idle,
        FollowingPath,
        FinalPrecision,
        FinalLoiter,
        Completed,
        Aborted,
        LandingAtFinal
    }

    [Header("References")]
    public MIMISKDroneAquaLocEstimator aquaLoc;
    public MIMISKDroneAquaLocPositionHold aquaHold;
    public MIMISKDroneKeyboardStationKeeping keyboardStationKeeping;
    public MIMISKDroneUdpGamepadReceiver udpReceiver;

    [Header("Keyboard")]
    public Key startMissionKey = Key.N;
    public Key abortMissionKey = Key.B;

    [Header("Mission")]
    public bool missionActive;
    public NavState navState = NavState.Idle;

    [Tooltip("Mission waypoints relative to mission start. Final waypoint is deployment point.")]
    public Vector3[] localWaypointOffsets = new Vector3[]
    {
        new Vector3(0f, 0f, 3f),
        new Vector3(2f, 0f, 3f),
        new Vector3(2f, 0f, 0f),
        new Vector3(0f, 0f, 0f)
    };

    public Vector3[] worldWaypoints;
    public Vector3[] pathPointsWorld;

    [Header("Path-Corridor / LOS Guidance")]
    public int currentSegmentIndex;
    public float lineOfSightLookaheadM = 0.90f;
    public float segmentSwitchDistanceM = 0.18f;
    public float segmentSwitchPredictedDistanceM = 0.25f;
    public float predictedSwitchLookaheadS = 0.70f;

    [Header("AquaNav v4.1 Vector-Field Corridor Correction")]
    public bool enableCrossTrackTargetCorrection = true;

    [Tooltip("How strongly the moving target is shifted across the path to pull the drone back to the corridor.")]
    public float crossTrackCorrectionGain = 0.70f;

    public float maxCrossTrackCorrectionM = 0.35f;

    public bool enableDynamicLookahead = true;
    public float minLookaheadM = 0.45f;
    public float maxLookaheadM = 0.95f;
    public float speedLookaheadGain = 0.20f;
    public float crossTrackLookaheadReductionM = 0.45f;

    public bool enableMovingTargetSmoothing = true;
    public float movingTargetResponse = 7.0f;

    public Vector3 rawCorridorTargetWorld;
    public Vector3 crossTrackCorrectionWorld;

    [Tooltip("For diagnostics only. This is not a hard constraint.")]
    public float desiredCorridorRadiusM = 0.35f;

    public bool slowNearFinalWaypoint = true;
    public float finalApproachDistanceM = 0.65f;

    [Header("Final Deployment Hold")]
    public bool finalWaypointIsDeploymentPoint = true;
    public float finalWaypointRadiusM = 0.20f;
    public float finalWaypointSpeedThresholdMS = 0.11f;
    public float finalStableRequiredSeconds = 1.2f;
    public float finalLoiterSeconds = 3.0f;
    public bool holdAtEnd = true;
    public bool landAtFinalWaypoint = false;

    [Header("Mission Behavior")]
    public bool useCurrentYawForMission = true;

    [Header("Runtime")]
    public Vector3 missionStartPositionWorld;
    public float missionYawDeg;

    public Vector3 currentTargetWorld;
    public Vector3 currentSegmentStartWorld;
    public Vector3 currentSegmentEndWorld;
    public Vector3 closestPointOnSegmentWorld;
    public Vector3 predictedPositionWorld;

    public float segmentLengthM;
    public float alongTrackM;
    public float alongTrack01;
    public float crossTrackErrorM;
    public float distanceToCurrentWaypointM;
    public float predictedDistanceToWaypointM;
    public float horizontalSpeedMS;

    public float finalStableTimerS;
    public float finalLoiterTimerS;
    public float missionTimerS;

    public bool finalArrivalCondition;
    public string lastEvent = "none";

    private bool movingTargetInitialized;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Update()
    {
        AutoFindReferencesIfNeeded();

        if (Keyboard.current != null)
        {
            if (Keyboard.current[startMissionKey].wasPressedThisFrame)
            {
                StartMissionFromCurrentAquaLocPose();
            }

            if (Keyboard.current[abortMissionKey].wasPressedThisFrame)
            {
                AbortMissionAndReturnManual();
            }
        }
    }

    private void FixedUpdate()
    {
        if (!missionActive || aquaLoc == null || aquaHold == null || !aquaLoc.estimatorReady)
        {
            return;
        }

        missionTimerS += Time.fixedDeltaTime;

        if (navState == NavState.FollowingPath)
        {
            UpdatePathCorridorGuidance();
        }
        else if (navState == NavState.FinalPrecision)
        {
            UpdateFinalPrecision(Time.fixedDeltaTime);
        }
        else if (navState == NavState.FinalLoiter)
        {
            UpdateFinalLoiter(Time.fixedDeltaTime);
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (aquaLoc == null)
        {
            aquaLoc = GetComponent<MIMISKDroneAquaLocEstimator>();
        }

        if (aquaHold == null)
        {
            aquaHold = GetComponent<MIMISKDroneAquaLocPositionHold>();
        }

        if (keyboardStationKeeping == null)
        {
            keyboardStationKeeping = GetComponent<MIMISKDroneKeyboardStationKeeping>();
        }

        if (udpReceiver == null)
        {
            udpReceiver = GetComponent<MIMISKDroneUdpGamepadReceiver>();
        }
    }

    private void AutoFindReferencesIfNeeded()
    {
        if (aquaLoc == null || aquaHold == null || keyboardStationKeeping == null || udpReceiver == null)
        {
            AutoFindReferences();
        }
    }

    [ContextMenu("Start Mission From Current AquaLoc Pose")]
    public void StartMissionFromCurrentAquaLocPose()
    {
        AutoFindReferences();

        if (aquaLoc == null || !aquaLoc.estimatorReady)
        {
            lastEvent = "Mission rejected: AquaLoc not ready";
            Debug.LogWarning("[MIMISK] AquaNav v4 mission rejected: AquaLoc not ready.");
            return;
        }

        if (aquaHold == null)
        {
            Debug.LogError("[MIMISK] AquaHold missing.");
            return;
        }

        missionStartPositionWorld = aquaLoc.estimatedPositionWorld;
        missionYawDeg = useCurrentYawForMission ? aquaLoc.estimatedYawDeg : 0.0f;

        BuildWorldPath();

        missionActive = true;
        navState = NavState.FollowingPath;
        currentSegmentIndex = 0;
        missionTimerS = 0.0f;
        finalStableTimerS = 0.0f;
        finalLoiterTimerS = 0.0f;

        if (udpReceiver != null)
        {
            udpReceiver.enabled = false;
        }

        if (keyboardStationKeeping != null)
        {
            keyboardStationKeeping.stationKeepingActive = true;
        }

        lastEvent = "AquaNav v4 path-corridor mission started";
        Debug.Log("[MIMISK] AquaNav v4 path-corridor mission started.");
    }

    private void BuildWorldPath()
    {
        if (localWaypointOffsets == null || localWaypointOffsets.Length == 0)
        {
            localWaypointOffsets = new Vector3[]
            {
                new Vector3(0f, 0f, 3f),
                new Vector3(2f, 0f, 3f),
                new Vector3(2f, 0f, 0f),
                new Vector3(0f, 0f, 0f)
            };
        }

        worldWaypoints = new Vector3[localWaypointOffsets.Length];

        Quaternion yawRotation = Quaternion.Euler(0.0f, missionYawDeg, 0.0f);

        for (int i = 0; i < localWaypointOffsets.Length; i++)
        {
            worldWaypoints[i] = missionStartPositionWorld + yawRotation * localWaypointOffsets[i];
        }

        pathPointsWorld = new Vector3[worldWaypoints.Length + 1];
        pathPointsWorld[0] = missionStartPositionWorld;

        for (int i = 0; i < worldWaypoints.Length; i++)
        {
            pathPointsWorld[i + 1] = worldWaypoints[i];
        }
    }

    private void UpdatePathCorridorGuidance()
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

        currentSegmentStartWorld = pathPointsWorld[currentSegmentIndex];
        currentSegmentEndWorld = pathPointsWorld[currentSegmentIndex + 1];

        Vector3 currentPosition = aquaLoc.estimatedPositionWorld;
        Vector3 horizontalVelocity = new Vector3(
            aquaLoc.estimatedVelocityWorld.x,
            0.0f,
            aquaLoc.estimatedVelocityWorld.z
        );

        horizontalSpeedMS = horizontalVelocity.magnitude;
        predictedPositionWorld = currentPosition + horizontalVelocity * predictedSwitchLookaheadS;

        Vector3 segment = currentSegmentEndWorld - currentSegmentStartWorld;
        segment.y = 0.0f;

        segmentLengthM = Mathf.Max(0.001f, segment.magnitude);
        Vector3 segmentDir = segment / segmentLengthM;

        Vector3 fromStart = currentPosition - currentSegmentStartWorld;
        fromStart.y = 0.0f;

        alongTrackM = Mathf.Clamp(Vector3.Dot(fromStart, segmentDir), 0.0f, segmentLengthM);
        alongTrack01 = alongTrackM / segmentLengthM;

        closestPointOnSegmentWorld = currentSegmentStartWorld + segmentDir * alongTrackM;
        closestPointOnSegmentWorld.y = currentSegmentEndWorld.y;

        Vector3 crossTrack = currentPosition - closestPointOnSegmentWorld;
        crossTrack.y = 0.0f;
        crossTrackErrorM = crossTrack.magnitude;

        Vector3 predictedErrorToEnd = currentSegmentEndWorld - predictedPositionWorld;
        predictedErrorToEnd.y = 0.0f;
        predictedDistanceToWaypointM = predictedErrorToEnd.magnitude;

        Vector3 errorToEnd = currentSegmentEndWorld - currentPosition;
        errorToEnd.y = 0.0f;
        distanceToCurrentWaypointM = errorToEnd.magnitude;

        bool finalSegment = currentSegmentIndex >= pathPointsWorld.Length - 2;

        if (!finalSegment)
        {
            Vector3 predictedFromStart = predictedPositionWorld - currentSegmentStartWorld;
            predictedFromStart.y = 0.0f;

            float predictedAlongTrackM =
                Vector3.Dot(predictedFromStart, segmentDir);

            bool physicallyNearSegmentEnd =
                distanceToCurrentWaypointM <= segmentSwitchDistanceM;

            bool predictedNearSegmentEnd =
                predictedAlongTrackM >= segmentLengthM - segmentSwitchDistanceM &&
                predictedDistanceToWaypointM <= segmentSwitchPredictedDistanceM;

            bool switchSegment =
                physicallyNearSegmentEnd ||
                predictedNearSegmentEnd;

            if (switchSegment)
            {
                currentSegmentIndex++;
                movingTargetInitialized = false;
                lastEvent = "Switched to path segment " + currentSegmentIndex;
                return;
            }
        }
        else
        {
            if (distanceToCurrentWaypointM <= finalApproachDistanceM)
            {
                EnterFinalPrecision();
                return;
            }
        }

        Vector3 crossTrackVector = currentPosition - closestPointOnSegmentWorld;
        crossTrackVector.y = 0.0f;

        float lookahead = lineOfSightLookaheadM;

        if (enableDynamicLookahead)
        {
            float crossTrackSeverity =
                Mathf.Clamp01(crossTrackErrorM / Mathf.Max(0.001f, crossTrackLookaheadReductionM));

            float baseLookahead =
                Mathf.Lerp(maxLookaheadM, minLookaheadM, crossTrackSeverity);

            float speedLookahead =
                horizontalSpeedMS * speedLookaheadGain;

            lookahead =
                Mathf.Clamp(baseLookahead + speedLookahead, minLookaheadM, maxLookaheadM);
        }

        if (finalSegment && slowNearFinalWaypoint)
        {
            float slowFactor =
                Mathf.Clamp01(distanceToCurrentWaypointM / Mathf.Max(0.001f, finalApproachDistanceM));

            lookahead =
                Mathf.Lerp(0.22f, lookahead, slowFactor);
        }

        float targetAlong =
            Mathf.Clamp(alongTrackM + lookahead, 0.0f, segmentLengthM);

        rawCorridorTargetWorld =
            currentSegmentStartWorld + segmentDir * targetAlong;

        rawCorridorTargetWorld.y = currentSegmentEndWorld.y;

        crossTrackCorrectionWorld = Vector3.zero;

        if (enableCrossTrackTargetCorrection)
        {
            crossTrackCorrectionWorld =
                -crossTrackVector * crossTrackCorrectionGain;

            if (crossTrackCorrectionWorld.magnitude > maxCrossTrackCorrectionM)
            {
                crossTrackCorrectionWorld =
                    crossTrackCorrectionWorld.normalized * maxCrossTrackCorrectionM;
            }

            rawCorridorTargetWorld += crossTrackCorrectionWorld;
        }

        if (!enableMovingTargetSmoothing || !movingTargetInitialized)
        {
            currentTargetWorld = rawCorridorTargetWorld;
            movingTargetInitialized = true;
        }
        else
        {
            float alpha =
                1.0f - Mathf.Exp(-Mathf.Max(0.001f, movingTargetResponse) * Time.fixedDeltaTime);

            currentTargetWorld =
                Vector3.Lerp(currentTargetWorld, rawCorridorTargetWorld, alpha);
        }

        currentTargetWorld.y = currentSegmentEndWorld.y;

        aquaHold.UpdateMovingNavigationTarget(currentTargetWorld, missionYawDeg);

        lastEvent = "Vector-field path segment " + currentSegmentIndex;
    }

    private void EnterFinalPrecision()
    {
        navState = NavState.FinalPrecision;
        movingTargetInitialized = false;
        finalStableTimerS = 0.0f;
        finalLoiterTimerS = 0.0f;

        currentTargetWorld = pathPointsWorld[pathPointsWorld.Length - 1];

        aquaHold.SetNavigationTarget(
            currentTargetWorld,
            missionYawDeg,
            MIMISKDroneAquaLocPositionHold.GuidanceMode.HoldTarget
        );

        lastEvent = "Final precision approach";
        Debug.Log("[MIMISK] AquaNav v4 final precision approach.");
    }

    private void UpdateFinalPrecision(float dt)
    {
        currentTargetWorld = pathPointsWorld[pathPointsWorld.Length - 1];

        Vector3 error = currentTargetWorld - aquaLoc.estimatedPositionWorld;
        error.y = 0.0f;

        distanceToCurrentWaypointM = error.magnitude;

        Vector3 horizontalVelocity = new Vector3(
            aquaLoc.estimatedVelocityWorld.x,
            0.0f,
            aquaLoc.estimatedVelocityWorld.z
        );

        horizontalSpeedMS = horizontalVelocity.magnitude;

        finalArrivalCondition =
            distanceToCurrentWaypointM <= finalWaypointRadiusM &&
            horizontalSpeedMS <= finalWaypointSpeedThresholdMS;

        if (finalArrivalCondition)
        {
            finalStableTimerS += dt;
        }
        else
        {
            finalStableTimerS = 0.0f;
        }

        if (finalStableTimerS >= finalStableRequiredSeconds)
        {
            navState = NavState.FinalLoiter;
            finalLoiterTimerS = 0.0f;
            lastEvent = "Final waypoint loiter";
        }
    }

    private void UpdateFinalLoiter(float dt)
    {
        finalLoiterTimerS += dt;

        if (finalLoiterTimerS < finalLoiterSeconds)
        {
            return;
        }

        if (landAtFinalWaypoint)
        {
            navState = NavState.LandingAtFinal;

            aquaHold.SetNavigationTarget(
                currentTargetWorld,
                missionYawDeg,
                MIMISKDroneAquaLocPositionHold.GuidanceMode.LandOnWaterAtTarget
            );

            lastEvent = "Landing at final waypoint";
        }
        else
        {
            CompleteMission();
        }
    }

    private void CompleteMission()
    {
        missionActive = false;
        navState = NavState.Completed;

        currentTargetWorld = pathPointsWorld != null && pathPointsWorld.Length > 0
            ? pathPointsWorld[pathPointsWorld.Length - 1]
            : aquaLoc.estimatedPositionWorld;

        if (holdAtEnd && aquaHold != null)
        {
            aquaHold.SetNavigationTarget(
                currentTargetWorld,
                missionYawDeg,
                MIMISKDroneAquaLocPositionHold.GuidanceMode.HoldTarget
            );
        }
        else
        {
            ReleaseToManual();
        }

        lastEvent = "Mission completed";
        Debug.Log("[MIMISK] AquaNav v4 mission completed.");
    }

    [ContextMenu("Abort Mission And Return Manual")]
    public void AbortMissionAndReturnManual()
    {
        missionActive = false;
        navState = NavState.Aborted;
        ReleaseToManual();

        lastEvent = "Mission aborted";
        Debug.Log("[MIMISK] AquaNav v4 mission aborted. Manual gamepad restored.");
    }

    private void ReleaseToManual()
    {
        if (aquaHold != null)
        {
            aquaHold.enableGuidance = false;
            aquaHold.guidanceMode = MIMISKDroneAquaLocPositionHold.GuidanceMode.Disabled;
            aquaHold.DisableAquaHold();
        }

        if (keyboardStationKeeping != null)
        {
            keyboardStationKeeping.stationKeepingActive = false;
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
            Gizmos.DrawWireSphere(pathPointsWorld[i], i == pathPointsWorld.Length - 1 ? finalWaypointRadiusM : desiredCorridorRadiusM);

            if (i > 0)
            {
                Gizmos.DrawLine(pathPointsWorld[i - 1], pathPointsWorld[i]);
            }
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(currentTargetWorld, 0.18f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(closestPointOnSegmentWorld, 0.12f);
    }
}
