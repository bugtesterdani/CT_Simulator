CT3xxSimulator.Interfaces
========================

Gemeinsame Laufzeit fuer externe und logische Schnittstellen des Simulators.

Enthaelt aktuell:

- Python-DUT-Prozessanbindung
- Kommunikation mit externen Device-Profilen
- Signal-, Interface- und Waveform-Zugriffe gegen das DUT

Dieses Projekt ist die vorgesehene Heimat fuer:

- RS232 / IEEE-488 / VISA
- I2C / SPI
- UIF / EA3 / EA3-R Kommunikationspfade
- weitere externe Tester-Interfaces

Kurzfristig dient es als saubere Trennung zwischen Simulationskern und Interface-/Device-Anbindung.
