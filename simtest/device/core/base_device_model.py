"""Abstract base runtime shared by all Python device models."""

from __future__ import annotations

import copy
import math
from abc import ABC, abstractmethod
from typing import Any


class BaseDeviceModel(ABC):
    """Abstrakte Basis-Klasse für alle Device-Models."""

    def __init__(self) -> None:
        self.now_ms = 0
        self.input_waveforms: dict[str, dict[str, Any]] = {}
        self.waveform_captures: dict[str, dict[str, Any]] = {}
        self.reset()

    def move_to_time(self, target_time_ms: int) -> None:
        """Advance the device model time without allowing time travel backwards."""
        if target_time_ms < self.now_ms:
            raise ValueError("target_time_ms must be >= current model time")
        self.now_ms = target_time_ms

    def set_waveform(self, name: str, waveform: dict[str, Any], options: dict[str, Any] | None = None) -> dict[str, Any]:
        """Store one waveform stimulus and optionally capture related response waveforms."""
        signal = name.strip().upper()
        normalized = self._normalize_waveform(signal, waveform)
        self.input_waveforms[signal] = normalized
        self.waveform_captures[signal] = {
            "signal": signal,
            "waveform": normalized,
            "captures": {},
        }

        self._apply_waveform_signal(signal)
        captures = self._capture_requested_waveforms(signal, normalized, options or {})
        if captures:
            self.waveform_captures[signal]["captures"] = captures

        return {
            "accepted": True,
            "signal": signal,
            "waveform": self._describe_waveform(normalized),
            "observed": self._observe_signals(options or {}),
            "captures": captures,
        }

    def read_waveform(self, name: str, options: dict[str, Any] | None = None) -> dict[str, Any]:
        """Read back one stored waveform or one captured response waveform."""
        signal = name.strip().upper()

        if signal in self.waveform_captures:
            return self.waveform_captures[signal]

        for capture in self.waveform_captures.values():
            captures = capture.get("captures") or {}
            if signal in captures:
                return captures[signal]

        if signal in self.input_waveforms:
            return {
                "signal": signal,
                "waveform": self.input_waveforms[signal],
            }

        raise KeyError(f"Unknown waveform '{name}'.")

    @abstractmethod
    def reset(self) -> None:
        pass

    @abstractmethod
    def set_input(self, name: str, value: Any) -> None:
        pass

    @abstractmethod
    def get_signal(self, name: str) -> float:
        pass

    @abstractmethod
    def read_state(self) -> dict[str, Any]:
        pass

    @abstractmethod
    def state_marker(self) -> dict[str, Any]:
        pass

    @abstractmethod
    def get_device_info(self) -> dict[str, Any]:
        pass

    def get_ctct_resistances(self) -> list[dict[str, Any]]:
        """Return optional CTCT resistance definitions for DUT-side contact tests."""
        return []

    def _normalize_waveform(self, signal: str, waveform: dict[str, Any]) -> dict[str, Any]:
        """Normalize incoming waveform payloads to one internal representation."""
        points = []
        for raw_point in waveform.get("points") or []:
            if not isinstance(raw_point, dict):
                continue
            points.append({
                "time_ms": float(raw_point.get("time_ms", 0.0)),
                "value": float(raw_point.get("value", 0.0)),
            })

        points.sort(key=lambda item: item["time_ms"])
        sample_time_ms = float(waveform.get("sample_time_ms", 0.0) or 0.0)
        delay_ms = float(waveform.get("delay_ms", 0.0) or 0.0)
        cycles = int(waveform.get("cycles", 1) or 1)
        periodic = bool(waveform.get("periodic", False))
        metadata = waveform.get("metadata") if isinstance(waveform.get("metadata"), dict) else {}

        return {
            "signal": signal,
            "name": str(waveform.get("name") or signal),
            "sample_time_ms": sample_time_ms,
            "delay_ms": delay_ms,
            "periodic": periodic,
            "cycles": cycles if cycles > 0 else 1,
            "points": points,
            "metadata": metadata,
            "metrics": self._analyze_waveform(points, sample_time_ms, delay_ms, periodic, cycles),
            "start_time_ms": int(self.now_ms),
        }

    def _analyze_waveform(
        self,
        points: list[dict[str, float]],
        sample_time_ms: float,
        delay_ms: float,
        periodic: bool,
        cycles: int,
    ) -> dict[str, Any]:
        """Calculate stable derived metrics once when a waveform is loaded."""
        values = [point["value"] for point in points]
        if not values:
            values = [0.0]

        peak = max(abs(value) for value in values)
        average = sum(values) / len(values)
        rms = math.sqrt(sum(value * value for value in values) / len(values))
        min_value = min(values)
        max_value = max(values)
        duration_ms = delay_ms
        if len(points) > 1:
            duration_ms += max(points[-1]["time_ms"] - points[0]["time_ms"], sample_time_ms * max(len(points) - 1, 1)) * max(cycles, 1)

        return {
            "sample_count": len(points),
            "peak": peak,
            "average": average,
            "rms": rms,
            "min": min_value,
            "max": max_value,
            "duration_ms": duration_ms,
            "shape": self._classify_waveform(values),
            "periodic": periodic,
            "cycles": cycles,
        }

    def _classify_waveform(self, values: list[float]) -> str:
        """Assign a coarse waveform shape label for declarative rules and diagnostics."""
        if len(values) < 3:
            return "dc"

        if max(values) - min(values) < 1e-9:
            return "dc"

        if len({round(value, 9) for value in values}) <= 3:
            return "square"

        sign_changes = 0
        previous_delta = None
        for index in range(1, len(values)):
            delta = values[index] - values[index - 1]
            if previous_delta is not None and ((previous_delta < 0 < delta) or (previous_delta > 0 > delta)):
                sign_changes += 1
            previous_delta = delta

        return "sine_like" if sign_changes >= 4 else "custom"

    def _waveform_value_at(self, waveform: dict[str, Any], absolute_time_ms: int | float) -> float:
        """Evaluate a waveform at one absolute simulation time."""
        points = waveform.get("points") or []
        if not points:
            return 0.0

        start_time_ms = float(waveform.get("start_time_ms", 0.0))
        delay_ms = float(waveform.get("delay_ms", 0.0))
        relative_ms = float(absolute_time_ms) - start_time_ms
        if relative_ms <= delay_ms:
            return float(points[0]["value"])

        effective_ms = relative_ms - delay_ms
        cycle_duration_ms = 0.0
        if len(points) > 1:
            cycle_duration_ms = max(
                float(points[-1]["time_ms"]) - float(points[0]["time_ms"]),
                float(waveform.get("sample_time_ms", 0.0)) * max(len(points) - 1, 1),
            )

        if waveform.get("periodic") and cycle_duration_ms > 0:
            effective_ms %= cycle_duration_ms
        elif cycle_duration_ms > 0:
            effective_ms = min(effective_ms, cycle_duration_ms)

        if effective_ms <= float(points[0]["time_ms"]):
            return float(points[0]["value"])

        for index in range(1, len(points)):
            previous = points[index - 1]
            current = points[index]
            previous_time = float(previous["time_ms"])
            current_time = float(current["time_ms"])
            if effective_ms > current_time:
                continue
            span = current_time - previous_time
            if span <= 0:
                return float(current["value"])
            ratio = (effective_ms - previous_time) / span
            return float(previous["value"]) + ((float(current["value"]) - float(previous["value"])) * ratio)

        return float(points[-1]["value"])

    def _apply_waveform_signal(self, signal: str) -> None:
        """Push the current waveform value into the model as if it were an external input."""
        waveform = self.input_waveforms.get(signal)
        if waveform is None:
            return

        value = self._waveform_value_at(waveform, self.now_ms)
        try:
            self.set_input(signal, value)
        except KeyError:
            pass

    def _observe_signals(self, options: dict[str, Any]) -> dict[str, float]:
        """Collect immediate scalar observations requested with the waveform command."""
        result: dict[str, float] = {}
        for item in options.get("observe_signals") or []:
            signal = str(item).strip().upper()
            if not signal:
                continue
            result[signal] = float(self.get_signal(signal))
        return result

    def _capture_requested_waveforms(self, source_signal: str, waveform: dict[str, Any], options: dict[str, Any]) -> dict[str, Any]:
        """Capture response curves by replaying the current runtime at sampled time points."""
        capture_signals = [str(item).strip().upper() for item in options.get("capture_signals") or [] if str(item).strip()]
        if not capture_signals:
            return {}

        sample_count = max(2, int(options.get("capture_sample_count", 64) or 64))
        capture_start_ms = int(self.now_ms)
        end_time_ms = int(capture_start_ms + max(float(waveform.get("metrics", {}).get("duration_ms", 0.0)), float(options.get("capture_duration_ms", 0.0) or 0.0)))
        if end_time_ms <= capture_start_ms:
            end_time_ms = capture_start_ms + 1

        captures: dict[str, Any] = {}
        for signal in capture_signals:
            # Each capture runs against a temporary snapshot so the live model state is restored afterwards.
            snapshot = self._snapshot_runtime_state()
            try:
                points: list[dict[str, float]] = []
                for index in range(sample_count):
                    absolute_time_ms = int(capture_start_ms + ((end_time_ms - capture_start_ms) * index / (sample_count - 1)))
                    self.move_to_time(absolute_time_ms)
                    for input_signal in list(self.input_waveforms.keys()):
                        self._apply_waveform_signal(input_signal)
                    value = self._read_signal_for_capture(signal)
                    points.append({
                        "time_ms": float(absolute_time_ms - capture_start_ms),
                        "value": value,
                    })

                captures[signal] = {
                    "signal": signal,
                    "source_waveform": source_signal,
                    "points": points,
                    "metrics": self._analyze_waveform(points, 0.0, 0.0, False, 1),
                }
            finally:
                self._restore_runtime_state(snapshot)

        return captures

    def _snapshot_runtime_state(self) -> dict[str, Any]:
        """Create a deep snapshot of the runtime so temporary captures can be rolled back."""
        return {
            "now_ms": self.now_ms,
            "state": copy.deepcopy(self.__dict__),
        }

    def _restore_runtime_state(self, snapshot: dict[str, Any]) -> None:
        """Restore a previously captured runtime snapshot."""
        self.__dict__.clear()
        self.__dict__.update(snapshot["state"])
        self.now_ms = int(snapshot["now_ms"])

    def _read_signal_for_capture(self, signal: str) -> float:
        """Read one signal during capture without requiring the full external protocol path."""
        for field_name in ("inputs", "sources", "internal"):
            values = getattr(self, field_name, None)
            if isinstance(values, dict) and signal in values:
                return float(values.get(signal, 0.0))
        try:
            return float(self.get_signal(signal))
        except KeyError:
            return 0.0

    def _describe_waveform(self, waveform: dict[str, Any]) -> dict[str, Any]:
        """Return the compact waveform description exposed over the pipe protocol."""
        return {
            "signal": waveform.get("signal"),
            "name": waveform.get("name"),
            "sample_time_ms": waveform.get("sample_time_ms"),
            "delay_ms": waveform.get("delay_ms"),
            "periodic": waveform.get("periodic"),
            "cycles": waveform.get("cycles"),
            "metrics": waveform.get("metrics"),
        }
