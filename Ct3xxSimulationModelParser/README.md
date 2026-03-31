# Ct3xxSimulationModelParser

Parser library for simulation-side YAML models that complement WireViz.

## Purpose

`WireViz` stays responsible for physical topology:

- connectors
- pins
- cables
- direct connections

`Ct3xxSimulationModelParser` adds behavioral elements on top of that topology:

- `relay`
- `resistor`
- `inductor`
- `transformer`
- `current_transformer`
- `limit`
- `assembly`
- `tester_supply`
- `tester_output`
- `switch`, `fuse`, `diode`, `load`, `voltage_divider` via generic element metadata
- `sensor`, `opto`, `transistor` via generic element metadata

This keeps the wiring model clean and lets the simulator apply electrical or logical behavior separately.

## Supported YAML shape

```yaml
elements:
  - id: DeviceBoard
    type: assembly
    wiring: board_device_wireviz.yaml
    simulation: board_device_simulation.yaml
    ports:
      IN: BoardPort.IN
      OUT: BoardPort.OUT
      GND: BoardPort.GND

  - id: K1
    type: relay
    coil:
      signal: VCC_Plus
      threshold_v: 10.0
    contacts:
      - a: K1.COM
        b: K1.NO
        mode: normally_open

  - id: R1
    type: resistor
    a: R1.A
    b: R1.B
    ohms: 1000

  - id: L1
    type: inductor
    a: L1.A
    b: L1.B
    henry: 95e-6

  - id: UEM3_1
    type: limit
    mode: voltage
    gain: 10
    max_voltage: 90
    nodes:
      - UEM3/1-1
      - UEM3/1-2
      - UEM3/1-3
```

## Assemblies

`assembly` is the key building block for hierarchical simulation.

It allows a top-level harness or adapter plan to contain reusable sub-simulations such as:

- boards
- modules
- fixtures
- device variants

The `ports` mapping binds the outer assembly connector pins to internal connector pins of the child model.

## Current runtime usage

The current simulator runtime actively evaluates:

- `relay`
- `resistor`
- `assembly`
- `transformer`
- `current_transformer`
- `limit`
- `tester_supply`
- `tester_output`
- `switch`
- `fuse`
- `diode`
- `load`
- `voltage_divider`
- `sensor`
- `opto`
- `transistor`

## Tester outputs and supplies

For test-system driven digital outputs such as `IOXX`, the simulator can now resolve `HIGH` / `LOW`
via declarative tester-side configuration instead of fixed hardcoded voltages.

Example:

```yaml
elements:
  - id: UIF_SUPPLY
    type: tester_supply
    signal: UIF_P_24V
    voltage: 24.0

  - id: UIF_OUT1_CFG
    type: tester_output
    signal: UIF_OUT1
    high_mode: supply
    high_supply: UIF_P_24V
    low_mode: open
```

Supported `tester_output` modes:

- `high_mode: supply`
- `high_mode: value`
- `high_mode: open`
- `low_mode: supply`
- `low_mode: value`
- `low_mode: open`

This is useful for outputs that are not simple `0 V / 24 V`, but switch against an external tester supply
or become electrically open in the inactive state.

## Transformer semantics

The runtime treats transformers as coupled pin pairs, not as shorts across a winding.

- `primary_a` couples to `secondary_a`
- `primary_b` couples to `secondary_b`
- `ratio` means `primary / secondary`

So with `ratio: 10.0`:

- writing `10 V` on the primary side yields `1 V` on the secondary side
- reading `1 V` on the secondary side resolves to `10 V` on the primary side

This keeps the galvanic separation intact while still allowing signal propagation through the simulation graph.

## Current transformer semantics

`current_transformer` is modeled as a sensing element and not as a direct wire between `secondary_a` and `secondary_b`.

- `primary_signal` names the simulated primary-side current signal
- `ratio` means `primary / secondary`
- when a measurement reaches the CT secondary side, the simulator can resolve it back to `primary_signal`

So with `ratio: 2000`:

- a primary current signal of `2.0 A` appears as `0.001 A` on the secondary side

## Example in this repository

See:

- `simtest/wireplan/Verdrahtung.yml`
- `simtest/wireplan/simulation.yaml`
- `simtest/wireplan/board_device_wireviz.yaml`
- `simtest/wireplan/board_device_simulation.yaml`

This example currently shows a top-level harness relay plus a `DeviceBoard` assembly with an internal resistor and DUT connector mapping.
