# simtest/device Optionen

Diese Datei beschreibt die moeglichen DUT-Modelle in diesem Ordner, die Pipe-Schnittstelle und die wichtigsten Pflichtfelder.

Dieser Ordner ist die gemeinsame zentrale Device-Bibliothek fuer die Beispielszenarien. Neue Szenarien sollen hier keine zweite Runtime-Struktur neben `main.py` und `core/` aufbauen.

## Unterstuetzte Modellarten

- Python-Modul unter `devices/*.py`
- deklaratives Profil unter `devices/*.json`
- deklaratives Profil unter `devices/*.yaml` oder `devices/*.yml`

## Auswahl in der Desktop-App

Als DUT-Datei kann direkt ausgewaehlt werden:

- `*.py`
- `*.json`
- `*.yaml`
- `*.yml`

Der Simulator startet dann `main.py` mit dem passenden Modus.

## Voraussetzungen

Pflicht fuer Python-Pipe-Betrieb:

- Windows
- ein passender Python-Interpreter
- `pywin32`

Zusaetzlich fuer YAML-Profile:

- `PyYAML`

## Python-Modul

Ein Python-DUT-Modul ist die flexible Variante.

Typisch erwartet:

- `reset()`
- `set_input(name, value)`
- `get_signal(name)`
- `read_state()`

Optional:

- `set_waveform(name, waveform, options)`
- `read_waveform(name, options)`
- `send_interface(name, payload)`
- `read_interface(name)`

Wenn ein Verhalten frei programmiert oder stark zustandsbehaftet sein soll, ist diese Variante die richtige.

## Deklaratives JSON-/YAML-Profil

Diese Variante ist fuer aehnliche DUT-Varianten gedacht, die sich ohne eigenen Python-Code beschreiben lassen.

### Typische Top-Level-Felder

- `name`
- `signals`
- `aliases`
- `derived_signals`
- `source_control`
- `initial_inputs`
- `initial_sources`
- `initial_internal`
- `input_curves`
- `outputs`
- `interfaces`
- `timers`
- `state_machines`

### `signals`

Erlaubt:

- `inputs`
- `outputs`
- `sources`
- `internal`

Wichtig:

- Namen werden intern in Grossbuchstaben behandelt
- diese Namen muessen zu dem passen, was der Simulator ueber WireViz und Signaltabelle aufloest

### `aliases`

Gedacht fuer:

- mehrere externe Signalnamen fuer denselben internen Signalnamen
- WireViz-Pinlabels
- alternative Interface-Namen

Unterstuetzt:

- `aliases.signals`
- `aliases.interfaces`

Wichtig:

- Alias-Namen werden vor der eigentlichen Signal- oder Interface-Aufloesung auf einen kanonischen Namen gemappt
- das eigentliche Profil soll trotzdem nur einen fachlich sauberen Kernnamen pro Signal fuehren

### `ctct`

Gedacht fuer:

- DUT-seitige Widerstandsnetze, die in CTCT-Durchgangstests mitberuecksichtigt werden sollen

Unterstuetzt:

- `ctct.resistances[]`
- `ctct.groups[]`
- `ctct.ring`

Jeder Eintrag:

- `id` (optional, sonst auto)
- `a` (Pin/Signalname)
- `b` (Pin/Signalname)
- `ohms` (Widerstandswert)

Beispiel:

```yaml
ctct:
  resistances:
    - id: DUT_R1
      a: DUT_P1
      b: DUT_P2
      ohms: 120
```

Gruppenvariante:

```yaml
ctct:
  groups:
    - id: DUT_BUNDLE
      a: DUT_P1
      ohms: 120
      pins: [DUT_P2, DUT_P3, DUT_P4]
```

Ring-Variante:

```yaml
ctct:
  ring:
    prefix: "DevicePort."
    start: 1
    end: 96
    skip: [14, 28]
    ohms: 1.0
```

### `derived_signals`

Gedacht fuer:

- interne Hilfssignale ohne eigenes Python-Modul
- Schwellwertsignale
- Phasen-/Dominanz-Erkennung ueber Waveform-Fenster

Unterstuetzt:

- `compare`
- `dominant_signal_window`

Bei `dominant_signal_window` aktuell wichtig:

- `signal`
- `peers`
- `window_ms`
- `metric`
- `min_metric`
- `min_delta`
- `true_value`
- `false_value`

### `source_control`

Aktuell wichtig:

- `minimums`

Beispiel:

```yaml
source_control:
  minimums:
    VCC_PLUS: 19.5
```

### Initialwerte

Erlaubt:

- `initial_inputs`
- `initial_sources`
- `initial_internal`

Fehlende Werte starten mit `0.0`.

### `input_curves`

Gedacht fuer:

- Rampen
- Einschwingverhalten
- simulierte Sensorsignale

Unterstuetzt:

- `mode: hold`
- `mode: linear`
- `points`

Wichtig:

- ein manuell gesetzter Eingangswert uebersteuert die Kurve fuer dieses Signal

### `outputs`

Jeder Ausgang kann haben:

- `default`
- `rules`

Eine Regel kann nutzen:

- `when`
- `value`
- `transfer_curve`
- `delay_ms`
- `slew_per_ms`

### Bedingungen in `when`

Aktuell unterstuetzt:

- `input`
- `source`
- `signal`
- `gt`
- `gte`
- `lt`
- `lte`
- `eq`
- `all`
- `any`

### `transfer_curve`

Gedacht fuer:

- Kennlinien
- lineare oder stufige Eingang-zu-Ausgang-Abbildung

