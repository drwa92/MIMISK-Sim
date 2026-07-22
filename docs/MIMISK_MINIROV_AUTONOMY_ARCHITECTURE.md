# MIMISK MiniROV Autonomy Architecture

## Core rule

Path planning and dwell/inspection are separate actions.

The MiniROV is underactuated:
- surge / forward motion
- yaw / heading
- ballast-based depth control
- no sway/lateral thruster

Therefore:
- while moving, yaw should normally follow travel heading;
- inspection yaw should be used while stopped in StationHold;
- stop-and-look behavior must be implemented as path mission + independent dwell action.

## Runtime chain

MissionManager
    -> PathPlanner
        -> PlantBasedController
            -> ControlManager

## Responsibilities

### PathPlanner

Responsible for:
- generating path geometry
- selecting path algorithm
- selecting speed profile
- selecting yaw policy
- setting home / return-home target
- configuring controller path references

Not responsible for:
- dwell sequencing
- mission state ownership
- direct motor commands

### MissionManager

Responsible for:
- starting planner-selected mission
- starting independent dwell action
- return home
- manual mode
- abort / hold
- recovery-ready logic
- compatibility with older bridge/setup scripts

Not responsible for:
- path geometry
- motor control

### PlantBasedController

Responsible for:
- StationHold
- GoToPoint
- PolylineLOS
- CircleLOS
- completion into StationHold
- final motor frame injection to ControlManager

Not responsible for:
- mission selection
- path generation
- dwell sequencing

## Accepted workflow

### Path only

1. Set PathPlanner.SelectedPathType.
2. MissionManager.StartSelectedMission.
3. Controller tracks path.
4. On completion, controller enters StationHold.

### Dwell only

1. MissionManager.StartDwellAtCurrentPose.
2. Controller holds current pose and yaw target.
3. Manager counts dwell timer.
4. On completion, manager returns to StationHolding.

### Path then dwell

1. Run normal path mission.
2. Wait for StationHolding.
3. Start independent dwell action.

### ReturnHome preemption

ReturnHome cancels:
- dwell action
- deprecated stop-look state
- manual control ownership

Then ReturnHome takes control and reaches RecoveryReady.


