from __future__ import annotations

from typing import Any

from core import BaseDeviceModel


class TemplateSm2LedAnalyzerModel(BaseDeviceModel):
    def reset(self) -> None:
        self.now_ms = 0
        self.rel_hv_voltage = 0.0
        self.gnd_voltage = 0.0
        self.last_led_response = "0,0,0,0"

    def set_input(self, name: str, value: Any) -> None:
        signal = name.strip().upper()
        numeric = float(value)
        if signal in {"REL_HV", "REL_HV_IN", "HV_IN", "SM4_5_0"}:
            self.rel_hv_voltage = numeric
            return
        if signal == "GND":
            self.gnd_voltage = numeric
            return
        raise KeyError(f"Unknown input '{name}'.")

    def get_signal(self, name: str) -> float:
        raise KeyError(f"Unknown signal '{name}'.")

    def send_interface(self, name: str, payload: Any) -> Any:
        interface_name = name.strip().upper()
        command = str(payload).strip().strip('"').strip("'")
        if interface_name != "INTERFACE LED ANALYZER":
            raise KeyError(f"Unknown interface '{name}'.")

        if command != "LSENS3,2,200?":
            self.last_led_response = "0,0,0,0"
            return self.last_led_response

        led_on = self.rel_hv_voltage >= 12.0
        self.last_led_response = "15,1,25,40" if led_on else "0,0,0,0"
        return self.last_led_response

    def read_interface(self, name: str) -> Any:
        interface_name = name.strip().upper()
        if interface_name != "INTERFACE LED ANALYZER":
            raise KeyError(f"Unknown interface '{name}'.")
        return self.last_led_response

    def read_state(self) -> dict[str, Any]:
        return {
            "time_ms": self.now_ms,
            "inputs": {
                "REL_HV": self.rel_hv_voltage,
                "GND": self.gnd_voltage,
            },
            "sources": {},
            "internal": {
                "LED_ON": 1.0 if self.rel_hv_voltage >= 12.0 else 0.0,
            },
            "outputs": {},
            "interfaces": {
                "INTERFACE LED ANALYZER": self.last_led_response,
            },
        }

    def state_marker(self) -> dict[str, Any]:
        return {
            "time_ms": self.now_ms,
            "inputs": {
                "REL_HV": self.rel_hv_voltage,
                "GND": self.gnd_voltage,
            },
            "internal": {
                "LED_ON": 1.0 if self.rel_hv_voltage >= 12.0 else 0.0,
            },
        }

    def get_device_info(self) -> dict[str, Any]:
        return {
            "name": "template-sm2-led-analyzer",
            "signals": ["REL_HV", "GND"],
            "interfaces": ["INTERFACE LED ANALYZER"],
            "kind": "python",
        }
