"""I2C protocol helpers for declarative device profiles."""

from __future__ import annotations

import copy
from typing import Any

from .profile_helpers import condition_matches


def initialize_i2c_interfaces(model: Any) -> None:
    """Create bus-local protocol state for every declarative I2C interface."""
    for name, definition in model.interfaces.items():
        if not isinstance(definition, dict):
            continue
        if str(definition.get("protocol") or "").strip().lower() != "i2c":
            continue

        state = model.interface_state.setdefault(name, {"last_request": None, "last_response": None, "history": []})
        config = _resolve_i2c_config(definition)
        state["i2c"] = {
            "selected_device_name": None,
            "read_mode": False,
            "pointer_register": 0,
            "pending_read_index": 0,
            "expect_pointer": False,
            "devices": _create_device_state_map(config),
        }


def handle_i2c_transaction(model: Any, interface_name: str, definition: dict[str, Any], payload: Any) -> Any:
    """Execute one declarative I2C slave-side response for a tester-driven master transaction."""
    if not isinstance(payload, dict):
        return _response(False, None, "I2C payload must be an object.")

    tester_role = str(payload.get("tester_role") or "master").strip().lower()
    if tester_role != "master":
        return _response(False, None, f"Unsupported tester role '{tester_role}'. Expected master.")
    external_device_role = str(payload.get("external_device_role") or "slave").strip().lower()
    if external_device_role != "slave":
        return _response(False, None, f"Unsupported external device role '{external_device_role}'. Expected slave.")

    state = model.interface_state.setdefault(interface_name, {"last_request": None, "last_response": None, "history": []})
    config = _resolve_i2c_config(definition)
    i2c_state = state.setdefault(
        "i2c",
        {
            "selected_device_name": None,
            "read_mode": False,
            "pointer_register": 0,
            "pending_read_index": 0,
            "expect_pointer": False,
            "devices": _create_device_state_map(config),
        },
    )
    devices = i2c_state.setdefault("devices", _create_device_state_map(config))

    if bool(payload.get("start_condition")):
        i2c_state["selected_device_name"] = None
        i2c_state["read_mode"] = False
        i2c_state["pending_read_index"] = 0
        i2c_state["expect_pointer"] = False

    to_send = _parse_byte(payload.get("to_send"))
    if to_send is None:
        return _response(False, None, "Missing I2C byte.")

    ack_mode = str(payload.get("ack_mode") or "READ").strip().upper()
    transfer_phase = str(payload.get("transfer_phase") or "").strip().lower()
    required_supply = float(config.get("required_supply_v", 0.0) or 0.0)
    supply_voltage = float(payload.get("supply_voltage", 0.0) or 0.0)
    if required_supply > 0.0 and supply_voltage < required_supply:
        return _response(False, None, f"Supply too low: {supply_voltage:0.###} V < {required_supply:0.###} V")

    if i2c_state["selected_device_name"] is None or bool(payload.get("start_condition")):
        device_name, device = _select_device(model, config, devices, to_send)
        if device is None:
            return _response(False, None, f"No I2C device matched address 0x{to_send:02X}.")

        i2c_state["selected_device_name"] = device_name
        i2c_state["read_mode"] = bool(to_send & 0x01)
        i2c_state["pending_read_index"] = 0
        i2c_state["expect_pointer"] = not i2c_state["read_mode"]
        i2c_state["pointer_register"] = int(device.get("pointer_register", 0) or 0) & 0xFF
        return _response(
            True,
            _master_readback_byte(config, to_send),
            f"Slave ACKed address phase {'read' if i2c_state['read_mode'] else 'write'}",
        )

    device = _get_selected_device(i2c_state, devices)
    if not isinstance(device, dict):
        return _response(False, None, "I2C device state is invalid.")

    if transfer_phase == "master_read" or ack_mode in {"WRITE", "NO ACK", "NO_ACK", "NACK"} or i2c_state["read_mode"]:
        read_value = _read_device_byte(device, i2c_state)
        return _response(True, read_value, f"Slave sent 0x{read_value:02X}")

    _write_device_byte(device, i2c_state, to_send)
    return _response(True, _master_readback_byte(config, to_send), f"Slave ACKed write 0x{to_send:02X}")


