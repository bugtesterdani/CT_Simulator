CT3xx Visual Simulator & Parser Toolkit
======================================

Dieses Repository enthaelt mehrere zusammenarbeitende Projekte rund um CT3xx-Testprogramme, Verdrahtung und Simulation.

- `Ct3xxSimulator.Desktop`
  WPF-Oberflaeche zum Auswaehlen von Testprogramm, Verdrahtung, Simulationsmodell und Python-Geraeteskript.
- `Ct3xxSimulator`
  Kern-Simulatorlogik fuer Testschritte, WireViz-Aufloesung und Python-Geraeteanbindung.
- `Ct3xxProgramParser`
  Parser fuer `.ctxprg` und referenzierte CT3xx-Dateien wie Signaltabellen.
- `Ct3xxWireVizParser`
  Parser fuer standardkonformes WireViz-YAML.
- `Ct3xxSimulationModelParser`
  Parser fuer `simulation.yaml` mit Verhaltensmodellen wie Relais, Widerstand und verschachtelten Baugruppen.

Repositorystruktur
------------------

```text
CT3xx/
|- Ct3xxProgramParser/
|- Ct3xxSimulationModelParser/
|- Ct3xxWireVizParser/
|- Ct3xxSimulator/
|- Ct3xxSimulator.Desktop/
|- Ct3xxProgramParser.Tests/
|- Ct3xxWireVizParser.Tests/
|- Ct3xxSimulator.WinAppDriverTests/
|- examples/
|- simtest/
`- testprogramme/
```

Build
-----

```powershell
dotnet build Ct3xxSimulator.Desktop\Ct3xxSimulator.Desktop.csproj
```

Desktop-App
-----------

```powershell
dotnet run --project Ct3xxSimulator.Desktop
```

In der App koennen explizit ausgewaehlt werden:

- Testprogramm-Ordner
- Verdrahtungs-Ordner
- Simulationsmodell-Ordner
- Python-Skript fuer das zu simulierende Geraet

Liegt im Testprogramm-Ordner genau eine `.ctxprg`, wird sie automatisch verwendet.
Bei mehreren `.ctxprg` fragt die App nach der gewuenschten Datei.

Simulation: Verdrahtung und Verhalten
-------------------------------------

Die Simulation trennt jetzt bewusst zwischen zwei Ebenen:

1. `WireViz`
   Beschreibt nur die physische Verdrahtung:
   - Stecker
   - Pins
   - Kabel
   - direkte Verbindungen

2. `simulation.yaml`
   Beschreibt das Verhalten von Elementen:
   - `relay`
   - `resistor`
   - `transformer`
   - `current_transformer`
   - `assembly`

Beispiel:

```yaml
elements:
  - id: DeviceBoard
    type: assembly
    wiring: board_device_wireviz.yaml
    simulation: board_device_simulation.yaml
    ports:
      VPLUS: BoardPort.VPLUS
      GND: BoardPort.GND
      IN: BoardPort.IN
      OUT: BoardPort.OUT
```

`assembly` erlaubt verschachtelte Sub-Simulationen. Damit koennen Platinen, Module und Baugruppen als eigene Untermodelle beschrieben und rekursiv in die Gesamtverdrahtung eingeblendet werden.

Vollstaendiges Beispiel
-----------------------

Das hierarchische Beispiel liegt unter:

- `simtest/wireplan/Verdrahtung.yml`
- `simtest/wireplan/simulation.yaml`
- `simtest/wireplan/board_device_wireviz.yaml`
- `simtest/wireplan/board_device_simulation.yaml`
- `simtest/device/device_39.py`

Dieses Beispiel zeigt:

- Hauptverdrahtung mit `DeviceBoard` als Baugruppe
- Sub-WireViz fuer die interne Platinenverdrahtung
- Sub-Simulationsmodell fuer Relais und Widerstand
- Python-Geraetesimulation fuer verschiedene DUT-Szenarien

Wichtige Umgebungsvariablen
---------------------------

- `CT3XX_TESTPROGRAM_ROOT`
  Optionaler Standardpfad fuer Testprogramme.
- `CT3XX_WIREVIZ_ROOT`
  Verdrahtungsordner fuer die Simulation.
- `CT3XX_SIMULATION_MODEL_ROOT`
  Ordner mit `simulation.yaml`.
- `CT3XX_PY_DEVICE_PIPE`
  Pipe zur Python-Geraetesimulation.
- `CT3XX_PYTHON_EXE`
  Optionaler Python-Interpreter fuer das Starten des Geraeteskripts.
- `CT3XX_APP_PATH`
  Pfad zur WPF-Anwendung fuer UI-Tests.

Hinweise
--------

- `WireViz` wird nicht um projektspezifische Sonderfelder erweitert.
- Simulationsverhalten gehoert in `simulation.yaml`.
- Neue Elementtypen koennen ueber `Ct3xxSimulationModelParser` und die Simulatorlogik schrittweise ergaenzt werden.
