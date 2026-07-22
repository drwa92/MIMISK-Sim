#!/usr/bin/env bash
set -e

source "$HOME/mimisk_minirov_unity/mimisk_minirov_v2/scripts/mimisk_ros2_env.sh"

cd "$MIMISK_ROS_WS"

source install/setup.bash

echo "[MIMISK] Starting gRPC server on port 30052..."
exec ros2 run mimisk_grpc_adapter mimisk_grpc_server
