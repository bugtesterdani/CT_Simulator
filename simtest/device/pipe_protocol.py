"""Windows Named Pipe Protocol Implementation"""
from __future__ import annotations
import json
import struct
from typing import Any

import win32pipe
import win32file
import pywintypes

 Konstanten
DEFAULT_PIPE_NAME = r"\\.\pipe\simtest_device"
BUFFER_SIZE = 4096
PIPE_TIMEOUT = 5000

class PipeClosedError(Exception):
    """Exception für geschlossene Pipes"""
    pass


def create_pipe_server(pipe_name: str) -> int:
    """Named Pipe Server erstellen"""
    return win32pipe.CreateNamedPipe(
        pipe_name,
        win32pipe.PIPE_ACCESS_DUPLEX,
        win32pipe.PIPE_TYPE_MESSAGE | win32pipe.PIPE_READMODE_MESSAGE | win32pipe.PIPE_WAIT,
        1,  # Max instances
        BUFFER_SIZE,  # Output buffer size
        BUFFER_SIZE,  # Input buffer size
        PIPE_TIMEOUT,  # Timeout
        None  # Security attributes
    )


def wait_for_client(pipe: int) -> None:
    """Auf Client-Verbindung warten"""
    win32pipe.ConnectNamedPipe(pipe, None)


def close_pipe(pipe: int) -> None:
    """Pipe schließen"""
    try:
        win32file.CloseHandle(pipe)
    except pywintypes.error:
        pass


def read_message(pipe: int, buffer: bytearray) -> dict[str, Any]:
    """Nachricht von Pipe lesen"""
    try:
        # Länge der Nachricht lesen (4 Bytes)
        length_data = win32file.ReadFile(pipe, 4)[1]
        if len(length_data) != 4:
            raise PipeClosedError("Connection closed while reading length")
        
        message_length = struct.unpack('<I', length_data)[0]
        
        # Nachricht lesen
        message_data = win32file.ReadFile(pipe, message_length)[1]
        if len(message_data) != message_length:
            raise PipeClosedError("Connection closed while reading message")
        
        # JSON dekodieren
        message_str = message_data.decode('utf-8')
        return json.loads(message_str)
        
    except pywintypes.error as e:
        if e.winerror in (109, 232):  # ERROR_BROKEN_PIPE, ERROR_NO_DATA
            raise PipeClosedError("Pipe connection lost")
        raise


def write_message(pipe: int, message: dict[str, Any]) -> None:
    """Nachricht an Pipe schreiben"""
    try:
        # JSON kodieren
        message_str = json.dumps(message, ensure_ascii=False)
        message_data = message_str.encode('utf-8')
        
        # Länge schreiben (4 Bytes)
        length_data = struct.pack('<I', len(message_data))
        win32file.WriteFile(pipe, length_data)
        
        # Nachricht schreiben
        win32file.WriteFile(pipe, message_data)
        
    except pywintypes.error as e:
        if e.winerror in (109, 232):  # ERROR_BROKEN_PIPE, ERROR_NO_DATA
            raise PipeClosedError("Pipe connection lost")
        raise
