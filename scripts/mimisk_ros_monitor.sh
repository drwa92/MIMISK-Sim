#!/usr/bin/env bash
set -e

MIMISK_ROOT="$HOME/mimisk_minirov_unity/mimisk_minirov_v2"

source "$MIMISK_ROOT/scripts/mimisk_ros2_env.sh"

usage() {
    cat << USAGE
MIMISK ROS2 monitor helper

Usage:
  ./scripts/mimisk_ros_monitor.sh drone
  ./scripts/mimisk_ros_monitor.sh minirov
  ./scripts/mimisk_ros_monitor.sh tether
  ./scripts/mimisk_ros_monitor.sh command
  ./scripts/mimisk_ros_monitor.sh ack
  ./scripts/mimisk_ros_monitor.sh events
  ./scripts/mimisk_ros_monitor.sh poses
USAGE
}

target="${1:-}"

case "$target" in
    drone)
        ros2 topic echo /mimisk/drone/state_text
        ;;
    minirov|rov)
        ros2 topic echo /mimisk/minirov/state_text
        ;;
    tether)
        ros2 topic echo /mimisk/tether/state_text
        ;;
    command|result)
        ros2 topic echo /mimisk/command_result
        ;;
    ack)
        ros2 topic echo /mimisk/command_ack
        ;;
    events)
        ros2 topic echo /mimisk/system/events
        ;;
    poses)
        echo "[MIMISK] Drone pose:"
        ros2 topic echo /mimisk/drone/pose
        ;;
    *)
        usage
        exit 1
        ;;
esac
