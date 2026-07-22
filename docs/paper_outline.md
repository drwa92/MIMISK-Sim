# Paper Outline

Working title:

**MIMISK-ROS: A ROS2/gRPC Digital Twin for Drone-Deployed Tethered MiniROV Inspection**

## Abstract

Introduce a Unity-ROS2/gRPC simulator for integrated aerial-surface-underwater inspection using a drone-deployed tethered MiniROV. Emphasize ROS2 APIs, sensor/camera streams, tether manager, reference-level control, and benchmark/dataset generation.

## 1. Introduction

- Need for integrated aerial and underwater inspection.
- Limitations of isolated drone or ROV simulators.
- Need for ROS2-compatible research interfaces.
- MIMISK contribution.

## 2. Related Work

- Marine robotics simulators.
- Unity-ROS/gRPC simulators such as MARUS.
- Underwater simulators such as DAVE/HoloOcean.
- Tethered surface-underwater systems and TMS.
- Drone/ROV localization and perception.

## 3. Simulator Architecture

- Unity simulation layer.
- gRPC bridge.
- ROS2 interface.
- Coordinate frames.
- Sensor and camera streams.
- Command and external reference interfaces.

## 4. Vehicle and Tether Models

- Drone model and mission states.
- Surface landing and readiness.
- Tether/winch model.
- MiniROV model and mission/path planner.
- Deployment and recovery workflow.

## 5. ROS2/gRPC Research Interface

- Topics.
- Services.
- TF and clock.
- Manual control.
- External references.
- Rosbag and experiment runner.

## 6. Benchmark Scenarios

- Middleware connectivity.
- Drone reference tracking.
- Surface landing and tether readiness.
- Tether deployment/recovery.
- MiniROV path execution.
- Integrated deploy-inspect-recover.
- Dataset generation.

## 7. Research Use Cases

- Drone localization.
- MiniROV localization.
- Tether-based ROV localization.
- AI/VLM perception and mission planning.
- Custom trajectory and reference control.
- Dataset generation.

## 8. Evaluation

Results to be inserted after benchmark runs.

## 9. Discussion

- Current limitations.
- Sensor noise and fidelity.
- Public release strategy.
- Future Nav2 and low-level actuator interfaces.

## 10. Conclusion

Summarize MIMISK-ROS as a reusable multi-domain simulator interface for drone-deployed tethered MiniROV research.
