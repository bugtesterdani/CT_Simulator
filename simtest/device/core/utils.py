"""Small conversion helpers shared by the device runtime."""

from typing import Any


def to_bool(value: Any) -> bool:
    """Convert loose simulator values into a stable Boolean representation."""
    if isinstance(value, bool):
        return value
    if isinstance(value, (int, float)):
        return value != 0
    return str(value).strip().lower() in {"1", "true", "yes", "on"}
