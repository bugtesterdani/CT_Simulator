Ct3xxSimulator.Export
=====================

Entkoppelte Exportlogik fuer Simulationsergebnisse.

Enthaelt:

- Exportmodelle fuer Schritte und Logs
- `PDF`-, `JSON`- und `CSV`-Writer
- Text- und `SVG`-Formatter fuer Verbindungsdiagramme

Die Desktop-App mappt ihre ViewModels auf die Exportmodelle und ruft danach nur noch dieses Projekt auf. Dadurch kann die Exportlogik spaeter auch von CLI- oder Service-Projekten wiederverwendet werden.
