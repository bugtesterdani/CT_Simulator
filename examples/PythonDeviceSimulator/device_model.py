from __future__ import annotations

from dataclasses import dataclass, field


@dataclass
class DeviceModel:
    vin: float = 0.0
    en: bool = False
    mode: int = 0
    load_ma: float = 0.0
    now_ms: int = 0
    startup_begin_ms: int | None = None
    fault_code: str | None = None
    overcurrent_since_ms: int | None = None
    last_outputs: dict[str, float | int] = field(default_factory=dict)

    def reset(self) -> None:
        self.vin = 0.0
        self.en = False
        self.mode = 0
        self.load_ma = 0.0
        self.now_ms = 0
        self.startup_begin_ms = None
        self.fault_code = None
        self.overcurrent_since_ms = None
        self.last_outputs.clear()

    def tick(self, delta_ms: int) -> None:
        if delta_ms < 0:
            raise ValueError("delta_ms must be >= 0")
        self.now_ms += delta_ms
        self._update_faults()

    def move_to_time(self, target_time_ms: int) -> None:
        if target_time_ms < self.now_ms:
            raise ValueError(
                f"target_time_ms ({target_time_ms}) must be >= current model time ({self.now_ms})"
            )

        self.tick(target_time_ms - self.now_ms)

    def set_input(self, name: str, value) -> None:
        signal = name.strip().upper()
        if signal == "VIN":
            self.vin = float(value)
            if self.en and self._can_start():
                self._ensure_startup()
        elif signal == "EN":
            self.en = _to_bool(value)
            if self.en and self._can_start():
                self._ensure_startup()
            else:
                self.startup_begin_ms = None
        elif signal == "MODE":
            self.mode = int(value)
        elif signal == "LOAD_MA":
            self.load_ma = float(value)
        else:
            raise KeyError(f"Unknown input '{name}'.")

        self._update_faults()

    def get_signal(self, name: str):
        outputs = self._compute_outputs()
        key = name.strip().upper()
        if key not in outputs:
            raise KeyError(f"Unknown signal '{name}'.")
        return outputs[key]

    def read_state(self) -> dict:
        outputs = self._compute_outputs()
        return {
            "time_ms": self.now_ms,
            "inputs": {
                "VIN": self.vin,
                "EN": int(self.en),
                "MODE": self.mode,
                "LOAD_MA": self.load_ma,
            },
            "outputs": outputs,
            "fault_code": self.fault_code,
            "startup_begin_ms": self.startup_begin_ms,
        }

    def state_marker(self) -> dict:
        return {
            "fault_code": self.fault_code,
            "startup_begin_ms": self.startup_begin_ms,
            "time_ms": self.now_ms,
        }

    def _compute_outputs(self) -> dict[str, float | int]:
        self._update_faults()

        if self.fault_code is not None:
            outputs = {
                "VOUT": 0.0,
                "PGOOD": 0,
                "FAULT": 1,
                "ID0": self.mode & 1,
            }
            self.last_outputs = outputs
            return outputs

        target_vout = 5.0 if self.mode else 3.3

        if not self.en or not self._input_in_range():
            outputs = {
                "VOUT": 0.0,
                "PGOOD": 0,
                "FAULT": 0,
                "ID0": self.mode & 1,
            }
            self.last_outputs = outputs
            return outputs

        if self.startup_begin_ms is None:
            self._ensure_startup()

        elapsed = max(0, self.now_ms - (self.startup_begin_ms or self.now_ms))
        ramp_ratio = min(1.0, elapsed / 300.0)
        load_droop = min(0.6, self.load_ma / 1000.0)
        vout = max(0.0, target_vout * ramp_ratio - load_droop)
        pgood = int(elapsed >= 350 and abs(vout - target_vout) <= 0.25)

        outputs = {
            "VOUT": round(vout, 3),
            "PGOOD": pgood,
            "FAULT": 0,
            "ID0": self.mode & 1,
        }
        self.last_outputs = outputs
        return outputs

    def _can_start(self) -> bool:
        return self.fault_code is None and self._input_in_range()

    def _input_in_range(self) -> bool:
        return 6.5 <= self.vin <= 15.0

    def _ensure_startup(self) -> None:
        if self.startup_begin_ms is None:
            self.startup_begin_ms = self.now_ms

    def _update_faults(self) -> None:
        if self.vin > 16.0:
            self.fault_code = "OVERVOLTAGE"
            return

        if not self.en:
            self.overcurrent_since_ms = None
            return

        current_limit = 700.0 if self.mode else 450.0
        if self.load_ma > current_limit:
            if self.overcurrent_since_ms is None:
                self.overcurrent_since_ms = self.now_ms
            elif self.now_ms - self.overcurrent_since_ms >= 100:
                self.fault_code = "OVERCURRENT"
        else:
            self.overcurrent_since_ms = None


def _to_bool(value) -> bool:
    if isinstance(value, bool):
        return value
    if isinstance(value, (int, float)):
        return value != 0
    text = str(value).strip().lower()
    if text in {"1", "true", "on", "high", "yes"}:
        return True
    if text in {"0", "false", "off", "low", "no"}:
        return False
    raise ValueError(f"Cannot convert '{value}' to bool.")
