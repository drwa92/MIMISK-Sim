# MIMISK-ROS Benchmark Scenarios

## B0 — Middleware connectivity

Purpose: verify Unity to ROS 2 / gRPC connection.

Required topics:

```text
/mimisk/drone/state
/mimisk/minirov/state
/mimisk/tether/state
/clock
/tf
```

Success criteria:

```text
all required topics available
command result feedback works
camera streams available if enabled
```

## B1 — Drone reference tracking

Purpose: evaluate the drone external-reference interface.

Input:

```text
/mimisk/reference/drone
```

Suggested trajectories:

```text
circle
spiral
figure-eight
deployment approach
```

Metrics:

```text
position tracking error
yaw tracking error
command latency
control mode correctness
```

## B2 — Drone surface landing and tether readiness

Purpose: validate the aerial-to-surface transition.

Mission states:

```text
Takeoff
TransitToSurveyArea
SurveyPattern
DeploymentApproach
LandingOnSurface
SurfaceStable
ReadyForTetherDeployment
```

Metrics:

```text
time to surface stable
vertical speed near contact
surface-ready detection
state transition correctness
```

## B3 — Tether deployment and recovery

Purpose: evaluate winch/tether/ROV deployment pipeline.

Metrics:

```text
deployment time
recovery time
target length error
slack
stretch
tension proxy
state transition sequence
```

## B4 — MiniROV path execution

Path types:

```text
StationHold
GoToPoint
LineOutAndBack
Square
RectangleSurvey
LawnmowerSurvey
CircleInspection
StopAndLookInspection
SpiralSearch
HelixInspection
FigureEight
ReturnHome
```

Metrics:

```text
path tracking error
depth tracking error
mission completion time
return-home distance
recovery-ready success
```

## B5 — Integrated deploy-inspect-recover mission

Sequence:

```text
1. drone surface-ready
2. attach ROV
3. deploy ROV
4. activate ROV control
5. enable TMS
6. execute MiniROV inspection path
7. return home
8. recover ROV
```

Metrics:

```text
mission success
total mission time
ROV tracking error
tether slack/stretch
recovery success
ROS command execution reliability
```

## B6 — ROS 2 physical gamepad

Purpose: verify manual control through ROS 2 / gRPC.

Metrics:

```text
manual command rate
input latency
mode-gate correctness
mapping correctness
```

## B7 — Dataset generation

Purpose: produce synchronized rosbags for localization, perception, and planning.

Recorded streams:

```text
state
odom
IMU
depth
camera
tether
manual commands
external references
command results
TF
clock
```
