"""Declarative SHRT (shortcut) handling for device profiles."""

from __future__ import annotations

from typing import Any

from .profile_helpers import lookup_numeric


def handle_shrt_request(model: Any, interface_name: str, definition: dict[str, Any], payload: Any) -> dict[str, Any]:
    """Resolve one SHRT request and return detected shorts for the DUT."""
    if not isinstance(payload, dict):
        return {"error": "SHRT payload must be a dict."}

    testpoints = _normalize_list(payload.get("testpoints"))
    threshold = lookup_numeric(payload, "threshold", None)
    entries = definition.get("shorts") or definition.get("measurements") or []
    pairs = payload.get("pairs") or []

    if not isinstance(entries, list):
        entries = [entries]

    if pairs:
        return _handle_pairs(definition, pairs)

    result: list[dict[str, Any]] = []
    for entry in entries:
        if not isinstance(entry, dict):
            continue
        if "error" in entry:
            return {"error": str(entry.get("error") or "SHRT measurement failed")}

        source = str(entry.get("a") or entry.get("source") or "").strip()
        target = str(entry.get("b") or entry.get("target") or "").strip()
        if not source or not target:
            continue

        if testpoints:
            if source not in testpoints or target not in testpoints:
                continue

        ohms = lookup_numeric(entry, "ohms", None)
        if ohms is None:
            continue

        if threshold is not None and ohms > threshold:
            continue

        result.append({"a": source, "b": target, "ohms": float(ohms)})

    return {"shorts": result}


def _normalize_list(raw: Any) -> list[str]:
    if not raw:
        return []
    if isinstance(raw, list):
        return [str(item).strip() for item in raw if str(item).strip()]
    return [str(raw).strip()]


def _handle_pairs(definition: dict[str, Any], pairs: Any) -> dict[str, Any]:
    if not isinstance(pairs, list):
        return {"error": "SHRT pairs must be a list."}

    default_ohms = lookup_numeric(definition, "default_ohms", None)
    overrides = _build_pair_overrides(definition.get("pairs") or definition.get("overrides") or [])

    result: list[dict[str, Any]] = []
    for entry in pairs:
        if not isinstance(entry, dict):
            continue
        source = str(entry.get("a") or entry.get("source") or "").strip()
        target = str(entry.get("b") or entry.get("target") or "").strip()
        if not source or not target:
            continue

        key = _normalize_pair(source, target)
        ohms = overrides.get(key, default_ohms)
        if ohms is None:
            return {"error": f"Kein SHRT-Wert fuer {source}-{target} definiert."}
        result.append({"a": source, "b": target, "ohms": float(ohms)})

    return {"shorts": result}


def _build_pair_overrides(raw: Any) -> dict[str, float]:
    overrides: dict[str, float] = {}
    if not isinstance(raw, list):
        raw = [raw]
    for entry in raw:
        if not isinstance(entry, dict):
            continue
        source = str(entry.get("a") or entry.get("source") or "").strip()
        target = str(entry.get("b") or entry.get("target") or "").strip()
        if not source or not target:
            continue
        ohms = lookup_numeric(entry, "ohms", None)
        if ohms is None:
            continue
        overrides[_normalize_pair(source, target)] = float(ohms)
    return overrides


def _normalize_pair(a: str, b: str) -> str:
    return "|".join(sorted([a.strip().lower(), b.strip().lower()]))
