# MIMISK-ROS ROS 2 API

## State topics

| Topic | Type | Description |
|---|---|---|
| `/mimisk/drone/state` | `mimisk_msgs/msg/DroneState` | Typed drone state. |
| `/mimisk/drone/state_text` | `std_msgs/msg/String` | JSON debug drone state. |
| `/mimisk/drone/odom` | `nav_msgs/msg/Odometry` | Drone odometry in ROS frame. |
| `/mimisk/drone/pose` | `geometry_msgs/msg/PoseStamped` | Drone pose. |
| `/mimisk/minirov/state` | `mimisk_msgs/msg/MiniROVState` | Typed MiniROV state. |
| `/mimisk/minirov/state_text` | `std_msgs/msg/String` | JSON debug MiniROV state. |
| `/mimisk/minirov/odom` | `nav_msgs/msg/Odometry` | MiniROV odometry in ROS frame. |
| `/mimisk/minirov/pose` | `geometry_msgs/msg/PoseStamped` | MiniROV pose. |
| `/mimisk/tether/state` | `mimisk_msgs/msg/TetherState` | Typed tether/winch state. |
| `/mimisk/tether/state_text` | `std_msgs/msg/String` | JSON debug tether state. |

## Sensor topics

| Topic | Type | Description |
|---|---|---|
| `/mimisk/drone/imu` | `sensor_msgs/msg/Imu` | Drone IMU-like orientation stream. |
| `/mimisk/minirov/imu` | `sensor_msgs/msg/Imu` | MiniROV IMU-like orientation stream. |
| `/mimisk/minirov/depth` | `sensor_msgs/msg/FluidPressure` | MiniROV depth converted to pressure. |
| `/mimisk/drone/camera/image_raw/compressed` | `sensor_msgs/msg/CompressedImage` | Drone compressed camera image. |
| `/mimisk/drone/camera/camera_info` | `sensor_msgs/msg/CameraInfo` | Drone camera intrinsics. |
| `/mimisk/minirov/front_camera/image_raw/compressed` | `sensor_msgs/msg/CompressedImage` | MiniROV front camera image. |
| `/mimisk/minirov/front_camera/camera_info` | `sensor_msgs/msg/CameraInfo` | MiniROV front camera intrinsics. |

## Command services

| Service | Type | Description |
|---|---|---|
| `/mimisk/drone/hold` | `std_srvs/srv/Trigger` | Drone hold command. |
| `/mimisk/drone/land_surface` | `std_srvs/srv/Trigger` | Land drone on surface. |
| `/mimisk/drone/takeoff` | `std_srvs/srv/Trigger` | Drone takeoff. |
| `/mimisk/drone/start_mission` | `std_srvs/srv/Trigger` | Start drone mission manager. |
| `/mimisk/drone/manual` | `std_srvs/srv/Trigger` | Enter drone gamepad/manual mode. |
| `/mimisk/drone/set_trajectory` | `mimisk_msgs/srv/SetString` | Select drone trajectory type. |
| `/mimisk/tether/attach_rov` | `std_srvs/srv/Trigger` | Attach ROV to tether endpoint. |
| `/mimisk/tether/deploy_rov` | `std_srvs/srv/Trigger` | Deploy ROV. |
| `/mimisk/tether/activate_rov_control` | `std_srvs/srv/Trigger` | Activate MiniROV control stack after deployment. |
| `/mimisk/tether/enable_smart_tms` | `std_srvs/srv/Trigger` | Enable smart tether management. |
| `/mimisk/tether/disable_smart_tms` | `std_srvs/srv/Trigger` | Disable smart tether management. |
| `/mimisk/tether/hold_winch` | `std_srvs/srv/Trigger` | Hold winch. |
| `/mimisk/tether/recover_rov` | `std_srvs/srv/Trigger` | Recover ROV. |
| `/mimisk/minirov/manual_direct` | `std_srvs/srv/Trigger` | Enter MiniROV direct manual mode. |
| `/mimisk/minirov/station_hold` | `std_srvs/srv/Trigger` | MiniROV station hold. |
| `/mimisk/minirov/set_path` | `mimisk_msgs/srv/SetString` | Select MiniROV path type. |
| `/mimisk/minirov/start_mission` | `std_srvs/srv/Trigger` | Start MiniROV selected mission. |
| `/mimisk/minirov/return_home` | `std_srvs/srv/Trigger` | MiniROV return home. |
| `/mimisk/minirov/dwell` | `std_srvs/srv/Trigger` | MiniROV dwell. |
| `/mimisk/minirov/abort_to_hold` | `std_srvs/srv/Trigger` | Abort to hold. |

## Command result topics

| Topic | Type | Description |
|---|---|---|
| `/mimisk/command_ack` | `std_msgs/msg/String` | ROS-side command queue acknowledgement. |
| `/mimisk/command_result` | `std_msgs/msg/String` | JSON Unity execution result. |
| `/mimisk/command_result_typed` | `mimisk_msgs/msg/CommandResult` | Typed Unity command execution result. |

## Manual-control topics

| Topic | Type | Description |
|---|---|---|
| `/mimisk/manual/drone` | `mimisk_msgs/msg/GamepadCommand` | Drone manual gamepad input. |
| `/mimisk/manual/minirov` | `mimisk_msgs/msg/GamepadCommand` | MiniROV manual gamepad input. |
| `/mimisk/manual/status` | `std_msgs/msg/String` | Manual input status. |

## External-reference topics

| Topic | Type | Description |
|---|---|---|
| `/mimisk/reference/drone` | `mimisk_msgs/msg/ExternalReference` | Drone external pose/velocity/yaw reference. |
| `/mimisk/reference/minirov` | `mimisk_msgs/msg/ExternalReference` | MiniROV GoToPoint/depth/yaw reference. |
| `/mimisk/reference/tether` | `mimisk_msgs/msg/ExternalReference` | Tether target length reference. |
| `/mimisk/reference/status` | `std_msgs/msg/String` | External reference status. |

## ROS infrastructure

| Topic | Type | Description |
|---|---|---|
| `/clock` | `rosgraph_msgs/msg/Clock` | Simulation time. |
| `/tf` | `tf2_msgs/msg/TFMessage` | Dynamic transforms. |
| `/tf_static` | `tf2_msgs/msg/TFMessage` | Static transforms. |

## Common command examples

```bash
./scripts/mimisk_ros_cmd.sh drone start_mission
./scripts/mimisk_ros_cmd.sh tether attach
./scripts/mimisk_ros_cmd.sh tether deploy
./scripts/mimisk_ros_cmd.sh tether activate_rov_control
./scripts/mimisk_ros_cmd.sh minirov set_path CircleInspection
./scripts/mimisk_ros_cmd.sh minirov start_mission
./scripts/mimisk_ros_cmd.sh minirov return_home
./scripts/mimisk_ros_cmd.sh tether recover
```

## External reference examples

```bash
./scripts/mimisk_ros_ref.sh drone_pose 0 -10 2 0
./scripts/mimisk_ros_ref.sh minirov_goto 1 -8 1.2 0
./scripts/mimisk_ros_ref.sh tether_length 3.0
```
