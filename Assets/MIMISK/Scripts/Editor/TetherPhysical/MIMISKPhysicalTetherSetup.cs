#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor installer for V6/V7 physical tether visuals.
/// V6 keeps the existing tether control/agents/mission stack and replaces only
/// the visible cable/solver with a CAD-yellow runtime cable driven by
/// Verlet-chain physical tether nodes.
/// </summary>
public static class MIMISKPhysicalTetherSetup
{
    [MenuItem("MIMISK/Tether/Install-Upgrade Physical Tether V7 ROV Rear Anchor Sync")]
    public static void InstallOrUpgradePhysicalTetherV7()
    {
        GameObject drone = FindDroneGameObject();
        GameObject miniRov = FindMiniRovGameObject();

        if (drone == null || miniRov == null)
        {
            EditorUtility.DisplayDialog(
                "MIMISK Physical Tether V7",
                "Could not find Drone and MiniROV in the active scene. Open 05_MiniROV_Finale_agent.unity, then run this installer again.",
                "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(drone, "Install/Upgrade MIMISK Physical Tether V7");
        Undo.RegisterFullObjectHierarchyUndo(miniRov, "Create MiniROV Rear Physical Tether Anchor");

        MIMISKDroneCoreTetherManager legacyTether = drone.GetComponent<MIMISKDroneCoreTetherManager>();
        MIMISKUnifiedTetherManager unifiedTether = drone.GetComponent<MIMISKUnifiedTetherManager>();
        MIMISKTetherSmartWinchController smartWinch = drone.GetComponent<MIMISKTetherSmartWinchController>();

        if (legacyTether != null)
        {
            legacyTether.AutoFindReferences();
        }

        if (unifiedTether != null)
        {
            unifiedTether.AutoFindReferences();
        }

        MIMISKMiniROVRearTetherAnchor rearProvider = miniRov.GetComponent<MIMISKMiniROVRearTetherAnchor>();
        if (rearProvider == null)
        {
            rearProvider = Undo.AddComponent<MIMISKMiniROVRearTetherAnchor>(miniRov);
        }

        ConfigureRearAnchorProvider(rearProvider);
        rearProvider.AutoPlaceNow();
        Transform rearAnchor = rearProvider.GetAnchorTransform();

        // V7.2 recovery: restore the original controller endpoints first.
        // The rear physical anchor is intentionally NOT written into the mission/winch managers.
        RestoreOriginalControllerEndpointReferences(drone, miniRov, legacyTether, unifiedTether, smartWinch);

        if (unifiedTether != null)
        {
            unifiedTether.adaptiveSlackManagement = true;
            unifiedTether.desiredOperationalSlackM = Mathf.Max(0.12f, unifiedTether.desiredOperationalSlackM);
            unifiedTether.slackDeadbandM = Mathf.Max(0.035f, unifiedTether.slackDeadbandM);
        }

        if (legacyTether != null)
        {
            legacyTether.enableTetherForceWhenMiniRovAttached = false;
        }

        MIMISKPhysicalTetherModel physical = drone.GetComponent<MIMISKPhysicalTetherModel>();
        if (physical == null)
        {
            physical = Undo.AddComponent<MIMISKPhysicalTetherModel>(drone);
        }
        ConfigurePhysicalModel(drone, physical, legacyTether, unifiedTether, rearAnchor, miniRov);

        MIMISKPhysicalTetherVisualizer visualizer = drone.GetComponent<MIMISKPhysicalTetherVisualizer>();
        if (visualizer == null)
        {
            visualizer = Undo.AddComponent<MIMISKPhysicalTetherVisualizer>(drone);
        }
        ConfigureVisualizer(drone, visualizer, physical);

        MIMISKPhysicalTetherSafetyGuard guard = drone.GetComponent<MIMISKPhysicalTetherSafetyGuard>();
        if (guard == null)
        {
            guard = Undo.AddComponent<MIMISKPhysicalTetherSafetyGuard>(drone);
        }
        ConfigureSafetyGuard(guard, physical, legacyTether, unifiedTether);

        MIMISKPhysicalTetherRovSyncMonitor sync = drone.GetComponent<MIMISKPhysicalTetherRovSyncMonitor>();
        if (sync == null)
        {
            sync = Undo.AddComponent<MIMISKPhysicalTetherRovSyncMonitor>(drone);
        }
        ConfigureRovSyncMonitor(sync, physical, rearProvider, rearAnchor, legacyTether, unifiedTether, smartWinch, miniRov);

        MIMISKPhysicalTetherResearchLogger logger = drone.GetComponent<MIMISKPhysicalTetherResearchLogger>();
        if (logger == null)
        {
            logger = Undo.AddComponent<MIMISKPhysicalTetherResearchLogger>(drone);
        }
        ConfigureLogger(logger, physical, guard, legacyTether, unifiedTether, smartWinch, drone);

        rearProvider.AssignReferencesNow();
        sync.ApplyRearAnchorReferencesNow();
        physical.AutoFindReferences();
        physical.RebuildCableFromCurrentEndpoints();
        visualizer.AutoFindReferences();
        visualizer.CopyMaterialFromWinchCableNow();
        visualizer.EstimateRadiusFromCadCableNow();
        visualizer.SuppressLegacyVisualsNow();

        DisableLegacyVisualDrivers(drone);
        HideOldRuntimeCableObjects();

        MarkDirty(drone, physical, visualizer, guard, logger, sync);
        MarkDirty(miniRov, rearProvider, rearAnchor != null ? rearAnchor.gameObject : null);

        string localText = rearProvider.anchorLocalPosition.ToString("F4");
        EditorUtility.DisplayDialog(
            "MIMISK Physical Tether V7",
            "Installed V7.4 BoxCollider rear-anchor sync. Original deployment/control endpoints were restored, and the new rear BoxCollider anchor is used only by the physical tether visual/model: " + localText + ". Force coupling remains OFF. The MiniROV should again stay attached to the drone cable on Play, deploy only a little into the water, then release normally on Activate Control.",
            "OK");
    }

    [MenuItem("MIMISK/Tether/V7 Recompute MiniROV Rear Anchor")]
    public static void RecomputeMiniRovRearAnchorV7()
    {
        GameObject miniRov = FindMiniRovGameObject();
        if (miniRov == null)
        {
            EditorUtility.DisplayDialog("MIMISK Physical Tether V7", "MiniROV object not found.", "OK");
            return;
        }

        MIMISKMiniROVRearTetherAnchor rearProvider = miniRov.GetComponent<MIMISKMiniROVRearTetherAnchor>();
        if (rearProvider == null)
        {
            rearProvider = Undo.AddComponent<MIMISKMiniROVRearTetherAnchor>(miniRov);
        }

        Undo.RecordObject(rearProvider, "Recompute MiniROV Rear Tether Anchor");
        ConfigureRearAnchorProvider(rearProvider);
        rearProvider.AutoPlaceNow();
        rearProvider.AssignReferencesNow();
        EditorUtility.SetDirty(rearProvider);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(miniRov.scene);

        EditorUtility.DisplayDialog("MIMISK Physical Tether V7", "Rear tether anchor recomputed at local " + rearProvider.anchorLocalPosition.ToString("F4") + ".", "OK");
    }

    [MenuItem("MIMISK/Tether/V7 Diagnose ROV Anchor And Tether Sync")]
    public static void DiagnoseV7AnchorAndSync()
    {
        Debug.Log("[MIMISK Physical Tether V7] Rear-anchor diagnostic start");

        MIMISKMiniROVRearTetherAnchor rear = Object.FindFirstObjectByType<MIMISKMiniROVRearTetherAnchor>();
        if (rear != null)
        {
            Transform anchor = rear.GetAnchorTransform();
            Debug.Log("[MIMISK Physical Tether V7] anchor=" + (anchor != null ? GetHierarchyPath(anchor) : "missing") +
                      " local=" + rear.anchorLocalPosition.ToString("F4") +
                      " world=" + (anchor != null ? anchor.position.ToString("F4") : "missing") +
                      " bounds=" + rear.boundsSource +
                      " min=" + rear.boundsMinLocal.ToString("F4") +
                      " max=" + rear.boundsMaxLocal.ToString("F4") +
                      " front=" + rear.frontAxisEvidence +
                      " event=" + rear.lastEvent);
        }
        else
        {
            Debug.LogWarning("[MIMISK Physical Tether V7] No MIMISKMiniROVRearTetherAnchor found.");
        }

        MIMISKPhysicalTetherModel physical = Object.FindFirstObjectByType<MIMISKPhysicalTetherModel>();
        if (physical != null)
        {
            Debug.Log("[MIMISK Physical Tether V7] physical endpointMode=" + physical.endpointMode +
                      " endAnchor=" + (physical.endAnchor != null ? GetHierarchyPath(physical.endAnchor) : "missing") +
                      " endpointGap=" + physical.endpointAttachmentErrorM.ToString("F5") +
                      " childOfRov=" + physical.endAnchorIsChildOfMiniRov +
                      " local=" + physical.endAnchorLocalOnMiniRov.ToString("F4") +
                      " deployed=" + physical.deployedLengthM.ToString("F3") +
                      " straight=" + physical.straightDistanceM.ToString("F3") +
                      " slack=" + physical.slackM.ToString("F3") +
                      " state=" + physical.physicalState);
        }
        else
        {
            Debug.LogWarning("[MIMISK Physical Tether V7] No physical tether model found.");
        }

        MIMISKPhysicalTetherRovSyncMonitor sync = Object.FindFirstObjectByType<MIMISKPhysicalTetherRovSyncMonitor>();
        if (sync != null)
        {
            sync.UpdateDiagnostics();
            Debug.Log("[MIMISK Physical Tether V7] sync required=" + sync.requiredLengthM.ToString("F3") +
                      " desired=" + sync.desiredLengthWithSlackM.ToString("F3") +
                      " deployed=" + sync.currentDeployedLengthM.ToString("F3") +
                      " error=" + sync.lengthErrorM.ToString("F3") +
                      " endpointGap=" + sync.physicalEndAnchorGapM.ToString("F5") +
                      " lastNodeGap=" + sync.cableLastNodeGapM.ToString("F5") +
                      " endpointLocked=" + sync.endAnchorLockedToRear +
                      " lengthSync=" + sync.winchLengthSynchronizedToRov +
                      " action=" + sync.lastAction);
        }
        else
        {
            Debug.LogWarning("[MIMISK Physical Tether V7] No ROV sync monitor found.");
        }
    }

    [MenuItem("MIMISK/Tether/Install-Upgrade Physical Tether V6 CAD Yellow Verlet Cable")]
    public static void InstallOrUpgradePhysicalTetherV6()
    {
        GameObject drone = FindDroneGameObject();
        if (drone == null)
        {
            EditorUtility.DisplayDialog(
                "MIMISK Physical Tether V6",
                "Could not find the Drone object or MIMISK tether managers in the active scene.",
                "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(drone, "Install/Upgrade MIMISK Physical Tether V6");

        MIMISKDroneCoreTetherManager legacyTether = drone.GetComponent<MIMISKDroneCoreTetherManager>();
        MIMISKUnifiedTetherManager unifiedTether = drone.GetComponent<MIMISKUnifiedTetherManager>();
        MIMISKTetherSmartWinchController smartWinch = drone.GetComponent<MIMISKTetherSmartWinchController>();

        if (legacyTether != null)
        {
            legacyTether.AutoFindReferences();
        }

        MIMISKPhysicalTetherModel physical = drone.GetComponent<MIMISKPhysicalTetherModel>();
        if (physical == null)
        {
            physical = Undo.AddComponent<MIMISKPhysicalTetherModel>(drone);
        }
        ConfigurePhysicalModel(drone, physical, legacyTether, unifiedTether);

        MIMISKPhysicalTetherVisualizer visualizer = drone.GetComponent<MIMISKPhysicalTetherVisualizer>();
        if (visualizer == null)
        {
            visualizer = Undo.AddComponent<MIMISKPhysicalTetherVisualizer>(drone);
        }
        ConfigureVisualizer(drone, visualizer, physical);

        MIMISKPhysicalTetherSafetyGuard guard = drone.GetComponent<MIMISKPhysicalTetherSafetyGuard>();
        if (guard == null)
        {
            guard = Undo.AddComponent<MIMISKPhysicalTetherSafetyGuard>(drone);
        }
        ConfigureSafetyGuard(guard, physical, legacyTether, unifiedTether);

        MIMISKPhysicalTetherResearchLogger logger = drone.GetComponent<MIMISKPhysicalTetherResearchLogger>();
        if (logger == null)
        {
            logger = Undo.AddComponent<MIMISKPhysicalTetherResearchLogger>(drone);
        }
        ConfigureLogger(logger, physical, guard, legacyTether, unifiedTether, smartWinch, drone);

        DisableLegacyVisualDrivers(drone);
        HideOldRuntimeCableObjects();

        physical.AutoFindReferences();
        physical.RebuildCableFromCurrentEndpoints();
        visualizer.AutoFindReferences();
        visualizer.CopyMaterialFromWinchCableNow();
        visualizer.EstimateRadiusFromCadCableNow();
        visualizer.SuppressLegacyVisualsNow();

        MarkDirty(drone, physical, visualizer, guard, logger);

        EditorUtility.DisplayDialog(
            "MIMISK Physical Tether V6",
            "Installed V6 in safe monitor-only mode. The visible tether is now a smooth CAD-yellow runtime cable that copies the winch cable material, while the physical motion is driven by a Verlet-chain/PBD solver. Existing tether control, agents, mission logic, drone, MiniROV, ROS, and gRPC are untouched. Press Play and inspect MIMISK_PhysicalTether_CADYellowRuntimeCable.",
            "OK");
    }

    [MenuItem("MIMISK/Tether/Use V6 CAD Yellow Cable Visual Only")]
    public static void UseV6CadYellowVisualOnly()
    {
        GameObject drone = FindDroneGameObject();
        if (drone == null)
        {
            EditorUtility.DisplayDialog("MIMISK Physical Tether V6", "No Drone object found.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(drone, "Use V6 CAD Yellow Cable Visual Only");

        MIMISKPhysicalTetherModel physical = drone.GetComponent<MIMISKPhysicalTetherModel>();
        if (physical == null)
        {
            physical = Undo.AddComponent<MIMISKPhysicalTetherModel>(drone);
        }

        MIMISKPhysicalTetherVisualizer visualizer = drone.GetComponent<MIMISKPhysicalTetherVisualizer>();
        if (visualizer == null)
        {
            visualizer = Undo.AddComponent<MIMISKPhysicalTetherVisualizer>(drone);
        }

        ConfigureVisualizer(drone, visualizer, physical);
        DisableLegacyVisualDrivers(drone);
        HideOldRuntimeCableObjects();
        visualizer.AutoFindReferences();
        visualizer.CopyMaterialFromWinchCableNow();
        visualizer.EstimateRadiusFromCadCableNow();
        visualizer.SuppressLegacyVisualsNow();
        MarkDirty(drone, physical, visualizer);
    }

    [MenuItem("MIMISK/Tether/Rebuild Physical Tether V6 Now")]
    public static void RebuildPhysicalTetherNow()
    {
        MIMISKPhysicalTetherModel physical = Object.FindFirstObjectByType<MIMISKPhysicalTetherModel>();
        if (physical == null)
        {
            EditorUtility.DisplayDialog("MIMISK Physical Tether V6", "No MIMISKPhysicalTetherModel found. Run the installer first.", "OK");
            return;
        }

        Undo.RecordObject(physical, "Rebuild Physical Tether V6");
        physical.AutoFindReferences();
        physical.RebuildCableFromCurrentEndpoints();
        EditorUtility.SetDirty(physical);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(physical.gameObject.scene);
    }

    [MenuItem("MIMISK/Tether/Set Physical Tether V6 Monitor Only")]
    public static void SetMonitorOnly()
    {
        MIMISKPhysicalTetherModel physical = Object.FindFirstObjectByType<MIMISKPhysicalTetherModel>();
        if (physical == null)
        {
            EditorUtility.DisplayDialog("MIMISK Physical Tether V6", "No MIMISKPhysicalTetherModel found. Run the installer first.", "OK");
            return;
        }

        Undo.RecordObject(physical, "Set Physical Tether Monitor Only");
        physical.DisablePhysicalForceCoupling();
        physical.preventImpossibleShortCableInMonitorMode = true;

        MIMISKPhysicalTetherSafetyGuard guard = physical.GetComponent<MIMISKPhysicalTetherSafetyGuard>();
        if (guard != null)
        {
            Undo.RecordObject(guard, "Set Physical Tether Guard Monitor Only");
            guard.SetMonitorOnly();
            EditorUtility.SetDirty(guard);
        }

        EditorUtility.SetDirty(physical);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(physical.gameObject.scene);
    }

    [MenuItem("MIMISK/Tether/Enable Physical Tether V6 Force Coupling")]
    public static void EnableForceCoupling()
    {
        MIMISKPhysicalTetherModel physical = Object.FindFirstObjectByType<MIMISKPhysicalTetherModel>();
        if (physical == null)
        {
            EditorUtility.DisplayDialog("MIMISK Physical Tether V6", "No MIMISKPhysicalTetherModel found. Run the installer first.", "OK");
            return;
        }

        Undo.RecordObject(physical, "Enable Physical Tether Force Coupling");
        physical.preventImpossibleShortCableInMonitorMode = false;
        physical.EnablePhysicalForceCoupling();

        MIMISKPhysicalTetherSafetyGuard guard = physical.GetComponent<MIMISKPhysicalTetherSafetyGuard>();
        if (guard != null)
        {
            Undo.RecordObject(guard, "Enable Physical Tether Active Guard");
            guard.SetActiveWinchProtection();
            EditorUtility.SetDirty(guard);
        }

        EditorUtility.SetDirty(physical);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(physical.gameObject.scene);
    }

    [MenuItem("MIMISK/Tether/Diagnose Physical Tether V6 Renderers")]
    public static void DiagnosePhysicalTetherRenderers()
    {
        Debug.Log("[MIMISK Physical Tether V6] Renderer diagnostic start");

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            string path = GetHierarchyPath(r.transform);
            string lower = path.ToLowerInvariant();
            if (lower.Contains("tether") || lower.Contains("cable") || lower.Contains("yellow") || lower.Contains("rope") || lower.Contains("physicaltether") || lower.Contains("winch"))
            {
                Debug.Log("[MIMISK Physical Tether V6] Renderer enabled=" + r.enabled + " type=" + r.GetType().Name + " path=" + path + " material=" + (r.sharedMaterial != null ? r.sharedMaterial.name : "none"));
            }
        }

        LineRenderer[] lines = Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lines.Length; i++)
        {
            LineRenderer line = lines[i];
            if (line == null) continue;
            string path = GetHierarchyPath(line.transform);
            string lower = path.ToLowerInvariant();
            if (lower.Contains("tether") || lower.Contains("cable") || lower.Contains("yellow") || lower.Contains("rope") || lower.Contains("physicaltether"))
            {
                Debug.Log("[MIMISK Physical Tether V6] LineRenderer enabled=" + line.enabled + " width=" + line.widthMultiplier + " path=" + path);
            }
        }

        MIMISKPhysicalTetherModel physical = Object.FindFirstObjectByType<MIMISKPhysicalTetherModel>();
        if (physical != null)
        {
            Debug.Log("[MIMISK Physical Tether V6] endpointMode=" + physical.endpointMode +
                      " points=" + physical.CablePointCount +
                      " commandedLength=" + physical.commandedDeployedLengthM.ToString("F3") +
                      " deployedLength=" + physical.deployedLengthM.ToString("F3") +
                      " straightDistance=" + physical.straightDistanceM.ToString("F3") +
                      " slack=" + physical.slackM.ToString("F3") +
                      " sag=" + physical.sagDepthM.ToString("F3") +
                      " state=" + physical.physicalState);
        }

        MIMISKPhysicalTetherVisualizer visualizer = Object.FindFirstObjectByType<MIMISKPhysicalTetherVisualizer>();
        if (visualizer != null)
        {
            Debug.Log("[MIMISK Physical Tether V6] visual points=" + visualizer.renderedPointCount +
                      " renderedLength=" + visualizer.renderedCableLengthM.ToString("F3") +
                      " materialSource=" + visualizer.materialSourcePath +
                      " activeRadius=" + visualizer.activeVisualRadiusM.ToString("F4") +
                      " lastEvent=" + visualizer.lastEvent);
        }
    }


    [MenuItem("MIMISK/Tether/V7.2 Recover Original Logic And Safe Rear Anchor")]
    public static void RecoverOriginalLogicAndSafeRearAnchorV72()
    {
        GameObject drone = FindDroneGameObject();
        GameObject miniRov = FindMiniRovGameObject();

        if (drone == null || miniRov == null)
        {
            EditorUtility.DisplayDialog("MIMISK Physical Tether V7.2", "Could not find Drone and MiniROV in the active scene.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(drone, "Recover MIMISK tether control logic");
        Undo.RegisterFullObjectHierarchyUndo(miniRov, "Recover MiniROV tether rear anchor");

        MIMISKDroneCoreTetherManager legacyTether = drone.GetComponent<MIMISKDroneCoreTetherManager>();
        MIMISKUnifiedTetherManager unifiedTether = drone.GetComponent<MIMISKUnifiedTetherManager>();
        MIMISKTetherSmartWinchController smartWinch = drone.GetComponent<MIMISKTetherSmartWinchController>();

        RestoreOriginalControllerEndpointReferences(drone, miniRov, legacyTether, unifiedTether, smartWinch);

        MIMISKMiniROVRearTetherAnchor rearProvider = miniRov.GetComponent<MIMISKMiniROVRearTetherAnchor>();
        if (rearProvider == null)
        {
            rearProvider = Undo.AddComponent<MIMISKMiniROVRearTetherAnchor>(miniRov);
        }

        ConfigureRearAnchorProvider(rearProvider);
        rearProvider.AutoPlaceNow();
        Transform rearAnchor = rearProvider.GetAnchorTransform();

        MIMISKPhysicalTetherModel physical = drone.GetComponent<MIMISKPhysicalTetherModel>();
        if (physical == null)
        {
            physical = Undo.AddComponent<MIMISKPhysicalTetherModel>(drone);
        }
        ConfigurePhysicalModel(drone, physical, legacyTether, unifiedTether, rearAnchor, miniRov);
        physical.forceEndAnchorToRovBackAnchor = false;
        physical.useDeploymentCableEndpointWhenCableManaged = true;
        physical.preferRearMiniRovTetherAnchor = true;
        physical.rovBackAnchor = rearAnchor;
        physical.endAnchor = FindBestMiniRovTetherAnchor();
        physical.DisablePhysicalForceCoupling();

        MIMISKPhysicalTetherRovSyncMonitor sync = drone.GetComponent<MIMISKPhysicalTetherRovSyncMonitor>();
        if (sync != null)
        {
            sync.preserveExistingMissionLogic = true;
            sync.writeRearAnchorToControllersEveryFrame = false;
            sync.rearAnchorProvider = rearProvider;
            sync.rearAnchor = rearAnchor;
            sync.physicalTether = physical;
        }

        MIMISKPhysicalTetherVisualizer visualizer = drone.GetComponent<MIMISKPhysicalTetherVisualizer>();
        if (visualizer != null)
        {
            visualizer.physicalTether = physical;
            visualizer.SuppressLegacyVisualsNow();
        }

        physical.AutoFindReferences();
        physical.RebuildCableFromCurrentEndpoints();

        MarkDirty(drone, physical, visualizer, sync);
        MarkDirty(miniRov, rearProvider, rearAnchor != null ? rearAnchor.gameObject : null);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(drone.scene);

        EditorUtility.DisplayDialog("MIMISK Physical Tether V7.2", "Recovered original tether mission logic. Unified/legacy/smart winch controllers now use the original ROV_TetherAnchor again; the new BoxCollider rear anchor is physical-visual only. Press Play and test Deploy -> Activate ROV Control.", "OK");
    }

    private static void RestoreOriginalControllerEndpointReferences(
        GameObject drone,
        GameObject miniRov,
        MIMISKDroneCoreTetherManager legacyTether,
        MIMISKUnifiedTetherManager unifiedTether,
        MIMISKTetherSmartWinchController smartWinch)
    {
        Transform rovAnchor = miniRov != null ? FindDeepChild(miniRov.transform, "ROV_TetherAnchor") : null;
        Transform miniRovPoint = miniRov != null ? FindDeepChild(miniRov.transform, "MiniROV_TetherPoint") : null;
        Transform fairlead = drone != null ? FindDeepChild(drone.transform, "WinchFairlead_for_Unity_LineRenderer_Start") : null;
        Transform yellowCableEnd = drone != null ? FindDeepChild(drone.transform, "real_mesh_short_yellow_deployment_cable_to_hook") : null;
        if (yellowCableEnd == null && drone != null)
        {
            yellowCableEnd = FindDeepChild(drone.transform, "MiniROV_CableEndFollowRoot");
        }
        Transform cableEndFollowRoot = drone != null ? FindDeepChild(drone.transform, "MiniROV_CableEndFollowRoot") : null;
        LineRenderer activeLine = null;
        Transform activeLineTransform = drone != null ? FindDeepChild(drone.transform, "MiniROV_ActiveYellowTetherLine") : null;
        if (activeLineTransform != null)
        {
            activeLine = activeLineTransform.GetComponent<LineRenderer>();
        }

        if (unifiedTether != null)
        {
            unifiedTether.miniRovRoot = miniRov != null ? miniRov.transform : unifiedTether.miniRovRoot;
            unifiedTether.miniRovRigidbody = miniRov != null ? miniRov.GetComponent<Rigidbody>() : unifiedTether.miniRovRigidbody;
            unifiedTether.miniRovTetherAnchorName = "ROV_TetherAnchor";
            unifiedTether.miniRovTetherAnchor = rovAnchor != null ? rovAnchor : (miniRovPoint != null ? miniRovPoint : unifiedTether.miniRovTetherAnchor);
            unifiedTether.fairleadLineStart = fairlead != null ? fairlead : unifiedTether.fairleadLineStart;
            unifiedTether.yellowCableEndPoint = yellowCableEnd != null ? yellowCableEnd : unifiedTether.yellowCableEndPoint;
            unifiedTether.cableEndFollowRoot = cableEndFollowRoot != null ? cableEndFollowRoot : unifiedTether.cableEndFollowRoot;
            unifiedTether.activeYellowLine = activeLine != null ? activeLine : unifiedTether.activeYellowLine;
        }

        if (legacyTether != null)
        {
            legacyTether.AutoFindReferences();
            legacyTether.fairleadLineStart = fairlead != null ? fairlead : legacyTether.fairleadLineStart;
            legacyTether.movingTetherEndVisual = yellowCableEnd != null ? yellowCableEnd : legacyTether.movingTetherEndVisual;
            legacyTether.tetherLineRenderer = activeLine != null ? activeLine : legacyTether.tetherLineRenderer;
            legacyTether.miniRovTetherPoint = null;
            legacyTether.miniRovRigidbody = null;
            legacyTether.useVirtualEndpointWhenNoMiniRov = true;
            legacyTether.enableTetherForceWhenMiniRovAttached = false;
        }

        if (smartWinch != null)
        {
            smartWinch.miniRovRigidbody = miniRov != null ? miniRov.GetComponent<Rigidbody>() : smartWinch.miniRovRigidbody;
            smartWinch.miniRovTetherPoint = rovAnchor != null ? rovAnchor : (miniRovPoint != null ? miniRovPoint : smartWinch.miniRovTetherPoint);
        }
    }

    private static void ConfigurePhysicalModel(
        GameObject drone,
        MIMISKPhysicalTetherModel physical,
        MIMISKDroneCoreTetherManager legacyTether,
        MIMISKUnifiedTetherManager unifiedTether,
        Transform rearAnchor = null,
        GameObject miniRov = null)
    {
        physical.autoFindReferences = true;
        physical.modelEnabled = true;
        physical.simulateInEditMode = false;
        physical.endpointSource = MIMISKPhysicalTetherModel.EndpointSource.AutoFromExistingTetherManagers;
        physical.unifiedTether = unifiedTether;
        physical.tetherManager = legacyTether;
        physical.droneRigidbody = drone.GetComponent<Rigidbody>();
        physical.miniRovRigidbody = miniRov != null ? miniRov.GetComponent<Rigidbody>() : null;

        physical.solverMode = MIMISKPhysicalTetherModel.SolverMode.VerletChain;
        physical.verletVelocityDamping = 0.965f;
        physical.verletEndpointVelocityInheritance = 0.55f;
        physical.useStrictEndpointLocks = true;
        physical.verletAdditionalDrag = 0.35f;
        physical.reduceCatenaryGuideInVerletMode = true;

        physical.nodeCount = 16;
        physical.constraintIterations = 80;
        physical.physicsSubsteps = 3;
        physical.segmentConstraintStiffness = 0.96f;
        physical.enforceSegmentLengthOnCompression = true;
        physical.maxCompressionStrain = 0.03f;
        physical.maxElasticStrain = 0.02f;
        physical.useAdaptiveNodeCount = true;
        physical.minimumAdaptiveNodeCount = 10;
        physical.maximumAdaptiveNodeCount = 96;
        physical.targetSegmentLengthM = 0.14f;
        physical.adaptiveNodeHysteresis = 4;
        physical.maximumNodeSpeedMS = 2.8f;

        physical.useDeploymentCableEndpointWhenCableManaged = true;
        physical.deploymentCableEndpoint = FindDeepChildContains(drone.transform, "real_mesh_short_yellow_deployment_cable_to_hook");
        physical.preferRearMiniRovTetherAnchor = true;
        physical.forceEndAnchorToRovBackAnchor = false;
        physical.rearMiniRovTetherAnchorName = "MIMISK_Tether_BackAnchor";
        physical.rearMiniRovTetherAnchorFallbackName1 = "ROV_TetherAnchor";
        physical.rearMiniRovTetherAnchorFallbackName2 = "MiniROV_TetherPoint";
        physical.rovBackAnchor = rearAnchor;
        physical.createRovBackAnchorIfMissing = true;
        physical.fallbackRovBackAnchorLocal = new Vector3(0.0f, 0.012f, -0.036f);

        physical.useCatenaryShapeGuide = true;
        physical.catenaryGuideStrength = 0.16f;
        physical.catenaryVelocityDamping = 0.40f;
        physical.slackToSagScale = 0.30f;
        physical.maximumCatenarySagM = 0.34f;
        physical.maximumCurrentLateralBendM = 0.05f;
        physical.currentLateralBendPerSlackM = 0.03f;
        physical.useBendingSmoothing = true;
        physical.bendingSmoothingIterations = 2;
        physical.bendingSmoothingStrength = 0.10f;
        physical.maximumBendingCorrectionM = 0.018f;

        physical.cableDiameterM = 0.008f;
        physical.massPerMeterKg = 0.045f;
        physical.axialStiffnessNPerM = 80.0f;
        physical.axialDampingNPerMS = 2.8f;
        physical.internalLinearDamping = 0.70f;
        physical.readWaterSurfaceFromUnifiedTether = true;
        physical.fallbackWaterSurfaceY = 0.0f;
        physical.waterCurrentWorldMS = new Vector3(0.015f, 0.0f, 0.004f);

        physical.applyForcesToMiniRov = false;
        physical.applyForcesToDrone = false;
        physical.writeCompatibilityMetricsToTetherManager = false;
        physical.preventImpossibleShortCableInMonitorMode = true;
        physical.monitorModeMinimumSlackM = 0.12f;
        physical.debiasMonitorOnlyTension = true;
        physical.monitorTautSlackForTensionM = 0.025f;
        physical.monitorSelfWeightTensionScale = 0.45f;
        physical.driveLineRenderer = false;
        physical.createLineRendererIfMissing = false;
        physical.lineRenderer = null;
        physical.overTensionThresholdN = 35.0f;
        physical.tautTensionThresholdN = 1.5f;
        physical.slackClassificationThresholdM = 0.05f;

        if (legacyTether != null)
        {
            physical.startAnchor = legacyTether.fairleadLineStart != null
                ? legacyTether.fairleadLineStart
                : (legacyTether.tetherAnchor != null ? legacyTether.tetherAnchor : legacyTether.winchPoint);

            if (physical.miniRovRigidbody == null)
            {
                physical.miniRovRigidbody = legacyTether.miniRovRigidbody;
            }

            legacyTether.enableTetherForceWhenMiniRovAttached = false;
        }

        if (unifiedTether != null)
        {
            if (physical.startAnchor == null)
            {
                physical.startAnchor = unifiedTether.fairleadLineStart;
            }

            if (physical.miniRovRigidbody == null)
            {
                physical.miniRovRigidbody = unifiedTether.miniRovRigidbody;
            }
        }

        Transform chosenEnd = null;
        if (unifiedTether != null && unifiedTether.miniRovTetherAnchor != null)
        {
            chosenEnd = unifiedTether.miniRovTetherAnchor;
        }

        if (chosenEnd == null)
        {
            chosenEnd = FindBestMiniRovTetherAnchor();
        }

        physical.endAnchor = chosenEnd;
        if (rearAnchor != null)
        {
            physical.rovBackAnchor = rearAnchor;
        }
    }

    private static void ConfigureRearAnchorProvider(MIMISKMiniROVRearTetherAnchor rear)
    {
        rear.anchorEnabled = true;
        rear.anchorName = "MIMISK_Tether_BackAnchor";
        rear.createIfMissing = true;
        rear.forceAnchorChildOfMiniRov = true;
        rear.autoPlaceFromMiniRovBounds = true;
        rear.preferRootBoxCollider = true;
        rear.preferBodyModelBounds = true;
        rear.useRendererBounds = true;
        rear.useColliderBoundsIfNoRenderer = true;
        rear.includeInactiveChildren = true;
        rear.ignoreCableTetherAndDebugRenderers = true;
        rear.preferredBodyModelName = "Body_Model";
        rear.rearAxisMode = MIMISKMiniROVRearTetherAnchor.RearAxisMode.AutoFromFrontCamera;
        rear.frontCameraName = "FrontCamera";
        // V7.4: use the MiniROV root BoxCollider rear face instead of a manual top/center anchor.
        // With BoxCollider center=(0,0,0.125), size=(0.131,0.07,0.31), and +Z as the front,
        // this gives approximately local=(0, 0.0105, -0.036).
        rear.useManualLocalPosition = false;
        rear.useRootBoxColliderRearFacePlacement = true;
        rear.boxLocalXFractionFromCenter = 0.0f;
        rear.boxLocalYFractionFromCenter = 0.15f;
        rear.boxRearFaceClearanceM = 0.006f;
        rear.keepAnchorOnColliderRearFace = false;
        rear.fallbackLocalPosition = new Vector3(0.0f, 0.012f, -0.036f);
        rear.manualLocalPosition = rear.fallbackLocalPosition;
        rear.rearOutsideClearanceM = 0.006f;
        rear.topOutsideClearanceM = 0.0f;
        rear.xOffsetM = 0.0f;
        rear.yOffsetM = 0.0f;
        rear.zOffsetM = 0.0f;
        rear.maximumAbsLocalZ = 0.60f;
        rear.maximumAbsLocalY = 0.35f;
        rear.maintainAnchorEveryFrame = true;
        rear.assignReferencesEveryFrame = false;
        rear.preserveExistingMissionLogic = true;
        rear.writeToUnifiedTetherManager = false;
        rear.writeToDroneCoreTetherManager = false;
        rear.writeToSmartWinchController = false;
        rear.writeToPhysicalTetherModel = true;
        rear.writeToRovSyncMonitor = true;
    }

    private static void ConfigureRovSyncMonitor(
        MIMISKPhysicalTetherRovSyncMonitor sync,
        MIMISKPhysicalTetherModel physical,
        MIMISKMiniROVRearTetherAnchor rearProvider,
        Transform rearAnchor,
        MIMISKDroneCoreTetherManager legacyTether,
        MIMISKUnifiedTetherManager unifiedTether,
        MIMISKTetherSmartWinchController smartWinch,
        GameObject miniRov)
    {
        sync.autoFindReferences = true;
        sync.monitorEnabled = true;
        sync.physicalTether = physical;
        sync.rearAnchorProvider = rearProvider;
        sync.rearAnchor = rearAnchor;
        sync.unifiedTether = unifiedTether;
        sync.tetherManager = legacyTether;
        sync.smartWinch = smartWinch;
        sync.miniRovRigidbody = miniRov != null ? miniRov.GetComponent<Rigidbody>() : null;
        sync.preserveExistingMissionLogic = true;
        sync.writeRearAnchorToControllersEveryFrame = false;
        sync.desiredSlackM = 0.12f;
        sync.lengthSyncToleranceM = 0.22f;
        sync.endpointGapToleranceM = 0.010f;
    }

    private static void ConfigureVisualizer(GameObject drone, MIMISKPhysicalTetherVisualizer visualizer, MIMISKPhysicalTetherModel physical)
    {
        visualizer.autoFindReferences = true;
        visualizer.visualizerEnabled = true;
        visualizer.physicalTether = physical;
        visualizer.renderMode = MIMISKPhysicalTetherVisualizer.RuntimeCableRenderMode.JacketedWinchCable;
        visualizer.tubeObjectName = "MIMISK_PhysicalTether_CADYellowRuntimeCable";
        visualizer.radialSegments = 16;
        visualizer.visualRadiusM = 0.0038f;
        visualizer.minimumVisibleRadiusM = 0.0023f;
        visualizer.enableSubtleJacketDesign = true;
        visualizer.jacketDesignRidgeCount = 3;
        visualizer.jacketDesignRidgeDepthM = 0.00006f;
        visualizer.jacketDesignPitchM = 0.0f;
        visualizer.estimateRadiusFromCadCable = true;
        visualizer.estimatedRadiusScale = 0.85f;
        visualizer.minimumEstimatedRadiusM = 0.0020f;
        visualizer.maximumEstimatedRadiusM = 0.0048f;
        visualizer.enableBraidedSurface = false;
        visualizer.braidStrandCount = 0;
        visualizer.braidStrandRadialSegments = 4;
        visualizer.braidStrandRadiusM = 0.00035f;
        visualizer.braidStrandCenterRadiusM = 0.0f;
        visualizer.braidPitchM = 0.120f;
        visualizer.animateBraidWithPayout = false;
        visualizer.useSmoothedRenderPath = true;
        visualizer.renderSubdivisionsPerSegment = 4;
        visualizer.renderPathSmoothingPasses = 1;
        visualizer.renderPathSmoothingStrength = 0.10f;
        visualizer.showLineFallback = false;
        visualizer.useWinchCableMaterial = true;
        visualizer.cloneSourceMaterial = true;
        visualizer.searchMaterialSourceEveryFewSeconds = true;
        visualizer.winchCableMaterialSource = FindBestWinchCableMaterialSource(drone.transform);
        visualizer.cableColor = new Color(1.0f, 0.74f, 0.08f, 1.0f);
        visualizer.colorByPhysicalState = false;
        visualizer.enableEmission = false;
        visualizer.emissionIntensity = 0.0f;
        visualizer.forceMatteMaterialProperties = true;
        visualizer.fallbackSmoothness = 0.28f;
        visualizer.suppressLegacyTetherVisuals = true;
        visualizer.suppressEveryFrame = true;
        visualizer.disableLegacyLineRenderers = true;
        visualizer.disableLegacyMeshRenderers = true;
        visualizer.disableLegacyVisualComponents = true;
        visualizer.disableStalePhysicalTetherRenderers = true;
        visualizer.keepWinchSpoolMeshesVisible = true;
        visualizer.hideLegacyShortDeploymentCableMeshes = true;
        visualizer.keepHookVisualVisible = true;
        visualizer.detectLegacyRenderersByParentPath = true;
    }

    private static void ConfigureSafetyGuard(
        MIMISKPhysicalTetherSafetyGuard guard,
        MIMISKPhysicalTetherModel physical,
        MIMISKDroneCoreTetherManager legacyTether,
        MIMISKUnifiedTetherManager unifiedTether)
    {
        guard.autoFindReferences = true;
        guard.guardEnabled = true;
        guard.actionMode = MIMISKPhysicalTetherSafetyGuard.GuardActionMode.MonitorOnly;
        guard.physicalTether = physical;
        guard.unifiedTether = unifiedTether;
        guard.tetherManager = legacyTether;
        guard.warningTensionN = 22.0f;
        guard.criticalTensionN = 35.0f;
        guard.minimumSlackM = 0.05f;
        guard.maximumSlackM = 2.0f;
        guard.emergencyPayoutSlackM = 0.25f;
    }

    private static void ConfigureLogger(
        MIMISKPhysicalTetherResearchLogger logger,
        MIMISKPhysicalTetherModel physical,
        MIMISKPhysicalTetherSafetyGuard guard,
        MIMISKDroneCoreTetherManager legacyTether,
        MIMISKUnifiedTetherManager unifiedTether,
        MIMISKTetherSmartWinchController smartWinch,
        GameObject drone)
    {
        logger.autoFindReferences = true;
        logger.enableLogging = false;
        logger.logHz = 30.0f;
        logger.physicalTether = physical;
        logger.safetyGuard = guard;
        logger.unifiedTether = unifiedTether;
        logger.tetherManager = legacyTether;
        logger.smartWinch = smartWinch;
        logger.rovSyncMonitor = drone.GetComponent<MIMISKPhysicalTetherRovSyncMonitor>();
        logger.droneRigidbody = drone.GetComponent<Rigidbody>();
        logger.miniRovRigidbody = unifiedTether != null ? unifiedTether.miniRovRigidbody : (legacyTether != null ? legacyTether.miniRovRigidbody : null);
    }

    private static void DisableLegacyVisualDrivers(GameObject drone)
    {
        if (drone == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour b = behaviours[i];
            if (b == null)
            {
                continue;
            }

            string typeName = b.GetType().Name;
            if (typeName == "MIMISKPhysicalTetherModel" || typeName == "MIMISKPhysicalTetherVisualizer" || typeName == "MIMISKPhysicalTetherSafetyGuard" || typeName == "MIMISKPhysicalTetherResearchLogger")
            {
                continue;
            }

            if (typeName == "MIMISKFinalTetherEndpointRig")
            {
                Undo.RecordObject(b, "Disable Legacy Endpoint Line Drawing");
                SetBoolFieldOrProperty(b, "driveActiveYellowLine", false);
                EditorUtility.SetDirty(b);
                continue;
            }

            if (IsLegacyVisualComponent(typeName))
            {
                Undo.RecordObject(b, "Disable Legacy Tether Visual Component");
                SetBoolFieldOrProperty(b, "visualAuthorityEnabled", false);
                SetBoolFieldOrProperty(b, "visualEnabled", false);
                SetBoolFieldOrProperty(b, "cableModelEnabled", false);
                SetBoolFieldOrProperty(b, "synchronizerEnabled", false);
                b.enabled = false;
                EditorUtility.SetDirty(b);
            }
        }

        LineRenderer[] lines = Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lines.Length; i++)
        {
            LineRenderer line = lines[i];
            if (line == null)
            {
                continue;
            }

            string n = GetHierarchyPath(line.transform).ToLowerInvariant();
            bool oldTetherLine = n.Contains("activeyellowtether") || n.Contains("continuousyellowtether") || n.Contains("yellowtetherline") || n.Contains("tether_line") || n.Contains("winchrope") || n.Contains("cablemodel");
            bool physicalLine = n.Contains("physicaltether") || n.Contains("cadyellowruntimecable") || n.Contains("winchmatchedruntimecable");
            if (oldTetherLine && !physicalLine)
            {
                Undo.RecordObject(line, "Disable Legacy Tether Line Renderer");
                line.enabled = false;
                EditorUtility.SetDirty(line);
            }
        }

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
            {
                continue;
            }

            string n = GetHierarchyPath(r.transform).ToLowerInvariant();
            bool physical = n.Contains("physicaltether") || n.Contains("cadyellowruntimecable") || n.Contains("winchmatchedruntimecable");
            bool oldShortCable = n.Contains("real_mesh_short_yellow_deployment_cable_to_hook") || n.Contains("short_yellow_deployment_cable") || n.Contains("yellow_deployment_cable");
            bool keepHook = n.Contains("small_dark_open_deployment_hook_for_minirov");
            bool keepWinchCable = n.Contains("real_mesh_yellow_cable_layer_1_wrapped_on_integrated_winch") ||
                                  n.Contains("real_mesh_yellow_cable_layer_2_wrapped_on_integrated_winch") ||
                                  n.Contains("real_mesh_yellow_cable_layer_3_wrapped_on_integrated_winch") ||
                                  n.Contains("real_mesh_yellow_cable_from_integrated_reel_to_fairlead");

            if (oldShortCable && !physical && !keepHook && !keepWinchCable)
            {
                Undo.RecordObject(r, "Hide Legacy Short Yellow Deployment Cable Mesh");
                r.enabled = false;
                EditorUtility.SetDirty(r);
            }
        }
    }

    private static bool IsLegacyVisualComponent(string typeName)
    {
        return
            typeName == "MIMISKSingleYellowTetherVisualAuthority" ||
            typeName == "MIMISKWinchRopeSingleVisual" ||
            typeName == "MIMISKTetherCableModel" ||
            typeName == "MIMISKFinalTetherLineSynchronizer" ||
            typeName == "MIMISKMiniROVTetherVisualBridge" ||
            typeName == "MIMISKFinalContinuousTetherVisual" ||
            typeName == "MIMISKTetherVisual" ||
            typeName == "MIMISKFinalTetherVisualBridge";
    }

    private static void HideOldRuntimeCableObjects()
    {
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            string n = GetHierarchyPath(r.transform).ToLowerInvariant();
            bool stalePhysical = n.Contains("mimisk_physicaltether") && !n.Contains("cadyellowruntimecable");
            if (stalePhysical)
            {
                Undo.RecordObject(r, "Hide Old Physical Tether Runtime Renderer");
                r.enabled = false;
                EditorUtility.SetDirty(r);
            }
        }
    }

    private static Transform FindBestWinchCableMaterialSource(Transform droneRoot)
    {
        if (droneRoot == null)
        {
            return null;
        }

        Transform t = FindDeepChildContains(droneRoot, "real_mesh_yellow_cable_from_integrated_reel_to_fairlead");
        if (t != null) return t;

        t = FindDeepChildContains(droneRoot, "real_mesh_short_yellow_deployment_cable_to_hook");
        if (t != null) return t;

        t = FindDeepChildContains(droneRoot, "real_mesh_yellow_cable_layer_3_wrapped_on_integrated_winch");
        if (t != null) return t;

        t = FindDeepChildContains(droneRoot, "real_mesh_yellow_cable_layer_2_wrapped_on_integrated_winch");
        if (t != null) return t;

        t = FindDeepChildContains(droneRoot, "real_mesh_yellow_cable_layer_1_wrapped_on_integrated_winch");
        if (t != null) return t;

        return null;
    }

    private static GameObject FindMiniRovGameObject()
    {
        GameObject rov = GameObject.Find("MiniROV");
        if (rov != null)
        {
            return rov;
        }

        MIMISKUnifiedTetherManager unified = Object.FindFirstObjectByType<MIMISKUnifiedTetherManager>();
        if (unified != null && unified.miniRovRigidbody != null)
        {
            return unified.miniRovRigidbody.gameObject;
        }

        MIMISKDroneCoreTetherManager tether = Object.FindFirstObjectByType<MIMISKDroneCoreTetherManager>();
        if (tether != null && tether.miniRovRigidbody != null)
        {
            return tether.miniRovRigidbody.gameObject;
        }

        Rigidbody[] bodies = Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null && bodies[i].name.ToLowerInvariant().Contains("minirov"))
            {
                return bodies[i].gameObject;
            }
        }

        return null;
    }

    private static GameObject FindDroneGameObject()
    {
        GameObject drone = GameObject.Find("Drone");
        if (drone != null)
        {
            return drone;
        }

        MIMISKDroneCoreTetherManager tether = Object.FindFirstObjectByType<MIMISKDroneCoreTetherManager>();
        if (tether != null)
        {
            return tether.gameObject;
        }

        MIMISKUnifiedTetherManager unified = Object.FindFirstObjectByType<MIMISKUnifiedTetherManager>();
        if (unified != null)
        {
            return unified.gameObject;
        }

        return null;
    }

    private static Transform FindBestMiniRovTetherAnchor()
    {
        GameObject rov = GameObject.Find("MiniROV");
        if (rov == null)
        {
            return null;
        }

        Transform t = FindDeepChild(rov.transform, "MiniROV_TetherPoint");
        if (t != null) return t;

        t = FindDeepChild(rov.transform, "ROV_TetherAnchor");
        if (t != null) return t;

        t = FindDeepChild(rov.transform, "TetherPoint");
        if (t != null) return t;

        return rov.transform;
    }

    private static void SetBoolFieldOrProperty(MonoBehaviour behaviour, string memberName, bool value)
    {
        if (behaviour == null)
        {
            return;
        }

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo field = behaviour.GetType().GetField(memberName, flags);
        if (field != null && field.FieldType == typeof(bool))
        {
            field.SetValue(behaviour, value);
            return;
        }

        PropertyInfo property = behaviour.GetType().GetProperty(memberName, flags);
        if (property != null && property.PropertyType == typeof(bool) && property.CanWrite)
        {
            property.SetValue(behaviour, value, null);
        }
    }

    private static void MarkDirty(GameObject sceneObject, params Object[] objects)
    {
        if (sceneObject != null)
        {
            EditorUtility.SetDirty(sceneObject);
        }

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
            {
                EditorUtility.SetDirty(objects[i]);
            }
        }

        if (sceneObject != null)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sceneObject.scene);
        }
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null)
        {
            return string.Empty;
        }

        string path = t.name;
        Transform p = t.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }

        return path;
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindDeepChildContains(Transform root, string namePart)
    {
        if (root == null || string.IsNullOrEmpty(namePart))
        {
            return null;
        }

        string target = namePart.ToLowerInvariant();
        if (root.name.ToLowerInvariant().Contains(target))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChildContains(root.GetChild(i), namePart);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
#endif
