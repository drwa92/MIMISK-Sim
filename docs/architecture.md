# MIMISK-ROS Architecture

## Purpose

MIMISK-ROS turns the Unity MIMISK simulator into a ROS 2-compatible research simulator for aerial-surface-underwater inspection missions.

The core design principle is **reference-level and command-level integration**, not transform teleportation. Unity remains responsible for physics, water interaction, visual rendering, cameras, simulated sensors, vehicle controllers, tether management, and deployment/recovery safety logic. ROS 2 receives standard robotics data streams and can send high-level mission commands, manual gamepad inputs, and external references.

## Software layers

```text
Unity / MIMISK
    Aerial drone dynamics and controller
    Surface landing and buoyancy logic
    Tether/winch deployment and recovery
    MiniROV dynamics and controller
    Cameras and simulated sensors
    Mission managers and safety gates

gRPC bridge
    SendTelemetry
    SendCameraFrame
    GetPendingCommands
    ReportCommandResult
    GetManualControls
    GetExternalReferences

ROS 2
    typed state topics
    odometry, IMU, depth, camera topics
    command services
    manual-control topics
    external-reference topics
    TF and clock
    rosbag recording
    experiment runner
```

## Control levels

MIMISK-ROS supports multiple autonomy levels:

```text
Level 1: Mission commands
    deploy ROV
    recover ROV
    start drone mission
    start MiniROV mission
    return home

Level 2: Reference control
    drone pose / velocity / yaw reference
    MiniROV GoToPoint / depth reference
    tether target length

Level 3: Manual control
    physical gamepad through ROS 2 / gRPC

Future Level 4:
    optional low-level actuator commands
```

The current public interface supports Levels 1–3. Level 4 is intentionally reserved as an optional future interface because the validated Unity plant controllers provide a stable baseline for research.

## Research modules enabled

External ROS 2 modules can implement:

- drone localization;
- MiniROV localization;
- tether-based ROV localization;
- visual inspection and AI perception;
- VLM/LLM mission planning;
- custom trajectory generation;
- tether-aware planning;
- dataset generation and benchmarking.


