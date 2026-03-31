"""Declarative DNIS (small inductivity) measurement handling for device profiles."""

from __future__ import annotations

from typing import Any

from .profile_helpers import lookup_numeric


def handle_dnis_request(model: Any, interface_name: str, definition: dict[str, Any], payload: Any) -> dict[str, Any]:
    """Resolve one DNIS measurement request from the declarative profile."""
    if not isinstance(payload, dict):
        return {"error": "DNIS payload must be a dict."}

    measurements = definition.get("measurements") or []
    signal = str(payload.get("signal") or "").strip()
    name = str(payload.get("name") or "").strip()
    channel = _normalize_int(payload.get("channel_index"))

    for entry in measurements:
        if not isinstance(entry, dict):
            continue
        if not _matches(entry.get("when") or {}, signal, name, channel):
            continue

        if "error" in entry:
            return {"error": str(entry.get("error") or "DNIS measurement failed")}

        if "measurements" in entry and isinstance(entry.get("measurements"), dict):
            return {"measurements": entry.get("measurements"), "details": entry.get("details")}

        result: dict[str, Any] = {}
        if "inductance" in entry:
            result["inductance"] = lookup_numeric(entry, "inductance", 0.0)
        elif "value" in entry:
            result["inductance"] = lookup_numeric(entry, "value", 0.0)

        if "serial_ohms" in entry:
            result["serial_ohms"] = lookup_numeric(entry, "serial_ohms", 0.0)
        if "serial_conductance" in entry:
            result["serial_ohms"] = lookup_numeric(entry, "serial_conductance", 0.0)
        if "impedance" in entry:
            result["serial_ohms"] = lookup_numeric(entry, "impedance", 0.0)

        if result:
            response = {"measurements": result}
            if entry.get("details"):
                response["details"] = entry.get("details")
            return response

    default_value = definition.get("default_inductance", definition.get("default_value"))
    if default_value is not None:
        return {"measurements": {"inductance": float(default_value)}}

    if definition.get("mode") == "echo_nominal" or definition.get("use_nominal"):
        fallback = _resolve_payload_fallback(payload)
        if fallback is not None:
            return {"measurements": {"inductance": fallback}, "details": "payload"}

    return {"error": f"Kein DNIS-Messwert fuer {signal or name or channel or 'unknown'} definiert."}


def _matches(condition: dict[str, Any], signal: str, name: str, channel: int | None) -> bool:
    """Executes _matches."""
    if not isinstance(condition, dict):
        return True

    if "signal" in condition and str(condition.get("signal") or "").strip() != signal:
        return False
    if "name" in condition and str(condition.get("name") or "").strip() != name:
        return False
    if "channel" in condition and _normalize_int(condition.get("channel")) != channel:
        return False
    return True


def _normalize_int(value: Any) -> int | None:
    try:
        if value is None:
            return None
        return int(value)
    except (TypeError, ValueError):
        return None


def _resolve_payload_fallback(payload: dict[str, Any]) -> float | None:
    """Executes _resolve_payload_fallback."""
    expected = _payload_numeric(payload.get("expected_inductance"))
    if expected is not None:
        return expected
    nominal = _payload_numeric(payload.get("nominal"))
    if nominal is not None:
        return nominal
    return None


def _payload_numeric(value: Any) -> float | None:
    try:
        if value is None:
            return None
        return float(value)
    except (TypeError, ValueError):
        return None
