# Coordinate Frames

## Unity to ROS conversion

MIMISK uses Unity world coordinates internally. The ROS 2 interface converts to a ROS-friendly frame using:

```text
Unity X → ROS X
Unity Z → ROS Y
Unity Y → ROS Z
```

Therefore:

```text
Unity(x, y, z) = ROS(x, z, y)
ROS(x, y, z)   = Unity(x, z, y)
```

## Main frames

```text
mimisk_world
 ├── drone_base_link
 │    ├── drone_imu_link
 │    ├── drone_camera_optical
 │    └── drone_payload_frame
 ├── winch_fairlead
 ├── tether_end
 └── minirov_base_link
      ├── minirov_imu_link
      ├── minirov_pressure_link
      ├── minirov_front_camera_optical
      └── minirov_tether_anchor
```

## External reference convention

Drone external reference:

```text
/mimisk/reference/drone
position.x = ROS X
position.y = ROS Y
position.z = ROS Z, positive upward
yaw_deg    = heading about ROS Z
```

MiniROV external reference:

```text
/mimisk/reference/minirov
position.x = ROS X
position.y = ROS Y
depth_m    = positive downward
yaw_deg    = heading about ROS Z
```

Tether external reference:

```text
/mimisk/reference/tether
target_length_m = desired tether deployed length
```
