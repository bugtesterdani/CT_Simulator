# Repository Optionen

Diese Datei sammelt die wichtigsten einstellbaren Stellen auf Repository-Ebene und verweist auf die tieferen `OPTIONS.md`-Dateien in den Unterordnern.

## Wichtige Umgebungsvariablen

- `CT3XX_TESTPROGRAM_ROOT`
  Standardordner fuer CT3xx-Testprogramme
- `CT3XX_WIREVIZ_ROOT`
  Ordner fuer `Verdrahtung.yml` und weitere WireViz-Dateien
- `CT3XX_SIMULATION_MODEL_ROOT`
  Ordner fuer `simulation.yaml`
- `CT3XX_PY_DEVICE_PIPE`
  Pipe-Name fuer die Python-DUT-Simulation
- `CT3XX_PYTHON_EXE`
  optionaler Python-Interpreter fuer DUT-Starts
- `CT3XX_APP_PATH`
  Pfad zur WPF-App fuer UI-Tests
- `WINAPPDRIVER_URL`
  URL fuer WinAppDriver-Tests

## Build und Test

Empfohlene Standardbefehle:

```powershell
dotnet build CT3xx.sln
dotnet test Ct3xxSimulator.Tests\Ct3xxSimulator.Tests.csproj
```

## Security und Paketpflege

- zentrale Paketversionen: `Directory.Packages.props`
- NuGet-Audit: `Directory.Build.props`
- Security-Scan:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\security-scan.ps1
```

## Detaillierte Options-Dateien

- [Ct3xxSimulator.Desktop/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/Ct3xxSimulator.Desktop/OPTIONS.md)
- [Ct3xxSimulator.Cli/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/Ct3xxSimulator.Cli/OPTIONS.md)
- [Ct3xxSimulator/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/Ct3xxSimulator/OPTIONS.md)
- [Ct3xxSimulationModelParser/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/Ct3xxSimulationModelParser/OPTIONS.md)
- [Ct3xxSimulator.Export/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/Ct3xxSimulator.Export/OPTIONS.md)
- [Ct3xxSimulator.Validation/README.md](C:/Users/hello/Desktop/CT3xx/Ct3xxSimulator.Validation/README.md)
- [Ct3xxAltiumWireVizExporter/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/Ct3xxAltiumWireVizExporter/OPTIONS.md)
- [examples/PythonDeviceSimulator/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/examples/PythonDeviceSimulator/OPTIONS.md)
- [simtest/ct3xx/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/simtest/ct3xx/OPTIONS.md)
- [simtest/wireplan/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/simtest/wireplan/OPTIONS.md)
- [simtest/device/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/simtest/device/OPTIONS.md)
- [simtest_template_sm2/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/simtest_template_sm2/OPTIONS.md)
- [simtest_template_sm2/ct3xx/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/simtest_template_sm2/ct3xx/OPTIONS.md)
- [simtest_template_sm2/wireplan/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/simtest_template_sm2/wireplan/OPTIONS.md)
- [simtest_template_sm2/device/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/simtest_template_sm2/device/OPTIONS.md)
- [simtest_transformer/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/simtest_transformer/OPTIONS.md)
- [simtest_transformer/ct3xx/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/simtest_transformer/ct3xx/OPTIONS.md)
- [simtest_transformer/wireplan/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/simtest_transformer/wireplan/OPTIONS.md)
- [simtest_transformer/device/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/simtest_transformer/device/OPTIONS.md)
