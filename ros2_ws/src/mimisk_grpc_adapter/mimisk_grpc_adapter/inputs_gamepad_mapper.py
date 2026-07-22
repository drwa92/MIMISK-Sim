#!/usr/bin/env python3

from __future__ import annotations

import json
import threading
import time
from typing import Dict, List, Optional

import rclpy
from rclpy.node import Node
from std_msgs.msg import String

from inputs import get_gamepad

from mimisk_msgs.msg import GamepadCommand


def clamp(x: float, lo: float = -1.0, hi: float = 1.0) -> float:
    return max(lo, min(hi, float(x)))


def apply_deadzone(x: float, deadzone: float) -> float:
    if abs(x) < deadzone:
        return 0.0

    return clamp(x)


def normalize_center_axis(value: Optional[int], deadzone: float) -> float:
    if value is None:
        return 0.0

    v = float(value)

    if 0.0 <= v <= 255.0:
        center = 128.0
        span = 127.0
    elif 0.0 <= v <= 1023.0:
        center = 512.0
        span = 511.0
    elif 0.0 <= v <= 65535.0:
        center = 32768.0
        span = 32767.0
    elif -32768.0 <= v <= 32767.0:
        center = 0.0
        span = 32767.0
    else:
        center = 0.0
        span = max(abs(v), 1.0)

    x = (v - center) / span
    return round(apply_deadzone(x, deadzone), 3)


def normalize_trigger(value: Optional[int], deadzone: float) -> float:
    if value is None:
        return 0.0

    v = float(value)

    if v > 1.5:
        if 0.0 <= v <= 255.0:
            x = v / 255.0
        elif 0.0 <= v <= 1023.0:
            x = v / 1023.0
        elif 0.0 <= v <= 65535.0:
            x = v / 65535.0
        else:
            x = v / max(abs(v), 1.0)
    else:
        x = v

    x = max(0.0, min(1.0, x))

    if x < deadzone:
        x = 0.0

    return round(x, 3)


def parse_button_list(text: str) -> List[str]:
    return [
        item.strip()
        for item in str(text).split(",")
        if item.strip()
    ]


def any_button_pressed(buttons: Dict[str, int], names: str) -> bool:
    for name in parse_button_list(names):
        if int(buttons.get(name, 0)) != 0:
            return True

    return False


class RawGamepadState:
    def __init__(self):
        self.lock = threading.Lock()
        self.axes: Dict[str, int] = {}
        self.buttons: Dict[str, int] = {}
        self.hats: Dict[str, int] = {
            "ABS_HAT0X": 0,
            "ABS_HAT0Y": 0,
        }
        self.last_event = "none"
        self.running = True