def _resolve_i2c_config(definition: dict[str, Any]) -> dict[str, Any]:
    """Return the I2C configuration block from an interface definition."""
    i2c_config = definition.get("i2c")
    return i2c_config if isinstance(i2c_config, dict) else definition


def _create_device_state_map(config: dict[str, Any]) -> dict[str, dict[str, Any]]:
    """Create runtime state containers for all declarative I2C devices."""
    devices = config.get("devices") or {}
    state_map: dict[str, dict[str, Any]] = {}
    iterator = devices.items() if isinstance(devices, dict) else enumerate(devices) if isinstance(devices, list) else []
    for key, candidate in iterator:
        if not isinstance(candidate, dict):
            continue
        state_map[str(candidate.get("name") or key)] = _create_device_state(candidate, key)
    return state_map


def _create_device_state(candidate: dict[str, Any], fallback_name: Any) -> dict[str, Any]:
    """Initialize one I2C device runtime state with registers and defaults."""
    registers = _create_register_map(candidate)
    return {
        "config": copy.deepcopy(candidate),
        "name": str(candidate.get("name") or fallback_name),
        "kind": str(candidate.get("kind") or "generic").strip().lower(),
        "pointer_register": int(candidate.get("initial_pointer", 0) or 0) & 0xFF,
        "temperature_c": float(candidate.get("temperature_c", 25.0) or 25.0),
        "registers": registers,
        "default_read_byte": _int_with_default(candidate.get("default_read_byte"), 0x00) & 0xFF,
    }


def _create_register_map(candidate: dict[str, Any]) -> dict[int, int]:
    """Build the initial register map from device configuration."""
    registers: dict[int, int] = {}
    _apply_initial_registers(registers, candidate.get("registers"))
    _apply_initial_registers(registers, candidate.get("initial_registers"))
    return registers


def _apply_initial_registers(registers: dict[int, int], raw_registers: Any) -> None:
    """Populate register defaults from dict or list definitions."""
    if isinstance(raw_registers, dict):
        for raw_address, raw_value in raw_registers.items():
            address = _parse_int(raw_address)
            value = _parse_int(raw_value)
            if address is None or value is None:
                continue
            registers[address & 0xFF] = value & 0xFF
        return

    if isinstance(raw_registers, list):
        for index, raw_value in enumerate(raw_registers):
            value = _parse_int(raw_value)
            if value is None:
                continue
            registers[index & 0xFF] = value & 0xFF


def _select_device(model: Any, config: dict[str, Any], devices: dict[str, dict[str, Any]], address_byte: int) -> tuple[str | None, dict[str, Any] | None]:
    """Choose the active device based on address and enable conditions."""
    address = (address_byte >> 1) & 0x7F
    for name, device in devices.items():
        device_config = device.get("config") or {}
        if int(device_config.get("address", -1)) != address:
            continue
        if not condition_matches(model, device_config.get("enabled_when")):
            continue
        return name, device
    return None, None


def _get_selected_device(i2c_state: dict[str, Any], devices: dict[str, dict[str, Any]]) -> dict[str, Any] | None:
    """Return the currently selected I2C device state if available."""
    selected_device_name = str(i2c_state.get("selected_device_name") or "").strip()
    if not selected_device_name:
        return None
    device = devices.get(selected_device_name)
    return device if isinstance(device, dict) else None


