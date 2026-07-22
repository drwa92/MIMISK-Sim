# MIMISK-ROS Quick Start

## Build ROS 2 bridge

```bash
cd ~/mimisk_minirov_unity/mimisk_minirov_v2
./scripts/build_mimisk_ros2_bridge.sh
```

## Start gRPC server

```bash
./scripts/run_mimisk_grpc_server.sh
```

## Start Unity

Open the MIMISK Unity project and press Play in the ROS 2 / gRPC bridge scene.

## Verify topics

```bash
source scripts/mimisk_ros2_env.sh
ros2 topic list | grep /mimisk
```

## Run a tether and MiniROV mission

```bash
./scripts/mimisk_ros_cmd.sh tether attach
./scripts/mimisk_ros_cmd.sh tether deploy
./scripts/mimisk_ros_cmd.sh tether activate_rov_control
./scripts/mimisk_ros_cmd.sh tether enable_smart_tms
./scripts/mimisk_ros_cmd.sh minirov set_path CircleInspection
./scripts/mimisk_ros_cmd.sh minirov start_mission
```

## Monitor

```bash
./scripts/mimisk_ros_monitor.sh drone_state
./scripts/mimisk_ros_monitor.sh minirov_state
./scripts/mimisk_ros_monitor.sh tether_state
./scripts/mimisk_ros_monitor.sh command_typed
```

## Record rosbag

```bash
./scripts/record_mimisk_rosbag.sh
```

## Physical gamepad

Drone:

```bash
./scripts/run_mimisk_inputs_gamepad.sh drone
```

MiniROV:

```bash
./scripts/run_mimisk_inputs_gamepad.sh minirov
```

Running the gamepad mapper automatically requests the corresponding manual mode.
