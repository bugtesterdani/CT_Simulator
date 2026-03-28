"""Declarative ICT measurement handling for device profiles."""

from __future__ import annotations

from typing import Any

from .profile_helpers import lookup_numeric


def handle_ict_request(model: Any, interface_name: str, definition: dict[str, Any], payload: Any) -> dict[str, Any]:
    """Resolve one ICT measurement request from the declarative profile."""
    if not isinstance(payload, dict):
        return {"error": "ICT payload must be a dict."}

    measurements = definition.get("measurements") or []
    metric_key = str(payload.get("metric") or "").strip().lower()
    name = str(payload.get("name") or "").strip()
    reference = str(payload.get("reference") or "").strip()
    type_id = str(payload.get("type_id") or "").strip()

    for entry in measurements:
        if not isinstance(entry, dict):
            continue
        if not _matches(entry.get("when") or {}, metric_key, name, reference, type_id):
            continue

        if "error" in entry:
            return {"error": str(entry.get("error") or "ICT measurement failed")}

        if "value" in entry:
            return {"value": lookup_numeric(entry, "value", 0.0), "details": entry.get("details")}

        if "measurements" in entry and isinstance(entry.get("measurements"), dict):
            return {"measurements": entry.get("measurements"), "details": entry.get("details")}

    default_value = definition.get("default_value")
    if default_value is not None:
        return {"value": float(default_value)}

    if definition.get("mode") == "echo_nominal" or definition.get("use_nominal"):
        value = _resolve_payload_fallback(payload)
        if value is not None:
            return {"value": value, "details": "payload"}

    return {"error": f"Kein ICT-Messwert fuer {reference or name or type_id or metric_key} definiert."}


def _matches(condition: dict[str, Any], metric_key: str, name: str, reference: str, type_id: str) -> bool:
    if not isinstance(condition, dict):
        return True

    if "metric" in condition and str(condition.get("metric") or "").strip().lower() != metric_key:
        return False
    if "name" in condition and str(condition.get("name") or "").strip() != name:
        return False
    if "reference" in condition and str(condition.get("reference") or "").strip() != reference:
        return False
    if "type_id" in condition and str(condition.get("type_id") or "").strip() != type_id:
        return False
    return True


def _resolve_payload_fallback(payload: dict[str, Any]) -> float | None:
    nominal = _payload_numeric(payload.get("nominal"))
    if nominal is not None:
        return nominal
    lower = _payload_numeric(payload.get("lower"))
    upper = _payload_numeric(payload.get("upper"))
    if lower is not None and upper is not None:
        return (lower + upper) / 2.0
    if lower is not None:
        return lower
    if upper is not None:
        return upper
    return None


def _payload_numeric(value: Any) -> float | None:
    try:
        if value is None:
            return None
        return float(value)
    except (TypeError, ValueError):
        return None
