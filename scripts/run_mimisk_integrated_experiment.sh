#!/usr/bin/env bash
set -e

ROV_PATH="${1:-CircleInspection}"
ROV_DURATION="${2:-30}"

source "$HOME/mimisk_minirov_unity/mimisk_minirov_v2/scripts/mimisk_ros2_env.sh"

echo "[MIMISK] Running integrated experiment"
echo "[MIMISK] ROV path: $ROV_PATH"
echo "[MIMISK] ROV mission duration: $ROV_DURATION s"

exec ros2 run mimisk_grpc_adapter mimisk_integrated_experiment \
  --rov-path "$ROV_PATH" \
  --rov-mission-duration "$ROV_DURATION"
