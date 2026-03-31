"""Declarative device runtime built on top of the shared base device model."""

from __future__ import annotations

from pathlib import Path
from typing import Any

from .base_device_model import BaseDeviceModel
from .profile_curves import (
    apply_input_curves,
    apply_slew,
    evaluate_derived_signal,
    apply_waveform_inputs,
    evaluate_curve_points,
    evaluate_transfer_curve,
    publish_waveform_metrics,
)
from .profile_document import load_profile_document
from .profile_helpers import (
    are_sources_enabled,
    compare,
    condition_matches,
    interface_request_matches,
    lookup_numeric,
    normalize_mapping,
    read_signal_value,
)
from .profile_dm30 import handle_dm30_request, initialize_dm30_interfaces
from .profile_ict import handle_ict_request
from .profile_shrt import handle_shrt_request
from .profile_i2c import handle_i2c_transaction, initialize_i2c_interfaces
from .profile_spi import handle_spi_transaction, initialize_spi_interfaces
from .profile_state import (
    apply_state_actions,
    set_state_flags,
    timer_output_signal,
    update_state_machines,
    update_timers,
)


class DeclarativeDeviceModel(BaseDeviceModel):
    """Drive a device model from one JSON/YAML profile instead of handwritten logic."""

    def __init__(self, profile_path: str):
        """Executes __init__."""
        self.profile_path = profile_path
        self.profile = load_profile_document(profile_path)
        self.device_name = str(self.profile.get("name") or Path(profile_path).stem)

        aliases = self.profile.get("aliases") or {}
        self.signal_aliases = self._normalize_aliases(aliases.get("signals") or {})
        self.interface_aliases = self._normalize_aliases(aliases.get("interfaces") or {})

        # Signal groups define the public contract between simulator and declarative profile.
        signals = self.profile.get("signals") or {}
        self.signal_inputs = {str(item).strip().upper() for item in signals.get("inputs", [])}
        self.signal_outputs = {str(item).strip().upper() for item in signals.get("outputs", [])}
        self.signal_sources = {str(item).strip().upper() for item in signals.get("sources", [])}
        self.signal_internal = {str(item).strip().upper() for item in signals.get("internal", [])}
        self.derived_signal_definitions = self._normalize_mapping(self.profile.get("derived_signals") or {})
        self.signal_internal |= set(self.derived_signal_definitions.keys())
        self.all_signals = sorted(self.signal_inputs | self.signal_outputs | self.signal_sources | self.signal_internal)

        self.source_control = self.profile.get("source_control") or {}
        self.output_definitions = self._normalize_mapping(self.profile.get("outputs") or {})
        self.initial_inputs = self._normalize_mapping(self.profile.get("initial_inputs") or {})
        self.initial_sources = self._normalize_mapping(self.profile.get("initial_sources") or {})
        self.initial_internal = self._normalize_mapping(self.profile.get("initial_internal") or {})
        self.input_curves = self._normalize_mapping(self.profile.get("input_curves") or {})
        self.interfaces = self._normalize_mapping(self.profile.get("interfaces") or {})
        self.timer_definitions = self._normalize_mapping(self.profile.get("timers") or {})
        self.state_machine_definitions = self._normalize_mapping(self.profile.get("state_machines") or {})
        self.ctct_resistances = self._normalize_ctct_resistances(self.profile.get("ctct") or {})

        super().__init__()

    def reset(self) -> None:
        """Reset runtime state, interface history, timers and state machines to profile defaults."""
        self.now_ms = 0
        self.input_waveforms = {}
        self.waveform_captures = {}
        self.inputs: dict[str, float] = {}
        self.sources: dict[str, float] = {}
        self.internal: dict[str, float] = {}
        self.interface_state: dict[str, dict[str, Any]] = {}
        self.output_state: dict[str, dict[str, Any]] = {}
        self.timer_state: dict[str, dict[str, Any]] = {}
        self.state_machine_state: dict[str, dict[str, Any]] = {}
        self.manual_override: set[str] = set()

        for name in self.signal_inputs:
            self.inputs[name] = self._lookup_numeric(self.initial_inputs, name, 0.0)

        for name in self.signal_sources:
            self.sources[name] = self._lookup_numeric(self.initial_sources, name, 0.0)

        for name in self.signal_internal:
            self.internal[name] = self._lookup_numeric(self.initial_internal, name, 0.0)

        for name in self.interfaces:
            self.interface_state[name] = {"last_request": None, "last_response": None, "history": []}

        initialize_i2c_interfaces(self)
        initialize_spi_interfaces(self)
        initialize_dm30_interfaces(self)

        # Timers and state machines publish internal helper signals during rule evaluation.
        for name in self.timer_definitions:
            timer_signal = self._timer_output_signal(name, self.timer_definitions[name])
            self.internal.setdefault(timer_signal, 0.0)
            self.timer_state[name] = {"active_since_ms": None, "done": False}

        for name, definition in self.state_machine_definitions.items():
            initial_state = str((definition or {}).get("initial_state") or (definition or {}).get("initial") or "IDLE").strip().upper()
            self.state_machine_state[name] = {"state": initial_state}
            self._set_state_flags(name, initial_state, definition if isinstance(definition, dict) else {})

    def move_to_time(self, target_time_ms: int) -> None:
        """Advance the model and recompute waveform inputs, curves, timers and state machines."""
        super().move_to_time(target_time_ms)
        self._apply_waveform_inputs()
        self._apply_input_curves()
        self._update_timers()
        self._update_state_machines()
        self._refresh_derived_signals()

    def set_input(self, name: str, value: Any) -> None:
        """Write one simulator-side input, source or internal signal into the profile runtime."""
        signal = self._resolve_signal_name(name)
        numeric = float(value)
        if signal in self.signal_inputs:
            self.inputs[signal] = numeric
            self.manual_override.add(signal)
            self._refresh_derived_signals()
            return
        if signal in self.signal_sources:
            self.sources[signal] = numeric
            self._refresh_derived_signals()
            return
        if signal in self.signal_internal:
            self.internal[signal] = numeric
            self._refresh_derived_signals()
            return
        raise KeyError(f"Unknown input '{name}'.")

    def get_signal(self, name: str) -> float:
        """Resolve one output or internal signal from the declarative ruleset."""
        signal = self._resolve_signal_name(name)
        self._refresh_derived_signals()
        if signal in self.signal_internal:
            return float(self.internal.get(signal, 0.0))
        if signal not in self.signal_outputs:
            raise KeyError(f"Unknown signal '{name}'.")
        if not self._are_sources_enabled():
            return 0.0

        definition = self.output_definitions.get(signal) or {}
        default_value = float(definition.get("default", 0.0))
        # Output state keeps pending-delay and slew information stable across time jumps.
        state = self.output_state.setdefault(
            signal,
            {
                "current_value": default_value,
                "target_value": default_value,
                "pending_target": default_value,
                "pending_since_ms": self.now_ms,
                "last_update_ms": self.now_ms,
                "active_rule_key": "",
            },
        )

        rule_key, target_value = self._evaluate_output_target(signal, definition, default_value)
        if rule_key != state["active_rule_key"] or target_value != state["pending_target"]:
            state["active_rule_key"] = rule_key
            state["pending_target"] = target_value
            state["pending_since_ms"] = self.now_ms

        active_rule = self._find_active_rule(definition, rule_key)
        delay_ms = int(active_rule.get("delay_ms", definition.get("delay_ms", 0)) or 0) if active_rule else int(definition.get("delay_ms", 0) or 0)
        if self.now_ms - int(state["pending_since_ms"]) >= delay_ms:
            state["target_value"] = state["pending_target"]

        elapsed_ms = max(0, self.now_ms - int(state["last_update_ms"]))
        state["current_value"] = self._apply_slew(float(state["current_value"]), float(state["target_value"]), elapsed_ms, active_rule, definition)
        state["last_update_ms"] = self.now_ms
        return float(state["current_value"])

    def read_state(self) -> dict[str, Any]:
        """Return the full state snapshot exposed to the simulator UI and exports."""
        return {
            "time_ms": self.now_ms,
            "inputs": dict(sorted(self.inputs.items())),
            "sources": dict(sorted(self.sources.items())),
            "internal": dict(sorted(self.internal.items())),
            "outputs": {name: self.get_signal(name) for name in sorted(self.signal_outputs)},
            "timers": self.timer_state,
            "state_machines": self.state_machine_state,
            "waveforms": self.waveform_captures,
            "interfaces": {name: self._describe_interface_state(name, state) for name, state in sorted(self.interface_state.items())},
        }

    def state_marker(self) -> dict[str, Any]:
        """Return the lightweight state marker stored alongside protocol responses."""
        return {
            "time_ms": self.now_ms,
            "inputs": dict(sorted(self.inputs.items())),
            "sources": dict(sorted(self.sources.items())),
            "internal": dict(sorted(self.internal.items())),
        }

    def get_device_info(self) -> dict[str, Any]:
        """Return static device metadata for the simulator handshake."""
        return {
            "name": self.device_name,
            "signals": self.all_signals,
            "profile_path": self.profile_path,
            "kind": "declarative",
            "supports_waveforms": True,
            "interfaces": sorted(self.interfaces.keys()),
            "ctct": {
                "resistances": self.ctct_resistances,
            },
        }

    def set_waveform(self, name: str, waveform: dict[str, Any], options: dict[str, Any] | None = None) -> dict[str, Any]:
        """Apply one waveform and publish its derived metrics as internal helper signals."""
        signal = self._resolve_signal_name(name)
        result = super().set_waveform(signal, waveform, options)
        self._publish_waveform_metrics(signal)
        self._refresh_derived_signals()
        return result

    def send_interface(self, name: str, payload: Any) -> Any:
        """Handle one outgoing simulator interface request against declarative response rules."""
        interface_name = self._resolve_interface_name(name)
        definition = self.interfaces.get(interface_name)
        if definition is None:
            raise KeyError(f"Unknown interface '{name}'.")

        requests = definition.get("requests") or []
        state = self.interface_state.setdefault(interface_name, {"last_request": None, "last_response": None, "history": []})
        state["last_request"] = payload

        if str(definition.get("protocol") or "").strip().lower() == "i2c":
            response = handle_i2c_transaction(self, interface_name, definition, payload)
            state["last_response"] = response
            state["history"].append({"request": payload, "response": response, "time_ms": self.now_ms})
            return response

        if str(definition.get("protocol") or "").strip().lower() == "spi":
            response = handle_spi_transaction(self, interface_name, definition, payload)
            state["last_response"] = response
            state["history"].append({"request": payload, "response": response, "time_ms": self.now_ms})
            return response

        if str(definition.get("protocol") or "").strip().lower() == "dm30":
            response = handle_dm30_request(self, interface_name, definition, payload)
            state["last_response"] = response
            state["history"].append({"request": payload, "response": response, "time_ms": self.now_ms})
            return response

        if str(definition.get("protocol") or "").strip().lower() == "ict":
            response = handle_ict_request(self, interface_name, definition, payload)
            state["last_response"] = response
            state["history"].append({"request": payload, "response": response, "time_ms": self.now_ms})
            return response

        if str(definition.get("protocol") or "").strip().lower() == "shrt":
            response = handle_shrt_request(self, interface_name, definition, payload)
            state["last_response"] = response
            state["history"].append({"request": payload, "response": response, "time_ms": self.now_ms})
            return response

        for request in requests:
            if (
                isinstance(request, dict)
                and self._interface_request_matches(request.get("when") or {}, payload)
                and self._condition_matches(request.get("state_when"))
            ):
                response = request.get("response")
                state["last_response"] = response
                state["history"].append({"request": payload, "response": response, "time_ms": self.now_ms})
                return response

        default_response = definition.get("default_response")
        state["last_response"] = default_response
        state["history"].append({"request": payload, "response": default_response, "time_ms": self.now_ms})
        return default_response

    def read_interface(self, name: str) -> Any:
        """Return the latest stored response for one declarative interface."""
        interface_name = self._resolve_interface_name(name)
        if interface_name not in self.interfaces:
            raise KeyError(f"Unknown interface '{name}'.")

        return self.interface_state.get(interface_name, {}).get("last_response")

    def _evaluate_output_target(self, signal: str, definition: dict[str, Any], default_value: float) -> tuple[str, float]:
        """Resolve the active target value for one output including rule selection."""
        rules = definition.get("rules") or []
        for index, rule in enumerate(rules):
            if not isinstance(rule, dict):
                continue
            if self._condition_matches(rule.get("when") or {}):
                if "transfer_curve" in rule:
                    return f"rule:{index}", self._evaluate_transfer_curve(rule["transfer_curve"])
                return f"rule:{index}", float(rule.get("value", default_value))

        if "transfer_curve" in definition:
            return "transfer_curve", self._evaluate_transfer_curve(definition["transfer_curve"])

        return "default", default_value

    def _find_active_rule(self, definition: dict[str, Any], rule_key: str) -> dict[str, Any] | None:
        """Resolve the rule metadata that produced the current output target."""
        if not rule_key.startswith("rule:"):
            return definition if rule_key == "transfer_curve" else None

        try:
            index = int(rule_key.split(":", 1)[1])
        except ValueError:
            return None

        rules = definition.get("rules") or []
        if 0 <= index < len(rules) and isinstance(rules[index], dict):
            return rules[index]
        return None

    def _apply_slew(self, current: float, target: float, elapsed_ms: int, active_rule: dict[str, Any] | None, definition: dict[str, Any]) -> float:
        """Executes _apply_slew."""
        return apply_slew(current, target, elapsed_ms, active_rule, definition)

    def _apply_input_curves(self) -> None:
        """Executes _apply_input_curves."""
        apply_input_curves(self)

    def _apply_waveform_inputs(self) -> None:
        """Executes _apply_waveform_inputs."""
        apply_waveform_inputs(self)

    def _update_timers(self) -> None:
        """Executes _update_timers."""
        update_timers(self)

    def _update_state_machines(self) -> None:
        """Executes _update_state_machines."""
        update_state_machines(self)

    def _apply_state_actions(self, machine_name: str, state_name: str, state_config: dict[str, Any]) -> None:
        """Executes _apply_state_actions."""
        apply_state_actions(self, machine_name, state_name, state_config)

    def _set_state_flags(self, machine_name: str, active_state: str, definition: dict[str, Any]) -> None:
        """Executes _set_state_flags."""
        set_state_flags(self, machine_name, active_state, definition)

    @staticmethod
    def _timer_output_signal(name: str, definition: dict[str, Any]) -> str:
        """Executes _timer_output_signal."""
        return timer_output_signal(name, definition)

    def _evaluate_transfer_curve(self, definition: Any) -> float:
        """Executes _evaluate_transfer_curve."""
        return evaluate_transfer_curve(self, definition)

    def _evaluate_curve_points(self, points: list[Any], x_value: float, mode: str) -> float:
        """Executes _evaluate_curve_points."""
        return evaluate_curve_points(points, x_value, mode)

    def _condition_matches(self, condition: Any) -> bool:
        """Executes _condition_matches."""
        return condition_matches(self, condition)

    def _interface_request_matches(self, condition: Any, payload: Any) -> bool:
        """Executes _interface_request_matches."""
        return interface_request_matches(condition, payload)

    def _read_signal_value(self, signal: str) -> float:
        """Executes _read_signal_value."""
        return read_signal_value(self, signal)

    def _publish_waveform_metrics(self, signal: str) -> None:
        """Executes _publish_waveform_metrics."""
        publish_waveform_metrics(self, signal)

    def _are_sources_enabled(self) -> bool:
        """Executes _are_sources_enabled."""
        return are_sources_enabled(self)

    def _refresh_derived_signals(self) -> None:
        """Executes _refresh_derived_signals."""
        for signal, definition in self.derived_signal_definitions.items():
            self.internal[signal] = float(self._evaluate_derived_signal(definition))

    def _evaluate_derived_signal(self, definition: Any) -> float:
        """Executes _evaluate_derived_signal."""
        return evaluate_derived_signal(self, definition)

    def _resolve_signal_name(self, name: str) -> str:
        """Executes _resolve_signal_name."""
        signal = str(name).strip().upper()
        return self.signal_aliases.get(signal, signal)

    def _resolve_interface_name(self, name: str) -> str:
        """Executes _resolve_interface_name."""
        interface_name = str(name).strip().upper()
        return self.interface_aliases.get(interface_name, interface_name)

    def _describe_interface_state(self, interface_name: str, state: dict[str, Any]) -> dict[str, Any]:
        """Project one protocol state down to a JSON-safe summary for UI and exports."""
        summary: dict[str, Any] = {
            "last_request": state.get("last_request"),
            "last_response": state.get("last_response"),
            "history_count": len(state.get("history") or []),
        }

        i2c_state = state.get("i2c")
        if isinstance(i2c_state, dict):
            summary["i2c"] = {
                "selected_device": i2c_state.get("selected_device_name"),
                "read_mode": bool(i2c_state.get("read_mode", False)),
                "pointer_register": i2c_state.get("pointer_register"),
                "expect_pointer": bool(i2c_state.get("expect_pointer", False)),
                "devices": {
                    name: self._describe_i2c_device_state(device_state)
                    for name, device_state in (i2c_state.get("devices") or {}).items()
                    if isinstance(device_state, dict)
                },
            }

        spi_state = state.get("spi")
        if isinstance(spi_state, dict):
            devices = spi_state.get("devices") or {}
            summary["spi"] = {
                "devices": {
                    name: self._describe_spi_device_state(device_state)
                    for name, device_state in devices.items()
                    if isinstance(device_state, dict)
                }
            }

        return summary

    @staticmethod
    def _describe_i2c_device_state(device_state: dict[str, Any]) -> dict[str, Any]:
        """Render one I2C slave state with a compact register preview."""
        registers = device_state.get("registers") or {}
        preview_items = sorted(
            (
                int(address) & 0xFF,
                int(value) & 0xFF,
            )
            for address, value in registers.items()
            if isinstance(address, int)
        )[:16]
        preview = {f"0x{address:02X}": f"0x{value:02X}" for address, value in preview_items}

        return {
            "kind": device_state.get("kind"),
            "pointer_register": device_state.get("pointer_register"),
            "temperature_c": device_state.get("temperature_c"),
            "register_count": len(registers),
            "register_preview": preview,
        }

    @staticmethod
    def _describe_spi_device_state(device_state: dict[str, Any]) -> dict[str, Any]:
        """Render one SPI slave state without exposing raw bytearrays to JSON serialization."""
        memory = device_state.get("memory")
        preview = ""
        if isinstance(memory, (bytes, bytearray)):
            preview = "".join(f"{value:02X}" for value in memory[:16])

        return {
            "busy_until_ms": device_state.get("busy_until_ms"),
            "status_register": device_state.get("status_register"),
            "write_protect": device_state.get("write_protect"),
            "memory_preview_hex": preview,
            "memory_size": len(memory) if isinstance(memory, (bytes, bytearray)) else 0,
        }

    @staticmethod
    def _normalize_mapping(raw: Any) -> dict[str, Any]:
        """Normalize YAML/JSON mappings into a plain dict."""
        return normalize_mapping(raw)

    @staticmethod
    def _normalize_aliases(raw: Any) -> dict[str, str]:
        """Normalize aliases into an uppercase lookup map."""
        aliases: dict[str, str] = {}
        if not isinstance(raw, dict):
            return aliases

        for canonical_name, items in raw.items():
            canonical = str(canonical_name).strip().upper()
            if not canonical:
                continue

            aliases[canonical] = canonical
            for item in items if isinstance(items, list) else [items]:
                alias = str(item).strip().upper()
                if alias:
                    aliases[alias] = canonical

        return aliases

    @staticmethod
    def _normalize_ctct_resistances(raw: Any) -> list[dict[str, Any]]:
        """Normalize CTCT resistance/group definitions into a flat list."""
        if not isinstance(raw, dict):
            raw = {}

        entries = raw.get("resistances") or raw.get("resistance") or raw.get("ctct_resistances") or []
        if not isinstance(entries, list):
            entries = [entries]

        groups = raw.get("groups") or raw.get("group") or raw.get("bundles") or []
        if not isinstance(groups, list):
            groups = [groups]

        ring = raw.get("ring") or raw.get("loop") or None

        normalized: list[dict[str, Any]] = []
        index = 0

        def append_entry(a: str, b: str, ohms_value: float, identifier: str | None = None) -> None:
            """Executes append_entry."""
            nonlocal index
            index += 1
            normalized.append(
                {
                    "id": identifier or f"DUT_CTCT_R{index}",
                    "a": a,
                    "b": b,
                    "ohms": max(0.0, ohms_value),
                }
            )

        for entry in entries:
            if not isinstance(entry, dict):
                continue
            a = str(entry.get("a") or entry.get("from") or entry.get("src") or "").strip()
            b = str(entry.get("b") or entry.get("to") or entry.get("dst") or "").strip()
            if not a or not b:
                continue
            ohms_raw = entry.get("ohms") or entry.get("resistance") or entry.get("value")
            try:
                ohms = float(ohms_raw)
            except (TypeError, ValueError):
                continue
            identifier = str(entry.get("id") or entry.get("name") or "").strip() or None
            append_entry(a, b, ohms, identifier)

        for group in groups:
            if not isinstance(group, dict):
                continue
            ohms_raw = group.get("ohms") or group.get("resistance") or group.get("value")
            try:
                ohms = float(ohms_raw)
            except (TypeError, ValueError):
                continue
            anchor = str(group.get("a") or group.get("from") or group.get("src") or group.get("anchor") or "").strip()
            pins = group.get("pins") or group.get("targets") or group.get("nodes") or []
            if not isinstance(pins, list):
                pins = [pins]
            pins = [str(item).strip() for item in pins if str(item).strip()]
            base_id = str(group.get("id") or group.get("name") or "").strip()

            if anchor and pins:
                for offset, pin in enumerate(pins, start=1):
                    if pin == anchor:
                        continue
                    identifier = f"{base_id}_{offset}" if base_id else None
                    append_entry(anchor, pin, ohms, identifier)

            pairs = group.get("pairs") or []
            if not isinstance(pairs, list):
                pairs = [pairs]
            for offset, pair in enumerate(pairs, start=1):
                if isinstance(pair, (list, tuple)) and len(pair) >= 2:
                    a = str(pair[0]).strip()
                    b = str(pair[1]).strip()
                elif isinstance(pair, dict):
                    a = str(pair.get("a") or pair.get("from") or "").strip()
                    b = str(pair.get("b") or pair.get("to") or "").strip()
                else:
                    continue
                if not a or not b:
                    continue
                identifier = f"{base_id}_{offset}" if base_id else None
                append_entry(a, b, ohms, identifier)

        if isinstance(ring, dict):
            prefix = str(ring.get("prefix") or "").strip()
            start_raw = ring.get("start")
            end_raw = ring.get("end")
            skip_raw = ring.get("skip") or []
            try:
                start = int(start_raw)
                end = int(end_raw)
            except (TypeError, ValueError):
                start = 0
                end = -1
            try:
                ohms = float(ring.get("ohms"))
            except (TypeError, ValueError):
                ohms = None

            if start > 0 and end >= start and ohms is not None:
                skip = {int(item) for item in (skip_raw if isinstance(skip_raw, list) else [skip_raw]) if str(item).strip()}
                pins = [index for index in range(start, end + 1) if index not in skip]
                for offset, source in enumerate(pins):
                    target = pins[(offset + 1) % len(pins)]
                    append_entry(f"{prefix}{source}", f"{prefix}{target}", ohms)

        return normalized

    @staticmethod
    def _lookup_numeric(values: dict[str, Any], key: str, default: float) -> float:
        """Executes _lookup_numeric."""
        return lookup_numeric(values, key, default)

    @staticmethod
    def _compare(value: float, conditions: dict[str, Any]) -> bool:
        """Executes _compare."""
        return compare(value, conditions)
