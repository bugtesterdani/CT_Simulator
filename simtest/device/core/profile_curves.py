"""Curve, slew and waveform helpers for declarative device profiles."""

from __future__ import annotations

from typing import Any


def apply_slew(current: float, target: float, elapsed_ms: int, active_rule: dict[str, Any] | None, definition: dict[str, Any]) -> float:
    """Move one output towards its target according to the configured slew rate."""
    if current == target:
        return current

    source = active_rule or definition
    slew_per_ms = source.get("slew_per_ms")
    if slew_per_ms is None:
        return target

    delta = float(slew_per_ms) * max(0, elapsed_ms)
    if delta <= 0:
        return current
    if current < target:
        return min(target, current + delta)
    return max(target, current - delta)


def evaluate_curve_points(points: list[Any], x_value: float, mode: str) -> float:
    """Evaluate a declarative point list either by hold or linear interpolation."""
    normalized: list[tuple[float, float]] = []
    for point in points:
        if isinstance(point, dict):
            x = point.get("time_ms", point.get("x"))
            y = point.get("value", point.get("y"))
        elif isinstance(point, (list, tuple)) and len(point) >= 2:
            x, y = point[0], point[1]
        else:
            continue

        normalized.append((float(x), float(y)))

    if not normalized:
        return 0.0

    normalized.sort(key=lambda item: item[0])
    if x_value <= normalized[0][0]:
        return normalized[0][1]
    if x_value >= normalized[-1][0]:
        return normalized[-1][1]

    previous = normalized[0]
    for current in normalized[1:]:
        if x_value <= current[0]:
            if mode == "hold":
                return previous[1]

            span = current[0] - previous[0]
            if span == 0:
                return current[1]
            ratio = (x_value - previous[0]) / span
            return previous[1] + ratio * (current[1] - previous[1])
        previous = current

    return normalized[-1][1]


def evaluate_transfer_curve(model: Any, definition: Any) -> float:
    """Evaluate one signal-driven transfer curve from the active model state."""
    if not isinstance(definition, dict):
        return 0.0

    signal_name = str(definition.get("input") or definition.get("signal") or "").strip().upper()
    if not signal_name:
        return 0.0

    input_value = model._read_signal_value(signal_name)
    points = definition.get("points") or []
    mode = str(definition.get("mode") or definition.get("interpolate") or "linear").strip().lower()
    return evaluate_curve_points(points, input_value, mode)


def apply_input_curves(model: Any) -> None:
    """Apply time-based declarative input curves to non-overridden signals."""
    for signal_name, raw_definition in model.input_curves.items():
        signal = signal_name.strip().upper()
        # Manual writes from the simulator take precedence over scripted input curves.
        if signal in model.manual_override:
            continue

        definition = raw_definition if isinstance(raw_definition, dict) else {"points": raw_definition}
        points = definition.get("points") or []
        if not points:
            continue

        mode = str(definition.get("mode") or "hold").strip().lower()
        value = evaluate_curve_points(points, model.now_ms, mode)
        if signal in model.signal_inputs:
            model.inputs[signal] = value
        elif signal in model.signal_internal:
            model.internal[signal] = value


def apply_waveform_inputs(model: Any) -> None:
    """Refresh all currently active waveform-driven inputs for the current time."""
    for signal in list(model.input_waveforms.keys()):
        model._apply_waveform_signal(signal)
        publish_waveform_metrics(model, signal)


def publish_waveform_metrics(model: Any, signal: str) -> None:
    """Publish derived waveform metrics as internal signals for profile rules."""
    waveform = model.input_waveforms.get(signal)
    if waveform is None:
        return

    metrics = waveform.get("metrics") or {}
    prefix = f"WF_{signal.strip().upper()}"
    model.internal[f"{prefix}_CURRENT"] = float(model._waveform_value_at(waveform, model.now_ms))
    model.internal[f"{prefix}_PEAK"] = float(metrics.get("peak", 0.0))
    model.internal[f"{prefix}_RMS"] = float(metrics.get("rms", 0.0))
    model.internal[f"{prefix}_AVG"] = float(metrics.get("average", 0.0))
    model.internal[f"{prefix}_MIN"] = float(metrics.get("min", 0.0))
    model.internal[f"{prefix}_MAX"] = float(metrics.get("max", 0.0))
    for shape in ("dc", "square", "sine_like", "custom"):
        model.internal[f"{prefix}_IS_{shape.upper()}"] = 1.0 if str(metrics.get("shape")) == shape else 0.0


