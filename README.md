CT3xx Visual Simulator & Parser Toolkit
======================================

Dieses Repository enthaelt Parser, Simulationslogik und eine WPF-Oberflaeche fuer CT3xx-Testprogramme, Verdrahtung und DUT-Simulation.

## Projekte

- `Ct3xxSimulator.Desktop`
  WPF-App zum Auswaehlen, Starten und Auswerten von Simulationen.
- `Ct3xxSimulator.Cli`
  Konsolenprojekt fuer Batch-Simulationen und CI-nahe Laeufe.
- `Ct3xxSimulator`
  Simulationskern fuer Testablauf, WireViz-Aufloesung, DUT-Anbindung und Auswertung.
- `Ct3xxSimulator.Interfaces`
  Schnittstellen- und DUT-Anbindung fuer Python-/Interface-Kommunikation.
- `Ct3xxSimulator.Modules`
  Verdrahtungs-, Fault- und modulnahe Runtime-Bausteine.
- `Ct3xxSimulator.Waveforms`
  Gemeinsame Waveform- und Recorder-Bausteine.
- `Ct3xxSimulator.TestTypes`
  vorbereitete Heimat fuer testtypspezifische Handler und kuenftige Dispatcher-Struktur.
- `Ct3xxSimulation.Abstractions`
  Gemeinsame DTOs und Interfaces fuer Simulation, UI und weitere Frontends.
- `Ct3xxSimulator.Export`
  Entkoppelte Ergebnis- und Diagrammexporte fuer `PDF`, `JSON` und `CSV`.
- `Ct3xxSimulator.Validation`
  Eigenstaendige Konfigurations- und Modellvalidierung fuer WireViz und `simulation.yaml`.
- `Ct3xxSimulator.Tests`
  Regressionstests fuer Simulationskern, Signalauflosung und End-to-End-Beispiele.
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
|- Ct3xxSimulator.Cli/
|- Ct3xxSimulator.Interfaces/
|- Ct3xxSimulator.Modules/
|- Ct3xxSimulator/
|- Ct3xxSimulator.Export/
|- Ct3xxSimulator.TestTypes/
|- Ct3xxSimulator.Tests/
|- Ct3xxSimulator.Validation/
|- Ct3xxSimulator.Desktop/
|- Ct3xxSimulator.WinAppDriverTests/
|- Ct3xxSimulator.Waveforms/
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

```powershell
dotnet test Ct3xxSimulator.Tests\Ct3xxSimulator.Tests.csproj
```

Die Projektmappe liegt in [CT3xx.sln](C:/Users/hello/Desktop/CT3xx/CT3xx.sln).
Die priorisierte Weiterentwicklungsplanung liegt in [ROADMAP.md](C:/Users/hello/Desktop/CT3xx/ROADMAP.md).
Format- und Konfigurationshinweise liegen gesammelt in [OPTIONS.md](C:/Users/hello/Desktop/CT3xx/OPTIONS.md).
Die testtypspezifische Referenz- und Priorisierungsmatrix liegt in [SUPPORT_MATRIX.md](C:/Users/hello/Desktop/CT3xx/testprogramme/documentation/SUPPORT_MATRIX.md).

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

- Auswahl von Szenario-Datei, Testprogramm-Ordner, Verdrahtungs-Ordner, Simulationsmodell-Ordner und DUT-Modell
- DUT-Modelle als `.py`, `.json`, `.yaml` oder `.yml`
- Szenario-Presets
- Validierung der Konfiguration
- Simulation komplett oder im Einzelschrittmodus mit `Weiter`, `Zurueck`, `Auto` und `Pause`
- Ergebnisexport als `PDF`, `JSON` oder `CSV`
- Detailfenster fuer Schrittergebnisse mit Verbindungsgraph und Messkurven
- Auswertungsanalyse mit Diagramm- und Detailreiter fuer Soll-/Ist-Vergleich, Grenzband und tabellarische Metriken
- optionales Live-Zustandsfenster fuer Signale, DUT-Zustaende, Relais, Faults und Zeitverlauf
- Live-Zustandsfenster zeigt bei `concurrent`-Gruppen jetzt auch Concurrent-Gruppe, Event und Branch-Zustaende
- fuer `concurrent`-Gruppen werden jetzt explizite globale Snapshot-Punkte wie Warten, Interface-Request/-Response und Prozessende erzeugt
- sichtbare Snapshot-Timeline im Hauptfenster mit globalen Events und Branch-Zusammenfassung
- `Weiter` und `Zurueck` navigieren im Schrittmodus ueber die Snapshot-Folge statt ueber Replay
- Snapshot-Sessions koennen als `*.snapshot.json` gespeichert und spaeter ohne erneuten Lauf wieder geladen werden
- Breakpoints auf Testschritten und Gruppen halten direkt nach dem jeweiligen Ablauf an, ohne dafuer zusaetzliche Spezial-Snapshots zu erzeugen
- das Hauptfenster zeigt zusaetzlich einen einfachen Laufstatus `Bereit`, `Laeuft`, `Pausiert`
- Ausfuehrung externer Testprogramm-Skripte/Dateien inklusive optionaler Exit-Code-Auswertung
- explizites Laden und Speichern von Szenario-Preset-Dateien als `.json`

