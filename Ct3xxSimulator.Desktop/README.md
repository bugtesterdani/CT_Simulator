# Ct3xxSimulator.Desktop

WPF-Oberflaeche fuer den CT3xx-Simulator.

## Funktionen

- Auswahl von:
  - Szenario-Datei (`.json`) fuer Laden und Speichern von Presets
  - Testprogramm-Ordner
  - Verdrahtungs-Ordner
  - Simulationsmodell-Ordner
  - DUT-Modell (`.py`, `.json`, `.yaml`, `.yml`)
- Szenario-Presets
- Validierung vor dem Start
- Start kompletter Simulationen
- Einzelschrittmodus mit `Weiter`, `Zurueck`, `Auto`, `Pause`
- Ergebnisliste pro Testschritt
- Verbindungsgraph pro Schritt
- optionales Live-Zustandsfenster
- Export als `PDF`, `JSON`, `CSV`

## Bedienung

- Presets werden standardmaessig unter `%LocalAppData%\\Ct3xxSimulatorDesktop\\scenarios.json` gespeichert.
- Ueber das Feld `Szenario-Datei` kann eine andere JSON-Datei explizit ausgewaehlt, geladen oder per `Speichern unter` neu angelegt werden.
- In der Verbindungsansicht oeffnen nur echte Unterbaugruppen mit internem Pfad ein weiteres Fenster.
- Einzelne Inline-Bauteile wie Relais im Hauptpfad bleiben sichtbar, sind aber nicht als eigenes Untermodul klickbar.
- Unteransichten uebernehmen die Richtung des ausgewaehlten Signals aus dem uebergeordneten Fenster und drehen sie nicht mehr selbststaendig um.

## Relevante Dateien

- `MainWindow.xaml`
  Hauptoberflaeche
- `MainWindow.xaml.cs`
  Steuerung, Simulationstart und Observer-Anbindung
- `MainWindow.Configuration.cs`
  Pfade, Presets und Programmauswahl
- `MainWindow.Simulation.cs`
  Simulationslauf, Start des DUT-Prozesses und Observer-Anbindung
- `MainWindow.Timeline.cs`
  Schrittmodus und Replay-Navigation
- `Views/ConnectionGraphWindow.*`
  Verbindungsansicht pro Testschritt
- `Views/LiveStateWindow.*`
  optionale Live-Zustandsanzeige
- `Configuration/`
  Presets

Export und tiefe Modellvalidierung liegen inzwischen in den separaten Projekten:

- `Ct3xxSimulator.Export`
- `Ct3xxSimulator.Validation`

## Start

```powershell
dotnet run --project Ct3xxSimulator.Desktop
```

Konfigurationshinweise stehen in [OPTIONS.md](C:/Users/hello/Desktop/CT3xx/Ct3xxSimulator.Desktop/OPTIONS.md).

## Hinweis

Die App enthaelt bewusst keine eigene Hardcode-Simulationslogik fuer DUT-Verhalten. Bauteilverhalten und DUT-Reaktionen sollen ueber den Simulationskern, `simulation.yaml` und die DUT-Modelle beschrieben werden.
