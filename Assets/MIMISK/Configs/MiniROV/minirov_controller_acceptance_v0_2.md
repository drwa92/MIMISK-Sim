# MiniROV Robust Nominal Controller v0.2 Acceptance Report

## Platform

Reduced-order MiniROV model:

- two rear thrusters
- ballast / DC depth actuation
- IMU yaw/yaw-rate sensing
- pressure/depth sensing
- ControlManager motor-frame backend

## Manual mapping

- lx = yaw
- ly = surge / throttle
- rt - lt = ballast / depth
- left_thruster = surge_cmd + yaw_cmd
- right_thruster = surge_cmd - yaw_cmd
- dc_port = ballast_cmd
- dc_starboard = ballast_cmd

## Inner-loop validation

- surge RMS error: 0.0200 m/s
- surge settling time: 6.19 s
- yaw settling time: 14.82 s
- depth settling time: 38.02 s
- actuator saturation: 0.0000

## Nominal LOS path validation

- line RMS tracking error: 0.0425 m
- square RMS tracking error: 0.0517 m
- circle RMS tracking error: 0.0650 m
- actuator saturation: 0.0000

## Robustness validation

### Light current

- line RMS: 0.0960 m
- square RMS: 0.0892 m
- circle RMS: 0.2364 m

### Sensor noise

- line RMS: 0.0401 m
- square RMS: 0.0568 m
- circle RMS: 0.0676 m

### 15 percent model mismatch

- line RMS: 0.0428 m
- square RMS: 0.0531 m
- circle RMS: 0.0648 m

### Combined robust case

- line RMS: 0.1031 m
- square RMS: 0.1180 m
- circle RMS: 0.2252 m

### Strong-current stress

- line RMS: 0.1938 m
- square RMS: 0.1966 m
- circle RMS: 0.5470 m

## Acceptance

This controller is accepted as the Python robust nominal MiniROV controller baseline for Unity implementation.

The strong-current circle case is treated as a stress test. It remains bounded and does not fail, but tighter circle tracking under strong current should later use current estimation or adaptive LOS compensation.
