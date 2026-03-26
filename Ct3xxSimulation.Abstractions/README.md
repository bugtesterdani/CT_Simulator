CT3xxSimulation.Abstractions
============================

Gemeinsame DTOs und Interfaces fuer die Simulationsschicht.

Enthaelt aktuell unter anderem:

- `ISimulationObserver`
- `ISimulationExecutionController`
- `TestOutcome`
- `StepEvaluation`
- `StepConnectionTrace`
- `MeasurementCurvePoint`
- `SimulationStateSnapshot`
- `ConcurrentBranchSnapshot`
- `ExternalDeviceStateSnapshot`

Das Projekt ist bewusst ohne WPF-Abhaengigkeiten gehalten und dient als gemeinsame Vertragsflaeche fuer Simulationskern, Desktop-App und weitere moegliche Frontends.

## Snapshot-Vertrag

`SimulationStateSnapshot` enthaelt inzwischen nicht nur globale Signal- und DUT-Zustaende, sondern auch Concurrent-Metadaten:

- aktive Concurrent-Gruppe
- letztes Concurrent-Event
- Liste von `ConcurrentBranchSnapshot`
- explizite Snapshot-Ereignisse fuer globale Konsistenzpunkte wie:
  - `branch_waiting`
  - `branch_resumed`
  - `interface_request`
  - `interface_response`
  - `process_exit`

Damit koennen Frontends spaeter echte Concurrent-Timelines und Snapshot-Navigation aufbauen, ohne eigene Annahmen ueber den Simulationskern treffen zu muessen.

Mit Stufe 3 der Concurrent-Roadmap tragen diese Snapshots jetzt auch eine echte gemeinsame Simulationszeit fuer parallele Branches. Frontends bekommen dadurch keine rein serielle Pseudo-Zeit mehr, sondern globale Zustandswechsel entlang einer gemeinsamen Concurrent-Uhr.

Mit Stufe 5 werden diese Snapshot-Daten jetzt auch persistent gespeichert und wieder geladen. Die Persistenz selbst liegt im Export-Projekt, der Vertragskern bleibt aber hier in den gemeinsamen Snapshot-Typen definiert.
