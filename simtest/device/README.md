# simtest/device

Dies ist die gemeinsame zentrale Geraetebibliothek fuer die Simulator-Beispiele in diesem Repository.

Neue Szenarien sollen hierauf aufbauen und keine eigene parallele Device-Runtime mehr mitbringen. Zulaessig sind pro neuem Szenario nur noch zusaetzliche:

- Python-Geraetemodule unter `devices/*.py`
- deklarative Profile unter `devices/*.json`, `devices/*.yaml`, `devices/*.yml`

Diese Beispielstruktur unterstuetzt zwei Varianten fuer DUT-Simulationen:

- Python-Geraetemodule unter `devices/*.py`
- deklarative Geraeteprofile unter `devices/*.json`, `devices/*.yaml` oder `devices/*.yml`

Hinweis:

- fuer die Pipe-Anbindung wird `pywin32` benoetigt
- fuer YAML-Profile wird zusaetzlich `PyYAML` benoetigt

## Startvarianten

Python-Modul:

```powershell
py -3.13 .\main.py IKI_xx --pipe \\.\pipe\ct3xx-demo
```

Deklaratives Profil:

```powershell
py -3.13 .\main.py --profile .\devices\IKI_good.json --pipe \\.\pipe\ct3xx-demo
```

Komplexeres YAML-Profil:

```powershell
py -3.13 .\main.py --profile .\devices\IKI_complex.yaml --pipe \\.\pipe\ct3xx-demo
```

Waveform-Beispielprofil:

```powershell
py -3.13 .\main.py --profile .\devices\IKI_waveform.yaml --pipe \\.\pipe\ct3xx-demo
```

## Ziel des Formats

Das deklarative Profil ist fuer DUT-Varianten gedacht, die ohne eigenes Python-Modul beschrieben werden koennen:

- mehrere Eingaenge
- mehrere Ausgaenge
- Versorgungsvoraussetzungen
- Kennlinien
- zeitliches Verhalten
- Waveform-Stimuli ueber die Pipe
- Response-Captures waehrend einer angelegten Signalform
- einfache Kommunikationsschnittstellen

Wenn das Verhalten frei programmierbar sein muss, bleibt ein normales Python-Geraetemodul weiter moeglich.

## Unterstuetzte Top-Level-Felder

```yaml
name: IKI_complex

signals:
  inputs: [ADC_IN, TEMP_IN, MODE_SEL]
  outputs: [DIG_OUT, STATUS_OUT]
  sources: [VCC_PLUS]
  internal: [WAVE_A]

source_control:
  minimums:
    VCC_PLUS: 19.5

initial_inputs:
  MODE_SEL: 0

initial_sources:
  VCC_PLUS: 0

initial_internal:
  WAVE_A: 0.0

input_curves:
  WAVE_A:
    mode: linear
    points:
      - time_ms: 0
        value: 0.0
      - time_ms: 100
        value: 1.2
      - time_ms: 200
        value: 3.3

outputs:
  DIG_OUT:
    default: 0.0
    rules:
      - when:
          all:
            - source: VCC_PLUS
              gte: 19.5
            - input: MODE_SEL
              gte: 1
        transfer_curve:
          input: ADC_IN
          mode: linear
          points:
            - x: 0.0
              y: 0.0
            - x: 3.3
              y: 3.3
        delay_ms: 40
        slew_per_ms: 0.05

interfaces:
  UART1:
    default_response: "ERR"
    requests:
      - when:
          contains: "PING"
        response: "PONG"
```

Optional zusaetzlich:

```yaml
timers:
  READY_DELAY:
    when:
      input: ADC_IN
      gte: 2.5
    delay_ms: 50
    output_signal: TIMER_READY

state_machines:
  MODE:
    initial_state: IDLE
    states:
      IDLE:
        transitions:
          - when:
              signal: TIMER_READY
              gte: 1
            to: ACTIVE
      ACTIVE:
        set_internal:
          MODE_ACTIVE_LEVEL: 1
```

Waveform-Stimuli werden zur Laufzeit per Pipe mit `set_waveform` auf deklarierte `input`-Signale gelegt. Das Profil kann diese Signale dann wie normale Eingangsverlaeufe auswerten.

## Signale

`signals` trennt die Rollen:

- `inputs`: vom Pruefsystem gesetzte Signale
- `outputs`: vom Geraetemodell berechnete Signale
- `sources`: Versorgungssignale wie `VCC_PLUS`
- `internal`: interne Hilfssignale oder Zustandswerte

Alle Namen werden intern gross geschrieben behandelt.

## Anfangswerte

Mit `initial_inputs`, `initial_sources` und `initial_internal` koennen Startwerte gesetzt werden. Fehlt ein Wert, startet das Signal mit `0.0`.

## Versorgungsfreigabe

`source_control.minimums` definiert Mindestwerte fuer Quellen. Solange eine benoetigte Quelle unterhalb des Grenzwerts liegt, liefern Ausgaenge `0.0`.

## Eingangskurven

`input_curves` erzeugt zeitabhaengige Werte. Das ist fuer Rampen, Einschwingverhalten oder simulierte Sensorsignale gedacht.

Unterstuetzte Felder:

- `points`: Liste aus `{ time_ms, value }` oder `[x, y]`
- `mode: hold`: letzter Stuetzwert bleibt bis zum naechsten Punkt stehen
- `mode: linear`: lineare Interpolation zwischen den Punkten

