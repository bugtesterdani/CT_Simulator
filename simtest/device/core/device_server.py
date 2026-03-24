from __future__ import annotations

import argparse
import importlib
from typing import Any

import pywintypes
import win32pipe

from .base_device_model import BaseDeviceModel
from .pipe_handler import DEFAULT_PIPE_NAME, PipeClosedError, PipeHandler
from .profile_device_model import DeclarativeDeviceModel


class DeviceServer:
    def __init__(self, model: BaseDeviceModel, device_name: str):
        self.model = model
        self.device_name = device_name
        self.pipe_handler = PipeHandler()

    def run(self, pipe_name: str = DEFAULT_PIPE_NAME) -> int:
        print(f"[{self.device_name}] listening on {pipe_name}")

        while True:
            pipe = self.pipe_handler.create_server(pipe_name)
            try:
                self.pipe_handler.wait_for_client(pipe)
                print(f"[{self.device_name}] client connected")
                while True:
                    request = self.pipe_handler.read_message(pipe)
                    response, shutdown_requested = self.handle_request(request)
                    self.pipe_handler.write_message(pipe, response)
                    if shutdown_requested:
                        return 0
            except PipeClosedError:
                print(f"[{self.device_name}] client disconnected")
            except pywintypes.error as error:
                print(f"[{self.device_name}] pipe error: {error}")
            finally:
                try:
                    win32pipe.DisconnectNamedPipe(pipe)
                except pywintypes.error:
                    pass
                self.pipe_handler.close_pipe(pipe)

    def handle_request(self, request: dict[str, Any]) -> tuple[dict[str, Any], bool]:
        request_id = request.get("id")
        action = str(request.get("action", "")).strip()
        sim_time_ms = request.get("sim_time_ms")

        if sim_time_ms is not None:
            self.model.move_to_time(int(sim_time_ms))

        state_at_request = self.model.state_marker()

        try:
            if action == "hello":
                return self._ok_response(request_id, state_at_request, self.model.get_device_info()), False

            if action == "set_input":
                name = str(request["name"])
                self.model.set_input(name, request["value"])
                return self._ok_response(request_id, state_at_request, {"accepted": True}), False

            if action == "get_signal":
                name = str(request["name"])
                value = self.model.get_signal(name)
                return self._ok_response(request_id, state_at_request, {"name": name, "value": value}), False

            if action == "read_state":
                return self._ok_response(request_id, state_at_request, self.model.read_state()), False

            if action == "send_interface":
                name = str(request["name"])
                payload = request.get("payload")
                if not hasattr(self.model, "send_interface"):
                    return self._error_response(request_id, state_at_request, "unsupported", "Device model does not support interfaces."), False
                result = self.model.send_interface(name, payload)  # type: ignore[attr-defined]
                return self._ok_response(request_id, state_at_request, {"name": name, "response": result}), False

            if action == "read_interface":
                name = str(request["name"])
                if not hasattr(self.model, "read_interface"):
                    return self._error_response(request_id, state_at_request, "unsupported", "Device model does not support interfaces."), False
                result = self.model.read_interface(name)  # type: ignore[attr-defined]
                return self._ok_response(request_id, state_at_request, {"name": name, "response": result}), False

            if action == "set_waveform":
                name = str(request["name"])
                waveform = request.get("waveform")
                options = request.get("options") or {}
                try:
                    result = self.model.set_waveform(name, waveform if isinstance(waveform, dict) else {}, options if isinstance(options, dict) else {})
                except NotImplementedError:
                    return self._error_response(request_id, state_at_request, "unsupported", "Device model does not support waveforms."), False
                return self._ok_response(request_id, state_at_request, {"name": name, "result": result}), False

            if action == "read_waveform":
                name = str(request["name"])
                options = request.get("options") or {}
                try:
                    result = self.model.read_waveform(name, options if isinstance(options, dict) else {})
                except NotImplementedError:
                    return self._error_response(request_id, state_at_request, "unsupported", "Device model does not support waveforms."), False
                return self._ok_response(request_id, state_at_request, {"name": name, "result": result}), False

            if action == "reset":
                self.model.reset()
                return self._ok_response(request_id, state_at_request, {"accepted": True}), False

            if action == "shutdown":
                return self._ok_response(request_id, state_at_request, {"accepted": True}), True

            return self._error_response(request_id, state_at_request, "unknown_action", f"Unsupported action '{action}'."), False

        except (KeyError, ValueError) as error:
            return self._error_response(request_id, state_at_request, "bad_request", str(error)), False

    def _ok_response(self, request_id: Any, state_at_request: dict[str, Any], result: Any) -> dict[str, Any]:
        return {
            "id": request_id,
            "ok": True,
            "sim_time_ms": self.model.now_ms,
            "state_at_request": state_at_request,
            "result": result,
        }

    def _error_response(self, request_id: Any, state_at_request: dict[str, Any], code: str, message: str) -> dict[str, Any]:
        return {
            "id": request_id,
            "ok": False,
            "sim_time_ms": self.model.now_ms,
            "state_at_request": state_at_request,
            "error": {"code": code, "message": message},
        }


def load_device_model(device_name: str | None = None, profile_path: str | None = None) -> BaseDeviceModel:
    if profile_path:
        return DeclarativeDeviceModel(profile_path)

    if not device_name:
        raise ValueError("Either 'device_name' or 'profile_path' must be provided.")

    try:
        module = importlib.import_module(f"devices.{device_name}")

        for attr_name in dir(module):
            attr = getattr(module, attr_name)
            if isinstance(attr, type) and issubclass(attr, BaseDeviceModel) and attr is not BaseDeviceModel:
                return attr()

        raise ValueError(f"No valid device model found in {device_name}")
    except ImportError as error:
        raise ValueError(f"Device '{device_name}' not found") from error


def create_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Device Simulator")
    parser.add_argument("device", nargs="?", help="Device module name")
    parser.add_argument("--profile", help="Declarative JSON/YAML device profile")
    parser.add_argument("--pipe", default=DEFAULT_PIPE_NAME, help="Windows named pipe path")
    return parser
