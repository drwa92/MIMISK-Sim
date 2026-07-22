using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKFinalRuntimeManifest : MonoBehaviour
{
    [Header("Manifest")]
    public bool manifestEnabled = true;
    public bool validateOnStart = true;
    public bool logReport = true;

    [Header("Required Runtime Counts")]
    public int commonBusCount;
    public int communicationBridgeCount;
    public int droneMissionManagerCount;
    public int droneFlightModeManagerCount;
    public int tetherManagerCount;
    public int finalMissionPlannerCount;
    public int miniRovModuleCount;
    public int miniRovCollisionIsolationCount;
    public int finalContinuousTetherVisualCount;

    [Header("Deprecated / Duplicate Runtime Counts")]
    public int oldCommonCommandChainEnabledCount;
    public int deprecatedDeploymentScriptEnabledCount;
    public int duplicateCameraFollowDriverCount;

    [Header("Status")]
    public int warningCount;
    public bool sceneLooksClean;

    [TextArea(8, 24)]
    public string report = "not_validated";

    private readonly string[] oldCommonCommandChain =
    {
        "MIMISKCommonCommandFrontend",
        "MIMISKCommandSafetyGate",
        "MIMISKCommandBridgeToExistingModules",
        "MIMISKUnifiedKeyboardFrontend",
        "MIMISKFinalPlannerAdapter"
    };

    private readonly string[] deprecatedDeploymentScripts =
    {
        "MIMISKDroneTetherHandoffMission",
        "MIMISKTetherDeploymentTestMission",
        "MIMISKStandaloneTetherDeploymentMission",
        "MIMISKMiniROVRealisticDeploymentManager",
        "MIMISKMiniROVDeploymentManager",
        "MIMISKMiniROVCableEndAttachmentManager",
        "MIMISKMiniROVWaterReleaseController",
        "MIMISKMiniROVPreDeploymentSafetyGuard",
        "MIMISKMiniROVStandaloneManualTest",
        "MIMISKMiniROVManualControlHandoff",
        "MIMISKDroneSurfaceIdleAntiVibrationGate"
    };

    private void Start()
    {
        if (validateOnStart)
        {
            ValidateScene();
        }
    }

    [ContextMenu("Validate Scene")]
    public void ValidateScene()
    {
        if (!manifestEnabled)
        {
            return;
        }

        warningCount = 0;

        commonBusCount = CountComponents("MIMISKCommonBus", false);
        communicationBridgeCount = CountComponents("MIMISKCommunicationBridge", false);
        droneMissionManagerCount = CountComponents("MIMISKDroneCoreMissionManager", false);
        droneFlightModeManagerCount = CountComponents("MIMISKDroneCoreFlightModeManager", false);
        tetherManagerCount = CountComponents("MIMISKDroneCoreTetherManager", false);
        finalMissionPlannerCount = CountComponents("MIMISKFinalMissionPlanner", false);
        miniRovModuleCount = CountComponents("MIMISKMiniROVModule", false);
        miniRovCollisionIsolationCount = CountComponents("MIMISKMiniROVCollisionIsolation", false);
        finalContinuousTetherVisualCount = CountComponents("MIMISKFinalContinuousTetherVisual", false);

        oldCommonCommandChainEnabledCount = CountNamedEnabled(oldCommonCommandChain);
        deprecatedDeploymentScriptEnabledCount = CountNamedEnabled(deprecatedDeploymentScripts);
        duplicateCameraFollowDriverCount = CountDuplicateCameraFollowDrivers();

        List<string> lines = new List<string>();
        lines.Add("MIMISK Final Runtime Manifest");
        lines.Add("----------------------------------------");
        lines.Add("CommonBus count: " + commonBusCount);
        lines.Add("CommunicationBridge count: " + communicationBridgeCount);
        lines.Add("DroneMissionManager count: " + droneMissionManagerCount);
        lines.Add("DroneFlightModeManager count: " + droneFlightModeManagerCount);
        lines.Add("TetherManager count: " + tetherManagerCount);
        lines.Add("FinalMissionPlanner count: " + finalMissionPlannerCount);
        lines.Add("MiniROVModule count: " + miniRovModuleCount);
        lines.Add("MiniROVCollisionIsolation count: " + miniRovCollisionIsolationCount);
        lines.Add("FinalContinuousTetherVisual count: " + finalContinuousTetherVisualCount);
        lines.Add("Old common command chain enabled: " + oldCommonCommandChainEnabledCount);
        lines.Add("Deprecated deployment scripts enabled: " + deprecatedDeploymentScriptEnabledCount);
        lines.Add("Duplicate camera follow drivers: " + duplicateCameraFollowDriverCount);

        CheckExact(lines, "MIMISKCommonBus", commonBusCount, 1);
        CheckExact(lines, "MIMISKCommunicationBridge", communicationBridgeCount, 1);
        CheckExact(lines, "MIMISKDroneCoreMissionManager", droneMissionManagerCount, 1);
        CheckExact(lines, "MIMISKDroneCoreFlightModeManager", droneFlightModeManagerCount, 1);
        CheckExact(lines, "MIMISKDroneCoreTetherManager", tetherManagerCount, 1);
        CheckExact(lines, "MIMISKFinalMissionPlanner", finalMissionPlannerCount, 1);
        CheckExact(lines, "MIMISKMiniROVModule", miniRovModuleCount, 1);

        if (oldCommonCommandChainEnabledCount > 0)
        {
            warningCount++;
            lines.Add("WARNING: Old common command chain components are enabled.");
        }

        if (deprecatedDeploymentScriptEnabledCount > 0)
        {
            warningCount++;
            lines.Add("WARNING: Deprecated/experimental deployment scripts are enabled.");
        }

        if (duplicateCameraFollowDriverCount > 0)
        {
            warningCount++;
            lines.Add("WARNING: A camera has more than one active follow driver.");
        }

        sceneLooksClean = warningCount == 0;
        lines.Add("Warnings: " + warningCount);
        lines.Add("Scene looks clean: " + sceneLooksClean);

        report = string.Join("\n", lines.ToArray());

        if (logReport)
        {
            Debug.Log(report);
        }
    }

    private void CheckExact(List<string> lines, string label, int count, int expected)
    {
        if (count != expected)
        {
            warningCount++;
            lines.Add("WARNING: " + label + " count is " + count + ", expected " + expected);
        }
    }

    private int CountComponents(string className, bool enabledOnly)
    {
        int count = 0;

        Behaviour[] behaviours =
            UnityEngine.Object.FindObjectsByType<Behaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour b = behaviours[i];

            if (b == null)
            {
                continue;
            }

            if (enabledOnly && !b.enabled)
            {
                continue;
            }

            if (b.GetType().Name == className)
            {
                count++;
            }
        }

        return count;
    }

    private int CountNamedEnabled(string[] names)
    {
        int count = 0;

        Behaviour[] behaviours =
            UnityEngine.Object.FindObjectsByType<Behaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour b = behaviours[i];

            if (b == null || !b.enabled)
            {
                continue;
            }

            string typeName = b.GetType().Name;

            for (int j = 0; j < names.Length; j++)
            {
                if (typeName == names[j])
                {
                    count++;
                    break;
                }
            }
        }

        return count;
    }

    private int CountDuplicateCameraFollowDrivers()
    {
        int count = 0;

        Camera[] cameras =
            UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];

            if (camera == null)
            {
                continue;
            }

            Behaviour[] behaviours =
                camera.GetComponents<Behaviour>();

            int activeFollowDrivers = 0;

            for (int j = 0; j < behaviours.Length; j++)
            {
                Behaviour b = behaviours[j];

                if (b == null || !b.enabled)
                {
                    continue;
                }

                string n = b.GetType().Name.ToLowerInvariant();

                if (n.Contains("follow") && n.Contains("camera"))
                {
                    activeFollowDrivers++;
                }
            }

            if (activeFollowDrivers > 1)
            {
                count++;
            }
        }

        return count;
    }
}
