from __future__ import annotations

import argparse
import itertools

from pipe_protocol import DEFAULT_PIPE_NAME, close_pipe, open_pipe_client, read_message, write_message


def build_parser() -> argparse.ArgumentParser:
    """Create the CLI argument parser for the mock client."""
    parser = argparse.ArgumentParser(description="Mock CT3xx client for the Python DUT simulator")
    parser.add_argument("--pipe", default=DEFAULT_PIPE_NAME, help="Windows named pipe path")
    return parser


def main() -> int:
    """Run a scripted sequence of requests against the example simulator."""
    args = build_parser().parse_args()
    request_counter = itertools.count(1)
    buffer = bytearray()
    pipe = open_pipe_client(args.pipe)
    try:
        def send(action: str, **payload):
            """Send one protocol request and print the response."""
            message = {"id": f"req-{next(request_counter)}", "action": action, **payload}
            print(f">>> {message}")
            write_message(pipe, message)
            response = read_message(pipe, buffer)
            print(f"<<< {response}")
            print()
            return response

        send("hello", sim_time_ms=0)
        send("set_input", sim_time_ms=0, name="VIN", value=12.0)
        send("set_input", sim_time_ms=10, name="MODE", value=0)
        send("set_input", sim_time_ms=20, name="EN", value=1)
        send("get_signal", sim_time_ms=200, name="VOUT")
        send("get_signal", sim_time_ms=400, name="VOUT")
        send("get_signal", sim_time_ms=400, name="PGOOD")
        send("set_input", sim_time_ms=450, name="LOAD_MA", value=500)
        send("read_state", sim_time_ms=650)
    finally:
        close_pipe(pipe)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
