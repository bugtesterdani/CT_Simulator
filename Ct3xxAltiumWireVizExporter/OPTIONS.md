# Ct3xxAltiumWireVizExporter Optionen

Diese Datei beschreibt die CLI-Argumente und die JSON-Konfiguration des Altium-Exporters.

## CLI-Argumente

- `--input`
  Pfad zur Altium-CSV
- `--config`
  Pfad zur JSON-Konfiguration
- `--output`
  Zielpfad fuer das WireViz-YAML

## Erwartete CSV-Spalten

Pflicht:

- `Net`
- `Designator`
- `Pin`

Optional:

- `ComponentKind`
- `PinName`

## JSON-Konfiguration

Unterstuetzte Felder:

- `boardName`
- `connectorType`
- `connectorSubtype`
- `defaultRole`
- `connectorPrefixes`
- `connectorAliases`
- `roleMappings`
- `excludeNets`

## Wichtige Wirkung

- `connectorAliases` aendert den Connectornamen im YAML
- `pinlabel` bleibt immer im Format `SteckerName_PinNummer`
- `excludeNets` verhindert die Ausgabe bestimmter Netze

## Worauf geachtet werden sollte

- nur Netze mit mindestens zwei Connector-Endpunkten werden exportiert
- Connector-Praefixe sollten zu den echten Steckerdesignatoren aus Altium passen
