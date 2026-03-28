from __future__ import annotations

from typing import Any

from core import BaseDeviceModel


class SmudBoundaryScanAdapterModel(BaseDeviceModel):
    def reset(self) -> None:
        self.now_ms = 0
        self.supply_voltage = 0.0
        self.ground_voltage = 0.0

    def set_input(self, name: str, value: Any) -> None:
        signal = name.strip().upper()
        numeric = float(value)
        if signal in {"DUT_SUPPLY", "SM2_VOUT"}:
            self.supply_voltage = numeric
            return
        if signal in {"DUT_GND", "SM2_GND"}:
            self.ground_voltage = numeric
            return
        raise KeyError(f"Unknown input '{name}'.")

    def get_signal(self, name: str) -> float:
        signal = name.strip().upper()
        if signal in {"DUT_CURRENT", "SM2_ISENSE"}:
            return 0.12 if self.supply_voltage >= 8.5 else 0.0
        raise KeyError(f"Unknown signal '{name}'.")

    def read_state(self) -> dict[str, Any]:
        return {
            "time_ms": self.now_ms,
            "inputs": {
                "DUT_SUPPLY": self.supply_voltage,
                "DUT_GND": self.ground_voltage,
            },
            "sources": {},
            "internal": {},
            "outputs": {
                "DUT_CURRENT": self.get_signal("DUT_CURRENT"),
            },
            "interfaces": {},
        }

    def state_marker(self) -> dict[str, Any]:
        return {
            "time_ms": self.now_ms,
            "inputs": {
                "DUT_SUPPLY": self.supply_voltage,
                "DUT_GND": self.ground_voltage,
            },
        }

    def get_device_info(self) -> dict[str, Any]:
        return {
            "name": "smud-boundary-scan-adapter",
            "signals": ["DUT_SUPPLY", "DUT_GND", "DUT_CURRENT"],
            "interfaces": [],
            "kind": "python",
        }
