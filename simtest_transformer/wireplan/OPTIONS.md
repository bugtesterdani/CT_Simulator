# simtest_transformer/wireplan Optionen

Dieser Ordner enthaelt die Verdrahtung und das Simulationsmodell fuer Transformator und Stromwandler.

## Dateien

- `Verdrahtung.yml`
- `simulation.yaml`

## Fachlich relevante Typen

- `transformer`
- `current_transformer`

## Wichtige Felder

### `transformer`

- `primary_a`
- `primary_b`
- `secondary_a`
- `secondary_b`
- `ratio`

### `current_transformer`

- `primary_signal`
- `secondary_a`
- `secondary_b`
- `ratio`

## Worauf geachtet werden sollte

- `ratio` bedeutet `primary / secondary`
- der Stromwandler arbeitet ueber ein Primaersignal, nicht als einfacher Draht
