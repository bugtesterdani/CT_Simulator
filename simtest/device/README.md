# simtest/device

Dies ist die gemeinsame zentrale Geraetebibliothek fuer die Simulator-Beispiele in diesem Repository.

Neue Szenarien sollen hierauf aufbauen und keine eigene parallele Device-Runtime mehr mitbringen. Zulaessig sind pro neuem Szenario nur noch zusaetzliche:

- Python-Geraetemodule unter `devices/*.py`
- deklarative Profile unter `devices/*.json`, `devices/*.yaml`, `devices/*.yml`

Diese Beispielstruktur unterstuetzt zwei Varianten fuer DUT-Simulationen:

- Python-Geraetemodule unter `devices/*.py`
- deklarative Geraeteprofile unter `devices/*.json`, `devices/*.yaml` oder `devices/*.yml`
- einfache Last- und Stromaufnahmeprofile fuer Versorgungstests wie `SMUD`

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
- deklarative I2C-Busse und I2C-Slave-Geraete
- deklarative SPI-Busse und SPI-Slave-Geraete

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

I2C kann ebenfalls deklarativ beschrieben werden:

```yaml
interfaces:
  UIF1_FRONT_CONNECTOR:
    protocol: i2c
    required_supply_v: 5.0
    master_readback: echo
    devices:
      LM75:
        kind: lm75
        address: 0x49
        temperature_c: 25.0
```

Optional zusaetzlich:

```yaml
aliases:
  signals:
    WAVE_IN_1:
      - ARB_IN_1
      - AM2/1 BNC + A-IO 3
  interfaces:
    INTERFACE LED ANALYZER:
      - LED_ANALYZER

derived_signals:
  LED_ON:
    compare:
      signal: DUT_HV
      gte: 12.0
      true_value: 1.0
      false_value: 0.0

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

## Alias-Namen

`aliases` erlaubt mehrere externe Namen fuer ein kanonisches Signal oder Interface.

- `aliases.signals`
- `aliases.interfaces`

Damit koennen WireViz-Pinlabels, generische Namen und fachliche DUT-Signale auf denselben internen Namen zeigen.

Beispiel:

```yaml
aliases:
  signals:
    WAVE_OUT_1:
      - SCO_OUT_1
      - AM2/1 BNC + A-IO 8
```

## Deklarative I2C-Busse

Wenn unter `interfaces` ein Eintrag `protocol: i2c` setzt, behandelt die Runtime `send_interface(...)` als I2C-Transaktion statt als einfache Textantwort.

Wichtig:

- das CT3xx-Testsystem bleibt dabei der I2C-Master
- das deklarative Profil beschreibt nur das externe I2C-Slave-Geraet auf diesem Bus
- fuer die Referenzprogramme `UIF I2C Test`, `EA3 I2C Test` und `EA3-R I2C Test` ist das ein `LM75`-Slave

Aktuell unterstuetzt:

- Adressphase fuer Read/Write
- `StartCond` / `EndCond`
- `Ack='Read'`
- `Ack='Write'`
- `Ack='No Ack'`
- mehrere aufeinanderfolgende Record-Schritte innerhalb eines `2C2I`
- `required_supply_v`
- `master_readback: echo`
- `master_readback: fixed`
- `devices` mit `kind: lm75`
- per-Testlauf persistenten Registerzustand pro I2C-Slave
- `initial_pointer`
- `initial_registers`

Wichtig:

- der I2C-Slave-State bleibt ueber den kompletten Testlauf erhalten
- Registerschreibvorgaenge gehen erst bei `reset()` oder beim Start eines neuen Testlaufs verloren
- damit koennen I2C-DUTs jetzt wie beim SPI ueber echte Register-/Speicherwerte modelliert werden

Referenzprofile:

- [i2c_lm75_good.yaml](C:/Users/hello/Desktop/CT3xx/simtest/device/devices/i2c_lm75_good.yaml)
- [i2c_lm75_fail.yaml](C:/Users/hello/Desktop/CT3xx/simtest/device/devices/i2c_lm75_fail.yaml)
- [i2c_lm75_error.yaml](C:/Users/hello/Desktop/CT3xx/simtest/device/devices/i2c_lm75_error.yaml)

## Deklarative SPI-Busse

Wenn unter `interfaces` ein Eintrag `protocol: spi` setzt, behandelt die Runtime `send_interface(...)` als SPI-Transaktion.

Wichtig:

- das CT3xx-Testsystem bleibt dabei der SPI-Master
- das deklarative Profil beschreibt nur das externe SPI-Slave-Geraet auf diesem Bus
- Takt, Phase, Polarity, Chip-Select-Aktivlevel und Versorgung koennen direkt validiert werden

Aktuell unterstuetzt:

- `protocol: spi`
- `required_supply_v`
- `clock_phase`
- `clock_polarity`
- `chip_select_active`
- `frequency_hz`
- `devices`
- per-Testlauf persistenten Speicher
- `WREN`
- Status-Register-Lesen
- `READ`
- `WRITE`
- Busy-/Write-Delay
- Page-Boundaries
- Write-Protect

Referenzprofile:

- [spi_cat25128_good.yaml](C:/Users/hello/Desktop/CT3xx/simtest/device/devices/spi_cat25128_good.yaml)
- [spi_cat25128_fail.yaml](C:/Users/hello/Desktop/CT3xx/simtest/device/devices/spi_cat25128_fail.yaml)
- [spi_cat25128_error.yaml](C:/Users/hello/Desktop/CT3xx/simtest/device/devices/spi_cat25128_error.yaml)
- [spi_93c46b_good.yaml](C:/Users/hello/Desktop/CT3xx/simtest/device/devices/spi_93c46b_good.yaml)

## Deklarative SMUD-Lastprofile

Fuer `SMUD`-Referenzszenarien koennen dieselben Profile als einfache Lastmodelle verwendet werden.

Wichtig:

- das CT3xx-Testsystem schreibt die Versorgung auf einen normalen DUT-Eingang wie `DUT_SUPPLY`
- das Profil liefert den gemessenen Strom ueber ein normales Ausgangssignal wie `DUT_CURRENT`
- dadurch bleiben Verdrahtung, Snapshot-Analyse und Live-Zustand im normalen Signalmodell

Referenzprofile:

- [smud_boundary_scan_good.yaml](C:/Users/hello/Desktop/CT3xx/simtest/device/devices/smud_boundary_scan_good.yaml)
- [smud_boundary_scan_fail.yaml](C:/Users/hello/Desktop/CT3xx/simtest/device/devices/smud_boundary_scan_fail.yaml)
- [smud_boundary_scan_error.yaml](C:/Users/hello/Desktop/CT3xx/simtest/device/devices/smud_boundary_scan_error.yaml)

Referenzmodul:

- [smud_boundary_scan_adapter.py](C:/Users/hello/Desktop/CT3xx/simtest/device/devices/smud_boundary_scan_adapter.py)

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

## Abgeleitete interne Signale

`derived_signals` berechnet interne Hilfssignale ohne eigenes Python-Modul.

Aktuell unterstuetzt:

- `compare`
- `dominant_signal_window`

### `compare`

Setzt ein internes Signal anhand eines Schwellwertvergleichs.

Beispiel:

```yaml
derived_signals:
  LED_ON:
    compare:
      signal: DUT_HV
      gte: 12.0
      true_value: 1.0
      false_value: 0.0
