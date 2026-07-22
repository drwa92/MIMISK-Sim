#!/usr/bin/env bash
set -e

source "$HOME/mimisk_minirov_unity/mimisk_minirov_v2/scripts/mimisk_ros2_env.sh"

cd "$MIMISK_ROOT"

echo "[MIMISK] Regenerating Python protobuf files..."
mkdir -p ros2_ws/src/mimisk_grpc_adapter/mimisk_grpc_adapter/protobuf

$MIMISK_PYTHON -m grpc_tools.protoc \
  -I proto \
  --python_out=ros2_ws/src/mimisk_grpc_adapter/mimisk_grpc_adapter/protobuf \
  --grpc_python_out=ros2_ws/src/mimisk_grpc_adapter/mimisk_grpc_adapter/protobuf \
  proto/mimisk_bridge.proto

echo "[MIMISK] Building ROS2 packages..."
cd "$MIMISK_ROS_WS"

rm -rf build/mimisk_msgs install/mimisk_msgs
rm -rf build/mimisk_grpc_adapter install/mimisk_grpc_adapter

colcon build --symlink-install --packages-select mimisk_msgs mimisk_grpc_adapter

source install/setup.bash

echo "[MIMISK] Verifying imports/interfaces..."
ros2 interface show mimisk_msgs/srv/SetString

$MIMISK_PYTHON - << 'PY'
import rclpy
import grpc
import mimisk_grpc_adapter.server as server
print("MIMISK ROS2/gRPC bridge import OK:", server.__file__)
PY

echo "[MIMISK] Build complete."
