# Research Use Cases Enabled by MIMISK-ROS

## Drone localization

MIMISK-ROS exposes drone odometry, IMU, pose, camera, TF, and clock. This supports:

- EKF / UKF / particle-filter localization;
- visual-inertial odometry;
- camera-based surface landing localization;
- RTK/GNSS-like evaluation using configurable future sensor models.

## MiniROV localization

MIMISK-ROS exposes MiniROV odometry, IMU, pressure/depth, front camera, TF, and ground truth. This supports:

- depth-aided localization;
- visual-depth-inertial odometry;
- camera-based underwater target tracking;
- inspection mapping.

## Tether-based ROV localization

MIMISK-ROS exposes tether length, target length, tether endpoints, straight distance, slack, stretch, tension proxy, MiniROV depth, and MiniROV ground truth. This supports:

- tether-length/depth relative localization;
- slack-aware uncertainty modeling;
- tether-assisted return-home policies;
- active tether management research.

## AI / VLM / LLM mission planning

The drone and MiniROV camera streams, mission states, command services, and external references allow researchers to implement:

- VLM-based inspection target selection;
- LLM-based mission sequencing;
- anomaly detection from drone/ROV cameras;
- human-in-the-loop command interpretation;
- autonomous deployment decision-making.

## Custom trajectory planning and reference control

External ROS 2 nodes can subscribe to odometry/tether states and publish:

```text
/mimisk/reference/drone
/mimisk/reference/minirov
/mimisk/reference/tether
```

This enables custom trajectory generation and reference-level path tracking without modifying Unity C# controllers.

## Dataset generation

MIMISK-ROS can produce synchronized rosbags containing:

- state;
- odometry;
- IMU;
- depth;
- cameras;
- tether telemetry;
- manual control;
- external references;
- command results;
- TF;
- clock.
