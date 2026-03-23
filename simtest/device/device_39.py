from __future__ import annotations

import argparse
import sys
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
EXAMPLE_DIR = ROOT / "examples" / "PythonDeviceSimulator"
if str(EXAMPLE_DIR) not in sys.path:
    sys.path.insert(0, str(EXAMPLE_DIR))

from pipe_protocol import (  # noqa: E402
    DEFAULT_PIPE_NAME,
    PipeClosedError,
    close_pipe,
    create_pipe_server,
    read_message,
    wait_for_client,
    write_message,
)

import pywintypes  # noqa: E402
import win32pipe  # noqa: E402


class Device39Model:
    def __init__(self) -> None:
        self.reset()

    def reset(self) -> None:
        self.now_ms = 0
        self.helper_supply_enabled = False
        self.input_voltage = 0.0

    def move_to_time(self, target_time_ms: int) -> None:
        if target_time_ms < self.now_ms:
            raise ValueError("target_time_ms must be >= current model time")
        self.now_ms = target_time_ms

    def set_input(self, name: str, value: Any) -> None:
        signal = name.strip().upper()
        if signal in {"INPUT", "ADC_IN"}:
            self.input_voltage = float(value)
            return
        if signal in {"HELPER_SUPPLY", "VCC_PLUS"}:
            self.helper_supply_enabled = _to_bool(value)
            return
        if signal in {"GND", "VCC_GND"}:
            return
        raise KeyError(f"Unknown input '{name}'.")

    def enable_helper_supply(self) -> None:
        self.helper_supply_enabled = True

    def get_signal(self, name: str) -> float:
        signal = name.strip().upper()
        if signal not in {"OUTPUT", "DIG_OUT"}:
            raise KeyError(f"Unknown signal '{name}'.")
        if not self.helper_supply_enabled:
            return 0.0
        return 3.3 if self.input_voltage >= 3.2 else 0.0

    def read_state(self) -> dict[str, Any]:
        return {
            "time_ms": self.now_ms,
            "helper_supply_enabled": self.helper_supply_enabled,
            "input_voltage": self.input_voltage,
            "output_voltage": self.get_signal("OUTPUT"),
        }

    def state_marker(self) -> dict[str, Any]:
        return {
            "time_ms": self.now_ms,
            "helper_supply_enabled": self.helper_supply_enabled,
            "input_voltage": self.input_voltage,
        }


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="simtest Python device for CT3xx")
    parser.add_argument("--pipe", default=DEFAULT_PIPE_NAME, help="Windows named pipe path")
    return parser


def main() -> int:
    args = build_parser().parse_args()
    model = Device39Model()
    print(f"[device_39] listening on {args.pipe}")

    while True:
        pipe = create_pipe_server(args.pipe)
        buffer = bytearray()
        try:
            wait_for_client(pipe)
            print("[device_39] client connected")
            while True:
                request = read_message(pipe, buffer)
                response, shutdown_requested = handle_request(model, request)
                write_message(pipe, response)
                if shutdown_requested:
                    return 0
        except PipeClosedError:
            print("[device_39] client disconnected")
        except pywintypes.error as error:
            print(f"[device_39] pipe error: {error}")
        finally:
            try:
                win32pipe.DisconnectNamedPipe(pipe)
            except pywintypes.error:
                pass
            close_pipe(pipe)


def handle_request(model: Device39Model, request: dict[str, Any]) -> tuple[dict[str, Any], bool]:
    request_id = request.get("id")
    action = str(request.get("action", "")).strip()
    sim_time_ms = request.get("sim_time_ms")
    if sim_time_ms is not None:
        model.move_to_time(int(sim_time_ms))
    state_at_request = model.state_marker()

    try:
        if action == "hello":
            return ok_response(
                request_id,
                model,
                state_at_request,
                {
                    "name": "simtest-device-39",
                    "signals": ["INPUT", "OUTPUT", "HELPER_SUPPLY"],
                },
            ), False

        if action == "set_input":
            name = str(request["name"])
            if name.strip().upper() == "HELPER_SUPPLY":
                model.enable_helper_supply()
            else:
                model.set_input(name, request["value"])
            return ok_response(request_id, model, state_at_request, {"accepted": True}), False

        if action == "get_signal":
            name = str(request["name"])
            value = model.get_signal(name)
            return ok_response(request_id, model, state_at_request, {"name": name, "value": value}), False

        if action == "read_state":
            return ok_response(request_id, model, state_at_request, model.read_state()), False

        if action == "reset":
            model.reset()
            return ok_response(request_id, model, state_at_request, {"accepted": True}), False

        if action == "shutdown":
            return ok_response(request_id, model, state_at_request, {"accepted": True}), True

        return error_response(request_id, model, state_at_request, "unknown_action", f"Unsupported action '{action}'."), False
    except KeyError as error:
        return error_response(request_id, model, state_at_request, "bad_request", str(error)), False
    except ValueError as error:
        return error_response(request_id, model, state_at_request, "bad_request", str(error)), False


def ok_response(request_id: Any, model: Device39Model, state_at_request: dict[str, Any], result: Any) -> dict[str, Any]:
    return {
        "id": request_id,
        "ok": True,
        "sim_time_ms": model.now_ms,
        "state_at_request": state_at_request,
        "result": result,
    }


def error_response(
    request_id: Any,
    model: Device39Model,
    state_at_request: dict[str, Any],
    code: str,
    message: str,
) -> dict[str, Any]:
    return {
        "id": request_id,
        "ok": False,
        "sim_time_ms": model.now_ms,
        "state_at_request": state_at_request,
        "error": {"code": code, "message": message},
    }


def _to_bool(value: Any) -> bool:
    if isinstance(value, bool):
        return value
    if isinstance(value, (int, float)):
        return value != 0
    return str(value).strip().lower() in {"1", "true", "yes", "on"}


if __name__ == "__main__":
    raise SystemExit(main())
