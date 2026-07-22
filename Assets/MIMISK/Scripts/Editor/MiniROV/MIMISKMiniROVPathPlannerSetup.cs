using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMiniROVPathPlannerSetup
{
    [MenuItem("MIMISK/MiniROV/Setup Path Planner")]
    public static void SetupPathPlanner()
    {
        GameObject miniRov =
            GameObject.Find("MiniROV");

        if (miniRov == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV.");
            return;
        }

        MIMISKMiniROVPathPlanner planner =
            miniRov.GetComponent<MIMISKMiniROVPathPlanner>();

        if (planner == null)
        {
            planner =
                miniRov.AddComponent<MIMISKMiniROVPathPlanner>();
        }

        planner.rb =
            miniRov.GetComponent<Rigidbody>();

        planner.controller =
            miniRov.GetComponent<MIMISKMiniROVPlantBasedController>();

        planner.selectedPathType =
            MIMISKMiniROVPathPlanner.MiniROVPathType.StationHold;

        planner.stopAndHoldAtEnd = true;
        planner.defaultPolylineArrivalRadiusM = 0.15f;
        planner.depthVaryingPolylineArrivalRadiusM = 0.25f;

        planner.selectedSpeedProfile =
            MIMISKMiniROVPathPlanner.MiniROVSpeedProfile.AutomaticByTask;

        planner.transitSpeedMS = 0.18f;
        planner.surveySpeedMS = 0.14f;
        planner.inspectionSpeedMS = 0.10f;
        planner.customMissionSpeedMS = 0.12f;

        planner.scaleLookaheadWithSpeed = true;
        planner.minLookaheadM = 0.25f;
        planner.maxLookaheadM = 0.60f;
        planner.lookaheadSpeedGain = 2.5f;

        planner.routeAlgorithm =
            MIMISKMiniROVPathPlanner.RouteAlgorithm.CurvatureLimitedHermite;
        planner.coverageAlgorithm =
            MIMISKMiniROVPathPlanner.CoverageAlgorithm.BoustrophedonLawnmower;
        planner.inspectionAlgorithm =
            MIMISKMiniROVPathPlanner.InspectionAlgorithm.OrbitCircle;

        planner.minimumTurnRadiusM = 0.35f;
        planner.pathSampleSpacingM = 0.15f;
        planner.smoothPolylineCorners = true;
        planner.cornerFilletRadiusM = 0.25f;
        planner.useCurrentDepthAtStart = true;
        planner.missionDepthM = 1.0f;

        planner.goToPointUseRoute = false;
        planner.goToPointUseRouteOnlyWhenTargetAhead = true;
        planner.goToPointRouteMaxInitialYawErrorDeg = 75.0f;
        planner.goToPointRouteMinDistanceM = 0.35f;

        planner.lineReturnStyle =
            MIMISKMiniROVPathPlanner.LineReturnStyle.RoundedUTurnLoop;
        planner.lineReturnOffsetM = 0.45f;
        planner.lineReturnMinOffsetM = 0.25f;

        planner.lineLengthM = 2.5f;
        planner.squareSideM = 1.2f;
        planner.rectangleLengthM = 3.0f;
        planner.rectangleWidthM = 1.5f;

        planner.lawnmowerLengthM = 3.0f;
        planner.lawnmowerWidthM = 1.5f;
        planner.lawnmowerLaneSpacingM = 0.35f;

        planner.circleRadiusM = 0.8f;
        planner.circleLaps = 1;
        planner.circleClockwise = true;
        planner.circleStartWithTangentForward = true;

        planner.spiralInitialRadiusM = 0.15f;
        planner.spiralFinalRadiusM = 1.2f;
        planner.spiralTurns = 3;
        planner.spiralSamplesPerTurn = 48;

        planner.helixRadiusM = 0.8f;
        planner.helixTurns = 2;
        planner.helixSamplesPerTurn = 48;
        planner.helixStartDepthM = 1.0f;
        planner.helixEndDepthM = 2.0f;

        planner.figureEightLengthM = 1.6f;
        planner.figureEightWidthM = 0.8f;

        planner.stopLookSourcePathType =
            MIMISKMiniROVPathPlanner.MiniROVPathType.LawnmowerSurvey;
        planner.stopLookYawPolicy =
            MIMISKMiniROVPathPlanner.StopLookYawPolicy.FacePathCenter;
        planner.stopLookWaypointSpacingM = 0.75f;
        planner.stopLookDwellSeconds = 3.0f;
        planner.stopLookFixedYawDeg = 0.0f;
        planner.stopLookUseHomeAsFacePoint = false;

        planner.figureEightSamples = 160;

        planner.AutoFindReferences();
        planner.SetHomeToCurrentPose();

        EditorUtility.SetDirty(planner);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] MiniROV path planner configured.");
    }
}
