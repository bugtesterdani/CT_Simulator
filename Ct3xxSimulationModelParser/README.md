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
- `transformer`
- `current_transformer`
- `assembly`

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

The following types are already parsed and available for future runtime semantics:

- `transformer`
- `current_transformer`

## Example in this repository

See:

- `simtest/wireplan/Verdrahtung.yml`
- `simtest/wireplan/simulation.yaml`
- `simtest/wireplan/board_device_wireviz.yaml`
- `simtest/wireplan/board_device_simulation.yaml`

This example shows a top-level `DeviceBoard` assembly that contains a relay and a resistor inside a child wiring/simulation model.
