from __future__ import annotations
from typing import Any
from core import BaseDeviceModel, to_bool

signal_inputs = {"ADC_IN"}
signal_outputs = {"DIG_OUT"}
signal_source = {"VCC_PLUS"}

signals = list(signal_inputs | signal_outputs | signal_source)

class Device39Model(BaseDeviceModel):
    """Simple single-file DUT model used for basic digital threshold behavior."""

    def reset(self) -> None:
        """Reset the model to its power-up defaults."""
        self.now_ms = 0
        self.vccplus_source_voltage = False
        self.adc_in_1 = 0.0

    def set_input(self, name: str, value: Any) -> None:
        """Apply a tester-driven input or supply signal to the model."""
        signal = name.strip().upper()
        if signal in signal_inputs:
            if signal == "ADC_IN":
                self.adc_in_1 = float(value)
            else:
                raise KeyError(f"Not implemented signal '{name}'.")
            return
        if signal in signal_source:
            self.vccplus_source_voltage = (float(value) >= 19.5)
            return
        raise KeyError(f"Unknown input '{name}'.")

    def get_signal(self, name: str) -> float:
        """Return one DUT output signal based on the current inputs/supply."""
        signal = name.strip().upper()
        if signal not in signal_outputs:
            raise KeyError(f"Unknown signal '{name}'.")
        if not self.vccplus_source_voltage: # Alle Ausgänge sind auf 0V, wenn keine Hilfsversorgung eingeschalten ist.
            return 0.0
        if signal == "DIG_OUT":
            return 3.3 if self.adc_in_1 >= 3.2 else 0.0 # Wenn der Input Eingang auf 3.2V oder höher eingestellt ist, ist der Ausgang auf 3.3V eingestellt. Ansonsten 0V
        
        raise KeyError(f"Not implemented signal '{name}'.")

    def read_state(self) -> dict[str, Any]:
        """Return a full diagnostic snapshot for UI display."""
        return {
            "time_ms": self.now_ms,
            "hilfsversorgung": self.vccplus_source_voltage,
            "ADC1_IN": self.adc_in_1,
            "Digital_OUT_1": self.get_signal("DIG_OUT"),
        }

    def state_marker(self) -> dict[str, Any]:
        """Return a minimal state marker stored with protocol responses."""
        return {
            "time_ms": self.now_ms,
            "ADC1_IN": self.adc_in_1,
            "Digital_OUT_1": self.get_signal("DIG_OUT"),
        }

    def get_device_info(self) -> dict[str, Any]:
        """Describe the device capabilities for the simulator handshake."""
        return {
            "name": "simtest-device-39",
            "signals": signals,
            "ctct": {
                "resistances": self.get_ctct_resistances(),
            },
        }
