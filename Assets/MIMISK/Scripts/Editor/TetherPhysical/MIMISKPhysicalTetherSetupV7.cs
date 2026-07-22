#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Compatibility wrapper for the V7 rear-anchor installer.
/// Earlier V7 repair builds had a separate SetupV7 class. This wrapper keeps
/// the menu item but delegates all installation/configuration to the maintained
/// MIMISKPhysicalTetherSetup implementation to avoid duplicate/inconsistent fields.
/// </summary>
public static class MIMISKPhysicalTetherSetupV7
{
    [MenuItem("MIMISK/Tether/Install-Upgrade Physical Tether V7 Rear ROV Anchor Sync")]
    public static void InstallOrUpgradeV7RearAnchorSync()
    {
        MIMISKPhysicalTetherSetup.InstallOrUpgradePhysicalTetherV7();
    }

    [MenuItem("MIMISK/Tether/V7 Recompute MiniROV Rear Anchor Now")]
    public static void RecomputeRearAnchorNow()
    {
        MIMISKMiniROVRearTetherAnchor rear = Object.FindFirstObjectByType<MIMISKMiniROVRearTetherAnchor>();
        if (rear == null)
        {
            GameObject rov = GameObject.Find("MiniROV");
            if (rov != null)
            {
                rear = rov.GetComponent<MIMISKMiniROVRearTetherAnchor>();
                if (rear == null) rear = Undo.AddComponent<MIMISKMiniROVRearTetherAnchor>(rov);
            }
        }

        if (rear == null)
        {
            EditorUtility.DisplayDialog("MIMISK Physical Tether V7", "MiniROV rear tether anchor provider was not found. Run the V7 installer first.", "OK");
            return;
        }

        Undo.RecordObject(rear, "Recompute MiniROV rear tether anchor");
        rear.AutoPlaceNow();
        rear.AssignReferencesNow();
        EditorUtility.SetDirty(rear);
        if (rear.anchor != null) EditorUtility.SetDirty(rear.anchor.gameObject);
        EditorUtility.DisplayDialog("MIMISK Physical Tether V7", "Rear tether anchor recomputed at local " + rear.anchorLocalPosition.ToString("F4"), "OK");
    }

    [MenuItem("MIMISK/Tether/V7 Diagnose ROV Anchor and Sync")]
    public static void DiagnoseRovAnchorAndSync()
    {
        MIMISKMiniROVRearTetherAnchor rear = Object.FindFirstObjectByType<MIMISKMiniROVRearTetherAnchor>();
        MIMISKPhysicalTetherRovSyncMonitor sync = Object.FindFirstObjectByType<MIMISKPhysicalTetherRovSyncMonitor>();
        MIMISKPhysicalTetherModel physical = Object.FindFirstObjectByType<MIMISKPhysicalTetherModel>();

        Debug.Log("[MIMISK V7 Diagnostics] rearProvider=" + (rear != null ? rear.name : "missing") +
                  " anchor=" + (rear != null && rear.anchor != null ? rear.anchor.name : "missing") +
                  " local=" + (rear != null ? rear.anchorLocalPosition.ToString("F4") : "n/a") +
                  " world=" + (rear != null ? rear.anchorWorldPosition.ToString("F4") : "n/a") +
                  " boundsSource=" + (rear != null ? rear.boundsSource : "n/a") +
                  " frontAxisEvidence=" + (rear != null ? rear.frontAxisEvidence : "n/a"));

        if (sync != null)
        {
            sync.AutoFindReferencesNow();
            sync.ApplyRearAnchorReferencesNow();
            sync.UpdateDiagnostics();
            Debug.Log("[MIMISK V7 Diagnostics] sync endpointLocked=" + sync.endAnchorLockedToRear +
                      " lastNodeLocked=" + sync.cableLastNodeLockedToRear +
                      " lengthOk=" + sync.winchLengthSynchronizedToRov +
                      " requiredLength=" + sync.requiredLengthM.ToString("F4") +
                      " deployedLength=" + sync.currentDeployedLengthM.ToString("F4") +
                      " lengthError=" + sync.lengthErrorM.ToString("F4") +
                      " endpointGap=" + sync.physicalEndAnchorGapM.ToString("F4") +
                      " nodeGap=" + sync.cableLastNodeGapM.ToString("F4") +
                      " action=" + sync.lastAction);
        }
        else
        {
            Debug.LogWarning("[MIMISK V7 Diagnostics] MIMISKPhysicalTetherRovSyncMonitor missing on Drone. Run installer.");
        }

        if (physical != null)
        {
            Debug.Log("[MIMISK V7 Diagnostics] physical endpointMode=" + physical.endpointMode +
                      " endAnchor=" + (physical.endAnchor != null ? physical.endAnchor.name : "missing") +
                      " endWorld=" + physical.endWorld.ToString("F4") +
                      " deployedLength=" + physical.deployedLengthM.ToString("F4") +
                      " slack=" + physical.slackM.ToString("F4") +
                      " strain=" + physical.maxSegmentStrain.ToString("F4"));
        }
    }

