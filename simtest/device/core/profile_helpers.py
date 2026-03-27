"""Shared helper functions used by the declarative profile runtime."""

from __future__ import annotations

import json
from typing import Any


def normalize_mapping(raw: Any) -> dict[str, Any]:
    """Normalize profile mappings to upper-case keys for case-insensitive access."""
    if not isinstance(raw, dict):
        return {}
    return {str(key).strip().upper(): value for key, value in raw.items()}


def lookup_numeric(values: dict[str, Any], key: str, default: float) -> float:
    """Read one numeric value from a normalized mapping with a fallback."""
    return float(values.get(key, values.get(key.lower(), default)) or default)


def compare(value: float, conditions: dict[str, Any]) -> bool:
    """Evaluate simple declarative comparison operators against a numeric value."""
    if "gte" in conditions and value < float(conditions["gte"]):
        return False
    if "gt" in conditions and value <= float(conditions["gt"]):
        return False
    if "lte" in conditions and value > float(conditions["lte"]):
        return False
    if "lt" in conditions and value >= float(conditions["lt"]):
        return False
    if "eq" in conditions and value != float(conditions["eq"]):
        return False
    return True


def read_signal_value(model: Any, signal: str) -> float:
    """Read one signal value from the runtime, including waveform-backed inputs."""
    if signal in model.input_waveforms:
        return float(model._waveform_value_at(model.input_waveforms[signal], model.now_ms))
    if signal in model.inputs:
        return float(model.inputs.get(signal, 0.0))
    if signal in model.sources:
        return float(model.sources.get(signal, 0.0))
    if signal in model.internal:
        return float(model.internal.get(signal, 0.0))
    if signal in model.signal_outputs:
        return float(model.get_signal(signal))
    return 0.0


def condition_matches(model: Any, condition: Any) -> bool:
    """Evaluate a declarative condition tree against the current model state."""
    if condition is None:
        return True
    if not isinstance(condition, dict):
        return False

    if "all" in condition:
        return all(condition_matches(model, item) for item in condition.get("all") or [])
    if "any" in condition:
        return any(condition_matches(model, item) for item in condition.get("any") or [])

    signal_name = condition.get("input") or condition.get("source") or condition.get("signal")
    if signal_name is None:
        return False

    value = read_signal_value(model, str(signal_name).strip().upper())
    return compare(value, condition)


def interface_request_matches(condition: Any, payload: Any) -> bool:
    """Match one declarative interface request filter against a payload."""
    if condition is None:
        return True
    if not isinstance(condition, dict):
        return False

    text = payload if isinstance(payload, str) else json.dumps(payload, sort_keys=True)
    if "equals" in condition and text != str(condition["equals"]):
        return False
    if "contains" in condition and str(condition["contains"]) not in text:
        return False
    return True


def are_sources_enabled(model: Any) -> bool:
    """Check whether source-control minimum conditions allow output evaluation."""
    minimums = model.source_control.get("minimums") or {}
    if not minimums:
        return True

    for name, threshold in minimums.items():
        signal = str(name).strip().upper()
        current = float(model.sources.get(signal, 0.0))
        if current < float(threshold):
            return False
    return True