Ein Eingang mit manuell gesetztem Wert wird nicht mehr durch die Kurve ueberschrieben.

## Waveform-Stimuli

Fuer jedes per Pipe angelegte Waveform-Signal publiziert das deklarative Modell zusaetzliche interne Signale:

- `WF_<SIGNAL>_CURRENT`
- `WF_<SIGNAL>_PEAK`
- `WF_<SIGNAL>_RMS`
- `WF_<SIGNAL>_AVG`
- `WF_<SIGNAL>_MIN`
- `WF_<SIGNAL>_MAX`
- `WF_<SIGNAL>_IS_DC`
- `WF_<SIGNAL>_IS_SQUARE`
- `WF_<SIGNAL>_IS_SINE_LIKE`
- `WF_<SIGNAL>_IS_CUSTOM`

Damit koennen Regeln direkt auf Signalform und Kenngroessen reagieren.

Beispiel:

```yaml
outputs:
  DIG_OUT:
    default: 0.0
    rules:
      - when:
          all:
            - source: VCC_PLUS
              gte: 19.5
            - signal: WF_ADC_IN_IS_SINE_LIKE
              gte: 1
        transfer_curve:
          input: ADC_IN
          mode: linear
          points:
            - x: -1.0
              y: -0.5
            - x: 0.0
              y: 0.0
            - x: 1.0
              y: 0.5
```

`set_waveform` kann ausserdem `observe_signals` und `capture_signals` mitgeben. Das Modell liefert dann direkt im Response aktuelle Werte und einen vorausberechneten Verlauf fuer diese Signale.

## Ausgangsregeln

Jeder Ausgang kann einen `default`-Wert und `rules` haben.

Eine Regel darf entweder:

- einen festen `value` setzen
- oder ueber `transfer_curve` eine Kennlinie auswerten

Zusatzfelder:

- `delay_ms`: Zielwert wird erst nach dieser Zeit aktiv
- `slew_per_ms`: Ausgang faehrt mit begrenzter Steigung auf den Zielwert

### Bedingungen

`when` unterstuetzt:

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

Beispiel:

```yaml
when:
  all:
    - source: VCC_PLUS
      gte: 19.5
    - input: TEMP_IN
      lte: 80
```

## Kennlinien

`transfer_curve` bildet einen Eingang auf einen Ausgang ab.

Unterstuetzte Felder:

- `input` oder `signal`
- `points`
- `mode: linear`
- `mode: hold`

Beispiel:

```yaml
transfer_curve:
  input: ADC_IN
  mode: linear
  points:
    - x: 0.0
      y: 0.0
    - x: 1.5
      y: 1.5
    - x: 3.3
      y: 3.3
```

## Kommunikationsschnittstellen

`interfaces` definiert einfache, deklarative Antwortmuster fuer Protokolle wie UART, CAN oder serielle Kommandos. Das ist keine vollstaendige Bus-Simulation, aber ausreichend fuer viele Antwortmuster.

Unterstuetzte Felder:

- `default_response`
- `requests`
- `when.equals`
- `when.contains`
- `response`

Beispiel:

```yaml
interfaces:
  UART1:
    default_response: "ERR"
    requests:
      - when:
          contains: "PING"
        response: "PONG"
      - when:
          equals: "READ:STATUS"
        response: "STATUS=OK"
```

Zur Laufzeit merkt sich das Profil pro Schnittstelle:

- letzte Anfrage
- letzte Antwort
- Verlauf der Anfragen

## Timer und einfache Zustandsautomaten

`timers` erzeugt interne Signalschalter nach einer Wartezeit.

- `when`
- `delay_ms`
- `output_signal`
- `reset_when_false`

Nach Ablauf der Zeit wird das interne Signal `1.0`.

`state_machines` erlaubt einfache endliche Automaten.

- `initial_state`
- `states`
- `transitions`
- `set_internal`

Fuer jeden Zustand wird automatisch ein internes Flag gesetzt:

- `STATE_<MACHINE>_<STATE>` = `1.0` fuer den aktiven Zustand
- alle anderen Zustandsflags der Maschine = `0.0`

## Beispiele

- `devices/IKI_good.json`: einfacher, guter DUT
- `devices/IKI_bad.json`: einfacher, fehlerhafter DUT
- `devices/IKI_complex.yaml`: mehrere Eingaenge, Kennlinien, Zeitverhalten und Schnittstellen
- `devices/IKI_waveform.yaml`: waveform-basierter DUT mit Signalform-Erkennung und Response-Kurve
- `devices/template_sm2_led_analyzer.py`: gemeinsames Python-Modul fuer das `template_SM2`-Beispiel
- `devices/template_splitted_am2_led_analyzer.py`: gemeinsames Python-Modul fuer das `template_splitted_am2`-Beispiel
- `devices/TrafoStromwandler_good.yaml`: gemeinsames deklaratives Profil fuer das Transformator-/Stromwandler-Beispiel

## Idee

Damit koennen mehrere aehnliche DUT-Varianten schnell ueber Dateien beschrieben werden, ohne fuer jede Abweichung ein neues Python-Modul schreiben zu muessen. Gleichzeitig bleibt Python als Escape-Hatch fuer komplexeres Verhalten erhalten.
