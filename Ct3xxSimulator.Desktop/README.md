# Ct3xxSimulator.Desktop

WPF-Oberflaeche fuer den CT3xx-Simulator.

## Funktionen

- Auswahl von:
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

## Relevante Dateien

- `MainWindow.xaml`
  Hauptoberflaeche
- `MainWindow.xaml.cs`
  Steuerung, Simulationstart und Observer-Anbindung
- `Views/ConnectionGraphWindow.*`
  Verbindungsansicht pro Testschritt
- `Views/LiveStateWindow.*`
  optionale Live-Zustandsanzeige
- `Export/`
  Ergebnisexport
- `Configuration/`
  Presets
- `Validation/`
  Konfigurations- und Modellpruefung

## Start

```powershell
dotnet run --project Ct3xxSimulator.Desktop
```

## Hinweis

Die App enthaelt bewusst keine eigene Hardcode-Simulationslogik fuer DUT-Verhalten. Bauteilverhalten und DUT-Reaktionen sollen ueber den Simulationskern, `simulation.yaml` und die DUT-Modelle beschrieben werden.
