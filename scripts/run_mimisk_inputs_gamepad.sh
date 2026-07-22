#!/usr/bin/env bash
set -e

TARGET="${1:-drone}"
shift || true

source "$HOME/mimisk_minirov_unity/mimisk_minirov_v2/scripts/mimisk_ros2_env.sh"

echo "[MIMISK] Starting physical inputs gamepad mapper"
echo "[MIMISK] Target: $TARGET"
echo "[MIMISK] Auto activation: ON"
echo ""
echo "Extra ROS args may be passed after target, for example:"
echo "  ./scripts/run_mimisk_inputs_gamepad.sh drone -p diagnose:=true"
echo ""

exec ros2 run mimisk_grpc_adapter mimisk_inputs_gamepad_mapper \
  --ros-args \
  -p target:="$TARGET" \
  -p auto_activate_mode:=true \
  "$@"
