"""Profile loading helpers for declarative JSON and YAML device models."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any


def load_profile_document(path: str) -> dict[str, Any]:
    """Load one declarative device profile and normalize it to a mapping."""
    profile_path = Path(path)
    if not profile_path.is_file():
        raise ValueError(f"Profile '{path}' not found.")

    extension = profile_path.suffix.lower()
    if extension == ".json":
        document = json.loads(profile_path.read_text(encoding="utf-8"))
    elif extension in {".yaml", ".yml"}:
        try:
            import yaml  # type: ignore
        except ImportError as error:
            raise ValueError("YAML profiles require the Python package 'PyYAML'.") from error

        document = yaml.safe_load(profile_path.read_text(encoding="utf-8"))
    else:
        raise ValueError(f"Unsupported profile type '{extension}'.")

    if not isinstance(document, dict):
        raise ValueError("Profile document must be a mapping.")

    return document
