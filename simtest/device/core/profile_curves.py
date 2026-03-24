from __future__ import annotations

from typing import Any


def apply_slew(current: float, target: float, elapsed_ms: int, active_rule: dict[str, Any] | None, definition: dict[str, Any]) -> float:
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
    for signal_name, raw_definition in model.input_curves.items():
        signal = signal_name.strip().upper()
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
    for signal in list(model.input_waveforms.keys()):
        model._apply_waveform_signal(signal)
        publish_waveform_metrics(model, signal)


def publish_waveform_metrics(model: Any, signal: str) -> None:
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
