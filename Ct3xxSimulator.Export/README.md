Ct3xxSimulator.Export
=====================

Entkoppelte Exportlogik fuer Simulationsergebnisse.

Enthaelt:

- Exportmodelle fuer Schritte und Logs
- `PDF`-, `JSON`- und `CSV`-Writer
- Text- und `SVG`-Formatter fuer Verbindungsdiagramme
- Snapshot-Session-Persistenz fuer `*.snapshot.json`

Die Desktop-App mappt ihre ViewModels auf die Exportmodelle und ruft danach nur noch dieses Projekt auf. Dadurch kann die Exportlogik spaeter auch von CLI- oder Service-Projekten wiederverwendet werden.

Snapshot-Sessions enthalten:

- Timeline-Snapshots
- Logs
- Schrittergebnisse
- Signalhistorie
- Concurrent-Metadaten
- Restore-Index fuer den zuletzt gewaehlten Snapshot

Details stehen in [OPTIONS.md](C:/Users/hello/Desktop/CT3xx/Ct3xxSimulator.Export/OPTIONS.md).
