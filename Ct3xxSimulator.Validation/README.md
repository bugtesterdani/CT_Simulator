Ct3xxSimulator.Validation
=========================

Eigenstaendige Validierung fuer Konfiguration, WireViz und `simulation.yaml`.

Enthaelt aktuell:

- `SimulationConfigurationValidator`
- `SimulationModelDeepValidator`

Geprueft werden unter anderem:

- fehlende Dateien und ungueltige Pfade
- unbekannte Stecker, Pins und Ports
- unverbundene Pins
- ungueltige Namen
- Assembly-Referenzen
- WireViz- und Element-Zyklen
- isolierte Simulationselemente

Die Validierung ist damit nicht mehr an die WPF-App gebunden und kann spaeter auch separat verwendet oder getestet werden.
