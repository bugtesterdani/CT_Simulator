CT3xx Visual Simulator & Parser Toolkit
======================================

Dieses Repository enthaelt Parser, Simulationslogik und eine WPF-Oberflaeche fuer CT3xx-Testprogramme, Verdrahtung und DUT-Simulation.

## Projekte

- `Ct3xxSimulator.Desktop`
  WPF-App zum Auswaehlen, Starten und Auswerten von Simulationen.
- `Ct3xxSimulator`
  Simulationskern fuer Testablauf, WireViz-Aufloesung, DUT-Anbindung und Auswertung.
- `Ct3xxSimulation.Abstractions`
  Gemeinsame DTOs und Interfaces fuer Simulation, UI und weitere Frontends.
- `Ct3xxSimulator.Export`
  Entkoppelte Ergebnis- und Diagrammexporte fuer `PDF`, `JSON` und `CSV`.
- `Ct3xxSimulator.Validation`
  Eigenstaendige Konfigurations- und Modellvalidierung fuer WireViz und `simulation.yaml`.
- `Ct3xxProgramParser`
  Parser fuer `.ctxprg` und referenzierte CT3xx-Dateien wie `ctsit`, `ctarb` oder `ctict`.
- `Ct3xxWireVizParser`
  Parser fuer standardkonformes WireViz-YAML.
- `Ct3xxSimulationModelParser`
  Parser fuer `simulation.yaml` mit Verhaltensmodellen.
- `Ct3xxAltiumWireVizExporter`
  Exporter von Altium-Konnektivitaetsdaten nach WireViz.

## Struktur

```text
CT3xx/
|- Ct3xxAltiumWireVizExporter/
|- Ct3xxProgramParser/
|- Ct3xxProgramParser.Tests/
|- Ct3xxSimulation.Abstractions/
|- Ct3xxSimulationModelParser/
|- Ct3xxSimulator/
|- Ct3xxSimulator.Export/
|- Ct3xxSimulator.Validation/
|- Ct3xxSimulator.Desktop/
|- Ct3xxSimulator.WinAppDriverTests/
|- Ct3xxWireVizParser/
|- Ct3xxWireVizParser.Tests/
|- examples/
|- simtest/
|- simtest_transformer/
`- testprogramme/
```

## Build

```powershell
dotnet build CT3xx.sln
```

Die Projektmappe liegt in [CT3xx.sln](C:/Users/hello/Desktop/CT3xx/CT3xx.sln).
Die priorisierte Weiterentwicklungsplanung liegt in [ROADMAP.md](C:/Users/hello/Desktop/CT3xx/ROADMAP.md).

## Paketverwaltung und Security

- Zentrale NuGet-Versionen liegen in [Directory.Packages.props](C:/Users/hello/Desktop/CT3xx/Directory.Packages.props)
- NuGet-Audit ist global aktiviert in [Directory.Build.props](C:/Users/hello/Desktop/CT3xx/Directory.Build.props)
- Lokaler Security-Scan:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\security-scan.ps1
```

- CI-Workflow fuer Restore, Build sowie Vulnerability- und Outdated-Scan:
  [security-scan.yml](C:/Users/hello/Desktop/CT3xx/.github/workflows/security-scan.yml)

## Desktop-App

```powershell
dotnet run --project Ct3xxSimulator.Desktop
```

Die App unterstuetzt aktuell:

- Auswahl von Testprogramm-Ordner, Verdrahtungs-Ordner, Simulationsmodell-Ordner und DUT-Modell
- DUT-Modelle als `.py`, `.json`, `.yaml` oder `.yml`
- Szenario-Presets
- Validierung der Konfiguration
- Simulation komplett oder im Einzelschrittmodus mit `Weiter`, `Zurueck`, `Auto` und `Pause`
- Ergebnisexport als `PDF`, `JSON` oder `CSV`
- Detailfenster fuer Schrittergebnisse mit Verbindungsgraph und Messkurven
- optionales Live-Zustandsfenster fuer Signale, DUT-Zustaende, Relais, Faults und Zeitverlauf

Liegt im Testprogramm-Ordner genau eine `.ctxprg`, wird sie automatisch verwendet. Bei mehreren Programmen kann die Datei explizit ausgewaehlt werden.

## Simulationsmodell

Die Simulation trennt sauber zwischen:

1. `WireViz`
   Nur physische Topologie:
   - Stecker
   - Pins
   - Kabel
   - Verbindungen

2. `simulation.yaml`
   Verhaltens- und Bauteilmodell:
   - `relay`
   - `resistor`
   - `transformer`
   - `current_transformer`
   - `assembly`
   - weitere Laufzeittypen wie `switch`, `fuse`, `diode`, `load`, `voltage_divider`

`assembly` erlaubt verschachtelte Unterverdrahtungen und Untermodelle, z. B. fuer Platinen oder Baugruppen.

## DUT-Modelle

Unter `simtest/device` gibt es zwei Wege fuer DUT-Simulationen:

- Python-Modelle
- deklarative JSON-/YAML-Profile

Deklarative Profile unterstuetzen unter anderem:

- mehrere Inputs, Outputs, Sources und Internal-Signale
- Kennlinien
- zeitliches Verhalten
- Timer und einfache Zustandsautomaten
- Schnittstellenantworten
- Waveform-Stimuli und Response-Captures

## Waveform- und AM2-Unterstuetzung

Der Simulator verarbeitet jetzt explizit `2ARB`-/Waveform-Tests:

- `.ctarb`-Dateien werden geladen und in ein gemeinsames Kurvenmodell ueberfuehrt
- Signalformen werden ueber die Named Pipe an das DUT weitergereicht
- das DUT kann sofort auf angelegte Signalformen reagieren
- optional koennen waehrend des Stimulus andere Signale beobachtet und als Response-Kurve zurueckgegeben werden
- deklarative YAML-Profile koennen auf Kenngroessen wie `RMS`, `Peak`, `Average` und erkannte Formtypen reagieren

## Fault-Injection

Die Simulation unterstuetzt derzeit einfache Fault-Typen ueber `faults.json`:

- `force_signal`
- `force_relay`
- `open_connection`
- `blow_fuse`
- `short_connection`
- `signal_drift`
- `contact_problem`
- `wrong_resistance`

## Beispiele

- `simtest/`
  Hauptbeispiel fuer hierarchische Verdrahtung, Simulation und DUT-Profile
- `simtest_transformer/`
  End-to-End-Beispiel fuer Transformator und Stromwandler
- `examples/PythonDeviceSimulator/`
  einfaches Python-Pipe-Beispiel
- `examples/altium-wireviz/`
  Beispiel fuer den Altium-WireViz-Export

## Wichtige Umgebungsvariablen

- `CT3XX_TESTPROGRAM_ROOT`
  Standardpfad fuer Testprogramme
- `CT3XX_WIREVIZ_ROOT`
  Verdrahtungsordner
- `CT3XX_SIMULATION_MODEL_ROOT`
  Ordner mit `simulation.yaml`
- `CT3XX_PY_DEVICE_PIPE`
  Named Pipe fuer die DUT-Simulation
- `CT3XX_PYTHON_EXE`
  optionaler Python-Interpreter fuer den DUT-Start
- `CT3XX_APP_PATH`
  Pfad zur WPF-App fuer UI-Tests

## Hinweise

- `WireViz` bleibt frei von projektspezifischen Sonderfeldern.
- Bauteilverhalten gehoert in `simulation.yaml`.
- Neue DUT- oder Bauteilmodelle sollten ueber die Parser- und Simulationsschicht erweitert werden, nicht durch Hardcoding in der UI.
