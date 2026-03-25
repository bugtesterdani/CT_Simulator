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
- Datei-/Skriptausfuehrung aus Testprogrammen
- `2ARB` / AM2-Waveform
- generische Testschritte mit Ergebnis- und Statuspublikation

## Waveform-Unterstuetzung

`.ctarb`-Dateien werden eingelesen und als gemeinsames Kurvenmodell verarbeitet. Der Simulator kann:

- Stimulus-Kurven an das DUT uebergeben
- Sofortantworten vom DUT auswerten
- Response-Captures waehrend des Stimulus abfragen
- Messkurven an die UI weitergeben

## Einbindung

Der Kern wird typischerweise aus der WPF-App verwendet, kann aber auch direkt aus Tools oder Tests aufgerufen werden.
