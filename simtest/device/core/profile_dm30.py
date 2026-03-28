"""DM30 digital pattern helpers for declarative device profiles."""

from __future__ import annotations

from typing import Any

from .profile_helpers import condition_matches


def initialize_dm30_interfaces(model: Any) -> None:
    """Create state storage for DM30 interfaces."""
    for name, definition in model.interfaces.items():
        if not isinstance(definition, dict):
            continue
        if str(definition.get("protocol") or "").strip().lower() != "dm30":
            continue

        state = model.interface_state.setdefault(name, {"last_request": None, "last_response": None, "history": []})
        state["dm30"] = {
            "last_acquisition": {},
        }


def handle_dm30_request(model: Any, interface_name: str, definition: dict[str, Any], payload: Any) -> Any:
    """Return acquisition patterns for a DM30 digital test request."""
    if not isinstance(payload, dict):
        return _error("DM30 payload must be an object.")

    if str(payload.get("operation") or "pattern_evaluate").strip().lower() != "pattern_evaluate":
        return _error("Unsupported DM30 operation.")

    acquisition = payload.get("acquisition") or []
    if not isinstance(acquisition, list):
        return _error("DM30 acquisition list missing.")

    mode = str(definition.get("mode") or "pass").strip().lower()
    if mode == "error":
        return _error(definition.get("error_message") or "DM30 device error.")

    override = definition.get("acquisition_overrides") or {}
    result_map: dict[str, str] = {}
    for entry in acquisition:
        if not isinstance(entry, dict):
            continue
        name = str(entry.get("name") or "").strip()
        if not name:
            continue
        if name in override:
            result_map[name] = _normalize_hex(override[name])
        else:
            result_map[name] = _normalize_hex(entry.get("nominal_hex") or "")

    if mode == "fail":
        _apply_failure(definition, acquisition, result_map)

    return {
        "status": "ok",
        "mode": mode,
        "acquisition": result_map,
        "details": definition.get("details") or "DM30 acquisition generated.",
    }


def _apply_failure(definition: dict[str, Any], acquisition: list[Any], result_map: dict[str, str]) -> None:
    """Flip one acquisition bit to force a failing compare result."""
    fail_signal = str(definition.get("fail_signal") or "").strip()
    fail_step = _int_with_default(definition.get("fail_step"), 1)

    if not fail_signal:
        for entry in acquisition:
            if isinstance(entry, dict):
                fail_signal = str(entry.get("name") or "").strip()
                if fail_signal:
                    break

    if not fail_signal or fail_signal not in result_map:
        return

    mask_hex = ""
    for entry in acquisition:
        if isinstance(entry, dict) and str(entry.get("name") or "").strip() == fail_signal:
            mask_hex = str(entry.get("mask_hex") or "")
            break

    bits = _hex_to_bits(result_map.get(fail_signal, ""))
    if not bits:
        return

    mask_bits = _hex_to_bits(mask_hex)
    index = max(0, fail_step - 1)
    if index >= len(bits):
        index = len(bits) - 1

    if mask_bits and index < len(mask_bits) and mask_bits[index] != 0:
        index = next((i for i, value in enumerate(mask_bits) if value == 0), index)

    bits[index] = 0 if bits[index] else 1
    result_map[fail_signal] = _bits_to_hex(bits)


def _hex_to_bits(raw_hex: str) -> list[int]:
    """Expand a hex string into a list of bit values (MSB first)."""
    normalized = _normalize_hex(raw_hex)
    if not normalized:
        return []

    bits: list[int] = []
    for character in normalized:
        value = int(character, 16)
        bits.extend([(value >> 3) & 1, (value >> 2) & 1, (value >> 1) & 1, value & 1])
    return bits


def _bits_to_hex(bits: list[int]) -> str:
    """Pack a list of bits into an uppercase hex string."""
    if not bits:
        return ""

    padded = bits[:]
    while len(padded) % 4 != 0:
        padded.append(0)

    hex_chars = []
    for index in range(0, len(padded), 4):
        nibble = (padded[index] << 3) | (padded[index + 1] << 2) | (padded[index + 2] << 1) | padded[index + 3]
        hex_chars.append(f"{nibble:X}")
    return "".join(hex_chars)


def _normalize_hex(raw_value: Any) -> str:
    """Normalize free-form hex input into a clean hex string."""
    if raw_value is None:
        return ""
    text = str(raw_value)
    return "".join(ch.upper() for ch in text if ch in "0123456789abcdefABCDEF")


def _int_with_default(value: Any, fallback: int) -> int:
    """Parse an integer or fall back to the provided default."""
    try:
        return int(value)
    except (TypeError, ValueError):
        return fallback


def _error(message: str) -> dict[str, Any]:
    """Build a standardized error response for DM30."""
    return {"status": "error", "details": message}
