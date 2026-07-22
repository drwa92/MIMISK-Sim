#!/usr/bin/env bash
set -e

source "$HOME/mimisk_minirov_unity/mimisk_minirov_v2/scripts/mimisk_ros2_env.sh"

STAMP="$(date +%Y%m%d_%H%M%S)"
OUT_DIR="$HOME/mimisk_minirov_unity/mimisk_minirov_v2/Logs/ROSBags/mimisk_${STAMP}"

mkdir -p "$(dirname "$OUT_DIR")"

echo "[MIMISK] Recording rosbag to:"
echo "  $OUT_DIR"

exec ros2 bag record \
  -o "$OUT_DIR" \
  /clock \
  /tf \
  /tf_static \
  /mimisk/drone/state \
  /mimisk/drone/odom \
  /mimisk/drone/imu \
  /mimisk/drone/pose \
  /mimisk/drone/camera/image_raw/compressed \
  /mimisk/drone/camera/camera_info \
  /mimisk/minirov/state \
  /mimisk/minirov/odom \
  /mimisk/minirov/imu \
  /mimisk/minirov/depth \
  /mimisk/minirov/pose \
  /mimisk/minirov/front_camera/image_raw/compressed \
  /mimisk/minirov/front_camera/camera_info \
  /mimisk/tether/state \
  /mimisk/command_result_typed \
  /mimisk/manual/drone \
  /mimisk/manual/minirov \
  /mimisk/camera/status \
  /mimisk/system/events