Liegt im Testprogramm-Ordner genau eine `.ctxprg`, wird sie automatisch verwendet. Bei mehreren Programmen kann die Datei explizit ausgewaehlt werden.

In der Verbindungsansicht gilt aktuell:

- Hauptansicht zeigt je Trace die drei Gruppen `Pruefsystem`, `Verdrahtung / Baugruppe`, `Geraet / Signal`
- Inline-Bauteile wie ein einzelnes Relais bleiben im Hauptpfad sichtbar, oeffnen aber kein eigenes Unterfenster
- nur echte Unterbaugruppen mit internem Teilpfad sind klickbar
- Unteransichten uebernehmen die Signalrichtung aus dem uebergeordneten Fenster

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
   - `tester_supply`
   - `tester_output`
   - weitere Laufzeittypen wie `switch`, `fuse`, `diode`, `load`, `voltage_divider`, `sensor`, `opto`, `transistor`

`assembly` erlaubt verschachtelte Unterverdrahtungen und Untermodelle, z. B. fuer Platinen oder Baugruppen.

## DUT-Modelle

Unter `simtest/device` gibt es zwei Wege fuer DUT-Simulationen:

- Python-Modelle
- deklarative JSON-/YAML-Profile

`simtest/device` ist dabei die gemeinsame zentrale Geraetebibliothek fuer alle Beispielszenarien. Neue Szenarien sollen keine eigene parallele Device-Runtime mehr aufbauen, sondern nur:

- ein neues Python-Modul unter `simtest/device/devices/*.py`
- oder ein neues JSON-/YAML-Profil unter `simtest/device/devices/*`

ergänzen und weiter dieselbe `simtest/device/main.py`- und `simtest/device/core`-Basis verwenden.

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
- Split-Unterablaeufe innerhalb eines `2ARB` laufen waehrend des aktiven Stimulus
- fuer `2ARB` wird das Ruecksignal ueber die Stimulusdauer aufgenommen und ueber erste Waveform-Metriken ausgewertet

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
  Hauptbeispiel fuer hierarchische Verdrahtung, UIF-gesteuertes Relais im Hauptplan, Simulation und DUT-Profile
- `simtest_template_sm2/`
  End-to-End-Beispiel fuer das vorhandene Testprogramm `template_SM2` mit `IOXX`, `ECLL`, `PWT$`, `E488` und `PET$`
- `simtest_template_splitted_am2/`
  End-to-End-Beispiel fuer das reale Testprogramm `template_splitted_am2` mit `2ARB` und Split-Unterablaeufen innerhalb des Tests
- `simtest_transformer/`
  End-to-End-Beispiel fuer Transformator und Stromwandler
- `examples/PythonDeviceSimulator/`
  einfaches Python-Pipe-Beispiel
- `examples/altium-wireviz/`
  Beispiel fuer den Altium-WireViz-Export

Bei neuen Beispielszenarien fuer DUT-Simulationen soll die Geraeteseite auf der gemeinsamen Struktur unter `simtest/device` aufbauen. Pro Szenario soll dabei nur das neue Geraetemodul bzw. Profil hinzukommen.

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
- Tester-seitige Ausgangsspannungen oder Open-Circuit-Verhalten koennen deklarativ ueber `tester_supply` und `tester_output` beschrieben werden.
- Neue DUT- oder Bauteilmodelle sollten ueber die Parser- und Simulationsschicht erweitert werden, nicht durch Hardcoding in der UI.
- `E488` ist im Simulator als Kommunikations-Test fuer `RS232` / `IEEE-488` / `VISA` modelliert, nicht als Mess-Test.
- Die Concurrent-/Snapshot-Architektur ist aktuell vollstaendig umgesetzt und in [ROADMAP.md](C:/Users/hello/Desktop/CT3xx/ROADMAP.md) als abgeschlossener Architekturbaustein dokumentiert.

## CLI

```powershell
dotnet run --project Ct3xxSimulator.Cli -- --help
```

Die CLI unterstuetzt aktuell:

- Batch-Laeufe ohne WPF
- direkte Pfadangaben fuer Programm, Verdrahtung, Simulation und DUT
- optionales Laden eines Presets aus einer bestehenden Szenario-JSON
- Vorab-Validierung
- optionalen Export als `PDF`, `JSON` oder `CSV`
- Exit-Codes anhand von `PASS`, `FAIL` oder Laufzeit-/Validierungsfehlern

Details:

- [Ct3xxSimulator.Cli/README.md](C:/Users/hello/Desktop/CT3xx/Ct3xxSimulator.Cli/README.md)
- [Ct3xxSimulator.Cli/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/Ct3xxSimulator.Cli/OPTIONS.md)
