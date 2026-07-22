# MIMISK Physical Tether Upgrade - Unity Phase 1

This patch adds a non-destructive physical tether subsystem to the existing MIMISK Unity scene.

It does not replace or edit the current mission, drone, MiniROV, ROS, gRPC, agent, or deployment scripts. It adds new components that read the existing tether state and can optionally drive the existing yellow tether LineRenderer.

## New runtime components

- `MIMISKPhysicalTetherModel`
  - segmented variable-length mass-spring cable
  - winch length read from `MIMISKDroneCoreTetherManager.deployedLengthM`
  - air/water transition forces
  - buoyancy, gravity, quadratic drag, damping
  - tension, slack, sag, submerged fraction, and force metrics
  - optional endpoint force coupling to MiniROV and drone

- `MIMISKPhysicalTetherSafetyGuard`
  - monitor-only by default
  - detects warning tension, critical tension, too-short cable, too much slack, and solver faults
  - can later be switched to active winch protection

- `MIMISKPhysicalTetherResearchLogger`
  - independent CSV logger for validation and publication metrics
  - disabled by default

- `MIMISKPhysicalTetherSetup`
  - Unity Editor menu installer

## Setup in your working scene

1. Open `Assets/MIMISK/Scenes/05_MiniROV_Finale_agent.unity`.
2. Import/copy this patch into your Unity project.
3. Wait for Unity to compile.
4. Run:

   `MIMISK > Tether > Install Physical Tether On Active Scene`

5. Press Play.
6. Use your existing tether workflow normally.
7. Select the `Drone` GameObject and inspect:

   - `MIMISKPhysicalTetherModel`
   - `MIMISKPhysicalTetherSafetyGuard`
   - `MIMISKPhysicalTetherResearchLogger`

## First test mode

The installer uses safe monitor-only mode:

- `applyForcesToMiniRov = false`
- `applyForcesToDrone = false`
- `writeCompatibilityMetricsToTetherManager = false`
- `SafetyGuard.actionMode = MonitorOnly`

This means the cable simulates physically and drives the visual line, but it does not yet perturb the current flight/deployment/control logic.

## Enabling real physical coupling

After the cable looks stable in Play Mode, use:

`MIMISK > Tether > Enable Physical Force Coupling`

This enables:

- endpoint forces on MiniROV
- endpoint reaction on drone
- compatibility metrics copied into the legacy tether manager
- active safety guard winch protection

Use this only after tuning the monitor-only cable parameters.

## Suggested initial parameters

- Node count: 56
- Cable diameter: 0.006 m
- Mass per meter: 0.035 kg/m
- Axial stiffness: 140 N/m
- Axial damping: 5 Ns/m
- Max elastic strain: 0.06
- Critical tension: 28 N
- Water current: `(0.08, 0, 0.02) m/s`

These are intentionally conservative starting values for stability.

## Publication metrics produced

The physical tether exposes or logs:

- deployed length
- straight distance
- geometric cable length
- slack
- geometric stretch
- elastic stretch
- maximum segment strain
- start tension
- end tension
- maximum tension
- mean tension
- sag depth
- submerged fraction
- MiniROV tether force
- drone tether reaction force
- safety guard state

These metrics are suitable for the simulator paper validation section.
