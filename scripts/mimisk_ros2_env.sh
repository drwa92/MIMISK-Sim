#!/usr/bin/env bash

# MIMISK ROS2/gRPC environment for ROS 2 Jazzy.
# Source this file before building or running the MIMISK ROS2 bridge.

export MIMISK_ROOT="$HOME/mimisk_minirov_unity/mimisk_minirov_v2"
export MIMISK_ROS_WS="$MIMISK_ROOT/ros2_ws"
export ROS_DISTRO="jazzy"

# Avoid accidentally using an active virtualenv/conda Python for ROS2.
if [ -n "${VIRTUAL_ENV:-}" ]; then
    export PATH="$(printf "%s" "$PATH" | tr ':' '\n' | grep -v "^${VIRTUAL_ENV}/bin$" | paste -sd ':' -)"
    unset VIRTUAL_ENV
fi

unset PYTHONHOME

source "/opt/ros/${ROS_DISTRO}/setup.bash"

if [ -f "$MIMISK_ROS_WS/install/setup.bash" ]; then
    source "$MIMISK_ROS_WS/install/setup.bash"
fi

# Keep source package visible for direct Python import tests.
case ":${PYTHONPATH:-}:" in
    *":$MIMISK_ROS_WS/src/mimisk_grpc_adapter:"*) ;;
    *)
        export PYTHONPATH="$MIMISK_ROS_WS/src/mimisk_grpc_adapter:${PYTHONPATH:-}"
        ;;
esac

export MIMISK_PYTHON="/usr/bin/python3"
