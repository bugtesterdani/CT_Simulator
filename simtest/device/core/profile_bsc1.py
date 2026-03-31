"""Declarative BSC1 (Boundary Scan) handling for device profiles."""

from __future__ import annotations

from typing import Any


def handle_bsc1_request(model: Any, interface_name: str, definition: dict[str, Any], payload: Any) -> dict[str, Any]:
    """Resolve one BSC1 request and return boundary scan results for the DUT."""
    if not isinstance(payload, dict):
        return {"status": "error", "details": "BSC1 payload must be a dict."}

    if isinstance(definition, dict) and definition.get("error"):
        return {"status": "error", "details": str(definition.get("error"))}

    test_name = str(payload.get("test_name") or payload.get("name") or "").strip()
    partial_count = _normalize_int(payload.get("partial_tests")) or 0
    entries = definition.get("partials") or definition.get("tests") or []
    if not isinstance(entries, list):
        entries = [entries]

    default_outcome = _normalize_outcome(definition.get("default_outcome") or definition.get("outcome") or "pass")
    partials: list[dict[str, Any]] = []

    for entry in entries:
        if not isinstance(entry, dict):
            continue
        when = entry.get("when") or {}
        if not _matches(when, test_name):
            continue
        if "error" in entry:
            return {"status": "error", "details": str(entry.get("error") or "BSC1 error")}
        index = _normalize_int(entry.get("index") or entry.get("partial")) or (len(partials) + 1)
        partials.append({
            "index": index,
            "name": entry.get("name"),
            "outcome": _normalize_outcome(entry.get("outcome") or entry.get("status") or default_outcome),
            "details": entry.get("details"),
        })

    if not partials and partial_count > 0:
        for index in range(1, partial_count + 1):
            partials.append({
                "index": index,
                "outcome": default_outcome,
                "details": definition.get("details"),
            })

    overall = _aggregate_outcome(default_outcome, partials)
    response: dict[str, Any] = {
        "status": "ok",
        "outcome": overall,
        "details": definition.get("details"),
    }

    if partials:
        response["partials"] = partials

    return response


def _matches(condition: dict[str, Any], test_name: str) -> bool:
    """Executes _matches."""
    if not isinstance(condition, dict):
        return True
    if "test_name" in condition and str(condition.get("test_name") or "").strip() != test_name:
        return False
    if "name" in condition and str(condition.get("name") or "").strip() != test_name:
        return False
    return True


def _aggregate_outcome(default_outcome: str, partials: list[dict[str, Any]]) -> str:
    """Executes _aggregate_outcome."""
    outcome = default_outcome
    for entry in partials:
        current = _normalize_outcome(entry.get("outcome") or default_outcome)
        if current == "error":
            return "error"
        if current == "fail":
            outcome = "fail"
    return outcome


def _normalize_outcome(raw: Any) -> str:
    """Executes _normalize_outcome."""
    text = str(raw or "").strip().lower()
    if text in {"ok", "pass"}:
        return "pass"
    if text == "fail":
        return "fail"
    if text == "error":
        return "error"
    return "error"


def _normalize_int(value: Any) -> int | None:
    """Executes _normalize_int."""
    try:
        if value is None:
            return None
        return int(value)
    except (TypeError, ValueError):
        return None
