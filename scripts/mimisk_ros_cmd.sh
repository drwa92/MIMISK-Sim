#!/usr/bin/env bash
set -e

MIMISK_ROOT="$HOME/mimisk_minirov_unity/mimisk_minirov_v2"

source "$MIMISK_ROOT/scripts/mimisk_ros2_env.sh"

usage() {
    cat << USAGE
MIMISK ROS2/gRPC command helper

Usage:
  ./scripts/mimisk_ros_cmd.sh check
  ./scripts/mimisk_ros_cmd.sh topics
  ./scripts/mimisk_ros_cmd.sh services

Drone:
  ./scripts/mimisk_ros_cmd.sh drone manual
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

Tether:
  ./scripts/mimisk_ros_cmd.sh tether attach
  ./scripts/mimisk_ros_cmd.sh tether deploy
  ./scripts/mimisk_ros_cmd.sh tether activate_rov_control
  ./scripts/mimisk_ros_cmd.sh tether enable_smart_tms
  ./scripts/mimisk_ros_cmd.sh tether disable_smart_tms
  ./scripts/mimisk_ros_cmd.sh tether hold_winch
  ./scripts/mimisk_ros_cmd.sh tether recover

MiniROV:
  ./scripts/mimisk_ros_cmd.sh minirov manual_direct
  ./scripts/mimisk_ros_cmd.sh minirov station_hold
  ./scripts/mimisk_ros_cmd.sh minirov set_path CircleInspection
  ./scripts/mimisk_ros_cmd.sh minirov start_mission
  ./scripts/mimisk_ros_cmd.sh minirov return_home
  ./scripts/mimisk_ros_cmd.sh minirov dwell
  ./scripts/mimisk_ros_cmd.sh minirov abort_to_hold

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

Monitor command results:
  ros2 topic echo /mimisk/command_result
USAGE
}

call_trigger() {
    local service_name="$1"
    ros2 service call "$service_name" std_srvs/srv/Trigger "{}"
}

call_set_string() {
    local service_name="$1"
    local value="$2"

    if [ -z "$value" ]; then
        echo "[MIMISK] ERROR: missing string value for $service_name"
        exit 1
    fi

    ros2 service call "$service_name" mimisk_msgs/srv/SetString "{value: '$value'}"
}

send_json_command() {
    local target="$1"
    local verb="$2"
    local arg="${3:-}"

    ros2 topic pub --once /mimisk/command_json std_msgs/msg/String \
        "{data: '{\"target_agent\":\"$target\",\"verb\":\"$verb\",\"string_arg\":\"$arg\"}'}"
}

if [ $# -lt 1 ]; then
    usage
    exit 0
fi

group="$1"
cmd="${2:-}"
arg="${3:-}"

case "$group" in
    help|-h|--help)
        usage
        ;;

    check)
        echo "=== MIMISK topics ==="
        ros2 topic list | grep /mimisk || true
        echo ""
        echo "=== MIMISK services ==="
        ros2 service list | grep /mimisk || true
        echo ""
        echo "=== Custom interfaces ==="
        ros2 interface show mimisk_msgs/srv/SetString || true
        ;;

    topics)
        ros2 topic list | grep /mimisk || true
        ;;

    services)
        ros2 service list | grep /mimisk || true
        ;;

    drone)
        case "$cmd" in
            manual|gamepad)
                call_trigger /mimisk/drone/manual
                ;;
            hold)
                call_trigger /mimisk/drone/hold
                ;;
            land_surface|land)
                call_trigger /mimisk/drone/land_surface
                ;;
            takeoff)
                call_trigger /mimisk/drone/takeoff
                ;;
            start_mission|start)
                call_trigger /mimisk/drone/start_mission
                ;;
            set_trajectory|trajectory)
                call_set_string /mimisk/drone/set_trajectory "$arg"
                ;;
            *)
                echo "[MIMISK] Unknown drone command: $cmd"
                usage
                exit 1
                ;;
        esac
        ;;

    tether)
        case "$cmd" in
            attach|attach_rov)
                call_trigger /mimisk/tether/attach_rov
                ;;
            deploy|deploy_rov)
                call_trigger /mimisk/tether/deploy_rov
                ;;
            activate_rov_control|activate|rov_control)
                call_trigger /mimisk/tether/activate_rov_control
                ;;
            enable_smart_tms|smart_tms_on)
                call_trigger /mimisk/tether/enable_smart_tms
                ;;
            disable_smart_tms|smart_tms_off)
                call_trigger /mimisk/tether/disable_smart_tms
                ;;
            hold_winch|hold)
                call_trigger /mimisk/tether/hold_winch
                ;;
            recover|recover_rov)
                call_trigger /mimisk/tether/recover_rov
                ;;
            *)
                echo "[MIMISK] Unknown tether command: $cmd"
                usage
                exit 1
                ;;
        esac
        ;;

    minirov|rov)
        case "$cmd" in
            manual_direct|manual|gamepad)
                call_trigger /mimisk/minirov/manual_direct
                ;;
            station_hold|hold)
                call_trigger /mimisk/minirov/station_hold
                ;;
            set_path|path)
                call_set_string /mimisk/minirov/set_path "$arg"
                ;;
            start_mission|start)
                call_trigger /mimisk/minirov/start_mission
                ;;
            return_home|home)
                call_trigger /mimisk/minirov/return_home
                ;;
            dwell)
                call_trigger /mimisk/minirov/dwell
                ;;
            abort_to_hold|abort|stop)
                call_trigger /mimisk/minirov/abort_to_hold
                ;;
            *)
                echo "[MIMISK] Unknown MiniROV command: $cmd"
                usage
                exit 1
                ;;
        esac
        ;;

    json)
        if [ $# -lt 3 ]; then
            echo "[MIMISK] Usage: ./scripts/mimisk_ros_cmd.sh json <target> <verb> [arg]"
            exit 1
        fi

        send_json_command "$2" "$3" "${4:-}"
        ;;

    *)
        echo "[MIMISK] Unknown command group: $group"
        usage
        exit 1
        ;;
esac