    [MenuItem("MIMISK/Tether/V7.4 Fix MiniROV Rear Anchor From BoxCollider")]
    public static void FixMiniRovRearAnchorFromBoxColliderV74()
    {
        GameObject drone = GameObject.Find("Drone");
        GameObject miniRov = GameObject.Find("MiniROV");
        if (drone == null || miniRov == null)
        {
            EditorUtility.DisplayDialog("MIMISK Physical Tether V7.4", "Could not find Drone and MiniROV in the active scene.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(drone, "V7.4 Fix physical tether rear endpoint");
        Undo.RegisterFullObjectHierarchyUndo(miniRov, "V7.4 Place MiniROV rear tether anchor");

        MIMISKMiniROVRearTetherAnchor rear = miniRov.GetComponent<MIMISKMiniROVRearTetherAnchor>();
        if (rear == null)
        {
            rear = Undo.AddComponent<MIMISKMiniROVRearTetherAnchor>(miniRov);
        }

        rear.anchorEnabled = true;
        rear.anchorName = "MIMISK_Tether_BackAnchor";
        rear.createIfMissing = true;
        rear.forceAnchorChildOfMiniRov = true;
        rear.useManualLocalPosition = false;
        rear.autoPlaceFromMiniRovBounds = true;
        rear.preferRootBoxCollider = true;
        rear.useRootBoxColliderRearFacePlacement = true;
        rear.rearAxisMode = MIMISKMiniROVRearTetherAnchor.RearAxisMode.AutoFromFrontCamera;
        rear.frontCameraName = "FrontCamera";
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
        rear.maintainAnchorEveryFrame = true;
        rear.assignReferencesEveryFrame = false;
        rear.preserveExistingMissionLogic = true;
        rear.writeToUnifiedTetherManager = false;
        rear.writeToDroneCoreTetherManager = false;
        rear.writeToSmartWinchController = false;
        rear.writeToPhysicalTetherModel = true;
        rear.writeToRovSyncMonitor = true;
        rear.AutoPlaceNow();
        Transform rearAnchor = rear.GetAnchorTransform();

        RestoreOriginalControllerReferencesV74(drone, miniRov);

        MIMISKPhysicalTetherModel physical = drone.GetComponent<MIMISKPhysicalTetherModel>();
        if (physical == null)
        {
            physical = Undo.AddComponent<MIMISKPhysicalTetherModel>(drone);
        }
        physical.preferRearMiniRovTetherAnchor = true;
        physical.forceEndAnchorToRovBackAnchor = false;
        physical.useDeploymentCableEndpointWhenCableManaged = true;
        physical.rearMiniRovTetherAnchorName = rear.anchorName;
        physical.rearMiniRovTetherAnchorFallbackName1 = "ROV_TetherAnchor";
        physical.rearMiniRovTetherAnchorFallbackName2 = "MiniROV_TetherPoint";
        physical.rovBackAnchor = rearAnchor;
        physical.endAnchor = rearAnchor;
        physical.fallbackRovBackAnchorLocal = new Vector3(0.0f, 0.012f, -0.036f);
        physical.DisablePhysicalForceCoupling();
        physical.AutoFindReferences();
        physical.RebuildCableFromCurrentEndpoints();

        MIMISKPhysicalTetherRovSyncMonitor sync = drone.GetComponent<MIMISKPhysicalTetherRovSyncMonitor>();
        if (sync != null)
        {
            sync.rearAnchorProvider = rear;
            sync.rearAnchor = rearAnchor;
            sync.physicalTether = physical;
            sync.preserveExistingMissionLogic = true;
            sync.writeRearAnchorToControllersEveryFrame = false;
            sync.ApplyRearAnchorReferencesNow();
            sync.UpdateDiagnostics();
        }

        MIMISKPhysicalTetherVisualizer vis = drone.GetComponent<MIMISKPhysicalTetherVisualizer>();
        if (vis != null)
        {
            vis.physicalTether = physical;
            vis.SuppressLegacyVisualsNow();
            EditorUtility.SetDirty(vis);
        }

        EditorUtility.SetDirty(rear);
        if (rearAnchor != null) EditorUtility.SetDirty(rearAnchor.gameObject);
        EditorUtility.SetDirty(physical);
        if (sync != null) EditorUtility.SetDirty(sync);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(drone.scene);

        BoxCollider box = miniRov.GetComponent<BoxCollider>();
        string boxText = box != null ? ("box center=" + box.center.ToString("F4") + " size=" + box.size.ToString("F4")) : "no root BoxCollider found";
        EditorUtility.DisplayDialog("MIMISK Physical Tether V7.4", "Rear physical tether anchor placed from MiniROV root BoxCollider.\n" + boxText + "\nanchor local=" + rear.anchorLocalPosition.ToString("F4") + "\n\nOriginal deployment/mission references were restored to ROV_TetherAnchor. Physical force coupling remains OFF.", "OK");
    }

    [MenuItem("MIMISK/Tether/V7.4 Diagnose Rear Anchor From BoxCollider")]
    public static void DiagnoseRearAnchorFromBoxColliderV74()
    {
        GameObject miniRov = GameObject.Find("MiniROV");
        MIMISKMiniROVRearTetherAnchor rear = Object.FindFirstObjectByType<MIMISKMiniROVRearTetherAnchor>();
        MIMISKPhysicalTetherModel physical = Object.FindFirstObjectByType<MIMISKPhysicalTetherModel>();
        BoxCollider box = miniRov != null ? miniRov.GetComponent<BoxCollider>() : null;

        Debug.Log("[MIMISK V7.4 Rear Anchor] miniRov=" + (miniRov != null ? miniRov.name : "missing") +
                  " box=" + (box != null ? ("center=" + box.center.ToString("F4") + " size=" + box.size.ToString("F4") + " rearZ=" + (box.center.z - box.size.z * 0.5f).ToString("F4")) : "missing") +
                  " anchor=" + (rear != null && rear.anchor != null ? rear.anchor.name : "missing") +
                  " local=" + (rear != null ? rear.anchorLocalPosition.ToString("F4") : "n/a") +
                  " boundsSource=" + (rear != null ? rear.boundsSource : "n/a") +
                  " frontEvidence=" + (rear != null ? rear.frontAxisEvidence : "n/a"));

        if (physical != null)
        {
            Debug.Log("[MIMISK V7.4 Rear Anchor] physical endpointMode=" + physical.endpointMode +
                      " endAnchor=" + (physical.endAnchor != null ? physical.endAnchor.name : "missing") +
                      " rovBackAnchor=" + (physical.rovBackAnchor != null ? physical.rovBackAnchor.name : "missing") +
                      " attachmentError=" + physical.endpointAttachmentErrorM.ToString("F4") +
                      " forceCouplingRov=" + physical.applyForcesToMiniRov +
                      " forceCouplingDrone=" + physical.applyForcesToDrone);
        }
    }

    private static void RestoreOriginalControllerReferencesV74(GameObject drone, GameObject miniRov)
    {
        Transform original = FindDeepChildV74(miniRov.transform, "ROV_TetherAnchor");
        if (original == null) original = FindDeepChildV74(miniRov.transform, "MiniROV_TetherPoint");
        if (original == null) original = miniRov.transform;

        MIMISKUnifiedTetherManager unified = drone.GetComponent<MIMISKUnifiedTetherManager>();
        if (unified != null)
        {
            unified.miniRovTetherAnchor = original;
            unified.miniRovTetherAnchorName = original.name;
            EditorUtility.SetDirty(unified);
        }

        MIMISKDroneCoreTetherManager legacy = drone.GetComponent<MIMISKDroneCoreTetherManager>();
        if (legacy != null)
        {
            legacy.miniRovTetherPoint = original;
            EditorUtility.SetDirty(legacy);
        }

        MIMISKTetherSmartWinchController smart = drone.GetComponent<MIMISKTetherSmartWinchController>();
        if (smart != null)
        {
            smart.miniRovTetherPoint = original;
            EditorUtility.SetDirty(smart);
        }
    }

    private static Transform FindDeepChildV74(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChildV74(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

}
#endif
