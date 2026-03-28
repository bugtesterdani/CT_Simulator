"""Named-pipe JSONL helpers for the example Python DUT simulator."""

from __future__ import annotations

import json
from typing import Any

import pywintypes
import win32file
import win32pipe


DEFAULT_PIPE_NAME = r"\\.\pipe\ct3xx-device-sim-example"
BUFFER_SIZE = 65536


class PipeClosedError(Exception):
    """Raised when the pipe connection is lost or closed."""
    pass


def create_pipe_server(pipe_name: str):
    """Create a named pipe server compatible with the example client."""
    return win32pipe.CreateNamedPipe(
        pipe_name,
        win32pipe.PIPE_ACCESS_DUPLEX,
        win32pipe.PIPE_TYPE_BYTE | win32pipe.PIPE_READMODE_BYTE | win32pipe.PIPE_WAIT,
        1,
        BUFFER_SIZE,
        BUFFER_SIZE,
        0,
        None,
    )


def wait_for_client(pipe_handle: int) -> None:
    """Wait for one client to connect to the server pipe."""
    try:
        win32pipe.ConnectNamedPipe(pipe_handle, None)
    except pywintypes.error as error:
        if error.winerror != 535:
            raise


def open_pipe_client(pipe_name: str):
    """Open a named pipe client handle for the given pipe name."""
    return win32file.CreateFile(
        pipe_name,
        win32file.GENERIC_READ | win32file.GENERIC_WRITE,
        0,
        None,
        win32file.OPEN_EXISTING,
        0,
        None,
    )


def close_pipe(pipe_handle: int) -> None:
    """Close a pipe handle and suppress duplicate-close errors."""
    try:
        win32file.CloseHandle(pipe_handle)
    except pywintypes.error:
        pass


def write_message(pipe_handle: int, message: dict[str, Any]) -> None:
    """Write one JSONL message to the pipe."""
    payload = (json.dumps(message, separators=(",", ":")) + "\n").encode("utf-8")
    win32file.WriteFile(pipe_handle, payload)


def read_message(pipe_handle: int, buffer: bytearray) -> dict[str, Any]:
    """Read one JSONL message from the pipe buffer."""
    while True:
        newline_index = buffer.find(b"\n")
        if newline_index >= 0:
            line = bytes(buffer[:newline_index])
            del buffer[: newline_index + 1]
            if not line.strip():
                continue
            return json.loads(line.decode("utf-8"))

        try:
            _, chunk = win32file.ReadFile(pipe_handle, BUFFER_SIZE)
        except pywintypes.error as error:
            if error.winerror in (109, 232):
                raise PipeClosedError() from error
            raise

        if not chunk:
            raise PipeClosedError()

        buffer.extend(chunk)
