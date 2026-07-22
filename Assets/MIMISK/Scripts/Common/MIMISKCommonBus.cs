using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKCommonBus : MonoBehaviour
{
    public static MIMISKCommonBus Instance { get; private set; }

    [Header("Bus")]
    public bool busEnabled = true;
    public bool logCommands = true;
    public bool logStateTransitions = false;

    [Header("Runtime")]
    public int commandCount;
    public int stateCount;
    public MIMISKCommandMessage lastCommand;
    public MIMISKStateMessage lastState;

    public int maxStateHistory = 300;
    public List<MIMISKStateMessage> stateHistory = new List<MIMISKStateMessage>();

    public event Action<MIMISKCommandMessage> OnCommand;
    public event Action<MIMISKStateMessage> OnState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MIMISKCommonBus] Duplicate bus found. Keeping the first bus instance.");
            return;
        }

        Instance = this;
    }

    public void SendCommand(MIMISKCommandMessage command)
    {
        if (!busEnabled || command == null)
        {
            return;
        }

        command.time = Time.timeAsDouble;
        lastCommand = command;
        commandCount++;

        if (logCommands)
        {
            Debug.Log(
                "[MIMISK BUS] command " +
                command.source + " -> " +
                command.target + " / " +
                command.verb + " / " +
                command.text
            );
        }

        if (OnCommand != null)
        {
            OnCommand(command);
        }
    }

    public void PublishState(MIMISKStateMessage state)
    {
        if (!busEnabled || state == null)
        {
            return;
        }

        state.time = Time.timeAsDouble;
        lastState = state;
        stateCount++;

        stateHistory.Add(state);

        if (stateHistory.Count > maxStateHistory)
        {
            stateHistory.RemoveAt(0);
        }

        if (logStateTransitions)
        {
            Debug.Log(
                "[MIMISK BUS] state " +
                state.subsystem + " / " +
                state.mode + " / " +
                state.eventText
            );
        }

        if (OnState != null)
        {
            OnState(state);
        }
    }

    public static MIMISKCommonBus GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject go = GameObject.Find("MIMISK_CommonInterface");

        if (go == null)
        {
            go = new GameObject("MIMISK_CommonInterface");
        }

        Instance = go.GetComponent<MIMISKCommonBus>();

        if (Instance == null)
        {
            Instance = go.AddComponent<MIMISKCommonBus>();
        }

        return Instance;
    }
}
