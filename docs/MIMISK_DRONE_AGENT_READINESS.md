# MIMISK Drone Agent Readiness

## Principle

Do not rewrite the validated drone control stack.

The DroneAgent will be a wrapper around:

- MIMISKDroneCoreMissionManager
- MIMISKDroneCoreFlightModeManager
- MIMISKDroneCoreRotorController
- MIMISKDroneCoreTrajectoryPlanner

## Existing validated responsibilities

### Mission Manager

High-level mission:
TakeoffIdle -> Takeoff -> HoldAfterTakeoff -> TransitToSurveyArea -> SurveyPattern -> DeploymentApproach -> PrecisionHold -> LandingOnSurface -> SurfaceStable -> ReadyForTetherDeployment.

### Flight Mode Manager

Runtime flight modes:
TakeoffIdle, Takeoff, Gamepad, PositionHold, PathTracking, LandingOnSurface, SurfaceStable, SurfaceHold, Failsafe.

### Rotor Controller

Low-level controller modes:
ManualGamepad, PositionHold, PathTracking, ExternalReference.

### Trajectory Planner

Trajectory references:
Circle, SpiralOut, HelixDown, HelixUpDown, FigureEight, SmoothSquare, Lawnmower, DeploymentApproach.

## Agent readiness cleanup

Agent mode disables direct keyboard/mode-button ownership:

- MissionManager.acceptKeyboardMissionCommands = false
- FlightModeManager.acceptKeyboardModeCommands = false
- FlightModeManager.acceptGamepadModeButtons = false
- RotorController.acceptKeyboardShortcuts = false

The public methods remain unchanged and are called by the DroneAgent.

## Future DroneAgent commands

- SetHome or capture current reference
- TakeoffIdle
- Takeoff
- Hold
- StartPath
- StartMission
- LandOnSurface
- Abort
- ManualMode
- Disarm
