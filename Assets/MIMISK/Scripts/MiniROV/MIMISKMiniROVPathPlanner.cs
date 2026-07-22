using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKMiniROVPathPlanner : MonoBehaviour
{
    public enum MiniROVSpeedProfile
    {
        AutomaticByTask,
        SlowInspection,
        StandardSurvey,
        FastTransit,
        Custom
    }

    public enum MiniROVYawPolicy
    {
        TravelHeading,
        FixedYaw,
        FacePoint,
        FaceHome,
        FinalYawOnly,
        CircleTangent,
        FaceCircleCenterAfterCompletion
    }

    public enum StopLookYawPolicy
    {
        KeepArrivalYaw,
        FixedYaw,
        FacePoint,
        FaceHome,
        FacePathCenter
    }

    public enum MiniROVPathType
    {
        StationHold,
        GoToPoint,
        LineOutAndBack,
        Square,
        RectangleSurvey,
        LawnmowerSurvey,
        CircleInspection,
        StopAndLookInspection, // Deprecated: use normal path mission + MissionManager.DwellAtCurrentPose
        SpiralSearch,
        HelixInspection,
        FigureEight,
        ReturnHome
    }

    public enum LineReturnStyle
    {
        DirectReverse,
        RoundedUTurnLoop
    }

    public enum RouteAlgorithm
    {
        DirectLOS,
        CurvatureLimitedHermite
    }

    public enum CoverageAlgorithm
    {
        BoustrophedonLawnmower
    }

    public enum InspectionAlgorithm
    {
        OrbitCircle,
        ArchimedeanSpiral,
        CylindricalHelix,
        GeronoFigureEight
    }

    [Header("References")]
    public Rigidbody rb;
    public MIMISKMiniROVPlantBasedController controller;

    [Header("Selected Mission Path")]
    public MiniROVPathType selectedPathType =
        MiniROVPathType.StationHold;

    public bool startImmediately = true;
    public bool stopAndHoldAtEnd = true;

    [Header("Deprecated Stop-and-Look Waypoint Generator")]
    [Tooltip("Base path used to generate inspection waypoints.")]
    public MiniROVPathType stopLookSourcePathType =
        MiniROVPathType.LawnmowerSurvey;

    public StopLookYawPolicy stopLookYawPolicy =
        StopLookYawPolicy.FacePoint;

    public float stopLookWaypointSpacingM = 0.75f;
    public float stopLookDwellSeconds = 3.0f;
    public float stopLookFixedYawDeg = 0.0f;
    public Vector3 stopLookFacePointWorld;
    public bool stopLookUseHomeAsFacePoint = false;

    [Tooltip("Generated inspection waypoints used by MissionManager.")]
    public Vector3[] stopLookWaypoints;

    public int stopLookWaypointCount;
    public string stopLookLastBuildEvent = "none";


    [Header("Completion Radius")]
    [Tooltip("Completion radius for normal horizontal polyline paths.")]
    public float defaultPolylineArrivalRadiusM = 0.15f;

    [Tooltip("Completion radius for depth-varying paths such as helix, because ballast/depth response is slow.")]
    public float depthVaryingPolylineArrivalRadiusM = 0.25f;

    [Header("Mission Speed Profile")]
    public MiniROVSpeedProfile selectedSpeedProfile =
        MiniROVSpeedProfile.AutomaticByTask;

    public float transitSpeedMS = 0.18f;
    public float surveySpeedMS = 0.14f;
    public float inspectionSpeedMS = 0.10f;
    public float customMissionSpeedMS = 0.12f;

    public bool scaleLookaheadWithSpeed = true;
    public float minLookaheadM = 0.25f;
    public float maxLookaheadM = 0.60f;
    public float lookaheadSpeedGain = 2.5f;

    [Header("Runtime Speed")]
    public float appliedMissionSpeedMS;
    public float appliedLookaheadM;

    [Header("Algorithms")]
    public RouteAlgorithm routeAlgorithm =
        RouteAlgorithm.CurvatureLimitedHermite;

    public CoverageAlgorithm coverageAlgorithm =
        CoverageAlgorithm.BoustrophedonLawnmower;

    public InspectionAlgorithm inspectionAlgorithm =
        InspectionAlgorithm.OrbitCircle;

    [Tooltip("Used by curvature-limited Hermite and rounded-corner path generation.")]
    public float minimumTurnRadiusM = 0.35f;

    [Tooltip("Approximate spacing between generated path points.")]
    public float pathSampleSpacingM = 0.15f;

    public bool smoothPolylineCorners = true;
    public float cornerFilletRadiusM = 0.25f;

    [Header("Yaw / Heading Planning")]
    [Tooltip("TravelHeading is the safe default. This MiniROV has no sway thruster, so yaw is also travel heading while moving.")]
    public MiniROVYawPolicy selectedYawPolicy =
        MiniROVYawPolicy.TravelHeading;

    [Tooltip("Normally OFF. If ON, the controller tries to keep inspection yaw while moving; this can hurt tracking because the ROV has no sway.")]
    public bool allowIndependentYawWhileMoving = false;

    public float fixedYawDeg = 0.0f;
    public Vector3 yawLookAtPointWorld;

    [Tooltip("At mission completion, set StationHold yaw using finalYawDeg or computed face-point yaw.")]
    public bool useFinalYawAtCompletion = false;

    public bool useCurrentYawAsFinalYaw = true;
    public float finalYawDeg = 0.0f;

    [Header("Home / Recovery")]
    public bool homeSet;
    public Vector3 homeWorld;
    public float homeDepthM = 1.0f;

    [Header("Common Depth")]
    public bool useCurrentDepthAtStart = true;
    public float missionDepthM = 1.0f;

    [Header("Go To Point")]
    public Vector3 goToPointWorld;
    public float goToPointDepthM = 1.0f;

    [Tooltip("When ON, GoToPoint is converted to a curvature-limited route and tracked with PolylineLOS.")]
    public bool goToPointUseRoute = false;

    [Tooltip("Safety guard: use a generated route only when target is generally in front. Otherwise explicit GoToPoint rotates first.")]
    public bool goToPointUseRouteOnlyWhenTargetAhead = true;

    [Tooltip("Maximum initial yaw error allowed before using route mode.")]
    public float goToPointRouteMaxInitialYawErrorDeg = 75.0f;

    [Tooltip("If target is closer than this, skip route generation and use explicit GoToPoint.")]
    public float goToPointRouteMinDistanceM = 0.35f;

    [Header("Line / Square / Rectangle")]
    public LineReturnStyle lineReturnStyle =
        LineReturnStyle.RoundedUTurnLoop;

    [Tooltip("Lateral offset between outbound and return lanes for LineOutAndBack.")]
    public float lineReturnOffsetM = 0.45f;

    [Tooltip("Minimum offset used to avoid direct 180-degree endpoint reversal.")]
    public float lineReturnMinOffsetM = 0.25f;

    public float lineLengthM = 2.5f;
    public float squareSideM = 1.2f;
    public float rectangleLengthM = 3.0f;
    public float rectangleWidthM = 1.5f;

    [Header("Lawnmower Survey / Boustrophedon")]
    public float lawnmowerLengthM = 3.0f;
    public float lawnmowerWidthM = 1.5f;
    public float lawnmowerLaneSpacingM = 0.35f;

    [Tooltip("If ON, the first lane is chosen to reduce travel from current position.")]
    public bool optimizeLawnmowerStartSide = true;

    [Header("Circle Inspection")]
    public float circleRadiusM = 0.8f;
    public int circleLaps = 1;
    public bool circleClockwise = true;
    public bool circleStartWithTangentForward = true;

    [Header("Spiral Search")]
    public float spiralInitialRadiusM = 0.15f;
    public float spiralFinalRadiusM = 1.2f;
    public int spiralTurns = 3;
    public int spiralSamplesPerTurn = 48;

    [Header("Helix Inspection")]
    public float helixRadiusM = 0.8f;
    public int helixTurns = 2;
    public int helixSamplesPerTurn = 48;
    public float helixStartDepthM = 1.0f;
    public float helixEndDepthM = 2.0f;

    [Header("Figure Eight")]
    public float figureEightLengthM = 1.6f;
    public float figureEightWidthM = 0.8f;
    public int figureEightSamples = 160;

    [Header("Runtime / Debug")]
    public Vector3[] lastGeneratedPath;
    public string lastPlanName = "none";
    public string lastAlgorithmName = "none";
    public int lastPathPointCount;
    public float lastEstimatedPathLengthM;
    public string lastEvent = "idle";

    private void Awake()
    {
        AutoFindReferences();

        if (!homeSet)
        {
            SetHomeToCurrentPose();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (controller == null)
        {
            controller =
                GetComponent<MIMISKMiniROVPlantBasedController>();
        }
    }

    [ContextMenu("Set Home To Current Pose")]
    public void SetHomeToCurrentPose()
    {
        AutoFindReferences();

        Vector3 p =
            rb != null
                ? rb.position
                : transform.position;

        homeWorld = p;
        homeDepthM = CurrentDepth();
        homeSet = true;

        lastEvent = "home_set_current_pose";
    }

    [ContextMenu("Start Selected Plan")]
    public void StartSelectedPlan()
    {
        AutoFindReferences();

        if (controller == null)
        {
            lastEvent = "start_failed_missing_controller";
            return;
        }

        if (useCurrentDepthAtStart)
        {
            missionDepthM = CurrentDepth();
        }

        if (selectedPathType == MiniROVPathType.StationHold)
        {
            ApplyYawPolicyToController();
            controller.StartHoldCurrentPose();

            lastPlanName = "station_hold";
            lastAlgorithmName = "StationHold";
            lastEvent = "station_hold_started";
            return;
        }

        if (selectedPathType == MiniROVPathType.GoToPoint)
        {
            StartGoToPointPlan();
            return;
        }

        if (selectedPathType == MiniROVPathType.CircleInspection)
        {
            StartCirclePlan();
            return;
        }

        if (selectedPathType == MiniROVPathType.StopAndLookInspection)
        {
            lastPlanName = "deprecated_stop_and_look";
            lastAlgorithmName = "Deprecated";
            lastEvent = "stop_and_look_deprecated_use_path_then_dwell_action";

            if (controller != null)
            {
                controller.StartHoldCurrentPose();
            }

            Debug.LogWarning(
                "[MIMISK MiniROV PathPlanner] StopAndLookInspection is deprecated. " +
                "Run a normal path mission, then call MissionManager.StartDwellAtCurrentPose."
            );

            return;
        }

        if (selectedPathType == MiniROVPathType.ReturnHome)
        {
            StartReturnHomePlan();
            return;
        }

        Vector3[] path =
            BuildPath(selectedPathType);

        StartPolylinePath(
            selectedPathType.ToString(),
            path
        );
    }

    [ContextMenu("Start Stop-and-Look Plan")]
    public void StartStopAndLookPlan()
    {
        AutoFindReferences();

        stopLookWaypoints =
            BuildStopAndLookWaypoints();

        stopLookWaypointCount =
            stopLookWaypoints != null
                ? stopLookWaypoints.Length
                : 0;

        lastGeneratedPath =
            stopLookWaypoints;

        lastPathPointCount =
            stopLookWaypointCount;

        lastEstimatedPathLengthM =
            EstimateLength(stopLookWaypoints);

        lastPlanName =
            "stop_and_look_" + stopLookSourcePathType.ToString();

        lastAlgorithmName =
            "StopAndLook over " + stopLookSourcePathType.ToString();

        if (stopLookWaypoints == null || stopLookWaypoints.Length == 0)
        {
            lastEvent = "stop_and_look_failed_no_waypoints";
            stopLookLastBuildEvent = lastEvent;
            return;
        }

        lastEvent =
            "stop_and_look_plan_ready_" +
            stopLookWaypointCount.ToString() +
            "_waypoints";

        stopLookLastBuildEvent =
            lastEvent;
    }

    [ContextMenu("Start Return Home")]
    public void StartReturnHomePlan()
    {
        AutoFindReferences();

        if (!homeSet)
        {
            lastEvent = "return_home_failed_home_not_set";
            return;
        }

        goToPointWorld = homeWorld;
        goToPointDepthM = homeDepthM;

        selectedPathType = MiniROVPathType.ReturnHome;

        if (goToPointUseRoute &&
            ShouldUseRouteToPoint(homeWorld, homeDepthM))
        {
            Vector3[] path =
                BuildCurvatureRoute(
                    CurrentPositionAtDepth(CurrentDepth()),
                    WithDepth(homeWorld, homeDepthM)
                );

            StartPolylinePath("return_home_route", path);
            ForceReturnHomeFinalYawZero();
            lastPlanName = "return_home_route";
            lastAlgorithmName = routeAlgorithm.ToString();
            lastEvent = "return_home_route_started";
            return;
        }

        if (controller == null)
        {
            lastEvent = "return_home_failed_missing_controller";
            return;
        }

        ApplyYawPolicyToController();
        ForceReturnHomeFinalYawZero();

        controller.targetPointWorld = homeWorld;
        controller.targetPointDepthM = homeDepthM;

        controller.StartController(
            MIMISKMiniROVPlantBasedController.ControlMode.GoToPoint
        );

        lastPlanName = "return_home";
        lastAlgorithmName = "GoToPoint";
        lastEvent = "return_home_started";
    }

    [ContextMenu("Stop And Hold")]
    public void StopAndHold()
    {
        AutoFindReferences();

        if (controller != null)
        {
            controller.StartHoldCurrentPose();
        }

        lastEvent = "stop_and_hold_requested";
    }

    private void ForceReturnHomeFinalYawZero()
    {
        if (controller == null)
        {
            return;
        }

        controller.allowIndependentYawWhileMoving = false;
        controller.yawReferenceMode =
            MIMISKMiniROVPlantBasedController.YawReferenceMode.FinalYawOnly;
        controller.hasFinalYawReference = true;
        controller.finalYawDeg = 0.0f;
    }

    private void StartGoToPointPlan()
    {
        if (controller == null)
        {
            lastEvent = "go_to_point_failed_missing_controller";
            return;
        }

        if (goToPointDepthM <= 0.001f)
        {
            goToPointDepthM = CurrentDepth();
        }

        if (goToPointUseRoute &&
            ShouldUseRouteToPoint(goToPointWorld, goToPointDepthM))
        {
            Vector3[] path =
                BuildCurvatureRoute(
                    CurrentPositionAtDepth(CurrentDepth()),
                    WithDepth(goToPointWorld, goToPointDepthM)
                );

            StartPolylinePath("go_to_point_route", path);

            lastPlanName = "go_to_point_route";
            lastAlgorithmName = routeAlgorithm.ToString();
            lastEvent = "go_to_point_route_started";
            return;
        }

        ApplyYawPolicyToController();

        controller.targetPointWorld = goToPointWorld;
        controller.targetPointDepthM = goToPointDepthM;
        controller.goToPointStopAtTarget = stopAndHoldAtEnd;

        controller.StartController(
            MIMISKMiniROVPlantBasedController.ControlMode.GoToPoint
        );

        lastPlanName = "go_to_point";
        lastAlgorithmName = "GoToPoint";
        lastEvent = "go_to_point_started";
    }

    private bool ShouldUseRouteToPoint(Vector3 targetWorld, float targetDepth)
    {
        Vector3 p =
            rb != null
                ? rb.position
                : transform.position;

        Vector3 target =
            WithDepth(targetWorld, targetDepth);

        Vector3 delta =
            target - p;

        delta.y = 0.0f;

        float dist =
            delta.magnitude;

        if (dist < goToPointRouteMinDistanceM)
        {
            lastEvent = "go_to_point_route_skipped_target_too_close";
            return false;
        }

        if (!goToPointUseRouteOnlyWhenTargetAhead)
        {
            return true;
        }

        Vector3 forward =
            HorizontalForward();

        Vector3 dir =
            delta.normalized;

        float yawErrDeg =
            Mathf.Abs(
                Vector3.SignedAngle(
                    forward,
                    dir,
                    Vector3.up
                )
            );

        if (yawErrDeg > goToPointRouteMaxInitialYawErrorDeg)
        {
            lastEvent =
                "go_to_point_route_skipped_yaw_error_" +
                yawErrDeg.ToString("F1");

            return false;
        }

        return true;
    }

    private void StartCirclePlan()
    {
        if (controller == null)
        {
            lastEvent = "circle_failed_missing_controller";
            return;
        }

        Vector3 p =
            rb != null
                ? rb.position
                : transform.position;

        Vector3 right =
            HorizontalRight();

        Vector3 center =
            p;

        if (circleStartWithTangentForward)
        {
            center =
                p +
                (circleClockwise ? right : -right) *
                Mathf.Max(0.2f, circleRadiusM);
        }

        center.y =
            DepthToWorldY(missionDepthM);

        bool oldUseFinalYawAtCompletion =
            useFinalYawAtCompletion;

        Vector3 oldYawLookAtPointWorld =
            yawLookAtPointWorld;

        if (selectedYawPolicy == MiniROVYawPolicy.FaceCircleCenterAfterCompletion)
        {
            yawLookAtPointWorld = center;
            useFinalYawAtCompletion = true;
        }

        ApplyYawPolicyToController();

        useFinalYawAtCompletion =
            oldUseFinalYawAtCompletion;

        yawLookAtPointWorld =
            oldYawLookAtPointWorld;

        controller.circleCenterWorld = center;
        controller.circleRadiusM = Mathf.Max(0.2f, circleRadiusM);
        controller.circleDepthM = missionDepthM;
        controller.circleClockwise = circleClockwise;
        controller.circleStopAfterCompletedLaps = stopAndHoldAtEnd;
        controller.circleTargetLaps = Mathf.Max(1, circleLaps);

        controller.circleAngularProgressRad = 0.0f;
        controller.circleHasPhase = false;
        controller.circleCompleted = false;

        controller.StartController(
            MIMISKMiniROVPlantBasedController.ControlMode.CircleLOS
        );

        lastGeneratedPath = BuildCirclePreview(center);
        lastPathPointCount = lastGeneratedPath.Length;
        lastEstimatedPathLengthM = EstimateLength(lastGeneratedPath);
        lastPlanName = "circle_inspection";
        lastAlgorithmName = InspectionAlgorithm.OrbitCircle.ToString();
        lastEvent = "circle_inspection_started";
    }

    private void StartPolylinePath(
        string planName,
        Vector3[] path)
    {
        if (path == null || path.Length < 2)
        {
            lastEvent = "polyline_failed_invalid_path";
            return;
        }

        ApplyYawPolicyToController();
        ApplyCompletionSettingsToController();

        controller.pathPointsWorld = path;
        controller.pathSegmentIndex = 0;
        controller.pathComplete = false;
        controller.holdFinalPointWhenPathComplete = stopAndHoldAtEnd;
        controller.polylineStopAtEnd = stopAndHoldAtEnd;

        controller.StartController(
            MIMISKMiniROVPlantBasedController.ControlMode.PolylineLOS
        );

        lastGeneratedPath = path;
        lastPathPointCount = path.Length;
        lastEstimatedPathLengthM = EstimateLength(path);
        lastPlanName = planName;
        lastEvent = "polyline_started_" + planName;
    }

    public Vector3[] BuildStopAndLookWaypoints()
    {
        AutoFindReferences();

        Vector3[] basePath =
            BuildStopLookBasePath();

        if (basePath == null || basePath.Length == 0)
        {
            return null;
        }

        Vector3[] sampled =
            SamplePathBySpacing(
                basePath,
                Mathf.Max(0.10f, stopLookWaypointSpacingM)
            );

        stopLookWaypointCount =
            sampled != null
                ? sampled.Length
                : 0;

        return sampled;
    }

    private Vector3[] BuildStopLookBasePath()
    {
        MiniROVPathType oldSelected =
            selectedPathType;

        MiniROVPathType source =
            stopLookSourcePathType;

        if (source == MiniROVPathType.StopAndLookInspection ||
            source == MiniROVPathType.StationHold ||
            source == MiniROVPathType.GoToPoint ||
            source == MiniROVPathType.ReturnHome)
        {
            source =
                MiniROVPathType.LawnmowerSurvey;
        }

        if (source == MiniROVPathType.CircleInspection)
        {
            Vector3 p =
                rb != null
                    ? rb.position
                    : transform.position;

            Vector3 right =
                HorizontalRight();

            Vector3 center =
                p +
                (circleClockwise ? right : -right) *
                Mathf.Max(0.2f, circleRadiusM);

            center.y =
                DepthToWorldY(missionDepthM);

            return BuildCirclePreview(center);
        }

        selectedPathType =
            source;

        Vector3[] path =
            BuildPath(source);

        selectedPathType =
            oldSelected;

        return path;
    }

    private Vector3[] SamplePathBySpacing(Vector3[] path, float spacing)
    {
        if (path == null || path.Length == 0)
        {
            return null;
        }

        if (path.Length == 1)
        {
            return path;
        }

        List<Vector3> samples =
            new List<Vector3>();

        samples.Add(path[0]);

        float accumulated =
            0.0f;

        Vector3 lastSample =
            path[0];

        for (int i = 1; i < path.Length; i++)
        {
            Vector3 a =
                path[i - 1];

            Vector3 b =
                path[i];

            float segLen =
                Vector3.Distance(a, b);

            if (segLen < 0.0001f)
            {
                continue;
            }

            float remainingOnSegment =
                segLen;

            Vector3 cursor =
                a;

            while (accumulated + remainingOnSegment >= spacing)
            {
                float need =
                    spacing - accumulated;

                float alpha =
                    Mathf.Clamp01(need / remainingOnSegment);

                Vector3 sample =
                    Vector3.Lerp(cursor, b, alpha);

                samples.Add(sample);

                lastSample =
                    sample;

                cursor =
                    sample;

                remainingOnSegment =
                    Vector3.Distance(cursor, b);

                accumulated =
                    0.0f;
            }

            accumulated +=
                remainingOnSegment;
        }

        Vector3 finalPoint =
            path[path.Length - 1];

        if (Vector3.Distance(lastSample, finalPoint) > 0.05f)
        {
            samples.Add(finalPoint);
        }

        return samples.ToArray();
    }

    public Vector3[] BuildPath(MiniROVPathType type)
    {
        if (type == MiniROVPathType.LineOutAndBack)
        {
            lastAlgorithmName = "LOS waypoint route with curvature-limited smoothing";
            return BuildLineOutAndBack();
        }

        if (type == MiniROVPathType.Square)
        {
            lastAlgorithmName = "rounded-corner square route";
            return BuildSquare();
        }

        if (type == MiniROVPathType.RectangleSurvey)
        {
            lastAlgorithmName = "rounded-corner rectangle route";
            return BuildRectangle();
        }

        if (type == MiniROVPathType.LawnmowerSurvey)
        {
            lastAlgorithmName = CoverageAlgorithm.BoustrophedonLawnmower.ToString();
            return BuildLawnmower();
        }

        if (type == MiniROVPathType.SpiralSearch)
        {
            lastAlgorithmName = InspectionAlgorithm.ArchimedeanSpiral.ToString();
            return BuildSpiral();
        }

        if (type == MiniROVPathType.HelixInspection)
        {
            lastAlgorithmName = InspectionAlgorithm.CylindricalHelix.ToString();
            return BuildHelix();
        }

        if (type == MiniROVPathType.FigureEight)
        {
            lastAlgorithmName = InspectionAlgorithm.GeronoFigureEight.ToString();
            return BuildFigureEight();
        }

        return BuildLineOutAndBack();
    }

    private Vector3[] BuildLineOutAndBack()
    {
        Vector3 p =
            CurrentPositionAtDepth(missionDepthM);

        Vector3 f =
            HorizontalForward();

        Vector3 r =
            HorizontalRight();

        float length =
            Mathf.Max(0.2f, lineLengthM);

        if (lineReturnStyle == LineReturnStyle.DirectReverse)
        {
            Vector3[] directControls =
            {
                p,
                p + f * length,
                p
            };

            return BuildPathFromControlPoints(directControls, true);
        }

        // Rounded U-turn / parallel return:
        // This avoids the 180-degree turn at the endpoint that causes yaw flipping.
        float offset =
            Mathf.Max(
                lineReturnMinOffsetM,
                lineReturnOffsetM,
                cornerFilletRadiusM * 2.0f
            );

        Vector3 p0 =
            p;

        Vector3 p1 =
            p + f * length;

        Vector3 p2 =
            p + f * length + r * offset;

        Vector3 p3 =
            p + r * offset;

        Vector3 p4 =
            p;

        Vector3[] controls =
        {
            p0,
            p1,
            p2,
            p3,
            p4
        };

        return BuildPathFromControlPoints(controls, true);
    }

    private Vector3[] BuildSquare()
    {
        Vector3 p =
            CurrentPositionAtDepth(missionDepthM);

        Vector3 f =
            HorizontalForward();

        Vector3 r =
            HorizontalRight();

        float side =
            Mathf.Max(0.2f, squareSideM);

        Vector3[] controls =
        {
            p,
            p + f * side,
            p + f * side + r * side,
            p + r * side,
            p
        };

        return BuildPathFromControlPoints(controls, true);
    }

    private Vector3[] BuildRectangle()
    {
        Vector3 p =
            CurrentPositionAtDepth(missionDepthM);

        Vector3 f =
            HorizontalForward();

        Vector3 r =
            HorizontalRight();

        float length =
            Mathf.Max(0.3f, rectangleLengthM);

        float width =
            Mathf.Max(0.2f, rectangleWidthM);

        Vector3[] controls =
        {
            p,
            p + f * length,
            p + f * length + r * width,
            p + r * width,
            p
        };

        return BuildPathFromControlPoints(controls, true);
    }

    private Vector3[] BuildLawnmower()
    {
        Vector3 p =
            CurrentPositionAtDepth(missionDepthM);

        Vector3 f =
            HorizontalForward();

        Vector3 r =
            HorizontalRight();

        float length =
            Mathf.Max(0.5f, lawnmowerLengthM);

        float width =
            Mathf.Max(0.2f, lawnmowerWidthM);

        float spacing =
            Mathf.Max(0.05f, lawnmowerLaneSpacingM);

        int lanes =
            Mathf.Max(
                2,
                Mathf.FloorToInt(width / spacing) + 1
            );

        List<Vector3> controls =
            new List<Vector3>();

        Vector3 origin =
            p - r * (width * 0.5f);

        bool startFromNearSide =
            true;

        if (optimizeLawnmowerStartSide)
        {
            float nearA =
                Vector3.Distance(p, origin);

            float nearB =
                Vector3.Distance(p, origin + r * width);

            startFromNearSide =
                nearA <= nearB;
        }

        controls.Add(p);

        for (int i = 0; i < lanes; i++)
        {
            int laneIndex =
                startFromNearSide
                    ? i
                    : lanes - 1 - i;

            float alpha =
                lanes <= 1
                    ? 0.0f
                    : (float)laneIndex / (float)(lanes - 1);

            Vector3 laneBase =
                origin + r * (alpha * width);

            Vector3 a =
                laneBase;

            Vector3 b =
                laneBase + f * length;

            bool forwardLane =
                i % 2 == 0;

            if (forwardLane)
            {
                controls.Add(a);
                controls.Add(b);
            }
            else
            {
                controls.Add(b);
                controls.Add(a);
            }
        }

        return BuildPathFromControlPoints(controls.ToArray(), true);
    }

    private Vector3[] BuildSpiral()
    {
        Vector3 center =
            CurrentPositionAtDepth(missionDepthM);

        Vector3 f =
            HorizontalForward();

        Vector3 r =
            HorizontalRight();

        int samples =
            Mathf.Max(
                16,
                Mathf.Max(1, spiralTurns) *
                Mathf.Max(8, spiralSamplesPerTurn)
            );

        List<Vector3> pts =
            new List<Vector3>();

        pts.Add(center);

        for (int i = 0; i <= samples; i++)
        {
            float q =
                (float)i / (float)samples;

            float angle =
                q *
                Mathf.Max(1, spiralTurns) *
                2.0f *
                Mathf.PI;

            float radius =
                Mathf.Lerp(
                    Mathf.Max(0.02f, spiralInitialRadiusM),
                    Mathf.Max(spiralInitialRadiusM, spiralFinalRadiusM),
                    q
                );

            Vector3 point =
                center +
                r * (Mathf.Sin(angle) * radius) +
                f * (Mathf.Cos(angle) * radius);

            point.y =
                DepthToWorldY(missionDepthM);

            pts.Add(point);
        }

        return ResamplePath(pts.ToArray(), pathSampleSpacingM);
    }

    private Vector3[] BuildHelix()
    {
        Vector3 p =
            rb != null
                ? rb.position
                : transform.position;

        Vector3 f =
            HorizontalForward();

        Vector3 r =
            HorizontalRight();

        float radius =
            Mathf.Max(0.2f, helixRadiusM);

        Vector3 center =
            p + r * radius;

        int samples =
            Mathf.Max(
                16,
                Mathf.Max(1, helixTurns) *
                Mathf.Max(8, helixSamplesPerTurn)
            );

        List<Vector3> pts =
            new List<Vector3>();

        for (int i = 0; i <= samples; i++)
        {
            float q =
                (float)i / (float)samples;

            float angle =
                q *
                Mathf.Max(1, helixTurns) *
                2.0f *
                Mathf.PI;

            float depth =
                Mathf.Lerp(
                    helixStartDepthM,
                    helixEndDepthM,
                    q
                );

            Vector3 point =
                center +
                r * (-Mathf.Cos(angle) * radius) +
                f * (Mathf.Sin(angle) * radius);

            point.y =
                DepthToWorldY(depth);

            pts.Add(point);
        }

        return ResamplePath(pts.ToArray(), pathSampleSpacingM);
    }

    private Vector3[] BuildFigureEight()
    {
        Vector3 c =
            CurrentPositionAtDepth(missionDepthM);

        Vector3 f =
            HorizontalForward();

        Vector3 r =
            HorizontalRight();

        int samples =
            Mathf.Max(32, figureEightSamples);

        float halfLength =
            Mathf.Max(0.2f, figureEightLengthM) * 0.5f;

        float halfWidth =
            Mathf.Max(0.1f, figureEightWidthM) * 0.5f;

        List<Vector3> pts =
            new List<Vector3>();

        for (int i = 0; i <= samples; i++)
        {
            float q =
                (float)i / (float)samples;

            float theta =
                q *
                2.0f *
                Mathf.PI;

            Vector3 point =
                c +
                r * (Mathf.Sin(theta) * halfLength) +
                f * (Mathf.Sin(2.0f * theta) * halfWidth);

            point.y =
                DepthToWorldY(missionDepthM);

            pts.Add(point);
        }

        return ResamplePath(pts.ToArray(), pathSampleSpacingM);
    }

    private Vector3[] BuildCirclePreview(Vector3 center)
    {
        int samples =
            Mathf.Max(32, circleLaps * 96);

        Vector3 f =
            HorizontalForward();

        Vector3 r =
            HorizontalRight();

        List<Vector3> pts =
            new List<Vector3>();

        float radius =
            Mathf.Max(0.2f, circleRadiusM);

        for (int i = 0; i <= samples; i++)
        {
            float q =
                (float)i / (float)samples;

            float angle =
                q *
                Mathf.Max(1, circleLaps) *
                2.0f *
                Mathf.PI *
                (circleClockwise ? 1.0f : -1.0f);

            Vector3 point =
                center +
                r * (Mathf.Sin(angle) * radius) +
                f * (Mathf.Cos(angle) * radius);

            point.y =
                DepthToWorldY(missionDepthM);

            pts.Add(point);
        }

        return pts.ToArray();
    }

    private Vector3[] BuildCurvatureRoute(Vector3 start, Vector3 end)
    {
        if (routeAlgorithm == RouteAlgorithm.DirectLOS)
        {
            return BuildPathFromControlPoints(
                new Vector3[]
                {
                    start,
                    end
                },
                false
            );
        }

        Vector3 delta =
            end - start;

        delta.y =
            0.0f;

        float dist =
            delta.magnitude;

        if (dist < 0.05f)
        {
            return new Vector3[]
            {
                start,
                end
            };
        }

        Vector3 startTangent =
            HorizontalForward();

        Vector3 endTangent =
            delta.normalized;

        float handle =
            Mathf.Min(
                dist * 0.5f,
                Mathf.Max(minimumTurnRadiusM, dist * 0.35f)
            );

        Vector3 p0 =
            start;

        Vector3 p1 =
            start + startTangent * handle;

        Vector3 p2 =
            end - endTangent * handle;

        Vector3 p3 =
            end;

        int samples =
            Mathf.Max(
                8,
                Mathf.CeilToInt(dist / Mathf.Max(0.05f, pathSampleSpacingM))
            );

        List<Vector3> pts =
            new List<Vector3>();

        for (int i = 0; i <= samples; i++)
        {
            float t =
                (float)i / (float)samples;

            pts.Add(CubicBezier(p0, p1, p2, p3, t));
        }

        return pts.ToArray();
    }

    private Vector3[] BuildPathFromControlPoints(Vector3[] controls, bool roundCorners)
    {
        if (controls == null || controls.Length < 2)
        {
            return controls;
        }

        Vector3[] processed =
            roundCorners && smoothPolylineCorners
                ? ApplyCornerFillets(controls)
                : controls;

        return ResamplePath(processed, pathSampleSpacingM);
    }

    private Vector3[] ApplyCornerFillets(Vector3[] controls)
    {
        if (controls == null || controls.Length < 3)
        {
            return controls;
        }

        List<Vector3> pts =
            new List<Vector3>();

        pts.Add(controls[0]);

        for (int i = 1; i < controls.Length - 1; i++)
        {
            Vector3 a =
                controls[i - 1];

            Vector3 b =
                controls[i];

            Vector3 c =
                controls[i + 1];

            Vector3 ab =
                b - a;

            Vector3 bc =
                c - b;

            float lab =
                ab.magnitude;

            float lbc =
                bc.magnitude;

            if (lab < 0.001f || lbc < 0.001f)
            {
                continue;
            }

            float radius =
                Mathf.Min(
                    cornerFilletRadiusM,
                    Mathf.Min(lab, lbc) * 0.4f
                );

            if (radius < 0.02f)
            {
                pts.Add(b);
                continue;
            }

            Vector3 pIn =
                b - ab.normalized * radius;

            Vector3 pOut =
                b + bc.normalized * radius;

            pts.Add(pIn);

            int samples =
                Mathf.Max(
                    3,
                    Mathf.CeilToInt(radius / Mathf.Max(0.05f, pathSampleSpacingM))
                );

            for (int k = 1; k < samples; k++)
            {
                float t =
                    (float)k / (float)samples;

                pts.Add(QuadraticBezier(pIn, b, pOut, t));
            }

            pts.Add(pOut);
        }

        pts.Add(controls[controls.Length - 1]);

        return pts.ToArray();
    }

    private Vector3[] ResamplePath(Vector3[] input, float spacing)
    {
        if (input == null || input.Length < 2)
        {
            return input;
        }

        spacing =
            Mathf.Max(0.03f, spacing);

        List<Vector3> output =
            new List<Vector3>();

        output.Add(input[0]);

        for (int i = 0; i < input.Length - 1; i++)
        {
            Vector3 a =
                input[i];

            Vector3 b =
                input[i + 1];

            float dist =
                Vector3.Distance(a, b);

            if (dist < 0.001f)
            {
                continue;
            }

            int steps =
                Mathf.Max(
                    1,
                    Mathf.CeilToInt(dist / spacing)
                );

            for (int k = 1; k <= steps; k++)
            {
                float t =
                    (float)k / (float)steps;

                output.Add(Vector3.Lerp(a, b, t));
            }
        }

        return output.ToArray();
    }

    private Vector3 CubicBezier(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        float t)
    {
        float u =
            1.0f - t;

        return
            u * u * u * p0 +
            3.0f * u * u * t * p1 +
            3.0f * u * t * t * p2 +
            t * t * t * p3;
    }

    private Vector3 QuadraticBezier(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        float t)
    {
        float u =
            1.0f - t;

        return
            u * u * p0 +
            2.0f * u * t * p1 +
            t * t * p2;
    }

    private void ApplyMissionSpeedToController()
    {
        if (controller == null)
        {
            return;
        }

        float speed =
            ResolveMissionSpeedMS();

        appliedMissionSpeedMS =
            speed;

        controller.waypointMaxSpeedMS =
            speed;

        controller.goToPointMaxSpeedMS =
            Mathf.Max(0.04f, speed);

        if (scaleLookaheadWithSpeed)
        {
            appliedLookaheadM =
                Mathf.Clamp(
                    speed * lookaheadSpeedGain,
                    minLookaheadM,
                    maxLookaheadM
                );

            controller.losLookaheadM =
                appliedLookaheadM;
        }
        else
        {
            appliedLookaheadM =
                controller.losLookaheadM;
        }
    }

    private float ResolveMissionSpeedMS()
    {
        if (selectedSpeedProfile == MiniROVSpeedProfile.Custom)
        {
            return Mathf.Clamp(customMissionSpeedMS, 0.03f, 0.35f);
        }

        if (selectedSpeedProfile == MiniROVSpeedProfile.SlowInspection)
        {
            return Mathf.Clamp(inspectionSpeedMS, 0.03f, 0.20f);
        }

        if (selectedSpeedProfile == MiniROVSpeedProfile.StandardSurvey)
        {
            return Mathf.Clamp(surveySpeedMS, 0.04f, 0.25f);
        }

        if (selectedSpeedProfile == MiniROVSpeedProfile.FastTransit)
        {
            return Mathf.Clamp(transitSpeedMS, 0.05f, 0.35f);
        }

        if (selectedPathType == MiniROVPathType.GoToPoint ||
            selectedPathType == MiniROVPathType.ReturnHome)
        {
            return Mathf.Clamp(transitSpeedMS, 0.05f, 0.35f);
        }

        if (selectedPathType == MiniROVPathType.CircleInspection ||
            selectedPathType == MiniROVPathType.SpiralSearch ||
            selectedPathType == MiniROVPathType.HelixInspection ||
            selectedPathType == MiniROVPathType.FigureEight)
        {
            return Mathf.Clamp(inspectionSpeedMS, 0.03f, 0.20f);
        }

        if (selectedPathType == MiniROVPathType.LineOutAndBack ||
            selectedPathType == MiniROVPathType.Square ||
            selectedPathType == MiniROVPathType.RectangleSurvey ||
            selectedPathType == MiniROVPathType.LawnmowerSurvey)
        {
            return Mathf.Clamp(surveySpeedMS, 0.04f, 0.25f);
        }

        return Mathf.Clamp(inspectionSpeedMS, 0.03f, 0.20f);
    }

    private void ApplyCompletionSettingsToController()
    {
        if (controller == null)
        {
            return;
        }

        bool depthVarying =
            selectedPathType == MiniROVPathType.HelixInspection;

        controller.polylineArrivalRadiusM =
            depthVarying
                ? Mathf.Max(0.05f, depthVaryingPolylineArrivalRadiusM)
                : Mathf.Max(0.05f, defaultPolylineArrivalRadiusM);
    }

    private void ApplyYawPolicyToController()
    {
        if (controller == null)
        {
            return;
        }

        ApplyMissionSpeedToController();

        controller.allowIndependentYawWhileMoving =
            allowIndependentYawWhileMoving;

        controller.hasFinalYawReference =
            useFinalYawAtCompletion;

        if (useCurrentYawAsFinalYaw)
        {
            controller.finalYawDeg =
                transform.eulerAngles.y;
        }
        else
        {
            controller.finalYawDeg =
                finalYawDeg;
        }

        controller.fixedYawDeg =
            fixedYawDeg;

        controller.yawLookAtPointWorld =
            yawLookAtPointWorld;

        if (selectedYawPolicy == MiniROVYawPolicy.TravelHeading ||
            selectedYawPolicy == MiniROVYawPolicy.CircleTangent)
        {
            controller.yawReferenceMode =
                MIMISKMiniROVPlantBasedController.YawReferenceMode.TravelHeading;

            return;
        }

        if (selectedYawPolicy == MiniROVYawPolicy.FixedYaw)
        {
            controller.yawReferenceMode =
                MIMISKMiniROVPlantBasedController.YawReferenceMode.FixedYaw;

            if (useFinalYawAtCompletion)
            {
                controller.finalYawDeg =
                    fixedYawDeg;
            }

            return;
        }

        if (selectedYawPolicy == MiniROVYawPolicy.FaceHome)
        {
            controller.yawReferenceMode =
                MIMISKMiniROVPlantBasedController.YawReferenceMode.FacePoint;

            controller.yawLookAtPointWorld =
                homeWorld;

            if (useFinalYawAtCompletion)
            {
                controller.finalYawDeg =
                    ComputeYawToPoint(homeWorld);
            }

            return;
        }

        if (selectedYawPolicy == MiniROVYawPolicy.FacePoint ||
            selectedYawPolicy == MiniROVYawPolicy.FaceCircleCenterAfterCompletion)
        {
            controller.yawReferenceMode =
                MIMISKMiniROVPlantBasedController.YawReferenceMode.FacePoint;

            controller.yawLookAtPointWorld =
                yawLookAtPointWorld;

            if (useFinalYawAtCompletion)
            {
                controller.finalYawDeg =
                    ComputeYawToPoint(yawLookAtPointWorld);
            }

            return;
        }

        if (selectedYawPolicy == MiniROVYawPolicy.FinalYawOnly)
        {
            controller.yawReferenceMode =
                MIMISKMiniROVPlantBasedController.YawReferenceMode.FinalYawOnly;

            controller.hasFinalYawReference =
                true;

            controller.finalYawDeg =
                useCurrentYawAsFinalYaw
                    ? transform.eulerAngles.y
                    : finalYawDeg;

            return;
        }

        controller.yawReferenceMode =
            MIMISKMiniROVPlantBasedController.YawReferenceMode.TravelHeading;
    }

    private float ComputeYawToPoint(Vector3 point)
    {
        Vector3 p =
            rb != null
                ? rb.position
                : transform.position;

        float dx =
            point.x - p.x;

        float dz =
            point.z - p.z;

        if (Mathf.Sqrt(dx * dx + dz * dz) < 0.01f)
        {
            return transform.eulerAngles.y;
        }

        return Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
    }

    private float EstimateLength(Vector3[] path)
    {
        if (path == null || path.Length < 2)
        {
            return 0.0f;
        }

        float length =
            0.0f;

        for (int i = 0; i < path.Length - 1; i++)
        {
            length +=
                Vector3.Distance(path[i], path[i + 1]);
        }

        return length;
    }

    private Vector3 CurrentPositionAtDepth(float depth)
    {
        Vector3 p =
            rb != null
                ? rb.position
                : transform.position;

        p.y =
            DepthToWorldY(depth);

        return p;
    }

    private Vector3 WithDepth(Vector3 point, float depth)
    {
        point.y =
            DepthToWorldY(depth);

        return point;
    }

    private Vector3 HorizontalForward()
    {
        Vector3 f =
            transform.forward;

        f.y =
            0.0f;

        if (f.sqrMagnitude < 0.0001f)
        {
            f =
                Vector3.forward;
        }

        return f.normalized;
    }

    private Vector3 HorizontalRight()
    {
        Vector3 r =
            transform.right;

        r.y =
            0.0f;

        if (r.sqrMagnitude < 0.0001f)
        {
            r =
                Vector3.right;
        }

        return r.normalized;
    }

    private float CurrentDepth()
    {
        if (controller != null && controller.depthM > 0.001f)
        {
            return controller.depthM;
        }

        Vector3 p =
            rb != null
                ? rb.position
                : transform.position;

        float water =
            controller != null
                ? controller.waterLevelY
                : 0.0f;

        return water - p.y;
    }

    private float DepthToWorldY(float depth)
    {
        float water =
            controller != null
                ? controller.waterLevelY
                : 0.0f;

        bool positiveDown =
            controller == null ||
            controller.depthPositiveDown;

        return positiveDown
            ? water - depth
            : water + depth;
    }
}
