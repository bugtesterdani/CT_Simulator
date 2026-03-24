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
- `ExternalDeviceStateSnapshot`

Das Projekt ist bewusst ohne WPF-Abhaengigkeiten gehalten und dient als gemeinsame Vertragsflaeche fuer Simulationskern, Desktop-App und weitere moegliche Frontends.
