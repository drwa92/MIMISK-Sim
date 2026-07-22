# MIMISK-Sim Research Roadmap

## Phase 1 — MiniROV autonomy core

Status: mostly complete.

Goals:

- Python MiniROV plant model
- Open-loop validation
- Closed-loop controller validation
- Robustness validation
- Unity plant-based controller
- StationHold
- GoToPoint
- PolylineLOS
- CircleLOS
- ReturnHome
- RecoveryReady
- Runtime CSV logger and analysis

Acceptance:

- StationHold stable
- GoToPoint reaches target and stops
- Circle completes and stops
- ReturnHome reaches RecoveryReady
- No manual/autonomous command conflict

## Phase 2 — Stop-and-look inspection

Status: starting now.

Goals:

- Move to inspection waypoint using travel heading
- Stop at waypoint
- StationHold
- Rotate to inspection yaw or face target
- Dwell for inspection time
- Continue to next waypoint
- Complete and StationHold

## Phase 3 — Common MIMISK agent interface

Goals:

- IMIMISKAgent
- MIMISKCommand
- MIMISKState
- MIMISKEvent
- MIMISKMissionPlan
- DroneAgent
- TetherAgent
- MiniROVAgent

## Phase 4 — Integrated drone-tether-ROV mission demo

Goals:

- Drone takeoff
- Drone transit
- Surface landing
- Tether deploy
- MiniROV release
- MiniROV inspection
- ReturnHome
- RecoveryReady
- Tether recovery
- Drone return

## Phase 5 — ROS 2 / gRPC / UDP adapters

Goals:

- ROS 2 topics/services/actions
- gRPC service
- UDP/HIL bridge
- CSV/rosbag export

## Phase 6 — Environmental and sensor realism

Goals:

- Underwater current
- Turbidity / visibility
- Sensor noise
- Communication delay
- Tether drag/slack/tension
- Camera degradation

## Phase 7 — Benchmark release and publication package

Goals:

- Benchmark scenes
- Mission configs
- Metrics scripts
- Example logs
- Documentation
- Baseline controllers
- Publication figures and tables
