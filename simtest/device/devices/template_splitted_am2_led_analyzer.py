from __future__ import annotations

from typing import Any

from core import BaseDeviceModel


class TemplateSplittedAm2LedAnalyzerModel(BaseDeviceModel):
    """Three-phase LED analyzer DUT used by AM2 waveform reference tests."""
    LOW_LEVEL = 0.2
    HIGH_LEVEL = 1.3
    ERROR_THRESHOLD = 0.2
    DOMINANCE_WINDOW_MS = 40
    DOMINANCE_EPSILON = 0.01

    def reset(self) -> None:
        """Reset the model to its power-up defaults."""
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
        """Apply tester-driven inputs and update phase tracking."""
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
            self.wave_out_3 = numeric
            return

        raise KeyError(f"Unknown input '{name}'.")

    def get_signal(self, name: str) -> float:
        """Resolve one DUT signal for waveform outputs or monitoring."""
        signal = name.strip().upper()

        if signal in {"WAVE_OUT", "WAVE_OUT_1", "WAVE_OUT_A", "SCO_OUT", "SCO_OUT_1", "SCO_OUT_A", "AM2/1 BNC + A-IO 8", "AM2/1 AM2_SCO_8", "BNC + A-IO 8", "AM2_SCO_8"}:
            return self._phase_output(1)
        if signal in {"WAVE_OUT_2", "WAVE_OUT_B", "SCO_OUT_2", "SCO_OUT_B", "AM2/2 BNC + A-IO 8", "AM2/2 AM2_SCO_8"}:
            return self._phase_output(2)
        if signal in {"WAVE_OUT_3", "WAVE_OUT_C", "SCO_OUT_3", "SCO_OUT_C", "AM2/3 BNC + A-IO 8", "AM2/3 AM2_SCO_8"}:
            return self._phase_output(3)
        if signal in {"WAVE_IN", "WAVE_IN_1", "WAVE_IN_A", "ARB_IN", "ARB_IN_1", "ARB_IN_A", "AM2/1 BNC + A-IO 3", "AM2/1 AM2_ARB_3", "BNC + A-IO 3", "AM2_ARB_3"}:
            return self.wave_in_1
        if signal in {"WAVE_IN_2", "WAVE_IN_B", "ARB_IN_2", "ARB_IN_B", "AM2/2 BNC + A-IO 3", "AM2/2 AM2_ARB_3"}:
            return self.wave_in_2
        if signal in {"WAVE_IN_3", "WAVE_IN_C", "ARB_IN_3", "ARB_IN_C", "AM2/3 BNC + A-IO 3", "AM2/3 AM2_ARB_3"}:
            return self.wave_in_3
        if signal in {"DUT_HV", "DUT_HV_IN", "HV_IN", "SM4_10_0"}:
            return self.dut_hv_voltage
        raise KeyError(f"Unknown signal '{name}'.")

    def _phase_output(self, phase_index: int) -> float:
        """Compute the output level for the requested phase."""
        if self.dut_hv_voltage < 12.0:
            return 0.0

        detected_phase = self._detect_fault_phase()
        return self.HIGH_LEVEL if detected_phase == phase_index else self.LOW_LEVEL

    def _phase_has_dominant_overvoltage(self, phase_index: int) -> bool:
        """Return true when the given phase is dominant in the evaluation window."""
        return self._detect_fault_phase() == phase_index

    def _detect_fault_phase(self) -> int | None:
        """Identify the dominant phase within the evaluation window."""
        window_start = max(0, self.now_ms - self.DOMINANCE_WINDOW_MS)
        sample_times = self._build_sample_times(window_start, self.now_ms)
        if not sample_times:
            sample_times = [self.now_ms]

        metrics = {
            phase_index: self._phase_window_metric(phase_index, sample_times)
            for phase_index in (1, 2, 3)
        }
        dominant_phase = max(metrics, key=metrics.get)
        dominant_metric = metrics[dominant_phase]
        other_metrics = [value for phase, value in metrics.items() if phase != dominant_phase]
        if dominant_metric < self.ERROR_THRESHOLD:
            return None

        if all(dominant_metric > other_metric + self.DOMINANCE_EPSILON for other_metric in other_metrics):
            return dominant_phase

        return None

    def _build_sample_times(self, start_time_ms: int, end_time_ms: int) -> list[int]:
        """Return the integer sample times used for dominance evaluation."""
        if end_time_ms <= start_time_ms:
            return [end_time_ms]

        sample_times = list(range(start_time_ms, end_time_ms + 1))
        if sample_times[-1] != end_time_ms:
            sample_times.append(end_time_ms)
        return sample_times

    def _phase_value_at(self, phase_index: int, sample_time_ms: int) -> float:
        """Resolve the phase input value at the specified time."""
        signal = f"WAVE_IN_{phase_index}"
        waveform = self.input_waveforms.get(signal)
        if waveform is not None:
            return float(self._waveform_value_at(waveform, sample_time_ms))

        if phase_index == 1:
            return self.wave_in_1
        if phase_index == 2:
            return self.wave_in_2
        return self.wave_in_3

    def _phase_window_metric(self, phase_index: int, sample_times: list[int]) -> float:
        """Compute a peak/RMS metric used for dominance detection."""
        samples = [abs(self._phase_value_at(phase_index, sample_time)) for sample_time in sample_times]
        if not samples:
            return 0.0

        peak = max(samples)
        rms = (sum(sample * sample for sample in samples) / len(samples)) ** 0.5
        return max(peak, rms)

    def send_interface(self, name: str, payload: Any) -> Any:
        """Handle LED analyzer interface commands."""
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
        """Return the last LED analyzer response."""
        interface_name = name.strip().upper()
        if interface_name != "INTERFACE LED ANALYZER":
            raise KeyError(f"Unknown interface '{name}'.")
        return self.last_led_response

    def read_state(self) -> dict[str, Any]:
        """Return a full diagnostic snapshot for UI display."""
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
                "L1_OVERVOLTAGE": 1.0 if self._phase_has_dominant_overvoltage(1) else 0.0,
                "L2_OVERVOLTAGE": 1.0 if self._phase_has_dominant_overvoltage(2) else 0.0,
                "L3_OVERVOLTAGE": 1.0 if self._phase_has_dominant_overvoltage(3) else 0.0,
            },
            "outputs": {
                "WAVE_OUT_1": self._phase_output(1),
                "WAVE_OUT_2": self._phase_output(2),
                "WAVE_OUT_3": self._phase_output(3),
            },
            "interfaces": {
                "INTERFACE LED ANALYZER": self.last_led_response,
            },
        }

    def state_marker(self) -> dict[str, Any]:
        """Return a minimal state marker stored with protocol responses."""
        return {
            "time_ms": self.now_ms,
            "inputs": {
                "DUT_HV": self.dut_hv_voltage,
                "WAVE_IN_1": self.wave_in_1,
                "WAVE_IN_2": self.wave_in_2,
                "WAVE_IN_3": self.wave_in_3,
            },
            "outputs": {
                "WAVE_OUT_1": self._phase_output(1),
                "WAVE_OUT_2": self._phase_output(2),
                "WAVE_OUT_3": self._phase_output(3),
            },
            "internal": {
                "LED_ON": 1.0 if self.dut_hv_voltage >= 12.0 else 0.0,
                "L1_OVERVOLTAGE": 1.0 if self._phase_has_dominant_overvoltage(1) else 0.0,
                "L2_OVERVOLTAGE": 1.0 if self._phase_has_dominant_overvoltage(2) else 0.0,
                "L3_OVERVOLTAGE": 1.0 if self._phase_has_dominant_overvoltage(3) else 0.0,
            },
        }

    def get_device_info(self) -> dict[str, Any]:
        """Describe the device capabilities for the simulator handshake."""
        return {
            "name": "template-splitted-am2-led-analyzer",
            "signals": ["DUT_HV", "GND", "WAVE_IN_1", "WAVE_IN_2", "WAVE_IN_3", "WAVE_OUT_1", "WAVE_OUT_2", "WAVE_OUT_3"],
            "interfaces": ["INTERFACE LED ANALYZER"],
            "kind": "python",
            "ctct": {
                "resistances": self.get_ctct_resistances(),
            },
        }
