from __future__ import annotations

from typing import Any

from core import BaseDeviceModel


class Mda1Sc3DeviceModel(BaseDeviceModel):
    """Python device model for the MDA1 SC3 adapter scenario."""

    CLOSED_POINTS = [
        f"TP{index}"
        for index in range(1, 97)
        if index not in (14, 28)
    ]

    def reset(self) -> None:
        """Reset runtime state for the DUT model."""
        self.now_ms = 0
        self.last_ict_response: dict[str, Any] | None = None
        self.last_shrt_response: dict[str, Any] | None = None

    def set_input(self, name: str, value: Any) -> None:
        """No external inputs are modeled for this DUT."""
        raise KeyError(f"Unknown input '{name}'.")

    def get_signal(self, name: str) -> float:
        """No direct output signals are modeled for this DUT."""
        raise KeyError(f"Unknown signal '{name}'.")

    def send_interface(self, name: str, payload: Any) -> Any:
        """Handle ICT and SHRT requests from the simulator."""
        interface_name = name.strip().upper()
        if interface_name == "ICT":
            response = self._handle_ict(payload)
            self.last_ict_response = response
            return response

        if interface_name == "SHRT":
            response = self._handle_shrt(payload)
            self.last_shrt_response = response
            return response

        raise KeyError(f"Unknown interface '{name}'.")

    def read_interface(self, name: str) -> Any:
        """Return the last stored interface response."""
        interface_name = name.strip().upper()
        if interface_name == "ICT":
            return self.last_ict_response or {}
        if interface_name == "SHRT":
            return self.last_shrt_response or {"shorts": []}
        raise KeyError(f"Unknown interface '{name}'.")

    def read_state(self) -> dict[str, Any]:
        """Return a minimal diagnostic snapshot for the UI."""
        return {
            "time_ms": self.now_ms,
            "inputs": {},
            "sources": {},
            "internal": {},
            "outputs": {},
            "interfaces": {
                "ICT": self.last_ict_response,
                "SHRT": self.last_shrt_response,
            },
        }

    def state_marker(self) -> dict[str, Any]:
        """Return a minimal state marker stored with protocol responses."""
        return {
            "time_ms": self.now_ms,
        }

    def get_device_info(self) -> dict[str, Any]:
        """Describe the device capabilities for the simulator handshake."""
        return {
            "name": "mda1-sc3",
            "signals": [],
            "interfaces": ["ICT", "SHRT"],
            "kind": "python",
            "ctct": {
                "resistances": self.get_ctct_resistances(),
            },
        }

    def get_ctct_resistances(self) -> list[dict[str, Any]]:
        """Provide DUT-side CTCT resistance network for contact tests."""
        resistances: list[dict[str, Any]] = []
        points = [f"DevicePort.{index}" for index in range(1, 97) if index not in (14, 28)]
        if len(points) < 2:
            return resistances

        for index, source in enumerate(points):
            target = points[(index + 1) % len(points)]
            resistances.append({
                "id": f"DUT_CTCT_R{index + 1}",
                "a": source,
                "b": target,
                "ohms": 1.0,
            })
        return resistances

    def _handle_shrt(self, payload: Any) -> dict[str, Any]:
        """Return shortcut measurements aligned with the CTCT contact network."""
        if not isinstance(payload, dict):
            return {"error": "SHRT payload must be a dict."}

        pairs = payload.get("pairs") or []
        threshold = _try_float(payload.get("threshold"))

        shorts: list[dict[str, Any]] = []
        default_ohms = threshold + 100.0 if threshold is not None else 1000.0
        for entry in pairs:
            if not isinstance(entry, dict):
                continue
            source = str(entry.get("a") or entry.get("source") or "").strip()
            target = str(entry.get("b") or entry.get("target") or "").strip()
            if not source or not target:
                continue
            shorts.append({"a": source, "b": target, "ohms": float(default_ohms)})

        return {"shorts": shorts}

    @staticmethod
    def _handle_ict(payload: Any) -> dict[str, Any]:
        """Return a measurement inside the ICT tolerance band."""
        if not isinstance(payload, dict):
            return {"error": "ICT payload must be a dict."}

        nominal = _try_float(payload.get("nominal"))
        lower = _try_float(payload.get("lower"))
        upper = _try_float(payload.get("upper"))

        if nominal is not None:
            return {"value": nominal, "details": "nominal"}

        if lower is not None and upper is not None:
            return {"value": (lower + upper) / 2.0, "details": "midpoint"}

        if lower is not None:
            return {"value": lower, "details": "lower"}

        if upper is not None:
            return {"value": upper, "details": "upper"}

        return {"value": 0.0, "details": "fallback"}


def _try_float(value: Any) -> float | None:
    """Executes _try_float."""
    try:
        if value is None:
            return None
        return float(value)
    except (TypeError, ValueError):
        return None