def evaluate_derived_signal(model: Any, definition: Any) -> float:
    """Evaluate one derived internal signal definition against the current runtime state."""
    if not isinstance(definition, dict):
        return float(definition or 0.0)

    compare_definition = definition.get("compare")
    if isinstance(compare_definition, dict):
        return _evaluate_compare_signal(model, compare_definition)

    dominant_definition = definition.get("dominant_signal_window")
    if isinstance(dominant_definition, dict):
        return _evaluate_dominant_signal_window(model, dominant_definition)

    if "value" in definition:
        return float(definition.get("value") or 0.0)

    return 0.0


def _evaluate_compare_signal(model: Any, definition: dict[str, Any]) -> float:
    """Evaluate a simple derived compare signal and return its true/false value."""
    signal_name = str(definition.get("signal") or definition.get("input") or "").strip().upper()
    if not signal_name:
        return float(definition.get("false_value", 0.0) or 0.0)

    value = model._read_signal_value(signal_name)
    true_value = float(definition.get("true_value", 1.0) or 1.0)
    false_value = float(definition.get("false_value", 0.0) or 0.0)
    return true_value if model._compare(value, definition) else false_value


def _evaluate_dominant_signal_window(model: Any, definition: dict[str, Any]) -> float:
    """Return a derived boolean-like signal for one dominant waveform over a time window."""
    signal_name = str(definition.get("signal") or "").strip().upper()
    peer_names = [str(item).strip().upper() for item in definition.get("peers") or [] if str(item).strip()]
    if not signal_name or not peer_names:
        return float(definition.get("false_value", 0.0) or 0.0)

    window_ms = max(0.0, float(definition.get("window_ms", 0.0) or 0.0))
    min_metric = float(definition.get("min_metric", 0.0) or 0.0)
    min_delta = float(definition.get("min_delta", 0.0) or 0.0)
    metric_mode = str(definition.get("metric") or "peak_or_rms_abs").strip().lower()
    true_value = float(definition.get("true_value", 1.0) or 1.0)
    false_value = float(definition.get("false_value", 0.0) or 0.0)

    sample_times = _build_window_sample_times(model.now_ms, window_ms)
    signal_metric = _signal_window_metric(model, signal_name, sample_times, metric_mode)
    if signal_metric < min_metric:
        return false_value

    for peer_name in peer_names:
        peer_metric = _signal_window_metric(model, peer_name, sample_times, metric_mode)
        if signal_metric <= peer_metric + min_delta:
            return false_value

    return true_value


def _build_window_sample_times(now_ms: int, window_ms: float) -> list[int]:
    """Create deterministic integer sample times for one recent evaluation window."""
    if window_ms <= 0:
        return [int(now_ms)]

    start_time_ms = max(0, int(now_ms - window_ms))
    end_time_ms = int(now_ms)
    if end_time_ms <= start_time_ms:
        return [end_time_ms]

    sample_times = list(range(start_time_ms, end_time_ms + 1))
    if sample_times[-1] != end_time_ms:
        sample_times.append(end_time_ms)
    return sample_times


def _signal_window_metric(model: Any, signal: str, sample_times: list[int], metric_mode: str) -> float:
    """Calculate one configured window metric for a signal over sampled times."""
    samples = [abs(_read_signal_value_at(model, signal, sample_time)) for sample_time in sample_times]
    if not samples:
        return 0.0

    peak = max(samples)
    rms = (sum(sample * sample for sample in samples) / len(samples)) ** 0.5
    average = sum(samples) / len(samples)

    if metric_mode == "peak_abs":
        return peak
    if metric_mode == "rms_abs":
        return rms
    if metric_mode == "avg_abs":
        return average
    return max(peak, rms)


def _read_signal_value_at(model: Any, signal: str, absolute_time_ms: int) -> float:
    """Read one signal at a specific absolute time without mutating the live runtime."""
    waveform = model.input_waveforms.get(signal)
    if waveform is not None:
        return float(model._waveform_value_at(waveform, absolute_time_ms))

    values = getattr(model, "inputs", None)
    if isinstance(values, dict) and signal in values:
        return float(values.get(signal, 0.0))

    values = getattr(model, "sources", None)
    if isinstance(values, dict) and signal in values:
        return float(values.get(signal, 0.0))

    values = getattr(model, "internal", None)
    if isinstance(values, dict) and signal in values:
        return float(values.get(signal, 0.0))

    return 0.0
