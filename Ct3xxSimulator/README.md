# Ct3xxSimulator

Simulationskern fuer CT3xx-Testprogramme.

## Aufgaben

- Ausfuehren von `.ctxprg`-Testablaeufen
- Aufloesen von Signalen gegen Signaltabelle und WireViz
- Einbinden von `simulation.yaml`
- DUT-Kommunikation ueber Python/Named Pipe
- DUT-Profile in `JSON` / `YAML`
- Auswertung von Grenzwerten und Schrittresultaten
- Fault-Injection
- Zustandssnapshots fuer die Desktop-Oberflaeche
- Concurrent-Snapshot-Metadaten fuer parallele Gruppen
- Ausfuehrung externer Testprogramm-Skripte und Exit-Code-Auswertung
- deklarative Tester-Ausgangslogik ueber `tester_supply` / `tester_output`

## Wichtige Bereiche

- `Simulation/Ct3xxProgramSimulator.cs`
  Hauptablauf der Testsimulation
- `Simulation/WireViz/`
  Verdrahtungs- und Laufzeitaufloesung
- `Simulation/Devices/`
  Pipe-Client und externe DUT-Sitzung
- `Simulation/FaultInjection/`
  Fault-Modell
- `Simulation/Waveforms/`
  gemeinsames Kurvenmodell fuer AM2-/Waveform-Tests

## Unterstuetzte Schwerpunkte

- `SC2C`
- `CDMA`
- `IOXX`
- `E488`
- Datei-/Skriptausfuehrung aus Testprogrammen
- `2ARB` / AM2-Waveform
- Split-Unterablaeufe innerhalb eines Tests, z. B. verschachtelte `Group`-/`IOXX`-/`E488`-/`PET$`-Schritte unter einem `2ARB`
- generische Testschritte mit Ergebnis- und Statuspublikation

## Schnittstellen-Tests

`E488` wird als Kommunikations-Test fuer `RS232`, `IEEE-488` und `VISA` simuliert.

Aktuell unterstuetzt der Simulationskern dabei:

- Senden von Kommandos an externe DUT-Modelle ueber die Named Pipe
- optionales Ruecklesen des letzten Interface-Responses
- Rueckschreiben von Antworten in CT3xx-Variablen
- automatische Zerlegung von CSV-aehnlichen Antworten in Array-Variablen wie `Result[1] ... Result[n]`

Typischer Ablauf:

- Testprogramm sendet ein Kommando ueber `E488`
- das DUT antwortet ueber die simulierte Kommunikationsschnittstelle
- die Antwort wird in Variablen geschrieben
- nachfolgende Tests wie `PET$` werten diese Variablen aus

## Waveform-Unterstuetzung

`.ctarb`-Dateien werden eingelesen und als gemeinsames Kurvenmodell verarbeitet. Der Simulator kann:

- Stimulus-Kurven an das DUT uebergeben
- Sofortantworten vom DUT auswerten
- Response-Captures waehrend des Stimulus abfragen
- Messkurven an die UI weitergeben
- Split-Unterablaeufe innerhalb eines `2ARB` waehrend des aktiven Stimulus ausfuehren
- 2ARB-Ruecksignale ueber die Simulationszeit sampeln und erste Metriken wie `UMAX`, `UMIN`, `UAVG`, `UEFF`, `NPUL` und `AWID` auswerten

## Einbindung

Der Kern wird typischerweise aus der WPF-App verwendet, kann aber auch direkt aus Tools oder Tests aufgerufen werden.

## Concurrent-Snapshots

Fuer `concurrent`-Gruppen publiziert der Simulationskern jetzt bereits Snapshot-Metadaten als Grundlage fuer eine spaetere tick-/eventbasierte Ausfuehrung:

- aktive Concurrent-Gruppe
- globale Concurrent-Events wie `group_sync:start` oder `branch_started:*`
- Branch-Snapshots mit Status wie `queued`, `running`, `completed`
- explizite globale Snapshot-Punkte fuer:
  - Warten (`branch_waiting`, `branch_resumed`)
  - Schnittstellenkommunikation (`interface_request`, `interface_response`)
  - asynchrones Prozessende (`process_exit`)
- echte gemeinsame Simulationszeit fuer Concurrent-Branches
- Event-Fortschritt bis zum jeweils naechsten globalen Zeitpunkt statt serieller Abarbeitung
- poll-basierte Integration paralleler Prozesse in die globale Zeitfortschreibung

Die Concurrent-/Snapshot-Architektur ist projektweit inzwischen bis zur Persistenzstufe umgesetzt. Der Gesamtstatus ist in [ROADMAP.md](C:/Users/hello/Desktop/CT3xx/ROADMAP.md) zusammengefuehrt.
