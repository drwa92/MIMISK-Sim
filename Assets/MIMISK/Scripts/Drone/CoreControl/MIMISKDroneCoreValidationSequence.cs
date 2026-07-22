using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MIMISKDroneCoreValidationSequence : MonoBehaviour
{
    public enum SequenceState
    {
        Idle,
        TakeoffIdle,
        Takeoff,
        HoldAfterTakeoff,
        PathTracking,
        HoldAfterPath,
        Landing,
        Completed,
        Aborted
    }

    [Header("References")]
    public MIMISKDroneCoreRotorController core;
    public MIMISKDroneCoreFlightModeManager manager;
    public MIMISKDroneCoreTrajectoryPlanner trajectoryPlanner;
    public MIMISKDroneCorePropellerAnimationBridge propellerBridge;

    [Header("Keyboard")]
    public Key startValidationKey = Key.V;
    public Key abortValidationKey = Key.B;

    [Header("Sequence Enable")]
    public bool sequenceEnabled = true;
    public bool runOnStart = false;

    [Header("Timing")]
    public float takeoffIdleSeconds = 2.0f;
    public float holdAfterTakeoffSeconds = 4.0f;
    public float holdAfterPathSeconds = 4.0f;

    public float maxTakeoffWaitSeconds = 25.0f;
    public float maxPathWaitSeconds = 80.0f;
    public float maxLandingWaitSeconds = 45.0f;

    [Header("Path")]
    public MIMISKDroneCoreFlightModeManager.PathKind validationPath =
        MIMISKDroneCoreFlightModeManager.PathKind.Circle;

    [Header("Runtime")]
    public SequenceState sequenceState = SequenceState.Idle;
    public float stateTimerS;
    public float sequenceTimerS;
    public string lastSequenceEvent = "idle";

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        if (runOnStart)
        {
            StartValidationSequence();
        }
    }

    private void Update()
    {
        if (!sequenceEnabled)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[startValidationKey].wasPressedThisFrame)
        {
            StartValidationSequence();
        }

        if (Keyboard.current[abortValidationKey].wasPressedThisFrame)
        {
            AbortValidationSequence();
        }
    }

    private void FixedUpdate()
    {
        if (!sequenceEnabled)
        {
            return;
        }

        if (sequenceState == SequenceState.Idle ||
            sequenceState == SequenceState.Completed ||
            sequenceState == SequenceState.Aborted)
        {
            return;
        }

        float dt = Time.fixedDeltaTime;

        stateTimerS += dt;
        sequenceTimerS += dt;

        UpdateSequence();
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (core == null)
        {
            core = GetComponent<MIMISKDroneCoreRotorController>();
        }

        if (manager == null)
        {
            manager = GetComponent<MIMISKDroneCoreFlightModeManager>();
        }

        if (trajectoryPlanner == null)
        {
            trajectoryPlanner = GetComponent<MIMISKDroneCoreTrajectoryPlanner>();
        }

        if (propellerBridge == null)
        {
            propellerBridge = GetComponent<MIMISKDroneCorePropellerAnimationBridge>();
        }
    }

    [ContextMenu("Start Validation Sequence")]
    public void StartValidationSequence()
    {
        AutoFindReferences();

        if (manager == null || core == null)
        {
            Debug.LogError("[MIMISK] Core validation cannot start: missing manager or core controller.");
            return;
        }

        sequenceTimerS = 0.0f;
        stateTimerS = 0.0f;

        manager.EnterTakeoffIdle();

        sequenceState = SequenceState.TakeoffIdle;
        lastSequenceEvent = "validation_started_takeoff_idle";

        Debug.Log("[MIMISK] Core validation sequence started.");
    }

    [ContextMenu("Abort Validation Sequence")]
    public void AbortValidationSequence()
    {
        AutoFindReferences();

        if (manager != null)
        {
            manager.EnterFailsafe();
        }

        sequenceState = SequenceState.Aborted;
        stateTimerS = 0.0f;
        lastSequenceEvent = "validation_aborted";

        Debug.Log("[MIMISK] Core validation sequence aborted.");
    }

    private void UpdateSequence()
    {
        if (manager == null)
        {
            return;
        }

        if (sequenceState == SequenceState.TakeoffIdle)
        {
            if (stateTimerS >= takeoffIdleSeconds)
            {
                manager.StartTakeoff();
                EnterState(SequenceState.Takeoff, "takeoff_started");
            }

            return;
        }

        if (sequenceState == SequenceState.Takeoff)
        {
            bool takeoffCompleted =
                manager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.PositionHold &&
                stateTimerS > 1.0f;

            bool timeout =
                stateTimerS >= maxTakeoffWaitSeconds;

            if (takeoffCompleted || timeout)
            {
                manager.CapturePositionHold();

                EnterState(
                    SequenceState.HoldAfterTakeoff,
                    takeoffCompleted
                        ? "takeoff_completed_hold"
                        : "takeoff_timeout_hold"
                );
            }

            return;
        }

        if (sequenceState == SequenceState.HoldAfterTakeoff)
        {
            if (stateTimerS >= holdAfterTakeoffSeconds)
            {
                manager.StartPathTracking(validationPath);
                EnterState(SequenceState.PathTracking, "path_tracking_started");
            }

            return;
        }

        if (sequenceState == SequenceState.PathTracking)
        {
            bool pathCompleted =
                manager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.PositionHold &&
                stateTimerS > 3.0f;

            bool timeout =
                stateTimerS >= maxPathWaitSeconds;

            if (pathCompleted || timeout)
            {
                manager.CapturePositionHold();

                EnterState(
                    SequenceState.HoldAfterPath,
                    pathCompleted
                        ? "path_completed_hold"
                        : "path_timeout_hold"
                );
            }

            return;
        }

        if (sequenceState == SequenceState.HoldAfterPath)
        {
            if (stateTimerS >= holdAfterPathSeconds)
            {
                manager.StartLandingOnSurface();
                EnterState(SequenceState.Landing, "landing_started");
            }

            return;
        }

        if (sequenceState == SequenceState.Landing)
        {
            bool landingCompleted =
                manager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceStable ||
                manager.flightMode == MIMISKDroneCoreFlightModeManager.FlightMode.SurfaceHold;

            bool timeout =
                stateTimerS >= maxLandingWaitSeconds;

            if (landingCompleted)
            {
                EnterState(SequenceState.Completed, "validation_completed_surface_stable");
            }
            else if (timeout)
            {
                manager.EnterFailsafe();
                EnterState(SequenceState.Aborted, "landing_timeout_failsafe");
            }

            return;
        }
    }

    private void EnterState(SequenceState newState, string eventText)
    {
        sequenceState = newState;
        stateTimerS = 0.0f;
        lastSequenceEvent = eventText;

        Debug.Log("[MIMISK] Core validation: " + eventText);
    }
}
