using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKRuntimeSceneManifest : MonoBehaviour
{
    [Header("Manifest")]
    public bool manifestEnabled = true;
    public bool validateOnStart = true;
    public bool logReport = true;

    [Header("Required Modules")]
    public bool foundDrone;
    public bool foundDroneMissionManager;
    public bool foundDroneFlightModeManager;
    public bool foundTetherManager;
    public bool foundFinalMissionPlanner;
    public bool foundMiniROV;
    public bool foundMiniROVModule;
    public bool foundCommonBus;
    public bool foundSafetyGate;
    public bool foundCommandBridge;

    [Header("Duplicate Counts")]
    public int droneMissionManagerCount;
    public int tetherManagerCount;
    public int finalMissionPlannerCount;
    public int miniRovModuleCount;
    public int commonBusCount;
    public int commandBridgeCount;
    public int safetyGateCount;

    [Header("Warnings")]
    public int enabledDeprecatedScriptCount;
    public int duplicateCameraFollowDriverCount;
    public int warningCount;

    [TextArea(5, 15)]
    public string report = "not_validated";

    private readonly string[] deprecatedOrExperimentalNames =
    {
        "MIMISKDroneTetherHandoffMission",
        "MIMISKTetherDeploymentTestMission",
        "MIMISKStandaloneTetherDeploymentMission",
        "MIMISKMiniROVRealisticDeploymentManager",
        "MIMISKMiniROVDeploymentManager",
        "MIMISKMiniROVCableEndAttachmentManager",
        "MIMISKMiniROVWaterReleaseController",
        "MIMISKDroneSurfaceIdleAntiVibrationGate",
        "MIMISKMiniROVPreDeploymentSafetyGuard",
        "MIMISKMiniROVStandaloneManualTest",
        "MIMISKMiniROVManualControlHandoff"
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
        enabledDeprecatedScriptCount = 0;
        duplicateCameraFollowDriverCount = 0;

        GameObject drone =
            GameObject.Find("Drone");

        GameObject miniRov =
            GameObject.Find("MiniROV");

        foundDrone = drone != null;
        foundMiniROV = miniRov != null;

        droneMissionManagerCount =
            CountSceneComponents("MIMISKDroneCoreMissionManager");

        tetherManagerCount =
            CountSceneComponents("MIMISKDroneCoreTetherManager");

        finalMissionPlannerCount =
            CountSceneComponents("MIMISKFinalMissionPlanner");

        miniRovModuleCount =
            CountSceneComponents("MIMISKMiniROVModule");

        commonBusCount =
            CountSceneComponents("MIMISKCommonBus");

        commandBridgeCount =
            CountSceneComponents("MIMISKCommandBridgeToExistingModules");

        safetyGateCount =
            CountSceneComponents("MIMISKCommandSafetyGate");

        foundDroneMissionManager =
            droneMissionManagerCount >= 1;

        foundDroneFlightModeManager =
            CountSceneComponents("MIMISKDroneCoreFlightModeManager") >= 1;

        foundTetherManager =
            tetherManagerCount >= 1;

        foundFinalMissionPlanner =
            finalMissionPlannerCount >= 1;

        foundMiniROVModule =
            miniRovModuleCount >= 1;

        foundCommonBus =
            commonBusCount >= 1;

        foundCommandBridge =
            commandBridgeCount >= 1;

        foundSafetyGate =
            safetyGateCount >= 1;

        enabledDeprecatedScriptCount =
            CountEnabledDeprecatedScripts();

        duplicateCameraFollowDriverCount =
            CountDuplicateCameraFollowDrivers();

        List<string> lines =
            new List<string>();

        lines.Add("MIMISK Runtime Scene Manifest");
        lines.Add("--------------------------------");
        lines.Add("Drone found: " + foundDrone);
        lines.Add("MiniROV found: " + foundMiniROV);
        lines.Add("Drone mission managers: " + droneMissionManagerCount);
        lines.Add("Tether managers: " + tetherManagerCount);
        lines.Add("Final mission planners: " + finalMissionPlannerCount);
        lines.Add("MiniROV modules: " + miniRovModuleCount);
        lines.Add("Common buses: " + commonBusCount);
        lines.Add("Command bridges: " + commandBridgeCount);
        lines.Add("Safety gates: " + safetyGateCount);
        lines.Add("Enabled deprecated/experimental scripts: " + enabledDeprecatedScriptCount);
        lines.Add("Duplicate camera follow drivers: " + duplicateCameraFollowDriverCount);

        CheckCount(lines, "Drone mission manager", droneMissionManagerCount, 1);
        CheckCount(lines, "Tether manager", tetherManagerCount, 1);
        CheckCount(lines, "Final mission planner", finalMissionPlannerCount, 1);
        CheckCount(lines, "MiniROV module", miniRovModuleCount, 1);
        CheckCount(lines, "Common bus", commonBusCount, 1);
        CheckCount(lines, "Command bridge", commandBridgeCount, 1);
        CheckCount(lines, "Safety gate", safetyGateCount, 1);

        if (enabledDeprecatedScriptCount > 0)
        {
            warningCount++;
            lines.Add("WARNING: Some deprecated/experimental scripts are enabled.");
        }

        if (duplicateCameraFollowDriverCount > 0)
        {
            warningCount++;
            lines.Add("WARNING: Some cameras have more than one active follow driver.");
        }

        lines.Add("Warnings: " + warningCount);

        report =
            string.Join("\n", lines.ToArray());

        if (logReport)
        {
            Debug.Log(report);
        }
    }

    private void CheckCount(List<string> lines, string label, int count, int expected)
    {
        if (count != expected)
        {
            warningCount++;
            lines.Add("WARNING: " + label + " count is " + count + ", expected " + expected);
        }
    }

    private int CountSceneComponents(string className)
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

            if (b != null &&
                b.GetType().Name == className)
            {
                count++;
            }
        }

        return count;
    }

    private int CountEnabledDeprecatedScripts()
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

            string typeName =
                b.GetType().Name;

            for (int j = 0; j < deprecatedOrExperimentalNames.Length; j++)
            {
                if (typeName == deprecatedOrExperimentalNames[j])
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
            Camera cam = cameras[i];

            if (cam == null)
            {
                continue;
            }

            Behaviour[] behaviours =
                cam.GetComponents<Behaviour>();

            int activeFollowDrivers = 0;

            for (int j = 0; j < behaviours.Length; j++)
            {
                Behaviour b = behaviours[j];

                if (b == null || !b.enabled)
                {
                    continue;
                }

                string n =
                    b.GetType().Name.ToLowerInvariant();

                if (n.Contains("follow") &&
                    n.Contains("camera"))
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
