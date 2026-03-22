# Ct3xxWireVizParser

Parser library for WireViz YAML input files.

The parser keeps the whole YAML document available as a navigable value tree and adds convenience accessors for common WireViz sections such as:

- `metadata`
- `options`
- `tweak`
- `connectors`
- `cables`
- `bundles`
- `connections`
- `additional_bom_items`

Unknown or future WireViz options are preserved instead of being discarded.

## CT3xx Naming Conventions

For CT3xx integration, the WireViz data is interpreted not only structurally, but also semantically.
Connector names and background colors are used to derive the connector role inside the simulator.

### Connector Roles

The current role mapping is:

- `TestSystem`
  Rule: connector designator is written in uppercase and has no yellow background.
  Typical use: CT3xx tester side, test system sockets, fixture-side system interfaces.

- `Harness`
  Rule: connector designator is written in uppercase and has a yellow background.
  Typical use: highlighted harness connector or adapter connector that should be treated as the preferred destination side.

- `Device`
  Rule: connector designator is not all-uppercase and has a yellow background.
  Typical use: DUT-side connector, board connector, device header, or highlighted field connector.

- `Unknown`
  Rule: anything that does not match the rules above.
  Typical use: intermediate/helper objects, connectors without CT3xx-specific naming, or incomplete data.

### What "uppercase" means

A connector is treated as uppercase when all alphabetic characters in its designator are uppercase.
Examples:

- `CT3`
- `X1`
- `HARNESS_A`

These are not treated as uppercase:

- `DevicePort`
- `Jdut1`
- `BoardConn`

### What counts as yellow

The parser currently treats the following values as yellow/emphasized:

- `YE`
- `YEL`
- `YELLOW`
- `#FFFF00`
- `#FF0`

This is read from `bgcolor` first, and from `color` as fallback.

## Recommended Naming

### Test system connectors

Use uppercase designators and no yellow background.

Recommended examples:

- `CT3`
- `TESTSYS`
- `FIXTURE`
- `X_CT3`

Example:

```yaml
connectors:
  CT3:
    bgcolor: WH
    pinlabels: [SC3_27_1, SC3_28_1, SC3_29_1]
```

### Harness or adapter connectors

Use uppercase designators and yellow background.

Recommended examples:

- `HARNESS_A`
- `ADAPTER_OUT`
- `X1`
- `PANEL_IF`

Example:

```yaml
connectors:
  HARNESS_A:
    bgcolor: YE
    pinlabels: [MISO_A, MOSI_A, GND]
```

### Device-side connectors

Use normal mixed-case names and yellow background.

Recommended examples:

- `DevicePort`
- `MainBoardJ1`
- `SensorHeader`
- `DutConnA`

Example:

```yaml
connectors:
  DevicePort:
    bgcolor: YE
    pinlabels: [MISO_PIN, MOSI_PIN, GND]
```

## Signal Mapping Rules

The CT3xx simulator currently resolves a signal assignment against WireViz in this order:

1. It reads all `SignalTable` assignments from the CT3xx program.
2. It first searches the canonical signaltable name in the format `Module_Channel_Board`, for example `SC3_27_1`.
3. It matches this primarily against the WireViz `pinlabels` on the test-system side.
4. It also supports legacy fallback via the raw signaltable name and direct `Connector.Pin`.
5. If multiple matching sources exist, `TestSystem` connectors are preferred.
6. If multiple connected targets exist, `Harness` and `Device` endpoints are preferred over `Unknown`.

This means:

- the tester-side WireViz `pinlabels` should use the canonical signaltable form
- the canonical form is `Module_Channel_Board`
- example: `SC3_27_1`
- connected DUT or harness pins can use their own local names

## Recommended Authoring Pattern

Use the canonical signaltable naming on the test-system side.
Use physical or DUT-specific names on the other side.

Example:

```yaml
connectors:
  CT3:
    bgcolor: WH
    pinlabels: [SC3_27_1, SC3_28_1, SC3_29_1]

  HARNESS_A:
    bgcolor: YE
    pinlabels: [A1.1, A1.2, A1.3]

  DevicePort:
    bgcolor: YE
    pinlabels: [J5.7, J5.8, J5.2]

connections:
  -
    - CT3: [1, 2, 3]
    - HARNESS_A: [1, 2, 3]
  -
    - HARNESS_A: [1, 2, 3]
    - DevicePort: [1, 2, 3]
```

With this structure, a signaltable entry like module `SC3`, channel `27`, board `1` resolves via the canonical name `SC3_27_1` from the test system side into the connected harness/device path.

## Things To Watch

- Keep the canonical signaltable names stable.
  If the signaltable entry is module `SC3`, channel `27`, board `1`, then prefer exactly `SC3_27_1` in the WireViz test-system `pinlabels`.

- Do not rely only on visual styling.
  The simulator uses naming and background color together, not just the rendered image.

- Avoid ambiguous uppercase naming.
  If everything is uppercase, the parser cannot distinguish test system from harness except by background color.

- Prefer explicit `bgcolor`.
  Do not rely on inherited defaults if the connector role matters for simulation.

- Keep one semantic meaning per connector.
  A connector should be clearly tester-side, harness-side, or device-side.

## Current Limitations

- Role detection is convention-based, not a native WireViz feature.
- Yellow detection currently recognizes only a limited set of yellow values.
- Matching is currently based on the canonical signaltable name, then legacy signal/testpoint names, then direct `Connector.Pin` references.
- If your project needs additional aliases or role markers, extend the role detection in code rather than relying on inconsistent naming.
