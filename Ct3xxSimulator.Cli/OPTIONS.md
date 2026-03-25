# Ct3xxSimulator.Cli Optionen

## CLI-Argumente

- `--program-file <pfad>`
- `--program-folder <pfad>`
- `--wiring-folder <pfad>`
- `--simulation-folder <pfad>`
- `--dut <pfad>`
- `--preset-file <pfad>`
- `--preset <name>`
- `--export <pfad>`
- `--dut-loop <n>`
- `--validate-only`
- `--help`

## Unterstuetzte DUT-Dateien

- `*.py`
- `*.json`
- `*.yaml`
- `*.yml`

## Worauf geachtet werden sollte

- bei mehreren `*.ctxprg` im Programmordner sollte `--program-file` verwendet werden
- `--preset-file` und `--preset` muessen zusammen benutzt werden
- Exportpfade koennen `.json`, `.csv` oder `.pdf` sein
