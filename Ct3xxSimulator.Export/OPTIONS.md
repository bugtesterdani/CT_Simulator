# Ct3xxSimulator.Export Optionen

Diese Datei beschreibt die aktuell verfuegbaren Export- und Persistenzformate.

## Ergebnisexport

Unterstuetzt sind:

- `PDF`
- `JSON`
- `CSV`

Die Ergebnisexporte enthalten:

- Schrittergebnisse
- Logs
- Verbindungsdiagramme
- Diagramm-Assets als `SVG`

## Snapshot-Session

Zusatzlich unterstuetzt das Export-Projekt persistente Snapshot-Sessions als:

- `*.snapshot.json`

Diese Sessions enthalten:

- Konfigurationszusammenfassung
- Schrittergebnisse
- Logs
- komplette Snapshot-Timeline
- Concurrent-Metadaten pro Snapshot
- Signalhistorie
- zuletzt ausgewaehlten Timeline-Index

## Worauf geachtet werden sollte

- Snapshot-Sessions sind fuer Analyse und Restore gedacht, nicht als Ersatz fuer das eigentliche Testprogramm
- die Session kann ohne erneuten Simulationslauf in der Desktop-App geladen werden
- bei `JSON`-Diffs ist die Snapshot-Session deutlich umfangreicher als der normale Ergebnisexport
