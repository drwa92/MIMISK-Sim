# MIMISK Inspector Validation Runner



## Run tests

1. Press Play.
2. Select `MIMISK_ValidationHarness` in the hierarchy.
3. In the Inspector, choose Run ID / Trial ID and click one of the buttons:
   - `Run Full Mission Trial`
   - `Run V8 Tether Trial`
   - `Run ROS2/gRPC Trial`
   - Drone trajectory buttons: `Circle`, `Square`, `Spiral`
   - MiniROV buttons: `Square`, `Lawnmower`, `Circle`, `FigureEight`, `Waypoints`
4. Logs are written to:

```text
Logs/MIMISKValidation/<run_id>/<trial_id>/
```

Each run writes:

```text
manifest.json
state_log.csv
mission_events.csv
tether_log.csv
performance_log.csv
ros_grpc_log.csv
```

For repeated trials, press `Next Trial` before starting the next run. For ROS2/gRPC tests, start the external bridge/server first and enable the ROS2/gRPC toggle or use the `Run ROS2/gRPC Trial` button.


## Complete workflow validation

For the simulator paper, use **Run Complete Workflow Trial (Drone + Tether + MiniROV)**.
This preset enables `include_drone_takeoff_landing=true` and `use_drone_mission_manager=true`, so the run includes drone takeoff, trajectory/mission execution, water-surface landing/hold, tether deployment, MiniROV mission, return-home, recovery, and reattachment.

Use **Run Tether + MiniROV Trial Only** only for debugging the underwater/tether subsystem without the drone transit and landing sequence.
