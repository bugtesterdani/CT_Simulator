# CT3xx WireViz Designer (Web)

Web UI for building WireViz-compatible wiring plans.

## Features

- Node/Port editor with WireViz properties (known keys + custom)
- Connections enforced via Cable nodes
- Wire color control per connection
- YAML preview (auto or manual refresh)
- Export minimal vs full, optional gzip
- Snapshot export (CSV + JSON)
- Pan (drag) and zoom (buttons or Ctrl + wheel)
- Resizable left/right panels
- Light/Dark mode (system default + toggle)

## Run

```
dotnet run --project Ct3xxWireVizDesigner.Web
```

## API

- `GET /api/wireviz/schema`
- `POST /api/wireviz/import`
- `POST /api/wireviz/export`

Swagger UI: `/swagger`
