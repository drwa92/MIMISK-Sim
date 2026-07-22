#!/usr/bin/env bash
set -e

source "$HOME/mimisk_minirov_unity/mimisk_minirov_v2/scripts/mimisk_ros2_env.sh"

usage() {
    cat << USAGE
MIMISK ROS external reference helper

Usage:
  ./scripts/mimisk_ros_ref.sh drone_pose <x> <y> <z> <yaw_deg>
      ROS frame: x forward/east, y lateral/north, z up.
      Unity mapping: Unity(x,y,z) = ROS(x,z,y)

  ./scripts/mimisk_ros_ref.sh minirov_goto <x> <y> <depth_m> [yaw_deg]
      ROS horizontal x/y, positive-down depth.

  ./scripts/mimisk_ros_ref.sh tether_length <length_m>

Examples:
  ./scripts/mimisk_ros_ref.sh drone_pose 0 -10 2 0
  ./scripts/mimisk_ros_ref.sh minirov_goto 1 -8 1.2 0
  ./scripts/mimisk_ros_ref.sh tether_length 3.0
USAGE
}

if [ $# -lt 1 ]; then
    usage
    exit 0
fi

cmd="$1"

case "$cmd" in
    drone_pose)
        if [ $# -lt 5 ]; then
            usage
            exit 1
        fi

        x="$2"
        y="$3"
        z="$4"
        yaw="$5"

        ros2 topic pub --rate 10 /mimisk/reference/drone mimisk_msgs/msg/ExternalReference \
"{target_agent: 'drone', enabled: true, position_valid: true, velocity_valid: true, acceleration_valid: true, yaw_valid: true, position: {x: $x, y: $y, z: $z}, velocity: {x: 0.0, y: 0.0, z: 0.0}, acceleration: {x: 0.0, y: 0.0, z: 0.0}, yaw_deg: $yaw, mode: 'external_pose'}"
        ;;

    minirov_goto)
        if [ $# -lt 4 ]; then
            usage
            exit 1
        fi

        x="$2"
        y="$3"
        depth="$4"
        yaw="${5:-0.0}"

        ros2 topic pub --rate 5 /mimisk/reference/minirov mimisk_msgs/msg/ExternalReference \
"{target_agent: 'minirov', enabled: true, position_valid: true, depth_valid: true, yaw_valid: true, position: {x: $x, y: $y, z: 0.0}, depth_m: $depth, yaw_deg: $yaw, mode: 'goto'}"
        ;;

    tether_length)
        if [ $# -lt 2 ]; then
            usage
            exit 1
        fi

        length="$2"

        ros2 topic pub --rate 2 /mimisk/reference/tether mimisk_msgs/msg/ExternalReference \
"{target_agent: 'tether', enabled: true, target_length_m: $length, mode: 'target_length'}"
        ;;

    *)
        usage
        exit 1
        ;;
esac
