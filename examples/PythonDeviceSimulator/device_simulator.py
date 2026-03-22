from __future__ import annotations

import argparse
import time
import traceback
from typing import Any

import pywintypes
import win32pipe

from device_model import DeviceModel
from pipe_protocol import (
    DEFAULT_PIPE_NAME,
    PipeClosedError,
    close_pipe,
    create_pipe_server,
    read_message,
    wait_for_client,
    write_message,
)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Example CT3xx Python DUT simulator")
    parser.add_argument("--pipe", default=DEFAULT_PIPE_NAME, help="Windows named pipe path")
    parser.add_argument(
        "--realtime",
        action="store_true",
        help="Advance model time from wall clock between requests",
    )
    return parser


def main() -> int:
    args = build_parser().parse_args()
    model = DeviceModel()
    print(f"[device-sim] starting server on {args.pipe}")

    shutdown_requested = False
    while not shutdown_requested:
        pipe = create_pipe_server(args.pipe)
        buffer = bytearray()
        try:
            wait_for_client(pipe)
            print("[device-sim] client connected")
            last_wall_clock = time.monotonic()

            while True:
                if args.realtime:
                    now = time.monotonic()
                    delta_ms = int((now - last_wall_clock) * 1000)
                    if delta_ms > 0:
                        model.tick(delta_ms)
                    last_wall_clock = now

                request = read_message(pipe, buffer)
                response, shutdown_requested = handle_request(model, request)
                write_message(pipe, response)

                if shutdown_requested:
                    print("[device-sim] shutdown requested")
                    break
        except PipeClosedError:
            print("[device-sim] client disconnected")
        except pywintypes.error as error:
            print(f"[device-sim] pipe error: {error}")
        except Exception:
            print("[device-sim] unexpected error")
            traceback.print_exc()
        finally:
            try:
                win32pipe.DisconnectNamedPipe(pipe)
            except pywintypes.error:
                pass
            close_pipe(pipe)

    return 0


def handle_request(model: DeviceModel, request: dict[str, Any]) -> tuple[dict[str, Any], bool]:
    request_id = request.get("id")
    action = str(request.get("action", "")).strip()
    sync_to_simulator_time(model, request)
    state_at_request = model.state_marker()

    try:
        if action == "hello":
            return ok_response(
                request_id,
                model,
                state_at_request,
                {
                    "name": "ct3xx-python-device-simulator",
                    "protocol": "jsonl-named-pipe-v1",
                    "signals": ["VIN", "EN", "MODE", "LOAD_MA", "VOUT", "PGOOD", "FAULT", "ID0"],
                },
            ), False

        if action == "set_input":
            model.set_input(str(request["name"]), request["value"])
            return ok_response(request_id, model, state_at_request, {"accepted": True}), False

        if action == "get_signal":
            value = model.get_signal(str(request["name"]))
            return ok_response(
                request_id,
                model,
                state_at_request,
                {"name": request["name"], "value": value},
            ), False

        if action == "read_state":
            return ok_response(request_id, model, state_at_request, model.read_state()), False

        if action == "tick":
            delta_ms = int(request.get("ms", 0))
            model.tick(delta_ms)
            return ok_response(request_id, model, state_at_request, {"time_ms": model.now_ms}), False

        if action == "reset":
            model.reset()
            return ok_response(request_id, model, state_at_request, {"accepted": True}), False

        if action == "shutdown":
            return ok_response(request_id, model, state_at_request, {"accepted": True}), True

        return error_response(
            request_id,
            model,
            state_at_request,
            "unknown_action",
            f"Unsupported action '{action}'.",
        ), False
    except KeyError as error:
        return error_response(request_id, model, state_at_request, "bad_request", str(error)), False
    except ValueError as error:
        return error_response(request_id, model, state_at_request, "bad_request", str(error)), False
    except Exception as error:
        return error_response(request_id, model, state_at_request, "internal_error", str(error)), False


def sync_to_simulator_time(model: DeviceModel, request: dict[str, Any]) -> None:
    sim_time_ms = request.get("sim_time_ms")
    if sim_time_ms is None:
        return

    model.move_to_time(int(sim_time_ms))


def ok_response(request_id: Any, model: DeviceModel, state_at_request: dict[str, Any], result: Any) -> dict[str, Any]:
    return {
        "id": request_id,
        "ok": True,
        "sim_time_ms": model.now_ms,
        "state_at_request": state_at_request,
        "result": result,
    }


def error_response(
    request_id: Any,
    model: DeviceModel,
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


if __name__ == "__main__":
    raise SystemExit(main())
