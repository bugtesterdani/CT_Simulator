# simtest/wireplan Optionen

Diese Datei beschreibt die Verdrahtungs- und Simulationsdateien im Ordner `wireplan` und welche Felder der Simulator aktuell daraus verwendet.

## Dateien im Ordner

- `Verdrahtung.yml`
  Haupt-WireViz fuer den aeusseren Pfad
- `simulation.yaml`
  Verhaltensmodell fuer Hauptverdrahtung und Baugruppen
- `board_device_wireviz.yaml`
  Unterverdrahtung der Beispielbaugruppe
- `board_device_simulation.yaml`
  Verhaltensmodell der Unterbaugruppe
- `faults.json`
  optionale Fault-Injection

## 1. `Verdrahtung.yml`

Hier steht nur die physische Topologie.

Erlaubt und erwartet:

- `connectors`
- `cables`
- `bundles`
- `connections`

Wichtig:

- keine projektspezifischen Sonderfelder wie `ct3xx_signals`
- Tester-, Verdrahtungs- und DUT-Seite werden ueber WireViz-Konventionen erkannt

### Connector-Konventionen

- Pruefsystem:
  uppercase, typischerweise nicht gelb, z. B. `CT3`, `UIF`
- Verdrahtung/Harness:
  uppercase und gelb, z. B. `RELAIS`
- Geraet/Baugruppe:
  gemischte Schreibweise und gelb, z. B. `DeviceBoard`, `DevicePort`

### Was gesetzt sein muss

- `pins`
  technischer Pinname
- `pinlabels`
  fachlicher Name oder lokale Pinbezeichnung

Wichtig:

- `pins` und `pinlabels` muessen in Anzahl und Reihenfolge zusammenpassen
- `connections` muessen auf existierende Connectoren und Pins zeigen

## 2. `simulation.yaml`

Hier steht das Verhalten auf dem Hauptpfad.

Top-Level:

```yaml
elements:
  - ...
```

Jedes Element braucht mindestens:

- `id`
- `type`

### Aktuell unterstuetzte Typen

- `relay`
- `resistor`
- `transformer`
- `current_transformer`
- `assembly`
- `tester_supply`
- `tester_output`
- `testsystem`
- `switch`
- `fuse`
- `diode`
- `load`
- `voltage_divider`
- `sensor`
- `opto`
- `transistor`

## 3. Typ-spezifische Felder

### `relay`

Pflicht:

- `coil.signal`
- `coil.threshold_v`
- `contacts`

Beispiel:

```yaml
- id: RELAIS
  type: relay
  coil:
    signal: UIF_OUT1
    threshold_v: 24.0
  contacts:
    - a: RELAIS.COM1
      b: RELAIS.NO1
      mode: normally_open
```

Optional:

- `delay_ms`

Wichtig:

- `signal` muss ein bekanntes Simulationssignal sein
- `contacts` muessen auf Knoten zeigen, die im WireViz-Pfad existieren

### `resistor`

Pflicht:

- `a`
- `b`
- `ohms`

### `transformer`

Pflicht:

- `primary_a`
- `primary_b`
- `secondary_a`
- `secondary_b`
- `ratio`

Wichtig:

- `ratio` bedeutet `primary / secondary`

### `current_transformer`

Pflicht:

- `primary_signal`
- `secondary_a`
- `secondary_b`
- `ratio`

Wichtig:

- der Primaerstrom ist ein Signal, keine direkte Drahtverbindung

### `assembly`

Pflicht:

- `wiring`
- `simulation`
- `ports`

Beispiel:

```yaml
- id: DeviceBoard
  type: assembly
  wiring: board_device_wireviz.yaml
  simulation: board_device_simulation.yaml
  ports:
    IN: BoardPort.IN
    OUT: BoardPort.OUT
```

Wichtig:

- `wiring` und `simulation` werden relativ zu `simulation.yaml` aufgeloest
- `ports` muessen zu Knoten in der Unterverdrahtung passen

### `tester_supply`

Gedacht fuer:

- deklarierte Pruefsystem-Spannungsquelle

Pflicht:

- `signal`
- `voltage`

### `tester_output`

Gedacht fuer:

- digitale Tester-Ausgaenge wie `IOXX`

Pflicht:

- `signal`
- `high_mode`
- `low_mode`

Unterstuetzte Modi:

- `supply`
- `value`
- `open`

Zusaetzliche Felder je nach Modus:

- `high_supply`
- `low_supply`
- `high_value`
- `low_value`

Wichtig:

- fuer `supply` muss eine passende `tester_supply` existieren
- damit laesst sich fachlich abbilden, dass ein Ausgang gegen eine externe Versorgung schaltet oder offen ist

### Generische Elemente

Fuer `switch`, `fuse`, `diode`, `load`, `voltage_divider`, `sensor`, `opto`, `transistor` werden Parameter ueber Metadaten gelesen.

Wichtig:

- Namen und Feldinhalte sollten stabil und sprechend gehalten werden
- bei `transistor` bevorzugt `transistor_type` verwenden

### `testsystem`

Gedacht fuer:

- globale Testsystem-Optionen, die nicht direkt an einem Pfadelement haengen

Beispiel:

```yaml
- id: Testsystem
  type: testsystem
  odbc_mode: real
  odbc_mock_result: "ODBC Mock: OK"
  odbc_timeout_seconds: 30
```

Unterstuetzte Felder:

- `odbc_mode`: `real` oder `mock` (default: `real`)
- `odbc_mock_result`: String fuer Mock-Antwort
- `odbc_timeout_seconds` oder `odbc_timeout_ms`

## 4. `board_device_wireviz.yaml` und `board_device_simulation.yaml`

Diese Dateien beschreiben die interne Verdrahtung einer Baugruppe.

Wichtig:

- die Assembly-Portnamen aus `simulation.yaml` muessen hier wieder auftauchen
- interne Knoten duerfen andere technische Namen tragen
- nur echte Unterbaugruppen mit innerem Pfad sind in der UI als Untermodul sinnvoll oeffnbar

## 5. `faults.json`

Optionales Fehlerprofil.

Top-Level:

```json
{
  "faults": [
    { "type": "open_connection", ... }
  ]
}
```

Aktuell unterstuetzte Fault-Typen:

- `force_signal`
- `force_relay`
- `open_connection`
- `blow_fuse`
- `short_connection`
- `signal_drift`
- `contact_problem`
- `wrong_resistance`

Wichtig:

- Fault-IDs und Verweise muessen zu Signalen, Relais oder Elementen passen
- Faults koennen Ergebnisse und Live-Zustand direkt beeinflussen

## Allgemeine Regeln

- `WireViz` beschreibt nur Topologie
- Verhalten gehoert in `simulation.yaml`
- Dateinamen und Relativpfade klein und stabil halten
- Fachnamen aus `Signaltable.ctsit` moeglichst durchgaengig erhalten

## Typische Fehlerquellen

- `connections` zeigen auf nicht existierende Pins
- `pins` und `pinlabels` passen nicht zusammen
- `assembly.ports` zeigen auf falsche interne Knoten
- Relaiskontakte sind im Simulationsmodell vorhanden, aber im WireViz-Pfad nicht
- `tester_output` nutzt `supply`, aber die referenzierte `tester_supply` fehlt
- Untermodul-Dateien liegen nicht relativ zum Hauptmodell
