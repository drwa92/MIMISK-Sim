#!/usr/bin/env python3

from __future__ import annotations

import argparse
import json
import time
from pathlib import Path
from typing import Callable, Optional

import rclpy
from rclpy.node import Node
from std_srvs.srv import Trigger

from mimisk_msgs.msg import DroneState, MiniROVState, TetherState, CommandResult
from mimisk_msgs.srv import SetString


class MIMISKIntegratedExperiment(Node):
    def __init__(self, args):
        super().__init__("mimisk_integrated_experiment")

        self.args = args

        self.drone_state: Optional[DroneState] = None
        self.rov_state: Optional[MiniROVState] = None
        self.tether_state: Optional[TetherState] = None
        self.last_command_result: Optional[CommandResult] = None

        self.create_subscription(DroneState, "/mimisk/drone/state", self.drone_cb, 10)
        self.create_subscription(MiniROVState, "/mimisk/minirov/state", self.rov_cb, 10)
        self.create_subscription(TetherState, "/mimisk/tether/state", self.tether_cb, 10)
        self.create_subscription(CommandResult, "/mimisk/command_result_typed", self.command_result_cb, 10)

        self.trigger_clients = {}
        self.set_string_clients = {}

        self.summary = {
            "experiment": "integrated_deploy_inspect_recover",
            "start_wall_time": time.time(),
            "events": [],
            "args": vars(args),
        }

    def drone_cb(self, msg: DroneState):
        self.drone_state = msg

    def rov_cb(self, msg: MiniROVState):
        self.rov_state = msg

    def tether_cb(self, msg: TetherState):
        self.tether_state = msg

    def command_result_cb(self, msg: CommandResult):
        self.last_command_result = msg

    def log_event(self, name: str, **kwargs):
        event = {
            "time": time.time(),
            "event": name,
            **kwargs,
        }
        self.summary["events"].append(event)
        self.get_logger().info(json.dumps(event))

    def get_trigger_client(self, service_name: str):
        if service_name not in self.trigger_clients:
            self.trigger_clients[service_name] = self.create_client(Trigger, service_name)
        return self.trigger_clients[service_name]

    def get_set_string_client(self, service_name: str):
        if service_name not in self.set_string_clients:
            self.set_string_clients[service_name] = self.create_client(SetString, service_name)
        return self.set_string_clients[service_name]

    def call_trigger(self, service_name: str, timeout_s: float = 5.0):
        client = self.get_trigger_client(service_name)

        if not client.wait_for_service(timeout_sec=timeout_s):
            raise RuntimeError(f"Service unavailable: {service_name}")

        req = Trigger.Request()
        future = client.call_async(req)
        rclpy.spin_until_future_complete(self, future, timeout_sec=timeout_s)

        if not future.done():
            raise RuntimeError(f"Service timeout: {service_name}")

        resp = future.result()
        self.log_event(
            "service_call",
            service=service_name,
            success=bool(resp.success),
            message=str(resp.message),
        )
        return resp

    def call_set_string(self, service_name: str, value: str, timeout_s: float = 5.0):
        client = self.get_set_string_client(service_name)

        if not client.wait_for_service(timeout_sec=timeout_s):
            raise RuntimeError(f"Service unavailable: {service_name}")

        req = SetString.Request()
        req.value = value
        future = client.call_async(req)
        rclpy.spin_until_future_complete(self, future, timeout_sec=timeout_s)

        if not future.done():
            raise RuntimeError(f"Service timeout: {service_name}")

        resp = future.result()
        self.log_event(
            "set_string_call",
            service=service_name,
            value=value,
            success=bool(resp.success),
            message=str(resp.message),
        )
        return resp

    def wait_for_data(self, timeout_s: float = 10.0):
        return self.wait_until(
            lambda: self.drone_state is not None and self.rov_state is not None and self.tether_state is not None,
            timeout_s,
            "initial telemetry",
        )

    def wait_until(self, predicate: Callable[[], bool], timeout_s: float, label: str):
        t0 = time.monotonic()

        while time.monotonic() - t0 < timeout_s:
            rclpy.spin_once(self, timeout_sec=0.1)

            if predicate():
                self.log_event("wait_success", label=label, elapsed_s=time.monotonic() - t0)
                return True

        self.log_event("wait_timeout", label=label, timeout_s=timeout_s)
        return False

    def drone_surface_ready(self):
        if self.drone_state is None:
            return False

        mission = self.drone_state.mission_state
        return mission in (
            "SurfaceStable",
            "SurfaceHold",
            "ReadyForTetherDeployment",
        )

    def tether_ready_for_rov_control(self):
        if self.tether_state is None:
            return False

        low = self.tether_state.low_level_state
        uni = self.tether_state.unified_state

        deployed_close = False
        if self.tether_state.target_length_m > 0.01:
            deployed_close = abs(self.tether_state.deployed_length_m - self.tether_state.target_length_m) < 0.20

        state_good = (
            "Holding" in low or
            "Deployed" in low or
            "ROVControl" in low or
            "RovControl" in uni or
            "ROVControl" in uni
        )

        return state_good or deployed_close

    def rov_recovery_ready(self):
        if self.rov_state is None:
            return False

        if self.rov_state.mission_state == "RecoveryReady":
            return True

        if self.rov_state.distance_to_home_m <= self.args.recovery_radius:
            return True

        return False

    def tether_recovered(self):
        if self.tether_state is None:
            return False

        low = self.tether_state.low_level_state
        uni = self.tether_state.unified_state

        return (
            "Recovered" in low or
            "Recovered" in uni or
            "ReadyForDeploy" in uni
        )

    def run(self):
        self.log_event("experiment_started")

        self.wait_for_data(timeout_s=15.0)

        if self.args.start_drone_mission:
            self.call_trigger("/mimisk/drone/start_mission")

        if self.args.wait_surface_ready:
            self.wait_until(
                self.drone_surface_ready,
                self.args.surface_ready_timeout,
                "drone surface ready",
            )

        if self.args.attach:
            self.call_trigger("/mimisk/tether/attach_rov")
            self.sleep_spin(1.0)

        self.call_trigger("/mimisk/tether/deploy_rov")

        self.wait_until(
            self.tether_ready_for_rov_control,
            self.args.deploy_timeout,
            "tether deployed / ready for ROV control",
        )

        self.call_trigger("/mimisk/tether/activate_rov_control")
        self.sleep_spin(1.0)

        if self.args.enable_smart_tms:
            self.call_trigger("/mimisk/tether/enable_smart_tms")

        self.call_set_string("/mimisk/minirov/set_path", self.args.rov_path)
        self.call_trigger("/mimisk/minirov/start_mission")

        self.log_event("rov_mission_running", duration_s=self.args.rov_mission_duration)
        self.sleep_spin(self.args.rov_mission_duration)

        if self.args.return_home:
            self.call_trigger("/mimisk/minirov/return_home")

            self.wait_until(
                self.rov_recovery_ready,
                self.args.return_home_timeout,
                "MiniROV recovery ready",
            )

        if self.args.recover:
            self.call_trigger("/mimisk/tether/recover_rov")

            self.wait_until(
                self.tether_recovered,
                self.args.recovery_timeout,
                "tether recovered",
            )

        self.summary["end_wall_time"] = time.time()
        self.summary["final"] = {
            "drone_mission_state": self.drone_state.mission_state if self.drone_state else "missing",
            "rov_mission_state": self.rov_state.mission_state if self.rov_state else "missing",
            "tether_unified_state": self.tether_state.unified_state if self.tether_state else "missing",
            "tether_low_level_state": self.tether_state.low_level_state if self.tether_state else "missing",
        }

        self.save_summary()
        self.log_event("experiment_completed")

    def sleep_spin(self, duration_s: float):
        t0 = time.monotonic()

        while time.monotonic() - t0 < duration_s:
            rclpy.spin_once(self, timeout_sec=0.1)

    def save_summary(self):
        out_dir = Path(self.args.output_dir).expanduser()
        out_dir.mkdir(parents=True, exist_ok=True)

        stamp = time.strftime("%Y%m%d_%H%M%S")
        out_path = out_dir / f"mimisk_integrated_experiment_{stamp}.json"

        with out_path.open("w", encoding="utf-8") as f:
            json.dump(self.summary, f, indent=2)

        self.get_logger().info(f"Experiment summary saved: {out_path}")