def _write_device_byte(device: dict[str, Any], i2c_state: dict[str, Any], value: int) -> None:
    """Write a byte either to the pointer register or the data register map."""
    if bool(i2c_state.get("expect_pointer", False)):
        pointer = value & 0xFF
        i2c_state["pointer_register"] = pointer
        i2c_state["pending_read_index"] = 0
        i2c_state["expect_pointer"] = False
        device["pointer_register"] = pointer
        return

    pointer = int(i2c_state.get("pointer_register", 0) or 0) & 0xFF
    registers = device.setdefault("registers", {})
    registers[pointer] = value & 0xFF
    pointer = (pointer + 1) & 0xFF
    i2c_state["pointer_register"] = pointer
    i2c_state["pending_read_index"] = 0
    device["pointer_register"] = pointer


def _read_device_byte(device: dict[str, Any], i2c_state: dict[str, Any]) -> int:
    """Read a byte from the selected device, including special device kinds."""
    kind = str(device.get("kind") or "generic").strip().lower()
    if kind == "lm75":
        return _read_lm75_byte(device, i2c_state)
    return _read_generic_register_byte(device, i2c_state)


def _read_lm75_byte(device: dict[str, Any], i2c_state: dict[str, Any]) -> int:
    """Emit LM75 temperature register bytes based on the configured temperature."""
    pointer = int(i2c_state.get("pointer_register", device.get("pointer_register", 0)) or 0) & 0xFF
    read_index = int(i2c_state.get("pending_read_index", 0) or 0)

    if pointer == 0x00:
        raw_temperature = int(round(float(device.get("temperature_c", 25.0) or 25.0))) & 0xFF
        value = raw_temperature if read_index == 0 else 0x00
        i2c_state["pending_read_index"] = read_index + 1
        device["pointer_register"] = pointer
        return value

    return _read_generic_register_byte(device, i2c_state)


def _read_generic_register_byte(device: dict[str, Any], i2c_state: dict[str, Any]) -> int:
    """Read one byte from the device register map and advance the pointer."""
    pointer = int(i2c_state.get("pointer_register", device.get("pointer_register", 0)) or 0) & 0xFF
    registers = device.get("registers") or {}
    value = int(registers.get(pointer, device.get("default_read_byte", 0)) or 0) & 0xFF
    pointer = (pointer + 1) & 0xFF
    i2c_state["pointer_register"] = pointer
    i2c_state["pending_read_index"] = int(i2c_state.get("pending_read_index", 0) or 0) + 1
    device["pointer_register"] = pointer
    return value


def _master_readback_byte(config: dict[str, Any], written_byte: int) -> int:
    """Return the byte echoed back to the tester during writes."""
    mode = str(config.get("master_readback", config.get("write_response", "echo")) or "echo").strip().lower()
    if mode == "fixed":
        return _parse_byte(config.get("master_readback_byte", config.get("write_response_byte"))) or 0
    return written_byte & 0xFF


def _parse_byte(value: Any) -> int | None:
    """Parse a hex/decimal byte value from profile data."""
    if isinstance(value, bool) or value is None:
        return None
    if isinstance(value, (int, float)):
        return int(value) & 0xFF

    text = str(value).strip().strip("'\"")
    if not text:
        return None
    if text.lower().startswith("0x"):
        text = text[2:]
    try:
        return int(text, 16) & 0xFF
    except ValueError:
        return None


def _parse_int(value: Any) -> int | None:
    """Parse a hex/decimal integer value from profile data."""
    if isinstance(value, bool) or value is None:
        return None
    if isinstance(value, (int, float)):
        return int(value)
    text = str(value).strip().strip("'\"")
    if not text:
        return None
    base = 16 if text.lower().startswith("0x") else 10
    if base == 16:
        text = text[2:]
    try:
        return int(text, base)
    except ValueError:
        return None


def _int_with_default(value: Any, default: int) -> int:
    """Return a parsed integer or the specified default."""
    parsed = _parse_int(value)
    return default if parsed is None else parsed


def _response(acknowledged: bool, actual_byte: int | None, details: str) -> dict[str, Any]:
    """Build the structured response returned to the C# simulator."""
    return {
        "acknowledged": bool(acknowledged),
        "actual_byte": None if actual_byte is None else int(actual_byte) & 0xFF,
        "details": details,
    }
