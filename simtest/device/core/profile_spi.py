"""SPI protocol helpers for declarative device profiles."""

from __future__ import annotations

import copy
from typing import Any

from .profile_helpers import condition_matches


def initialize_spi_interfaces(model: Any) -> None:
    """Create bus-local protocol state for every declarative SPI interface."""
    for name, definition in model.interfaces.items():
        if not isinstance(definition, dict):
            continue
        if str(definition.get("protocol") or "").strip().lower() != "spi":
            continue

        state = model.interface_state.setdefault(name, {"last_request": None, "last_response": None, "history": []})
        config = _resolve_spi_config(definition)
        state["spi"] = {
            "devices": _create_device_state_map(config),
        }


def handle_spi_transaction(model: Any, interface_name: str, definition: dict[str, Any], payload: Any) -> Any:
    """Execute one declarative SPI slave-side response for a tester-driven master transaction."""
    if not isinstance(payload, dict):
        return _error("SPI payload must be an object.")

    tester_role = str(payload.get("tester_role") or "master").strip().lower()
    if tester_role != "master":
        return _error(f"Unsupported tester role '{tester_role}'. Expected master.")
    external_device_role = str(payload.get("external_device_role") or "slave").strip().lower()
    if external_device_role != "slave":
        return _error(f"Unsupported external device role '{external_device_role}'. Expected slave.")

    config = _resolve_spi_config(definition)
    state = model.interface_state.setdefault(interface_name, {"last_request": None, "last_response": None, "history": []})
    spi_state = state.setdefault("spi", {"devices": _create_device_state_map(config)})
    devices = spi_state.setdefault("devices", _create_device_state_map(config))
    device_name, device = _select_device(model, config, devices)
    if device is None:
        return _error("No SPI slave is active on this interface.")

    operation = str(payload.get("operation") or "transaction").strip().lower()
    validation_error = _validate_bus(config, payload, operation)
    if validation_error is not None:
        return _error(validation_error)

    if operation == "transaction":
        return _handle_spi_transaction(config, payload, device_name or "SPI", device, model.now_ms)
    if operation == "dm30_write_serial":
        return _handle_dm30_write_serial(payload, device_name or "DM30_SPI_EEPROM", device, model.now_ms)
    if operation == "dm30_dump_hex":
        return _handle_dm30_dump_hex(device_name or "DM30_SPI_EEPROM", device)

    return _error(f"Unsupported SPI operation '{operation}'.")


def _resolve_spi_config(definition: dict[str, Any]) -> dict[str, Any]:
    """Return the SPI configuration block from an interface definition."""
    spi_config = definition.get("spi")
    return spi_config if isinstance(spi_config, dict) else definition


def _create_device_state_map(config: dict[str, Any]) -> dict[str, dict[str, Any]]:
    """Create runtime state containers for all declarative SPI devices."""
    devices = config.get("devices") or {}
    state_map: dict[str, dict[str, Any]] = {}
    iterator = devices.items() if isinstance(devices, dict) else enumerate(devices) if isinstance(devices, list) else []
    for key, candidate in iterator:
        if not isinstance(candidate, dict):
            continue
        state_map[str(candidate.get("name") or key)] = _create_device_state(candidate)
    return state_map


def _create_device_state(candidate: dict[str, Any]) -> dict[str, Any]:
    """Initialize one SPI device runtime state with memory and defaults."""
    size_bytes = max(1, _int_with_default(candidate.get("size_bytes"), 256))
    memory = bytearray(size_bytes)
    _apply_initial_memory(memory, candidate)
    status_register = _int_with_default(candidate.get("status_register"), 0x00) & 0xFF
    write_protect = bool(candidate.get("write_protect", False))
    if write_protect:
        status_register |= 0x0C

    return {
        "config": copy.deepcopy(candidate),
        "memory": memory,
        "status_register": status_register,
        "busy_until_ms": 0,
        "address_width_bytes": max(1, _int_with_default(candidate.get("address_width_bytes"), 2)),
        "page_size": max(1, _int_with_default(candidate.get("page_size"), 64)),
        "write_delay_ms": max(0, _int_with_default(candidate.get("write_delay_ms"), 0)),
        "default_read_byte": _int_with_default(candidate.get("default_read_byte"), 0xFF) & 0xFF,
        "write_protect": write_protect,
    }


