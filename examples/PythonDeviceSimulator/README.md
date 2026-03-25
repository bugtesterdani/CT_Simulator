# Python Device Simulator Example

This example project shows how to simulate a DUT in Python and communicate with the CT3xx simulator through a Windows named pipe.

The example is intentionally simple but stateful:

- it reacts to previous commands
- it models timing-dependent behavior
- it supports analog and digital signals
- it uses newline-delimited JSON messages over a named pipe

## Why named pipes

For a parallel-running Python DUT simulator on Windows, named pipes are a practical IPC mechanism:

- easy to connect from .NET with `NamedPipeClientStream`
- local-only communication
- no TCP port management
- state can remain inside the Python process while the simulator sends commands/events

## Files

- `device_simulator.py`
  Main pipe server and DUT simulation loop.

- `device_model.py`
  Example stateful DUT model.

- `pipe_protocol.py`
  JSON framing and pipe helpers.

- `mock_simulator_client.py`
  Small client script for local testing without changing the CT3xx simulator.

- `requirements.txt`
  Python dependency list.

- `wireviz_example.yaml`
  Example WireViz file that matches the Python DUT signal names and CT3xx role conventions.

## Requirements

- Windows
- Python 3.11+
- `pywin32`

Install:

```powershell
cd examples\PythonDeviceSimulator
py -3 -m pip install -r requirements.txt
```

## Start the device simulator

```powershell
cd examples\PythonDeviceSimulator
py -3 device_simulator.py
```

Default pipe name:

```text
\\.\pipe\ct3xx-device-sim-example
```

You can override it:

```powershell
py -3 device_simulator.py --pipe \\.\pipe\my-ct3xx-device
```

The configurable options and supported pipe actions are summarized in [OPTIONS.md](C:/Users/hello/Desktop/CT3xx/examples/PythonDeviceSimulator/OPTIONS.md).

## Test with the mock client

In a second terminal:

```powershell
cd examples\PythonDeviceSimulator
py -3 mock_simulator_client.py
```

## WireViz example

An example WireViz harness file is included as:

`wireviz_example.yaml`

It follows the CT3xx naming conventions already used in the simulator integration:

- `CT3`
  test system side, uppercase, white/normal background

- `HARNESS_A`
  harness side, uppercase, yellow background

- `DevicePort`
  DUT side, mixed case, yellow background

The pin labels are intentionally identical to the Python signal names:
No longer on the test-system side.

The test-system connector now uses the canonical Signaltable naming:

- `Module_Channel_Board`

Example:

- `SC3_27_1`

That means:

- module name: `SC3`
- first number: channel `27`
- third value: board `1`

That makes the C# simulator able to bridge
`CT3xx SignalTable -> WireViz -> Python device simulator`
without an extra alias table.

## Protocol

The pipe protocol is newline-delimited JSON.

Each request is a single JSON object followed by `\n`.
Each response is also a single JSON object followed by `\n`.

Time is a first-class part of the protocol.
The CT3xx simulator can provide the current runtime in milliseconds since the beginning of the run.
The Python device simulator then evaluates the request at exactly that simulated time.

### Request format

```json
{
  "id": "req-1",
  "action": "set_input",
  "sim_time_ms": 1250,
  "name": "VIN",
  "value": 12.0
}
```

`sim_time_ms` is optional.
If provided, it means:

- "process this request at runtime 1250 ms since test start"

If omitted, the Python simulator keeps using its own local time model.

### Response format

```json
{
  "id": "req-1",
  "ok": true,
  "sim_time_ms": 1250,
  "state_at_request": {
    "fault_code": null,
    "startup_begin_ms": null
  },
  "result": {
    "accepted": true
  }
}
```

### Error response

```json
{
  "id": "req-1",
  "ok": false,
  "sim_time_ms": 1250,
  "state_at_request": {
    "fault_code": "OVERCURRENT",
    "startup_begin_ms": 800
  },
  "error": {
    "code": "unknown_action",
    "message": "Unsupported action 'foo'."
  }
}
```

## Supported actions

- `hello`
  Health check and protocol info.

- `set_input`
  Set a simulator input such as `VIN`, `EN`, `MODE`, `LOAD_MA`.

- `get_signal`
  Read a DUT signal such as `VOUT`, `PGOOD`, `ID0`, `FAULT`.

- `read_state`
  Return a snapshot of the entire simulated device state.

- `tick`
  Advance simulated time explicitly in milliseconds.
  Useful for deterministic tests.

- `reset`
  Reset the device state.

- `shutdown`
  Stop the Python simulator process.

## Example DUT behavior

This sample models a small power board:

- Inputs:
  - `VIN` supply voltage
  - `EN` enable signal
  - `MODE` output selection (`0` => 3.3 V, `1` => 5.0 V)
  - `LOAD_MA` output load

- Outputs:
  - `VOUT`
  - `PGOOD`
  - `ID0`
  - `FAULT`

### State rules

- If `VIN` is too low, output stays off.
- If `VIN` is too high, an overvoltage fault is latched.
- When `EN=1` and the input range is valid, the output ramps up after a startup delay.
- `MODE` changes the target output voltage.
- High load can trigger a latched overcurrent fault.
- `PGOOD` only goes high after the output is stable.

This is the important pattern for CT3xx integration:
the Python process keeps state and evolves based on previous commands and time.

## Suggested CT3xx integration pattern

When you wire this into the CT3xx simulator later, keep the responsibilities separate:

1. CT3xx simulator detects a relevant event.
2. Simulator sends a pipe command to Python.
3. Python updates the DUT state.
4. Simulator asks Python for resulting signals or measurements.

Examples:

- Set `VIN` when the tester applies a supply.
- Set `EN` when a digital output is asserted.
- Read `PGOOD` when the CT3xx test expects a digital input.
- Read `VOUT` when the CT3xx test performs a voltage measurement.
- Pass `sim_time_ms` from the CT3xx simulator so the Python side evaluates each action at the correct runtime position.

## Naming recommendations

Keep the Python signal names simple and stable.

Recommended style:

- Analog inputs: `VIN`, `LOAD_MA`
- Digital inputs: `EN`, `MODE`
- Analog outputs: `VOUT`
- Digital outputs: `PGOOD`, `FAULT`, `ID0`

If you later map these to WireViz or the CT3xx signal table, prefer a single canonical name per signal.

## Notes for the C# side

This example uses plain UTF-8 JSON messages over the pipe so the .NET side can talk to it without Python-specific serialization.

On the C# side, the simplest setup is:

- `NamedPipeClientStream`
- `StreamReader` / `StreamWriter`
- one JSON object per line

The current CT3xx simulator integration uses the environment variable:

```powershell
$env:CT3XX_PY_DEVICE_PIPE="\\.\pipe\ct3xx-device-sim-example"
```

If this variable is set, the simulator will:

- connect to the Python device simulator
- pass `sim_time_ms` on every request
- use WireViz-resolved target pin labels as Python signal names

## Limitations

- Single client connection at a time
- Windows-only example
- No authentication
- No binary payloads
- No subscription or streaming updates yet

## Recommended Timing Contract

For the C# side, the cleanest contract is:

1. CT3xx simulator tracks runtime since program start.
2. Every pipe request includes `sim_time_ms`.
3. Python advances its model to that exact time before handling the action.
4. Every response echoes:
   - `sim_time_ms`
   - `state_at_request`

This makes it easy to debug timing-sensitive DUT behavior.

If you want, the next step can be a matching C# pipe client inside `Ct3xxSimulator`.
