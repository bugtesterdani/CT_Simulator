"""Timer and state-machine helpers for declarative device profiles."""

from __future__ import annotations

from typing import Any

from .profile_helpers import condition_matches, normalize_mapping


def update_timers(model: Any) -> None:
    """Advance all declarative timers to the current simulation time."""
    for name, definition in model.timer_definitions.items():
        if not isinstance(definition, dict):
            continue

        timer_signal = timer_output_signal(name, definition)
        state = model.timer_state.setdefault(name, {"active_since_ms": None, "done": False})
        delay_ms = int(definition.get("delay_ms", 0) or 0)
        condition = definition.get("when")
        active = condition_matches(model, condition)

        if active:
            if state["active_since_ms"] is None:
                state["active_since_ms"] = model.now_ms
            elapsed = model.now_ms - int(state["active_since_ms"])
            done = elapsed >= delay_ms
            state["done"] = done
            model.internal[timer_signal] = 1.0 if done else 0.0
        else:
            reset_when_false = bool(definition.get("reset_when_false", True))
            if reset_when_false:
                state["active_since_ms"] = None
                state["done"] = False
                model.internal[timer_signal] = 0.0


def update_state_machines(model: Any) -> None:
    """Advance all declarative state machines by evaluating their active state transitions."""
    for name, definition in model.state_machine_definitions.items():
        if not isinstance(definition, dict):
            continue

        state_definition = model.state_machine_state.setdefault(
            name,
            {"state": str(definition.get("initial_state") or definition.get("initial") or "IDLE").strip().upper()},
        )
        current_state = str(state_definition.get("state") or "IDLE").strip().upper()
        states = definition.get("states") or {}
        state_config = states.get(current_state) if isinstance(states, dict) else None
        if not isinstance(state_config, dict):
            continue

        apply_state_actions(model, name, current_state, state_config)
        transitions = state_config.get("transitions") or []
        for transition in transitions:
            if not isinstance(transition, dict):
                continue
            if condition_matches(model, transition.get("when")):
                next_state = str(transition.get("to") or "").strip().upper()
                if next_state:
                    state_definition["state"] = next_state
                    next_config = states.get(next_state) if isinstance(states, dict) else {}
                    set_state_flags(model, name, next_state, next_config if isinstance(next_config, dict) else {})
                break


def apply_state_actions(model: Any, machine_name: str, state_name: str, state_config: dict[str, Any]) -> None:
    """Apply the immediate side effects declared for one active state."""
    for signal_name, raw_value in normalize_mapping(state_config.get("set_internal") or {}).items():
        model.internal[signal_name] = float(raw_value)


def set_state_flags(model: Any, machine_name: str, active_state: str, definition: dict[str, Any]) -> None:
    """Expose one-hot internal state flags for the current machine state."""
    states = definition.get("states") or {}
    if not isinstance(states, dict):
        return

    prefix = machine_name.strip().upper()
    for state_name in states.keys():
        flag = f"STATE_{prefix}_{str(state_name).strip().upper()}"
        model.internal[flag] = 1.0 if str(state_name).strip().upper() == active_state else 0.0


def timer_output_signal(name: str, definition: dict[str, Any]) -> str:
    """Resolve the internal output signal used for one declarative timer."""
    configured = str(definition.get("output_signal") or "").strip().upper()
    return configured or f"TIMER_{name.strip().upper()}"