def parse_args():
    parser = argparse.ArgumentParser(description="Run MIMISK integrated ROS2 experiment.")

    parser.add_argument("--start-drone-mission", action="store_true")
    parser.add_argument("--wait-surface-ready", action="store_true")
    parser.add_argument("--surface-ready-timeout", type=float, default=90.0)

    parser.add_argument("--attach", action="store_true", default=True)
    parser.add_argument("--deploy-timeout", type=float, default=60.0)

    parser.add_argument("--enable-smart-tms", action="store_true", default=True)

    parser.add_argument("--rov-path", default="CircleInspection")
    parser.add_argument("--rov-mission-duration", type=float, default=30.0)

    parser.add_argument("--return-home", action="store_true", default=True)
    parser.add_argument("--return-home-timeout", type=float, default=90.0)
    parser.add_argument("--recovery-radius", type=float, default=0.30)

    parser.add_argument("--recover", action="store_true", default=True)
    parser.add_argument("--recovery-timeout", type=float, default=90.0)

    parser.add_argument(
        "--output-dir",
        default="~/mimisk_minirov_unity/mimisk_minirov_v2/Logs/ROSExperiments",
    )

    return parser.parse_args()


def main():
    args = parse_args()

    rclpy.init()
    node = MIMISKIntegratedExperiment(args)

    try:
        node.run()
    except Exception as exc:
        node.get_logger().error(f"Experiment failed: {exc}")
        node.summary["error"] = str(exc)
        node.save_summary()
        raise
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
