# Ct3xxSimulator.Desktop Optionen

Diese Datei beschreibt die konfigurierbaren Teile der WPF-App.

## In der App auswaehlbar

- Szenario-Datei fuer Presets (`*.json`)
- Testprogramm-Ordner
- Verdrahtungs-Ordner
- Simulationsmodell-Ordner
- DUT-Datei

Unterstuetzte DUT-Dateien:

- `*.py`
- `*.json`
- `*.yaml`
- `*.yml`

## Verhalten der Programmauswahl

- genau eine `*.ctxprg` im Testprogramm-Ordner:
  automatische Verwendung
- mehrere `*.ctxprg`:
  Bedienerauswahl

## Presets

Die App unterstuetzt Szenario-Presets fuer die oben genannten Pfade.

Aktuell:

- Standardpfad ist `%LocalAppData%\\Ct3xxSimulatorDesktop\\scenarios.json`
- der Speicherort kann in der App explizit ausgewaehlt werden
- Laden und Speichern koennen auf verschiedene vom Bediener gewaehlte JSON-Dateien gelenkt werden

Sinnvoll fuer:

- Gut-/Schlecht-/Grenzfall
- unterschiedliche DUT-Modelle
- unterschiedliche Verdrahtungsvarianten

## Verfuegbare Aktionen

- `Validieren`
- komplette Simulation starten
- Schrittmodus mit `Weiter`, `Zurueck`, `Auto`, `Pause`
- gruppierte Testschritt-Baumansicht verwenden
- Live-Zustand separat oeffnen
- Live-Zustand zwischen `Kompakt` und `Expert` umschalten
- Verbindungsansicht per Doppelklick auf einen Testschritt oeffnen
- Ergebnisse exportieren
- Snapshot-Session speichern
- Snapshot-Session laden
- zum letzten Snapshot des aktuell gewaehlten Testschritt-Knotens springen
- Concurrent-Kontext im Live-Zustandsfenster beobachten

## Verbindungsansicht

Aktuelles Bedienverhalten:

- Hauptansicht gruppiert in `Pruefsystem`, `Verdrahtung / Baugruppe`, `Geraet / Signal`
- ein einfacher Klick waehlt nur den Testschritt aus
- ein Doppelklick oeffnet die Verbindungsansicht des ausgewaehlten Testschritts
- Module in der Mitte zeigen `Eingang` und `Ausgang`
- nur echte Unterbaugruppen mit internem Teilpfad sind klickbar
- Relais oder andere einzelne Inline-Bauteile im Hauptpfad oeffnen kein Unterfenster
- Unterfenster uebernehmen die Signalrichtung des uebergeordneten Traces

## Testschritt-Ansicht

- orientiert sich an der `Group`-/DUTLoop-Struktur des geladenen CT3xx-Programms
- verwendet eine Baum-/Kartenansicht statt einer starren Tabellenzeile
- Gruppen sind einklappbar
- Gruppen starten aufgeklappt
- nach erfolgreichem Gruppenabschluss werden normale Gruppen automatisch wieder eingeklappt
- der `Test Loop`-Knoten bleibt dauerhaft sichtbar und aufgeklappt
- `Concurrent`, `Loop`, `Concurrent Loop` und `Test Loop` werden im Knoten selbst sichtbar gekennzeichnet
- `Concurrent`-Gruppen tragen einen Hinweis, dass die enthaltenen Schritte parallel ausgefuehrt werden koennen
- Testschritte sind unterhalb ihrer Gruppen eingerueckt
- die Ueberschriften sind auf die Baumansicht abgestimmt:
  - `Struktur`
  - `Status`
  - `Messwert / Grenzen`
  - `Hinweis`
- bei mehrfachen Auswertungen innerhalb eines Tests, z. B. `PET$`, entstehen unter dem Test weitere Ergebnisknoten

## Exportformate

- `PDF`
- `JSON`
- `CSV`

## Concurrent-Snapshots

Im aktuellen Stand zeigt das Live-Zustandsfenster bei `concurrent`-Gruppen zusaetzlich:

- `Concurrent-Gruppe`
- `Concurrent-Event`
- Liste der Concurrent-Branches mit:
  - Branchname
  - Status
  - aktuellem Item
  - Details

Das ist die Datenbasis fuer die spaetere tick-/eventbasierte Concurrent-Navigation.

Zusatzlich erscheinen jetzt globale Concurrent-Events fuer:

- `branch_waiting`
- `branch_resumed`
- `interface_request`
- `interface_response`
- `process_exit`

Zusatzlich steht im Hauptfenster eine Snapshot-Timeline zur Verfuegung:

- zeigt Reihenfolge der globalen Snapshot-Zustaende
- zeigt Zeit, Event und Branch-Zusammenfassung
- Auswahl eines Timeline-Eintrags setzt die Anzeige auf genau diesen Snapshot
- der Verlauf im Live-Zustandsfenster wird dabei nur bis zu diesem Snapshot aufgebaut
- die Testschritt-Anzeige wird fuer diesen Snapshot-Zeitpunkt neu rekonstruiert statt den Endstand beizubehalten
- `Weiter` und `Zurueck` navigieren ueber diese Snapshot-Folge
- im Schrittmodus wird an Snapshot-Punkten pausiert, nicht nur an Testenden

Ansichtsmodi im Live-Zustandsfenster:

- `Kompakt`
  - fokussiert auf schnelle Uebersicht
  - gruppiert in `System`, `DUT`, `Zustaende und Concurrent`
- `Expert`
  - zeigt alle Details
  - strukturiert ueber Tabs fuer `System`, `DUT`, `Zustaende`, `Concurrent`

Snapshot-Sessions:

- werden als `*.snapshot.json` gespeichert
- enthalten Timeline, Logs, Schrittergebnisse inklusive Snapshot-Zuordnung und Signalhistorie
- koennen nur ausserhalb einer laufenden Simulation geladen oder gespeichert werden

## Relevante Umgebungsvariablen

- `CT3XX_TESTPROGRAM_ROOT`
- `CT3XX_WIREVIZ_ROOT`
- `CT3XX_SIMULATION_MODEL_ROOT`
- `CT3XX_PYTHON_EXE`
- `CT3XX_PY_DEVICE_PIPE`

## Worauf geachtet werden sollte

- Pfade muessen zusammenpassen: `ctxprg`, Signaltabelle, WireViz und `simulation.yaml`
- DUT-Modellnamen muessen zu den vom Simulator aufgeloesten Signalnamen passen
- bei YAML-DUT-Profilen muss `PyYAML` installiert sein
- fuer Python-Pipes wird `pywin32` benoetigt
