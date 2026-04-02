# CT3xx WireViz Block Designer (MVP)

## Skizze

```
[Connector/Testsystem] --(Edge)--> [Cable] --(Edge)--> [Harness] --(Edge)--> [Device]
     Ports P1..Pn          W1..Wn              H1..Hn             D1..Dn
```

## BlockGraph (MVP)

- BlockGraph
  - `nodes[]`: BlockNode
  - `edges[]`: BlockEdge
  - `metadata`: Title, SourceFormat, Tags
- BlockNode
  - `id`, `name`, `type`, `ports[]`, `x`, `y`
- BlockPort
  - `id`, `name`, `index`, `direction`, `role`
- BlockEdge
  - `fromNodeId`, `fromPortId`, `toNodeId`, `toPortId`, `label`

## Mapping-Regeln (WireViz)

1. `connectors` -> BlockNode (Type: Connector/Device/Harness)
2. `cables` -> BlockNode (Type: Cable, Ports aus `wirecount`/`wirelabels`)
3. `connections` -> BlockEdges (Pin-Index nach Position gematcht)
4. Export: BlockNodes -> WireViz `connectors`/`cables`, Edges -> `connections`

## MVP-Plan

1. BlockGraph Modell + JSON Persistenz
2. WireViz Import/Export Mapper
3. Web UI (Block-Liste, Board, Edges)
4. Desktop Host (WebView2 + File-Dialoge)
5. CLI/Server-Hosting via `Ct3xxWireVizDesigner.Web`
