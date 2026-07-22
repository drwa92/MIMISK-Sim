# Sensors, Ground Truth, and Noise Models

## Ground-truth streams

The following topics are simulator ground-truth or direct simulator-state outputs:

```text
/mimisk/drone/pose
/mimisk/drone/odom
/mimisk/minirov/pose
/mimisk/minirov/odom
/mimisk/tether/state
```

These streams are intended for benchmarking localization, controller tracking, and mission performance.

## Simulated measurement streams

The following topics are ROS-compatible simulated sensor streams:

```text
/mimisk/drone/imu
/mimisk/minirov/imu
/mimisk/minirov/depth
/mimisk/drone/camera/image_raw/compressed
/mimisk/minirov/front_camera/image_raw/compressed
```

The first release provides idealized or lightly processed simulated measurements. Future releases should expose configurable noise, bias, delay, dropout, and quantization models.

## Recommended noise models

### IMU

```text
angular velocity white noise
linear acceleration white noise
orientation bias
bias random walk
timestamp jitter
```

### Depth / pressure

```text
pressure white noise
depth bias
quantization
slow drift
```

### Camera

```text
resolution
JPEG quality
frame rate
lens distortion
latency
dropout
motion blur
water visibility effects
```

### Tether

```text
encoder length noise
winch rate quantization
slack uncertainty
stretch uncertainty
tension-proxy uncertainty
endpoint position uncertainty
```

### Drone GNSS / RTK-like measurements

```text
position noise
dropout
update rate
RTK-fixed / RTK-float quality modes
```

## Supported research

MIMISK-ROS enables:

- drone localization against ground truth;
- MiniROV localization using IMU, depth, camera, and tether geometry;
- tether-based MiniROV relative localization;
- VLM/AI perception using synchronized camera streams;
- dataset generation using rosbag2.
