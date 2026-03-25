CT3xx Roadmap
=============

Diese Roadmap priorisiert moegliche Erweiterungen nach Nutzen, Risiko und technischem Aufwand fuer den aktuellen Stand des Repositories.

Priorisierung
-------------

Die Einordnung ist pragmatisch:

- `P1`
  hoher Nutzen, niedrige bis mittlere Risiken, gute Hebelwirkung fuer Alltag und Debugging
- `P2`
  fachlich starke Erweiterungen mit hoeherem Implementierungsaufwand
- `P3`
  strategische oder komfortorientierte Erweiterungen fuer spaeteres Wachstum

P1
--

1. CLI fuer Batch-Simulationen

Status:
- umgesetzt in `Ct3xxSimulator.Cli`
- dokumentiert in `Ct3xxSimulator.Cli/README.md`

Nutzen:
- reproduzierbare Regressionstests ohne WPF
- einfacher Einsatz in CI
- gute Basis fuer automatisierte Freigaben

Umsetzung:
- neues Projekt `Ct3xxSimulator.Cli`
- nimmt Szenario- oder Einzelpfade entgegen
- verwendet `Ct3xxSimulator`, `Ct3xxSimulator.Validation`, `Ct3xxSimulator.Export`
- Exit-Code anhand von PASS/FAIL/ERROR

Warum P1:
- hoher praktischer Nutzen
- nutzt die bereits existierende Schichtung gut aus

2. Portable Szenario-Dateien

Nutzen:
- reproduzierbare Konfigurationen
- leichter Austausch zwischen Projekten und Kollegen
- bessere Versionsverwaltung als nur lokale Presets

Umsetzung:
- neues Format `scenario.json`
- enthaelt Testprogramm, Verdrahtung, Simulationsmodell, DUT, Faults, optionale Metadaten
- Desktop-App: `Importieren` / `Exportieren`
- CLI kann dieselbe Datei direkt nutzen

Warum P1:
- sehr gute Hebelwirkung fuer Bedienbarkeit und CI

3. Vergleichslaufe ueber mehrere DUT-Modelle

Nutzen:
- schnelles Durchtesten von `good`, `bad`, `borderline`
- staerkt Testabdeckung
- sofortiger Mehrwert fuer Entwicklung und Debugging

Umsetzung:
- Szenario kann mehrere DUT-Profile referenzieren
- App und CLI fuehren dieselbe Simulation mehrfach aus
- Sammelreport mit Differenzen pro Schritt

Warum P1:
- baut direkt auf dem vorhandenen Export und Preset-System auf

4. Fault-Katalog mit UI

Nutzen:
- Faults muessen nicht mehr nur manuell ueber `faults.json` gepflegt werden
- gezielte Testabdeckung fuer Fehlerszenarien

Umsetzung:
- eigenes Fault-Dialogfenster in der Desktop-App
- Aktivierung, Parametrisierung und Speichern von Fault-Profilen
- optional Export als `faults.json`

Warum P1:
- Fault-Injection ist schon vorhanden, die Bedienung ist aber noch technisch

5. Trace mit Ursachenanalyse

Nutzen:
- nicht nur PASS/FAIL, sondern nachvollziehbar warum
- deutlich schnelleres Debugging

Umsetzung:
- `StepEvaluation` um strukturierte Ursachen erweitern
- Quelle, Pfad, Transformation, aktive Faults, Grenzentscheidung mitgeben
- Detailfenster und Export entsprechend erweitern

Warum P1:
- hoher Nutzerwert bei vergleichsweise ueberschaubarem Risiko

P2
--

6. Snapshot-Persistenz fuer echten Ruecksprung

Nutzen:
- `Zurueck` nicht nur per Replay, sondern per Restore
- bessere Analyse bei langen Programmen

Umsetzung:
- serialisierbarer Simulationszustand pro Schritt
- optionales Checkpointing in Datei oder Speicher
- Desktop-App stellt daraus echten Schritt-Ruecksprung bereit

Warum P2:
- fachlich sinnvoll, aber tiefer Eingriff in den Simulationskern

7. Testabdeckungsanalyse

Nutzen:
- zeigt, welche Signale, Relais, Bauteile und Fehlerbilder wirklich getestet werden
- hilft bei Luecken im Testprogramm

Umsetzung:
- waehrend der Simulation Coverage-Daten sammeln
- Report fuer Signale, Pfade, Bauteile, Fault-Typen
- Export als JSON/CSV/PDF

Warum P2:
- hoher Nutzen, aber benoetigt saubere Instrumentierung

8. Graphische Modellvalidierung

Nutzen:
- Modellfehler schneller sichtbar als nur per Textliste
- staerkt WireViz-/Simulation-Authoring

