Ct3xxSimulator.Validation
=========================

Eigenstaendige Validierung fuer Konfiguration, WireViz und `simulation.yaml`.

Enthaelt aktuell:

- `SimulationConfigurationValidator`
- `SimulationModelDeepValidator`

## Eingaben

- Testprogramm-Ordner
- Verdrahtungs-Ordner
- Simulationsmodell-Ordner
- optional DUT-Datei

Geprueft werden unter anderem:

- fehlende Dateien und ungueltige Pfade
- unbekannte Stecker, Pins und Ports
- unverbundene Pins
- ungueltige Namen
- Assembly-Referenzen
- WireViz- und Element-Zyklen
- isolierte Simulationselemente

## Wichtige Annahmen

- `Verdrahtung.yml` und `simulation.yaml` muessen logisch zusammenpassen
- Assembly-Unterdateien muessen relativ aufloesbar sein
- Fault- und DUT-Dateien werden separat behandelt, nicht als Teil der WireViz-Topologie

## Worauf geachtet werden sollte

- die Validierung prueft Struktur und Konsistenz, nicht jede physikalische Fachlogik
- ein fachlich unerwuenschtes Modell kann formal trotzdem valide sein

Die Validierung ist damit nicht mehr an die WPF-App gebunden und kann spaeter auch separat verwendet oder getestet werden.
