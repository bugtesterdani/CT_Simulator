# Ct3xxSimulator.Cli

Konsolenprojekt fuer Batch-Simulationen ohne WPF.

## Zweck

- reproduzierbare Regressionstests
- Einsatz in CI oder Skripten
- gleicher Simulationskern wie in der Desktop-App

## Unterstuetzte Eingaben

- direkte Pfade fuer Programm, Verdrahtung, Simulation und DUT
- optional Laden eines Presets aus einer Desktop-Szenario-JSON

## Beispiele

Direkte Pfade:

```powershell
dotnet run --project Ct3xxSimulator.Cli -- `
  --program-folder .\simtest\ct3xx `
  --wiring-folder .\simtest\wireplan `
  --simulation-folder .\simtest\wireplan `
  --dut .\simtest\device\devices\IKI_good.json
```

Mit Export:

```powershell
dotnet run --project Ct3xxSimulator.Cli -- `
  --program-folder .\simtest\ct3xx `
  --wiring-folder .\simtest\wireplan `
  --simulation-folder .\simtest\wireplan `
  --dut .\simtest\device\devices\IKI_good.json `
  --export .\artifacts\simtest.json
```

Mit Preset-Datei:

```powershell
dotnet run --project Ct3xxSimulator.Cli -- `
  --preset-file "$env:LocalAppData\Ct3xxSimulatorDesktop\scenarios.json" `
  --preset "Simtest Good"
```

Nur validieren:

```powershell
dotnet run --project Ct3xxSimulator.Cli -- `
  --program-folder .\simtest\ct3xx `
  --wiring-folder .\simtest\wireplan `
  --simulation-folder .\simtest\wireplan `
  --dut .\simtest\device\devices\IKI_good.json `
  --validate-only
```

## Exit-Codes

- `0`
  alle Schritte `PASS` oder reine Validierung erfolgreich
- `1`
  mindestens ein Schritt `FAIL`
- `2`
  Validierungsfehler oder Laufzeitfehler
