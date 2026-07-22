# MIMISK ROS2/gRPC Command Cheat Sheet

This document summarizes how to run the MIMISK v2 ROS2/gRPC bridge.

## 1. Environment

Use this in every new terminal:

```bash
source ~/mimisk_minirov_unity/mimisk_minirov_v2/scripts/mimisk_ros2_env.sh
2. Build bridge

~/mimisk_minirov_unity/mimisk_minirov_v2/scripts/build_mimisk_ros2_bridge.sh
3. Regenerate Unity C# gRPC files

Use this when proto/mimisk_bridge.proto changes:
~/mimisk_minirov_unity/mimisk_minirov_v2/scripts/generate_mimisk_grpc_csharp.sh

3. Regenerate Unity C# gRPC files

Use this when proto/mimisk_bridge.proto changes:
~/mimisk_minirov_unity/mimisk_minirov_v2/scripts/generate_mimisk_grpc_csharp.sh

4. Run gRPC server

Terminal 1:
~/mimisk_minirov_unity/mimisk_minirov_v2/scripts/run_mimisk_grpc_server.sh

6. Monitor telemetry
./scripts/mimisk_ros_monitor.sh drone
./scripts/mimisk_ros_monitor.sh minirov
./scripts/mimisk_ros_monitor.sh tether
./scripts/mimisk_ros_monitor.sh command

ros2 topic echo /mimisk/drone/state_text
ros2 topic echo /mimisk/minirov/state_text
ros2 topic echo /mimisk/tether/state_text
ros2 topic echo /mimisk/command_result

7. Drone commands

./scripts/mimisk_ros_cmd.sh drone hold
./scripts/mimisk_ros_cmd.sh drone land_surface
./scripts/mimisk_ros_cmd.sh drone takeoff
./scripts/mimisk_ros_cmd.sh drone start_mission
./scripts/mimisk_ros_cmd.sh drone set_trajectory SpiralOut

Drone trajectory options:

Circle
SpiralOut
HelixDown
HelixUpDown
FigureEight
SmoothSquare
Lawnmower
DeploymentApproach

8. Tether commands
./scripts/mimisk_ros_cmd.sh tether attach
./scripts/mimisk_ros_cmd.sh tether deploy
./scripts/mimisk_ros_cmd.sh tether activate_rov_control
./scripts/mimisk_ros_cmd.sh tether enable_smart_tms
./scripts/mimisk_ros_cmd.sh tether disable_smart_tms
./scripts/mimisk_ros_cmd.sh tether hold_winch
./scripts/mimisk_ros_cmd.sh tether recover

Manual equivalents:

ros2 service call /mimisk/tether/attach_rov std_srvs/srv/Trigger "{}"
ros2 service call /mimisk/tether/deploy_rov std_srvs/srv/Trigger "{}"
ros2 service call /mimisk/tether/activate_rov_control std_srvs/srv/Trigger "{}"
ros2 service call /mimisk/tether/enable_smart_tms std_srvs/srv/Trigger "{}"
ros2 service call /mimisk/tether/disable_smart_tms std_srvs/srv/Trigger "{}"
ros2 service call /mimisk/tether/hold_winch std_srvs/srv/Trigger "{}"
ros2 service call /mimisk/tether/recover_rov std_srvs/srv/Trigger "{}"

Recommended tether workflow:

./scripts/mimisk_ros_cmd.sh tether attach
./scripts/mimisk_ros_cmd.sh tether deploy
./scripts/mimisk_ros_cmd.sh tether activate_rov_control
./scripts/mimisk_ros_cmd.sh tether enable_smart_tms
9. MiniROV commands
./scripts/mimisk_ros_cmd.sh minirov station_hold
./scripts/mimisk_ros_cmd.sh minirov set_path CircleInspection
./scripts/mimisk_ros_cmd.sh minirov start_mission
./scripts/mimisk_ros_cmd.sh minirov return_home
./scripts/mimisk_ros_cmd.sh minirov dwell
./scripts/mimisk_ros_cmd.sh minirov abort_to_hold

Manual equivalents:

ros2 service call /mimisk/minirov/station_hold std_srvs/srv/Trigger "{}"
ros2 service call /mimisk/minirov/set_path mimisk_msgs/srv/SetString "{value: 'CircleInspection'}"
ros2 service call /mimisk/minirov/start_mission std_srvs/srv/Trigger "{}"
ros2 service call /mimisk/minirov/return_home std_srvs/srv/Trigger "{}"
ros2 service call /mimisk/minirov/dwell std_srvs/srv/Trigger "{}"
ros2 service call /mimisk/minirov/abort_to_hold std_srvs/srv/Trigger "{}"

MiniROV path options:

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
10. JSON fallback command

Use this for advanced commands or temporary testing:

./scripts/mimisk_ros_cmd.sh json minirov set_path GoToPoint

Manual equivalent:

ros2 topic pub --once /mimisk/command_json std_msgs/msg/String \
"{data: '{\"target_agent\":\"minirov\",\"verb\":\"set_path\",\"string_arg\":\"GoToPoint\"}'}"
11. Full integrated example

Start gRPC server:

./scripts/run_mimisk_grpc_server.sh

Press Play in Unity.

Then in another terminal:

source ./scripts/mimisk_ros2_env.sh

./scripts/mimisk_ros_cmd.sh drone start_mission

# After drone is surface-ready:
./scripts/mimisk_ros_cmd.sh tether attach
./scripts/mimisk_ros_cmd.sh tether deploy
./scripts/mimisk_ros_cmd.sh tether activate_rov_control
./scripts/mimisk_ros_cmd.sh tether enable_smart_tms

./scripts/mimisk_ros_cmd.sh minirov set_path CircleInspection
./scripts/mimisk_ros_cmd.sh minirov start_mission

# Later:
./scripts/mimisk_ros_cmd.sh minirov return_home
./scripts/mimisk_ros_cmd.sh tether recover

Monitor:

./scripts/mimisk_ros_monitor.sh tether
./scripts/mimisk_ros_monitor.sh minirov
./scripts/mimisk_ros_monitor.sh command


Gamepad:
cd ~/mimisk_minirov_unity/mimisk_minirov_v2

./scripts/run_mimisk_inputs_gamepad.sh drone

cd ~/mimisk_minirov_unity/mimisk_minirov_v2
source scripts/mimisk_ros2_env.sh

./scripts/mimisk_ros_cmd.sh tether attach
./scripts/mimisk_ros_cmd.sh tether deploy
./scripts/mimisk_ros_cmd.sh tether activate_rov_control

./scripts/run_mimisk_inputs_gamepad.sh minirov


Reference:
./scripts/mimisk_ros_ref.sh drone_pose 0 -10 2 0

./scripts/mimisk_ros_ref.sh minirov_goto 1 -8 1.2 0

