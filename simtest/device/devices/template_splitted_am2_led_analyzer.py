from __future__ import annotations

from typing import Any

from core import BaseDeviceModel


class TemplateSplittedAm2LedAnalyzerModel(BaseDeviceModel):
    LOW_LEVEL = 0.2
    HIGH_LEVEL = 1.3
    ERROR_THRESHOLD = 0.5

    def reset(self) -> None:
        self.now_ms = 0
        self.dut_hv_voltage = 0.0
        self.gnd_voltage = 0.0
        self.wave_in_1 = 0.0
        self.wave_in_2 = 0.0
        self.wave_in_3 = 0.0
        self.wave_out_1 = 0.0
        self.wave_out_2 = 0.0
        self.wave_out_3 = 0.0
        self.last_led_response = "0,0,0,0"

    def set_input(self, name: str, value: Any) -> None:
        signal = name.strip().upper()
        numeric = float(value)

        if signal in {"DUT_HV", "DUT_HV_IN", "HV_IN", "SM4_10_0"}:
            self.dut_hv_voltage = numeric
            return

        if signal in {"GND", "SM4_34_0"}:
            self.gnd_voltage = numeric
            return

        phase_1_inputs = {"WAVE_IN", "WAVE_IN_1", "WAVE_IN_A", "ARB_IN", "ARB_IN_1", "ARB_IN_A", "AM2/1 BNC + A-IO 3", "AM2/1 AM2_ARB_3", "BNC + A-IO 3", "AM2_ARB_3"}
        phase_2_inputs = {"WAVE_IN_2", "WAVE_IN_B", "ARB_IN_2", "ARB_IN_B", "AM2/2 BNC + A-IO 3", "AM2/2 AM2_ARB_3"}
        phase_3_inputs = {"WAVE_IN_3", "WAVE_IN_C", "ARB_IN_3", "ARB_IN_C", "AM2/3 BNC + A-IO 3", "AM2/3 AM2_ARB_3"}

        if signal in phase_1_inputs:
            self.wave_in_1 = numeric
            self.wave_out_1 = numeric
            return

        if signal in phase_2_inputs:
            self.wave_in_2 = numeric
            self.wave_out_2 = numeric
            return

        if signal in phase_3_inputs:
            self.wave_in_3 = numeric
            return

        raise KeyError(f"Unknown input '{name}'.")

    def get_signal(self, name: str) -> float:
        signal = name.strip().upper()

        if signal in {"WAVE_OUT", "WAVE_OUT_1", "WAVE_OUT_A", "SCO_OUT", "SCO_OUT_1", "SCO_OUT_A", "AM2/1 BNC + A-IO 8", "AM2/1 AM2_SCO_8", "BNC + A-IO 8", "AM2_SCO_8"}:
            return self._phase_output(self.wave_in_1)
        if signal in {"WAVE_OUT_2", "WAVE_OUT_B", "SCO_OUT_2", "SCO_OUT_B", "AM2/2 BNC + A-IO 8", "AM2/2 AM2_SCO_8"}:
            return self._phase_output(self.wave_in_2)
        if signal in {"WAVE_OUT_3", "WAVE_OUT_C", "SCO_OUT_3", "SCO_OUT_C", "AM2/3 BNC + A-IO 8", "AM2/3 AM2_SCO_8"}:
            return self._phase_output(self.wave_in_3)
        if signal in {"WAVE_IN", "WAVE_IN_1", "WAVE_IN_A", "ARB_IN", "ARB_IN_1", "ARB_IN_A", "AM2/1 BNC + A-IO 3", "AM2/1 AM2_ARB_3", "BNC + A-IO 3", "AM2_ARB_3"}:
            return self.wave_in_1
        if signal in {"WAVE_IN_2", "WAVE_IN_B", "ARB_IN_2", "ARB_IN_B", "AM2/2 BNC + A-IO 3", "AM2/2 AM2_ARB_3"}:
            return self.wave_in_2
        if signal in {"WAVE_IN_3", "WAVE_IN_C", "ARB_IN_3", "ARB_IN_C", "AM2/3 BNC + A-IO 3", "AM2/3 AM2_ARB_3"}:
            return self.wave_in_3
        if signal in {"DUT_HV", "DUT_HV_IN", "HV_IN", "SM4_10_0"}:
            return self.dut_hv_voltage
        raise KeyError(f"Unknown signal '{name}'.")

    def _phase_output(self, phase_value: float) -> float:
        if self.dut_hv_voltage < 12.0:
            return self.LOW_LEVEL
        return self.HIGH_LEVEL if phase_value >= self.ERROR_THRESHOLD else self.LOW_LEVEL

    def send_interface(self, name: str, payload: Any) -> Any:
        interface_name = name.strip().upper()
        command = str(payload).strip().strip('"').strip("'")
        if interface_name != "INTERFACE LED ANALYZER":
            raise KeyError(f"Unknown interface '{name}'.")

        if command != "LSENS3,2,200?":
            self.last_led_response = "0,0,0,0"
            return self.last_led_response

        led_on = self.dut_hv_voltage >= 12.0
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
                "DUT_HV": self.dut_hv_voltage,
                "GND": self.gnd_voltage,
                "WAVE_IN_1": self.wave_in_1,
                "WAVE_IN_2": self.wave_in_2,
                "WAVE_IN_3": self.wave_in_3,
            },
            "sources": {},
            "internal": {
                "LED_ON": 1.0 if self.dut_hv_voltage >= 12.0 else 0.0,
            },
            "outputs": {
                "WAVE_OUT_1": self._phase_output(self.wave_in_1),
                "WAVE_OUT_2": self._phase_output(self.wave_in_2),
                "WAVE_OUT_3": self._phase_output(self.wave_in_3),
            },
            "interfaces": {
                "INTERFACE LED ANALYZER": self.last_led_response,
            },
        }

    def state_marker(self) -> dict[str, Any]:
        return {
            "time_ms": self.now_ms,
            "inputs": {
                "DUT_HV": self.dut_hv_voltage,
                "WAVE_IN_1": self.wave_in_1,
                "WAVE_IN_2": self.wave_in_2,
                "WAVE_IN_3": self.wave_in_3,
            },
            "outputs": {
                "WAVE_OUT_1": self._phase_output(self.wave_in_1),
                "WAVE_OUT_2": self._phase_output(self.wave_in_2),
                "WAVE_OUT_3": self._phase_output(self.wave_in_3),
            },
            "internal": {
                "LED_ON": 1.0 if self.dut_hv_voltage >= 12.0 else 0.0,
            },
        }

    def get_device_info(self) -> dict[str, Any]:
        return {
            "name": "template-splitted-am2-led-analyzer",
            "signals": ["DUT_HV", "GND", "WAVE_IN_1", "WAVE_IN_2", "WAVE_IN_3", "WAVE_OUT_1", "WAVE_OUT_2", "WAVE_OUT_3"],
            "interfaces": ["INTERFACE LED ANALYZER"],
            "kind": "python",
        }
