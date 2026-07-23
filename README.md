# MIMISK-Sim

MIMISK-Sim is a Unity-based aerial--aquatic digital-twin simulator for drone-deployed tethered MiniROV inspection missions. The simulator integrates an aerial drone, water-surface landing, winch/reel deployment, runtime tether visualization, MiniROV underwater navigation, mission agents, validation logging, and a ROS2/gRPC bridge.

## Main Features

- Unity-based aerial--aquatic simulation environment.
- Drone takeoff, trajectory tracking, water landing, and surface-hold workflow.
- Winch/reel deployment and recovery sequence for a tethered MiniROV.
- Runtime rear-attached MiniROV tether visualization and tether-state logging.
- MiniROV path following, depth control, return-home, and recovery readiness.
- Guarded mission workflow and agent-based command interface.
- ROS2/gRPC bridge for telemetry, camera streams, commands, manual input, and external references.



## Demo

<p align="center">
  <video src="figures/media/mimisk_sim_complete_demo.mp4" controls width="900"></video>
</p>

<p align="center">
  <em>Complete MIMISK-Sim mission demonstration: surface deployment, underwater MiniROV inspection, runtime tether tracking, and recovery and reattachment.</em>
</p>

### Preview

<p align="center">
  <img src="figures/media/mimisk_sim_4panel_demo_900.gif" alt="MIMISK-Sim workflow demo" width="900"/>
</p>

<p align="center">
  <em>Surface deployment, underwater MiniROV inspection, runtime tether tracking, and Recovery and Reattachment.</em>
</p>


## Repository Structure

```text
Assets/MIMISK/              Unity simulator assets and scripts
Packages/                   Unity package manifest
ProjectSettings/            Unity project settings
proto/                      gRPC / Protocol Buffer definitions
ros2_ws/src/                ROS2 bridge and message packages
unity_bridge_package/       Unity bridge support package
tools/                      Validation and analysis tools
scripts/                    Utility scripts
docs/                       Documentation
figures/                    Paper and documentation figures
examples/                   Example configurations
```

## Requirements

- Unity 6 or the Unity version specified in `ProjectSettings/ProjectVersion.txt`.
- ROS2 Humble or newer for the bridge workspace.
- Python 3.10+ for validation and analysis scripts.
- Git LFS for large Unity assets.

## Quick Start

Clone the repository and pull large assets:

```bash
git clone https://github.com/drwa92/MIMISK-Sim.git
cd MIMISK-Sim
git lfs pull
```

Open the project in Unity and load the main scene:

```text
Assets/MIMISK/Scenes/05_MiniROV_Finale_agent.unity
```

Run the simulator from the Unity Editor.

## ROS2/gRPC Bridge

Build the ROS2 workspace:

```bash
cd ros2_ws
colcon build
source install/setup.bash
```

Start the bridge according to the documentation in `docs/` or the bridge package README. The default Unity bridge endpoint used during validation is typically `localhost:30052`.




## Citation

If you use this simulator in research, please cite the related MIMISK-Sim paper or this software repository:

```bibtex
@software{mimisk_sim,
  title  = {MIMISK: A Modular Aerial–Tether–Underwater Simulation Framework for Kelp Farm Inspection and Marine Monitoring\\
},
  author = {Waseem Akram and Fausto Ferreira},
  year   = {2026},
  url    = {https://github.com/drwa92/MIMISK-Sim}
}
```