class MIMISKInputsGamepadMapper(Node):
    def __init__(self):
        super().__init__("mimisk_inputs_gamepad_mapper")

        self.target = str(
            self.declare_parameter("target", "drone").value
        ).strip().lower()

        self.hz = float(
            self.declare_parameter("hz", 50.0).value
        )

        self.deadzone = float(
            self.declare_parameter("deadzone", 0.08).value
        )

        self.diagnose = bool(
            self.declare_parameter("diagnose", False).value
        )

        # Drone mapping: same semantic structure as pc_sender_drone.py.
        self.forward_code = str(
            self.declare_parameter("forward_code", "ABS_Y").value
        )

        self.forward_sign = float(
            self.declare_parameter("forward_sign", -1.0).value
        )

        self.right_code = str(
            self.declare_parameter("right_code", "ABS_X").value
        )

        self.right_sign = float(
            self.declare_parameter("right_sign", 1.0).value
        )

        self.yaw_code = str(
            self.declare_parameter("yaw_code", "ABS_Z").value
        )

        self.yaw_sign = float(
            self.declare_parameter("yaw_sign", 1.0).value
        )

        self.alt_code = str(
            self.declare_parameter("alt_code", "ABS_RZ").value
        )

        self.alt_sign = float(
            self.declare_parameter("alt_sign", -1.0).value
        )

        self.arm_buttons = str(
            self.declare_parameter("arm_buttons", "BTN_SOUTH,BTN_A").value
        )

        self.takeoff_buttons = str(
            self.declare_parameter("takeoff_buttons", "BTN_NORTH,BTN_Y").value
        )

        self.hold_buttons = str(
            self.declare_parameter("hold_buttons", "BTN_WEST,BTN_X").value
        )

        self.manual_buttons = str(
            self.declare_parameter("manual_buttons", "BTN_TL,BTN_TR").value
        )

        self.land_buttons = str(
            self.declare_parameter("land_buttons", "BTN_EAST,BTN_B").value
        )

        self.disarm_buttons = str(
            self.declare_parameter("disarm_buttons", "BTN_SELECT").value
        )

        self.failsafe_buttons = str(
            self.declare_parameter("failsafe_buttons", "BTN_START").value
        )

        self.mission_buttons = str(
            self.declare_parameter("mission_buttons", "").value
        )

        # MiniROV mapping: same semantic structure as the direct Unity UDP sender.
        self.minirov_surge_scale = float(
            self.declare_parameter("minirov_surge_scale", 1.0).value
        )

        self.minirov_yaw_scale = float(
            self.declare_parameter("minirov_yaw_scale", 1.0).value
        )

        self.minirov_depth_scale = float(
            self.declare_parameter("minirov_depth_scale", 1.0).value
        )

        self.invert_minirov_surge = bool(
            self.declare_parameter("invert_minirov_surge", False).value
        )

        self.invert_minirov_yaw = bool(
            self.declare_parameter("invert_minirov_yaw", False).value
        )

        if self.target in ("rov", "minirov"):
            self.topic = "/mimisk/manual/minirov"
            self.target_agent = "minirov"
            self.activation_verb = "manual_direct"
        else:
            self.topic = "/mimisk/manual/drone"
            self.target_agent = "drone"
            self.activation_verb = "manual"

        # Auto mode activation.
        self.auto_activate_mode = bool(
            self.declare_parameter("auto_activate_mode", True).value
        )

        self.auto_activate_repeat_count = int(
            self.declare_parameter("auto_activate_repeat_count", 8).value
        )

        self.auto_activate_period_s = float(
            self.declare_parameter("auto_activate_period_s", 0.25).value
        )

        self.activation_remaining = (
            self.auto_activate_repeat_count
            if self.auto_activate_mode
            else 0
        )

        self.activation_last_time_s = -999.0

        self.state = RawGamepadState()

        self.pub = self.create_publisher(
            GamepadCommand,
            self.topic,
            10,
        )

        self.command_pub = self.create_publisher(
            String,
            "/mimisk/command_json",
            10,
        )

        self.reader_thread = threading.Thread(
            target=self.reader_loop,
            daemon=True,
        )
        self.reader_thread.start()

        self.timer = self.create_timer(
            1.0 / max(1.0, self.hz),
            self.publish_command,
        )

        self.get_logger().info(
            "MIMISK inputs mapper publishing "
            f"target={self.target_agent} to {self.topic} at {self.hz:.1f} Hz"
        )

        if self.auto_activate_mode:
            self.get_logger().info(
                f"Auto activation enabled: {self.target_agent}/{self.activation_verb}"
            )

    def reader_loop(self):
        while self.state.running:
            try:
                events = get_gamepad()
            except Exception as exc:
                self.get_logger().warn(f"Gamepad read error: {exc}")
                time.sleep(0.1)
                continue

            with self.state.lock:
                for event in events:
                    code = str(getattr(event, "code", ""))
                    value = int(getattr(event, "state", 0))

                    if self.diagnose:
                        self.get_logger().info(f"{code:12s} {value}")

                    self.state.last_event = f"{code}={value}"

                    if code.startswith("ABS_"):
                        self.state.axes[code] = value

                        if code in self.state.hats:
                            self.state.hats[code] = value

                    elif code.startswith("BTN_"):
                        self.state.buttons[code] = value

    def maybe_publish_activation_command(self):
        if self.activation_remaining <= 0:
            return

        now = self.get_clock().now().nanoseconds * 1e-9

        if now - self.activation_last_time_s < self.auto_activate_period_s:
            return

        self.activation_last_time_s = now
        self.activation_remaining -= 1

        msg = String()
        msg.data = json.dumps(
            {
                "target_agent": self.target_agent,
                "verb": self.activation_verb,
                "string_arg": "",
                "numeric_arg": 0.0,
            }
        )

        self.command_pub.publish(msg)

        self.get_logger().info(
            f"Auto-activation command queued: {self.target_agent}/{self.activation_verb}"
        )

    def publish_command(self):
        self.maybe_publish_activation_command()

        with self.state.lock:
            axes = dict(self.state.axes)
            buttons = dict(self.state.buttons)
            hats = dict(self.state.hats)

        msg = GamepadCommand()
        msg.header.stamp = self.get_clock().now().to_msg()
        msg.header.frame_id = "mimisk_gamepad_inputs"
        msg.enabled = True

        if self.target_agent == "minirov":
            self.fill_minirov_command(msg, axes, buttons, hats)
        else:
            self.fill_drone_command(msg, axes, buttons, hats)

        self.pub.publish(msg)

    def fill_drone_command(
        self,
        msg: GamepadCommand,
        axes: Dict[str, int],
        buttons: Dict[str, int],
        hats: Dict[str, int],
    ):
        forward = normalize_center_axis(
            axes.get(self.forward_code),
            self.deadzone,
        ) * self.forward_sign

        right = normalize_center_axis(
            axes.get(self.right_code),
            self.deadzone,
        ) * self.right_sign

        yaw = normalize_center_axis(
            axes.get(self.yaw_code),
            self.deadzone,
        ) * self.yaw_sign

        altitude = normalize_center_axis(
            axes.get(self.alt_code),
            self.deadzone,
        ) * self.alt_sign

        msg.target_agent = "drone"

        # Semantic layout consumed by MIMISKGrpcManualControlBridge:
        # ly = forward, lx = right, rx = yaw, ry = altitude.
        msg.ly = round(clamp(forward), 3)
        msg.lx = round(clamp(right), 3)
        msg.rx = round(clamp(yaw), 3)
        msg.ry = round(clamp(altitude), 3)

        msg.lt = normalize_trigger(axes.get("ABS_Z"), 0.03)
        msg.rt = normalize_trigger(axes.get("ABS_RZ"), 0.03)

        msg.hat_x = int(hats.get("ABS_HAT0X", 0))
        msg.hat_y = int(hats.get("ABS_HAT0Y", 0))

        arm = any_button_pressed(buttons, self.arm_buttons)
        takeoff = any_button_pressed(buttons, self.takeoff_buttons)
        hold = any_button_pressed(buttons, self.hold_buttons)
        manual = any_button_pressed(buttons, self.manual_buttons)
        land = any_button_pressed(buttons, self.land_buttons)
        disarm = any_button_pressed(buttons, self.disarm_buttons)
        failsafe = any_button_pressed(buttons, self.failsafe_buttons)
        mission = any_button_pressed(buttons, self.mission_buttons)

        msg.button_a = arm
        msg.button_y = takeoff
        msg.button_x = hold
        msg.button_lb = manual
        msg.button_rb = manual
        msg.button_b = land
        msg.button_back = disarm
        msg.button_start = failsafe or mission

    def fill_minirov_command(
        self,
        msg: GamepadCommand,
        axes: Dict[str, int],
        buttons: Dict[str, int],
        hats: Dict[str, int],
    ):
        lx_raw = normalize_center_axis(axes.get("ABS_X"), self.deadzone)
        ly_raw = normalize_center_axis(axes.get("ABS_Y"), self.deadzone)
        rx_raw = normalize_center_axis(axes.get("ABS_RX"), self.deadzone)
        ry_raw = normalize_center_axis(axes.get("ABS_RY"), self.deadzone)

        lt = normalize_trigger(axes.get("ABS_Z"), 0.03)
        rt = normalize_trigger(axes.get("ABS_RZ"), 0.03)

        # Validated MiniROV mapping:
        # surge = -ly, yaw = rx, depth = rt - lt.
        surge = -ly_raw
        yaw = rx_raw
        depth = rt - lt

        if self.invert_minirov_surge:
            surge = -surge

        if self.invert_minirov_yaw:
            yaw = -yaw

        surge = round(clamp(surge * self.minirov_surge_scale), 3)
        yaw = round(clamp(yaw * self.minirov_yaw_scale), 3)
        depth = round(clamp(depth * self.minirov_depth_scale), 3)

        msg.target_agent = "minirov"

        # Semantic layout consumed by MIMISKGrpcManualControlBridge:
        # ly = surge, rx = yaw, rt - lt = depth.
        msg.ly = surge
        msg.rx = yaw

        # Keep raw axes visible for diagnostics.
        msg.lx = lx_raw
        msg.ry = ry_raw

        if depth >= 0.0:
            msg.rt = depth
            msg.lt = 0.0
        else:
            msg.rt = 0.0
            msg.lt = -depth

        msg.hat_x = int(hats.get("ABS_HAT0X", 0))
        msg.hat_y = int(hats.get("ABS_HAT0Y", 0))

        msg.button_a = any_button_pressed(buttons, "BTN_SOUTH,BTN_A")
        msg.button_b = any_button_pressed(buttons, "BTN_EAST,BTN_B")
        msg.button_x = any_button_pressed(buttons, "BTN_WEST,BTN_X")
        msg.button_y = any_button_pressed(buttons, "BTN_NORTH,BTN_Y")
        msg.button_lb = any_button_pressed(buttons, "BTN_TL")
        msg.button_rb = any_button_pressed(buttons, "BTN_TR")
        msg.button_back = any_button_pressed(buttons, "BTN_SELECT")
        msg.button_start = any_button_pressed(buttons, "BTN_START")

    def destroy_node(self):
        self.state.running = False
        super().destroy_node()


def main():
    rclpy.init()
    node = MIMISKInputsGamepadMapper()

    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