Unterstuetzt:

- `input` oder `signal`
- `mode: linear`
- `mode: hold`
- `points`

### `interfaces`

Gedacht fuer:

- UART
- einfache Protokolle
- deklarative Antworttabellen

Unterstuetzt:

- `default_response`
- `requests`
- `when.equals`
- `when.contains`
- `state_when`
- `response`

Zusaetzlich unterstuetzt:

- `protocol: i2c`
- `protocol: spi`
- `protocol: ict`
- `protocol: shrt`
- `required_supply_v`
- `master_readback: echo`
- `master_readback: fixed`
- `master_readback_byte`
- `devices`

Fuer I2C-Geraete aktuell wichtig:

- `kind: lm75`
- `address`
- `temperature_c`
- `initial_pointer`
- `initial_registers`

Wichtig:

- das Testsystem ist in diesem Modell immer der I2C-Master
- das Profil beschreibt das externe I2C-Slave-Geraet
- der I2C-Slave-Zustand bleibt ueber den aktuellen Testlauf erhalten
- Registerwerte werden erst bei `reset()` oder in einem neuen Testlauf auf ihre Initialwerte gesetzt
- die bisherigen Felder `write_response` und `write_response_byte` bleiben aus Kompatibilitaetsgruenden weiter lesbar, sollen aber nicht mehr neu verwendet werden

Fuer SPI zusaetzlich aktuell wichtig:

- das Testsystem ist immer der SPI-Master
- das Profil beschreibt das externe SPI-Slave-Geraet

Fuer einfache `SMUD`-Lastprofile aktuell wichtig:

- Versorgung wird ueber einen normalen Eingang wie `DUT_SUPPLY` modelliert
- Strommessung wird ueber einen normalen Ausgang wie `DUT_CURRENT` modelliert
- dadurch sind keine separaten Spezialhooks im Python-Host noetig
- PASS-/FAIL-/ERROR-Varianten lassen sich ueber unterschiedliche Profile oder Python-Module abbilden
- `clock_phase`
- `clock_polarity`
- `chip_select_active`
- `frequency_hz`
- per-Testlauf persistenter Speicher ueber `initial_memory`, `initial_memory_hex` oder Byte-Initialwerte
- Sonderfaelle wie `WREN`, Status-Register, Busy-/Write-Delay, Page-Boundaries und Write-Protect

Fuer ICT zusaetzlich aktuell wichtig:

- das Testsystem bleibt der Mess-Master
- das Profil liefert den Messwert, die Grenzwertbewertung passiert im Simulator
- `measurements[]` mit `when`-Filtern wie `name`, `reference`, `metric` oder `type_id`
- optional `mode: echo_nominal` fuer Fallback ueber Payload-Grenzwerte

Fuer SHRT zusaetzlich aktuell wichtig:

- das Testsystem bleibt der Mess-Master
- das Profil liefert die gefundenen Kurzschluss-Paare
- `shorts[]` mit `a`, `b`, `ohms`
- optional `default_ohms` + `pairs[]`/`overrides[]` fuer WireViz-basierte Paarlisten
- wenn `pairs[]` im Payload gesetzt ist, verwendet das Profil diese Paarliste

### `timers`

Gedacht fuer:

- Einschaltverzoegerungen
- Freigaben nach Wartezeit

Unterstuetzt:

- `when`
- `delay_ms`
- `output_signal`
- `reset_when_false`

### `state_machines`

Gedacht fuer:

- einfache endliche Automaten

Unterstuetzt:

- `initial_state`
- `states`
- `transitions`
- `set_internal`

## Pipe-Protokoll

Aktuell unterstuetzte Aktionen:

- `hello`
- `set_input`
- `get_signal`
- `read_state`
- `send_interface`
- `read_interface`
- `set_waveform`
- `read_waveform`
- `reset`
- `shutdown`

## Waveform-Unterstuetzung

`set_waveform` wird genutzt, wenn das CT3xx-Programm einen `2ARB`-/AM2-Stimulus anlegt.

Wichtig:

- der Name des Waveform-Signals muss zu einem auswertbaren Eingang passen
- das Modell kann direkt auf Form und Kennwerte reagieren

Zusatzoptionen fuer `set_waveform`:

- `observe_signals`
- `capture_signals`
- `capture_sample_count`
- `capture_duration_ms`

Damit kann das DUT direkt beim Anlegen einer Signalform Antworten oder Verlaufsdaten zurueckgeben.

## Was im `read_state` sichtbar sein sollte

Der Simulator verwendet den Zustand fuer:

- Live-Zustandsfenster
- Debugging
- Fehleranalyse

Sinnvoll sind deshalb:

- `inputs`
- `sources`
- `outputs`
- `internal`
- optional Schnittstellenzustand

## Typische Fehlerquellen

- Signalname im DUT stimmt nicht mit Signaltabelle/WireViz ueberein
- Versorgungssignal ist im Profil nicht als `source` definiert
- Waveform liegt auf einem Signal, das im Profil nie ausgewertet wird
- YAML-Profil verwendet `PyYAML`, aber das Paket ist lokal nicht installiert
- Python-Modul liefert keinen stabilen `read_state()`-Inhalt

## Empfehlung

- einfache Gut-/Schlecht-/Grenzfall-Varianten als JSON/YAML
- freie oder komplexe Logik als Python-Modul
- Signale fachlich benennen und nicht zwischen `IN`, `OUT`, `INPUT`, `OUTPUT` und echten Signalnamen mischen
