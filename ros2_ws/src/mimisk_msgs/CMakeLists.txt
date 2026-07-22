cmake_minimum_required(VERSION 3.8)
project(mimisk_msgs)

find_package(ament_cmake REQUIRED)
find_package(rosidl_default_generators REQUIRED)
find_package(std_msgs REQUIRED)
find_package(geometry_msgs REQUIRED)

rosidl_generate_interfaces(${PROJECT_NAME}
  "msg/DroneState.msg"
  "msg/MiniROVState.msg"
  "msg/TetherState.msg"
  "msg/CommandResult.msg"
  "msg/GamepadCommand.msg"
  "msg/ExternalReference.msg"
  "srv/SetString.srv"
  DEPENDENCIES std_msgs geometry_msgs
)

ament_export_dependencies(rosidl_default_runtime)
ament_package()
