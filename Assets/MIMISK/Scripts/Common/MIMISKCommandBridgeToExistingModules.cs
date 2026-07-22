using System;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKCommandBridgeToExistingModules : MonoBehaviour
{
    [Header("Common Interface")]
    public MIMISKCommonBus bus;
    public bool bridgeEnabled = true;

    [Header("Safety Gate")]
    public bool requireSafetyGate = true;
    public string authorizedCommandSource = "MIMISKCommandSafetyGate";

    [Header("Existing Drone Module")]
    public GameObject droneObject;
    public MIMISKDroneCoreMissionManager droneMission;
    public MIMISKDroneCoreFlightModeManager droneFlightMode;

    [Header("Existing Tether / Final Planner Module")]
    public MIMISKFinalMissionPlanner finalPlanner;
    public MIMISKDroneCoreTetherManager tetherManager;

    [Header("Existing MiniROV Module")]
    public GameObject miniRovObject;
    public MIMISKMiniROVModule miniRovModule;

    [Header("Runtime")]
    public int commandsReceived;
    public int commandsIgnoredUngated;
    public int commandsHandled;
    public string lastCommand = "none";
    public string lastBridgeEvent = "idle";

    private void Awake()
    {
        AutoFindReferences();
    }

    private void OnEnable()
    {
        AutoFindReferences();

        if (bus != null)
        {
            bus.OnCommand += HandleCommand;
        }
    }

    private void OnDisable()
    {
        if (bus != null)
        {
            bus.OnCommand -= HandleCommand;
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (bus == null)
        {
            bus = MIMISKCommonBus.GetOrCreate();
        }

        if (droneObject == null)
        {
            droneObject = GameObject.Find("Drone");
        }

        if (droneObject != null)
        {
            if (droneMission == null)
            {
                droneMission =
                    droneObject.GetComponent<MIMISKDroneCoreMissionManager>();
            }

            if (droneFlightMode == null)
            {
                droneFlightMode =
                    droneObject.GetComponent<MIMISKDroneCoreFlightModeManager>();
            }

            if (finalPlanner == null)
            {
                finalPlanner =
                    droneObject.GetComponent<MIMISKFinalMissionPlanner>();
            }

            if (tetherManager == null)
            {
                tetherManager =
                    droneObject.GetComponent<MIMISKDroneCoreTetherManager>();
            }
        }

        if (miniRovObject == null)
        {
            miniRovObject = GameObject.Find("MiniROV");
        }

        if (miniRovObject != null && miniRovModule == null)
        {
            miniRovModule =
                miniRovObject.GetComponent<MIMISKMiniROVModule>();
        }
    }

    private void HandleCommand(MIMISKCommandMessage command)
    {
        if (!bridgeEnabled || command == null)
        {
            return;
        }

        commandsReceived++;

        if (requireSafetyGate && command.source != authorizedCommandSource)
        {
            commandsIgnoredUngated++;
            lastBridgeEvent =
                "ignored_ungated_command_from_" +
                command.source +
                "_" +
                command.verb;

            return;
        }

        lastCommand =
            command.source + " -> " +
            command.target + " / " +
            command.verb + " / " +
            command.text;

        switch (command.verb)
        {
            case MIMISKCommandVerb.StartMission:
                StartExistingDroneMission();
                break;

            case MIMISKCommandVerb.DeployTether:
                DeployTetherUsingExistingPlanner();
                break;

            case MIMISKCommandVerb.EnableMiniROVControl:
                EnableMiniROVControlUsingExistingPlanner();
                break;

            case MIMISKCommandVerb.RecoverMiniROV:
                RecoverMiniROVUsingExistingPlanner();
                break;

            case MIMISKCommandVerb.HoldTether:
                HoldTetherUsingExistingPlanner();
                break;

            case MIMISKCommandVerb.ResetMission:
            case MIMISKCommandVerb.ResetFault:
                ResetExistingPlanner();
                break;
        }
    }

    private void StartExistingDroneMission()
    {
        AutoFindReferences();

        if (droneMission == null)
        {
            lastBridgeEvent = "start_failed_missing_drone_mission";
            Debug.LogWarning("[MIMISK Command Bridge] Cannot start mission: missing drone mission manager.");
            return;
        }

        string[] methodNames =
        {
            "StartMission",
            "StartCoreMission",
            "StartFullMission",
            "StartMIMISKMission",
            "BeginMission",
            "BeginCoreMission",
            "StartMissionSequence",
            "StartFromKeyboard",
            "StartTakeoffSequence",
            "RequestStartMission"
        };

        Type t = droneMission.GetType();

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        for (int i = 0; i < methodNames.Length; i++)
        {
            MethodInfo method =
                t.GetMethod(methodNames[i], flags);

            if (method != null &&
                method.GetParameters().Length == 0)
            {
                method.Invoke(droneMission, null);

                commandsHandled++;
                lastBridgeEvent =
                    "start_mission_invoked_existing_method_" +
                    methodNames[i];

                Debug.Log("[MIMISK Command Bridge] " + lastBridgeEvent);
                return;
            }
        }

        lastBridgeEvent =
            "start_mission_not_routed_no_existing_start_method_found_keep_using_P_for_now";

        Debug.LogWarning(
            "[MIMISK Command Bridge] Could not find a no-argument start method on existing mission manager. " +
            "Keep using P for now, or expose a public StartMission() wrapper later."
        );
    }

    private void DeployTetherUsingExistingPlanner()
    {
        AutoFindReferences();

        if (finalPlanner == null)
        {
            lastBridgeEvent = "deploy_failed_missing_final_planner";
            Debug.LogWarning("[MIMISK Command Bridge] Missing final planner.");
            return;
        }

        finalPlanner.RequestTetherDeployment();

        commandsHandled++;
        lastBridgeEvent =
            "deploy_tether_called_existing_final_planner";
    }

    private void EnableMiniROVControlUsingExistingPlanner()
    {
        AutoFindReferences();

        if (finalPlanner != null)
        {
            finalPlanner.RequestMiniRovControlHandoff();

            commandsHandled++;
            lastBridgeEvent =
                "enable_minirov_control_called_existing_final_planner";

            return;
        }

        if (miniRovModule != null)
        {
            miniRovModule.EnableExternalControl();

            commandsHandled++;
            lastBridgeEvent =
                "enable_minirov_control_called_existing_minirov_module";

            return;
        }

        lastBridgeEvent =
            "enable_minirov_control_failed_missing_final_planner_and_minirov_module";

        Debug.LogWarning("[MIMISK Command Bridge] Missing final planner and MiniROV module.");
    }

    private void RecoverMiniROVUsingExistingPlanner()
    {
        AutoFindReferences();

        if (finalPlanner == null)
        {
            lastBridgeEvent = "recovery_failed_missing_final_planner";
            Debug.LogWarning("[MIMISK Command Bridge] Missing final planner.");
            return;
        }

        finalPlanner.RequestRecovery();

        commandsHandled++;
        lastBridgeEvent =
            "recover_minirov_called_existing_final_planner";
    }

    private void HoldTetherUsingExistingPlanner()
    {
        AutoFindReferences();

        if (finalPlanner != null)
        {
            finalPlanner.HoldTether();

            commandsHandled++;
            lastBridgeEvent =
                "hold_tether_called_existing_final_planner";

            return;
        }

        if (tetherManager != null)
        {
            tetherManager.targetLengthM = tetherManager.deployedLengthM;
            tetherManager.winchCommandRateMS = 0.0f;
            tetherManager.tetherState =
                MIMISKDroneCoreTetherManager.TetherState.HoldingDeployed;

            commandsHandled++;
            lastBridgeEvent =
                "hold_tether_called_existing_tether_manager";

            return;
        }

        lastBridgeEvent = "hold_failed_missing_tether";
    }

    private void ResetExistingPlanner()
    {
        AutoFindReferences();

        if (finalPlanner != null)
        {
            finalPlanner.ResetPlanner();

            commandsHandled++;
            lastBridgeEvent =
                "reset_called_existing_final_planner";

            return;
        }

        lastBridgeEvent =
            "reset_failed_missing_final_planner";
    }
}
