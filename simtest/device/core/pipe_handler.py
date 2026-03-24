"""Vereinfachte Pipe-Behandlung"""
from __future__ import annotations
import json
import struct
from typing import Any
import win32pipe
import win32file
import pywintypes

DEFAULT_PIPE_NAME = r"\\.\pipe\simtest_device"

class PipeClosedError(Exception):
    pass

class PipeHandler:
    @staticmethod
    def create_server(pipe_name: str) -> int:
        return win32pipe.CreateNamedPipe(
            pipe_name,
            win32pipe.PIPE_ACCESS_DUPLEX,
            win32pipe.PIPE_TYPE_MESSAGE | win32pipe.PIPE_READMODE_MESSAGE | win32pipe.PIPE_WAIT,
            1, 4096, 4096, 5000, None
        )
    
    @staticmethod
    def wait_for_client(pipe: int) -> None:
        win32pipe.ConnectNamedPipe(pipe, None)
    
    @staticmethod
    def close_pipe(pipe: int) -> None:
        try:
            win32file.CloseHandle(pipe)
        except pywintypes.error:
            pass
    
    @staticmethod
    def read_message(pipe: int) -> dict[str, Any]:
        try:
            length_data = win32file.ReadFile(pipe, 4)[1]
            if len(length_data) != 4:
                raise PipeClosedError("Connection closed")
            
            message_length = struct.unpack('<I', length_data)[0]
            message_data = win32file.ReadFile(pipe, message_length)[1]
            
            return json.loads(message_data.decode('utf-8'))
        except pywintypes.error:
            raise PipeClosedError("Pipe error")
    
    @staticmethod
    def write_message(pipe: int, message: dict[str, Any]) -> None:
        try:
            message_data = json.dumps(message).encode('utf-8')
            length_data = struct.pack('<I', len(message_data))
            win32file.WriteFile(pipe, length_data)
            win32file.WriteFile(pipe, message_data)
        except pywintypes.error:
            raise PipeClosedError("Pipe error")
