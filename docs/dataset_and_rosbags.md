# Dataset and Rosbag Generation

## Record a rosbag

Start the gRPC server and Unity simulation, then run:

```bash
./scripts/record_mimisk_rosbag.sh
```

## Recommended recorded topics

```text
/clock
/tf
/tf_static
/mimisk/drone/state
/mimisk/drone/odom
/mimisk/drone/imu
/mimisk/drone/camera/image_raw/compressed
/mimisk/drone/camera/camera_info
/mimisk/minirov/state
/mimisk/minirov/odom
/mimisk/minirov/imu
/mimisk/minirov/depth
/mimisk/minirov/front_camera/image_raw/compressed
/mimisk/minirov/front_camera/camera_info
/mimisk/tether/state
/mimisk/command_result_typed
/mimisk/manual/drone
/mimisk/manual/minirov
/mimisk/reference/drone
/mimisk/reference/minirov
/mimisk/reference/tether
/mimisk/system/events
```

## Sample bag naming

```text
mimisk_<scenario>_<date>_<run_id>
```

Examples:

```text
mimisk_integrated_circleinspection_20260712_run01
mimisk_tether_deploy_recover_20260712_run02
mimisk_minirov_lawnmower_20260712_run03
```

## Dataset metadata

Each dataset should include:

```text
scenario name
Unity scene name
ROS 2 package version
git commit hash
vehicle configuration
sensor settings
camera resolution and rate
tether settings
mission path
notes
```

## Dataset use cases

MIMISK rosbags can support:

- drone localization;
- MiniROV localization;
- tether-based relative localization;
- visual inspection and AI perception;
- VLM/LLM mission planning;
- manual-control demonstration learning;
- trajectory planning and reference-control evaluation.
