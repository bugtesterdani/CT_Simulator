# Ct3xxAltiumWireVizExporter

Konverter von Altium-Konnektivitaetsdaten nach WireViz-YAML fuer Simulations-Unterverdrahtungen.

## Zweck

Dieses Projekt erzeugt aus einem Altium-CSV-Export einen WireViz-kompatiblen Verdrahtungsplan fuer Platinen oder Baugruppen.

Jeder exportierte Steckerpin bekommt automatisch ein `pinlabel` im Format:

`(SteckerName aus Altium)_(Pin Nummer)`

Beispiele:

- `J1_1`
- `X3_12`
- `CN7_5`

Ein optionaler Connector-Alias aendert nur den Connectornamen im WireViz-Dokument, nicht das `pinlabel`.

Damit lassen sich Altium-Boards direkt als Sub-WireViz in `assembly`-Simulationen verwenden.

## Eingabe

Der Generator erwartet einen CSV-Export mit mindestens diesen Spalten:

- `Net`
- `Designator`
- `Pin`

Optional:

- `ComponentKind`
- `PinName`

## Konfiguration

Die Exportregeln werden in einer JSON-Datei beschrieben:

```json
{
  "boardName": "DeviceBoard",
  "connectorType": "altium_connector",
  "connectorSubtype": "board_connector",
  "defaultRole": "harness",
  "connectorPrefixes": ["J", "X", "P"],
  "connectorAliases": {
    "J1": "BoardIn",
    "J2": "BoardOut"
  },
  "roleMappings": {
    "J1": "device",
    "J2": "harness"
  },
  "excludeNets": ["GND_FLOOD"]
}
```

## Aufruf

```powershell
dotnet run --project Ct3xxAltiumWireVizExporter -- `
  --input .\examples\altium-wireviz\example_connectivity.csv `
  --config .\examples\altium-wireviz\example_config.json `
  --output .\examples\altium-wireviz\generated_wireviz.yaml
```

## Ausgabe

Die Ausgabe ist standardkonformes WireViz-YAML mit:

- `connectors`
- `pins`
- `pinlabels`
- `connections`

Netze mit weniger als zwei Stecker-Endpunkten werden nicht ausgegeben.

## Erweiterbarkeit

Die Struktur ist bewusst einfach gehalten:

- `Configuration/`
  Exportregeln und Mapping
- `Altium/`
  CSV-Import
- `WireViz/`
  internes Modell und YAML-Writer

Neue Importquellen koennen spaeter zusaetzlich angebunden werden, ohne die WireViz-Ausgabe neu zu bauen.
