# WireViz Known Properties (reference)

Source: `wireviz/WireViz` syntax definition (docs/syntax.md).

Reference git hash: `e4fe099f8c7b86736aee7b4227cc794b6e8b36f0`

## Main sections

- `connectors`
- `cables`
- `connections`
- `additional_bom_items`
- `metadata`
- `options`
- `tweak`

## Connector properties

- type
- subtype
- color
- image
- notes
- ignore_in_bom
- pn
- manufacturer
- mpn
- supplier
- spn
- additional_components
- pincount
- pins
- pinlabels
- pincolors
- bgcolor
- bgcolor_title
- style
- show_name
- show_pincount
- hide_disconnected_pins
- loops

## Cable properties

- category
- type
- gauge
- show_equiv
- length
- shield
- color
- image
- notes
- ignore_in_bom
- pn
- manufacturer
- mpn
- supplier
- spn
- additional_components
- wirecount
- colors
- color_code
- wirelabels
- bgcolor
- bgcolor_title
- show_name
- show_wirecount
- show_wirenumbers

## Notes

- Only the properties listed above are exposed in the UI and mapped for import/export.
- Unknown WireViz fields are ignored by design in this editor.
