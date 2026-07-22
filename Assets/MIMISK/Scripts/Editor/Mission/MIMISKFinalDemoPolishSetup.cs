using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKFinalDemoPolishSetup
{
    [MenuItem("MIMISK/Final Mission/Apply Final Demo Polish")]
    public static void ApplyFinalDemoPolish()
    {
        GameObject drone =
            GameObject.Find("Drone");

        GameObject miniRov =
            GameObject.Find("MiniROV");

        if (drone == null)
        {
            Debug.LogError("[MIMISK] Could not find Drone.");
            return;
        }

        if (miniRov == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV.");
            return;
        }

        MIMISKFinalMissionPlanner planner =
            drone.GetComponent<MIMISKFinalMissionPlanner>();

        if (planner != null)
        {
            // Avoid duplicate straight active tether from older planner visual.
            planner.enableActiveYellowTetherLine = false;
            EditorUtility.SetDirty(planner);
        }

        MIMISKDroneDeploymentSurfaceAnchor anchor =
            drone.GetComponent<MIMISKDroneDeploymentSurfaceAnchor>();

        if (anchor == null)
        {
            anchor = drone.AddComponent<MIMISKDroneDeploymentSurfaceAnchor>();
        }

        anchor.finalPlanner = planner;
        anchor.core = drone.GetComponent<MIMISKDroneCoreRotorController>();
        anchor.flightManager = drone.GetComponent<MIMISKDroneCoreFlightModeManager>();
        anchor.rb = drone.GetComponent<Rigidbody>();

        anchor.anchorEnabled = true;
        anchor.captureCurrentSurfacePose = true;
        anchor.cutMotorsWhileAnchored = true;
        anchor.forceSurfaceStableMode = true;

        anchor.verticalKp = 45.0f;
        anchor.verticalKd = 18.0f;
        anchor.maxVerticalForceN = 160.0f;
        anchor.horizontalDampingHz = 1.5f;
        anchor.angularDampingHz = 2.5f;

        EditorUtility.SetDirty(anchor);

        MIMISKMiniROVCollisionIsolation isolation =
            miniRov.GetComponent<MIMISKMiniROVCollisionIsolation>();

        if (isolation == null)
        {
            isolation = miniRov.AddComponent<MIMISKMiniROVCollisionIsolation>();
        }

        isolation.miniRovRoot = miniRov.transform;
        isolation.droneRoot = drone;
        isolation.isolationEnabled = true;
        isolation.ignoreDroneCollisions = true;
        isolation.ApplyIsolation();

        EditorUtility.SetDirty(isolation);

        MIMISKFinalContinuousTetherVisual tetherVisual =
            drone.GetComponent<MIMISKFinalContinuousTetherVisual>();

        if (tetherVisual == null)
        {
            tetherVisual = drone.AddComponent<MIMISKFinalContinuousTetherVisual>();
        }

        tetherVisual.finalPlanner = planner;
        tetherVisual.tetherStart =
            FindDeepChild(drone.transform, "WinchFairlead_for_Unity_LineRenderer_Start");

        if (tetherVisual.tetherStart == null)
        {
            tetherVisual.tetherStart =
                FindDeepChild(drone.transform, "TetherAnchor");
        }

        if (tetherVisual.tetherStart == null)
        {
            tetherVisual.tetherStart =
                FindDeepChild(drone.transform, "WinchPoint");
        }

        tetherVisual.rovTetherAnchor =
            FindDeepChild(miniRov.transform, "ROV_TetherAnchor");

        if (tetherVisual.rovTetherAnchor == null)
        {
            tetherVisual.rovTetherAnchor =
                FindDeepChild(miniRov.transform, "MiniROV_TetherPoint");
        }

        if (tetherVisual.rovTetherAnchor == null)
        {
            tetherVisual.rovTetherAnchor =
                FindDeepChild(miniRov.transform, "TetherPoint");
        }

        tetherVisual.hookVisual =
            FindDeepChild(drone.transform, "small_dark_open_deployment_hook_for_miniROV");

        tetherVisual.lineObjectName = "MIMISK_Final_ContinuousYellowTether";
        tetherVisual.segments = 28;
        tetherVisual.widthM = 0.022f;
        tetherVisual.baseSagM = 0.10f;
        tetherVisual.sagPerMeter = 0.055f;
        tetherVisual.maxSagM = 0.65f;
        tetherVisual.lateralCurveM = 0.025f;
        tetherVisual.activeOnlyDuringROVControl = true;
        tetherVisual.disableOtherTetherLineRenderers = true;
        tetherVisual.moveHookVisualToROVAnchor = true;
        tetherVisual.DeactivateVisual();

        EditorUtility.SetDirty(tetherVisual);

        MIMISKMiniROVModule module =
            miniRov.GetComponent<MIMISKMiniROVModule>();

        if (module != null)
        {
            module.enableSimpleRovBuoyancyInFinalStack = true;
            module.useGravityInControl = true;
            module.enableCollidersDuringControl = true;

            module.correctOrientationOnHandoff = true;
            module.freeSwimWorldEuler = Vector3.zero;
            module.preserveCurrentYaw = false;
            module.keepTetherAnchorLockedDuringOrientationFix = true;

            module.tetherVisualBridge = null;

            EditorUtility.SetDirty(module);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MIMISK] Final demo polish applied: surface anchor, MiniROV-drone collision isolation, and continuous sagging yellow tether visual."
        );
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found =
                FindDeepChild(root.GetChild(i), childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