Umsetzung:
- separates Validierungsfenster
- visuelle Markierung unverbundener Pins, Zyklen, kaputter Port-Mappings
- Sprung zu betroffenen Dateien und Elementen

Warum P2:
- sinnvoll, aber mehr UI- und Visualisierungsaufwand

9. Geraeteprofil-Editor

Nutzen:
- YAML/JSON-Profile muessen nicht mehr komplett manuell geschrieben werden
- senkt Einstiegshuerde fuer neue DUT-Szenarien

Umsetzung:
- Formular-Editor fuer Signale, Regeln, Kurven, Interfaces
- Live-Validierung und Vorschau
- Speichern als YAML/JSON

Warum P2:
- fachlich attraktiv, aber merklich mehr UI-Aufwand

10. Schnittstellen-Simulation ausbauen

Nutzen:
- realistischer fuer UART, CAN, LIN, I2C, SPI
- wichtig fuer komplexere Geraete

Umsetzung:
- neues Kommunikationsmodell mit Zeitverhalten, Frames, Timeouts und Fehlern
- Python- und YAML-DUT erweitern
- Export von Interface-Traffic

Warum P2:
- technisch aufwendiger, aber fachlich stark

11. Testdatenpakete

Nutzen:
- komplette Szenarien sauber archivieren und transportieren
- hilfreich fuer Support, Regression und Kundenfaelle

Umsetzung:
- Paketformat mit Manifest
- enthaelt Programm, Verdrahtung, Simulation, DUT, Faults, evtl. Erwartungen
- Import/Export in App und CLI

Warum P2:
- gute Grundlage fuer reproduzierbare Testfaelle

P3
--

12. Physikalischere Analogsimulation

Nutzen:
- mehr Realitaetsnaehe fuer analoge Pfade
- besser fuer Lasten, Teiler, Widerstandsnetzwerke, Halbleiter

Umsetzung:
- eigener Analog-Solver oder vereinfachtes Netzmodell
- Schritt fuer Schritt fuer `resistor`, `load`, `divider`, `sensor`, `transistor`

Warum P3:
- fachlich wertvoll, aber deutlich aufwendiger und fehleranfaelliger

13. Plugin-Modell fuer Bauteile

Nutzen:
- neue Bauteiltypen ohne Kernumbau
- langfristig bessere Erweiterbarkeit

Umsetzung:
- Plugin-Interface fuer Laufzeitbauteile
- Laden ueber Reflection oder registrierte Module

Warum P3:
- eher strategisch, lohnt sich bei wachsender Bauteilbibliothek

14. Weitere Importer fuer reale Projektdaten

Nutzen:
- erleichtert Anbindung an reale Engineering-Daten
- reduziert manuelle Pflege

Umsetzung:
- weitere Importpfade neben Altium
- z. B. CSV, XML, EPLAN-nahe Exporte

Warum P3:
- sinnvoll, wenn konkrete Quellsysteme gebraucht werden

15. Differenzansicht fuer Ergebnisse

Nutzen:
- zwei oder mehr Laeufe direkt vergleichen
- hilfreich fuer Fault-Analyse und DUT-Vergleich

Umsetzung:
- Vergleichsmodus in App und Export
- Differenzen fuer Werte, Pfade, Faults und Relaiszustande

Warum P3:
- sehr nuetzlich, aber erst nach staerkerem CLI-/Szenario-Unterbau besonders wertvoll

Empfohlene Reihenfolge
----------------------

Wenn die Roadmap in praktikablen Schritten umgesetzt werden soll:

1. CLI fuer Batch-Simulationen
2. Portable Szenario-Dateien
3. Vergleichslaufe ueber mehrere DUT-Modelle
4. Fault-Katalog mit UI
5. Trace mit Ursachenanalyse
6. Snapshot-Persistenz
7. Testabdeckungsanalyse
8. Graphische Modellvalidierung
9. Geraeteprofil-Editor
10. Schnittstellen-Simulation ausbauen
11. Testdatenpakete
12. Physikalischere Analogsimulation
13. Plugin-Modell fuer Bauteile
14. Weitere Importer
15. Differenzansicht

Empfohlene naechste konkrete Umsetzung
--------------------------------------

Wenn direkt ein naechster Schritt begonnen werden soll, ist aktuell am sinnvollsten:

1. `Ct3xxSimulator.Cli`
2. `scenario.json`
3. Mehrfachlauf ueber mehrere DUT-Modelle

Diese drei Punkte staerken sofort:

- Regressionstests
- Nachvollziehbarkeit
- Reproduzierbarkeit
- spaetere Automatisierung