def _apply_initial_memory(memory: bytearray, config: dict[str, Any]) -> None:
    """Seed the EEPROM memory from dict/list/hex configuration."""
    initial_memory = config.get("initial_memory")
    if isinstance(initial_memory, dict):
        for raw_address, raw_value in initial_memory.items():
            address = _parse_int(raw_address)
            value = _parse_int(raw_value)
            if address is None or value is None:
                continue
            if 0 <= address < len(memory):
                memory[address] = value & 0xFF
        return

    if isinstance(initial_memory, list):
        for index, raw_value in enumerate(initial_memory):
            value = _parse_int(raw_value)
            if value is None or index >= len(memory):
                continue
            memory[index] = value & 0xFF
        return

    raw_hex = config.get("initial_memory_hex")
    if raw_hex is None:
        return

    hex_text = _normalize_hex(raw_hex)
    for index in range(0, min(len(hex_text), len(memory) * 2), 2):
        memory[index // 2] = int(hex_text[index:index + 2], 16)


def _select_device(model: Any, config: dict[str, Any], devices: dict[str, dict[str, Any]]) -> tuple[str | None, dict[str, Any] | None]:
    """Choose the active SPI device based on enabled conditions."""
    for name, device in devices.items():
        device_config = device.get("config") or {}
        if not condition_matches(model, device_config.get("enabled_when")):
            continue
        return name, device
    return None, None


def _validate_bus(config: dict[str, Any], payload: dict[str, Any], operation: str) -> str | None:
    """Validate power and bus-parameter expectations for one SPI transfer."""
    required_supply = float(config.get("required_supply_v", 0.0) or 0.0)
    supply_voltage = float(payload.get("power_source_voltage", 0.0) or 0.0)
    if required_supply > 0.0 and supply_voltage < required_supply:
        return f"Supply too low: {supply_voltage:0.###} V < {required_supply:0.###} V."

    expected_phase = _normalize_text(config.get("clock_phase"))
    actual_phase = _normalize_text(payload.get("clock_phase"))
    if expected_phase and actual_phase and expected_phase != actual_phase:
        return f"Clock phase mismatch: expected '{expected_phase}', got '{actual_phase}'."

    expected_polarity = _normalize_text(config.get("clock_polarity"))
    actual_polarity = _normalize_text(payload.get("clock_polarity"))
    if expected_polarity and actual_polarity and expected_polarity != actual_polarity:
        return f"Clock polarity mismatch: expected '{expected_polarity}', got '{actual_polarity}'."

    expected_cs_active = _normalize_text(config.get("chip_select_active"))
    actual_cs_active = _normalize_text(payload.get("chip_select_active"))
    if expected_cs_active and actual_cs_active and expected_cs_active != actual_cs_active:
        return f"Chip-select polarity mismatch: expected '{expected_cs_active}', got '{actual_cs_active}'."

    expected_frequency = float(config.get("frequency_hz", 0.0) or 0.0)
    actual_frequency = float(payload.get("frequency_hz", 0.0) or 0.0)
    if expected_frequency > 0.0 and actual_frequency > 0.0 and abs(actual_frequency - expected_frequency) > 0.5:
        return f"Clock frequency mismatch: expected {expected_frequency:0.###} Hz, got {actual_frequency:0.###} Hz."

    if operation == "transaction":
        bit_count = int(payload.get("bit_count", 0) or 0)
        if bit_count <= 0:
            return "Missing SPI bit count."
        if bit_count % 8 != 0:
            return f"Unsupported SPI bit count {bit_count}. Only full-byte transfers are supported."

        if not _normalize_hex(payload.get("tx_hex")):
            return "Missing SPI payload."

    return None


def _handle_spi_transaction(config: dict[str, Any], payload: dict[str, Any], device_name: str, device: dict[str, Any], now_ms: int) -> dict[str, Any]:
    """Execute a full SPI transaction against the selected device."""
    tx_hex = _normalize_hex(payload.get("tx_hex"))
    if not tx_hex:
        return _error("Missing SPI TX data.")

    tx_bytes = _hex_to_bytes(tx_hex)
    kind = str((device.get("config") or {}).get("kind") or "generic").strip().lower()
    if kind == "cat25128":
        return _handle_cat25128_transaction(config, payload, device_name, device, now_ms, tx_bytes)

    return _ok(
        "".join(f"{device.get('default_read_byte', 0xFF):02X}" for _ in tx_bytes),
        f"{device_name}: generic SPI slave returned idle-high bytes.",
        tx_bytes,
    )


def _handle_cat25128_transaction(config: dict[str, Any], payload: dict[str, Any], device_name: str, device: dict[str, Any], now_ms: int, tx_bytes: list[int]) -> dict[str, Any]:
    """Handle CAT25128 EEPROM command semantics."""
    rx_bytes = [int(device.get("default_read_byte", 0xFF)) & 0xFF for _ in tx_bytes]
    status_register = _effective_status_register(device, now_ms)
    command = tx_bytes[0]

    if command == 0x06:
        device["status_register"] = status_register | 0x02
        return _ok(_bytes_to_hex(rx_bytes), f"{device_name}: WREN accepted.", tx_bytes)

    if command == 0x04:
        device["status_register"] = status_register & ~0x02
        return _ok(_bytes_to_hex(rx_bytes), f"{device_name}: WRDI accepted.", tx_bytes)

    if command == 0x05:
        for index in range(1, len(rx_bytes)):
            rx_bytes[index] = _effective_status_register(device, now_ms)
        return _ok(_bytes_to_hex(rx_bytes), f"{device_name}: status register read.", tx_bytes)

    if command == 0x03:
        address = _parse_address(tx_bytes, int(device.get("address_width_bytes", 2) or 2))
        for index in range(1 + int(device.get("address_width_bytes", 2) or 2), len(tx_bytes)):
            memory_index = (address + (index - 1 - int(device.get("address_width_bytes", 2) or 2))) % len(device["memory"])
            rx_bytes[index] = device["memory"][memory_index]
        return _ok(_bytes_to_hex(rx_bytes), f"{device_name}: READ from 0x{address:04X}.", tx_bytes)

    if command == 0x02:
        if _effective_status_register(device, now_ms) & 0x01:
            return _error(f"{device_name}: device is busy.")
        if not (_effective_status_register(device, now_ms) & 0x02):
            return _ok(_bytes_to_hex(rx_bytes), f"{device_name}: WRITE ignored because WEL is not set.", tx_bytes)
        if bool(device.get("write_protect", False)):
            return _error(f"{device_name}: write-protect is active.")

        address_width = int(device.get("address_width_bytes", 2) or 2)
        address = _parse_address(tx_bytes, address_width)
        payload_bytes = tx_bytes[1 + address_width:]
        page_size = int(device.get("page_size", 64) or 64)
        page_start = (address // page_size) * page_size
        page_offset = address % page_size
        for index, value in enumerate(payload_bytes):
            memory_index = page_start + ((page_offset + index) % page_size)
            if 0 <= memory_index < len(device["memory"]):
                device["memory"][memory_index] = value & 0xFF

        delay_ms = int(device.get("write_delay_ms", 0) or 0)
        device["busy_until_ms"] = now_ms + delay_ms
        device["status_register"] = (_effective_status_register(device, now_ms) | 0x01) & ~0x02
        return _ok(_bytes_to_hex(rx_bytes), f"{device_name}: WRITE {len(payload_bytes)} byte(s) to 0x{address:04X}.", tx_bytes)

    return _ok(_bytes_to_hex(rx_bytes), f"{device_name}: unsupported command 0x{command:02X} treated as idle readback.", tx_bytes)


def _handle_dm30_write_serial(payload: dict[str, Any], device_name: str, device: dict[str, Any], now_ms: int) -> dict[str, Any]:
    """Write a DM30 serial string into EEPROM memory."""
    serial_text = str(payload.get("serial_text") or "")
    if not serial_text:
        return _error(f"{device_name}: missing serial text.")

    encoded = serial_text.encode("ascii", errors="replace")
    memory = device["memory"]
    for index, value in enumerate(encoded):
        if index >= len(memory):
            break
        memory[index] = value

    delay_ms = int(device.get("write_delay_ms", 0) or 0)
    device["busy_until_ms"] = now_ms + delay_ms
    return {
        "status": "ok",
        "details": f"{device_name}: wrote serial '{serial_text}' to EEPROM image.",
        "read_hex": None,
        "read_bytes": [],
        "serial_text": serial_text,
    }


def _handle_dm30_dump_hex(device_name: str, device: dict[str, Any]) -> dict[str, Any]:
    """Dump the full EEPROM memory as a hex string."""
    memory = device["memory"]
    read_hex = "".join(f"{value:02X}" for value in memory)
    return {
        "status": "ok",
        "details": f"{device_name}: dumped {len(memory)} EEPROM byte(s).",
        "read_hex": read_hex,
        "read_bytes": list(memory),
    }


def _effective_status_register(device: dict[str, Any], now_ms: int) -> int:
    """Compute the current status register, respecting busy timing."""
    status = int(device.get("status_register", 0x00) or 0x00) & 0xFF
    if now_ms >= int(device.get("busy_until_ms", 0) or 0):
        status &= ~0x01
        device["status_register"] = status
    else:
        status |= 0x01
    return status & 0xFF


def _parse_address(tx_bytes: list[int], address_width: int) -> int:
    """Parse a big-endian address from the command byte stream."""
    address = 0
    for index in range(address_width):
        value = tx_bytes[index + 1] if index + 1 < len(tx_bytes) else 0
        address = (address << 8) | (value & 0xFF)
    return address


def _ok(read_hex: str | None, details: str, tx_bytes: list[int]) -> dict[str, Any]:
    """Build an ok response with decoded readback bytes."""
    read_bytes = _hex_to_bytes(read_hex) if read_hex else []
    return {
        "status": "ok",
        "details": details,
        "read_hex": read_hex,
        "rx_hex": read_hex,
        "read_bytes": read_bytes,
        "tx_bytes": tx_bytes,
    }


def _error(details: str) -> dict[str, Any]:
    """Build a standardized error response for SPI operations."""
    return {
        "status": "error",
        "details": details,
        "read_hex": None,
        "rx_hex": None,
        "read_bytes": [],
    }


def _normalize_hex(value: Any) -> str:
    """Normalize free-form hex strings into an even-length uppercase buffer."""
    text = "".join(ch for ch in str(value or "").strip().strip("'\"") if ch.upper() in "0123456789ABCDEF")
    if len(text) % 2 != 0:
        text = "0" + text
    return text.upper()


def _hex_to_bytes(value: str | None) -> list[int]:
    """Convert a hex string into a list of byte values."""
    hex_text = _normalize_hex(value)
    return [int(hex_text[index:index + 2], 16) for index in range(0, len(hex_text), 2)]


def _bytes_to_hex(values: list[int]) -> str:
    """Convert a list of byte values into a hex string."""
    return "".join(f"{value & 0xFF:02X}" for value in values)


def _normalize_text(value: Any) -> str:
    """Normalize free-form text to lowercase for comparisons."""
    return str(value or "").strip().lower()


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