```

### `dominant_signal_window`

Prueft, ob ein Signal ueber ein Zeitfenster gegenueber mehreren Vergleichssignalen dominant ist.

Unterstuetzte Felder:

- `signal`
- `peers`
- `window_ms`
- `metric`
- `min_metric`
- `min_delta`
- `true_value`
- `false_value`

Unterstuetzte Metriken:

- `peak_or_rms_abs`
- `peak_abs`
- `rms_abs`
- `avg_abs`

Beispiel:

```yaml
derived_signals:
  L3_OVERVOLTAGE:
    dominant_signal_window:
      signal: WAVE_IN_3
      peers: [WAVE_IN_1, WAVE_IN_2]
      window_ms: 40
      metric: peak_or_rms_abs
      min_metric: 0.2
      min_delta: 0.01
```

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
- `state_when`
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
      - when:
          equals: "LSENS3,2,200?"
        state_when:
          signal: LED_ON
          gte: 1
        response: "15,1,25,40"
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
- `devices/template_splitted_am2_led_analyzer.yaml`: deklaratives Profil fuer dasselbe `template_splitted_am2`-Verhalten
- `devices/TrafoStromwandler_good.yaml`: gemeinsames deklaratives Profil fuer das Transformator-/Stromwandler-Beispiel
- `devices/spi_cat25128_device.py`: Python-Wrapper fuer das deklarative SPI-CAT25128-Referenzgeraet
- `devices/spi_93c46b_device.py`: Python-Wrapper fuer das deklarative SPI-93C46B-Referenzgeraet
- `devices/spi_cat25128_good.yaml`: deklaratives SPI-CAT25128-Gutprofil
- `devices/spi_cat25128_fail.yaml`: deklaratives SPI-CAT25128-Failprofil
- `devices/spi_cat25128_error.yaml`: deklaratives SPI-CAT25128-Errorprofil
- `devices/spi_93c46b_good.yaml`: deklaratives SPI-93C46B-Referenzprofil
- `devices/noop_device.yaml`: neutrales Dummy-Geraetemodell fuer Szenarien, die in der Desktop-App ein Pflicht-Geraetemodell brauchen, aber keine fachliche DUT-Logik benoetigen

## Idee

Damit koennen mehrere aehnliche DUT-Varianten schnell ueber Dateien beschrieben werden, ohne fuer jede Abweichung ein neues Python-Modul schreiben zu muessen. Gleichzeitig bleibt Python als Escape-Hatch fuer komplexeres Verhalten erhalten.
