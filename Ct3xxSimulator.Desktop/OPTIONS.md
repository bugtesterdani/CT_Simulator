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
- Live-Zustand separat oeffnen
- Verbindungsansicht pro Testschritt oeffnen
- Ergebnisse exportieren

## Verbindungsansicht

Aktuelles Bedienverhalten:

- Hauptansicht gruppiert in `Pruefsystem`, `Verdrahtung / Baugruppe`, `Geraet / Signal`
- Module in der Mitte zeigen `Eingang` und `Ausgang`
- nur echte Unterbaugruppen mit internem Teilpfad sind klickbar
- Relais oder andere einzelne Inline-Bauteile im Hauptpfad oeffnen kein Unterfenster
- Unterfenster uebernehmen die Signalrichtung des uebergeordneten Traces

## Exportformate

- `PDF`
- `JSON`
- `CSV`

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
