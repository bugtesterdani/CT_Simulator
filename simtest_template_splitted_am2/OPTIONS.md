# simtest_template_splitted_am2 Optionen

## Relevante Pfade

- Testprogramm: `testprogramme/template_splitted_am2`
- Verdrahtung: `simtest_template_splitted_am2/wireplan`
- Simulation: `simtest_template_splitted_am2/wireplan`
- DUT: `simtest/device/devices/template_splitted_am2_led_analyzer.py`

## Verdrahtungsannahmen

- `DUT_HV` kommt vom Testsystem und wird ueber `tester_output` auf `24 V` oder `0 V` gesetzt
- `GND` ist die gemeinsame Masse
- die drei `2ARB`-Kanaele werden auf drei Karteninstanzen abgebildet:
  - `AM2/1`
  - `AM2/2`
  - `AM2/3`
- pro Karteninstanz werden dieselben Frontsignale verwendet:
  - `BNC + A-IO 3` als ARB-Ausgang
  - `BNC + A-IO 8` als SCO-Ruecklesepfad

## DUT-Verhalten

- `WAVE_IN_1`, `WAVE_IN_2`, `WAVE_IN_3` nehmen die drei vom `2ARB`-Schritt gesetzten Signalformen an
- `WAVE_OUT_1`, `WAVE_OUT_2`, `WAVE_OUT_3` spiegeln diese Signalformen als beobachtbare Ruecksignale
- Default-Ruecksignal je Phase: `0.2 V`
- bei aktiver Hilfsversorgung und einem Phasenwert `>= 0.5 V` geht der jeweilige Rueckkanal auf `1.3 V`
- `DUT_HV` steuert die Kommunikationsantwort der Schnittstelle `Interface LED Analyzer`

## 2ARB-Ablauf

- der 2ARB-Stimulus wird zuerst gestartet
- anschliessend laufen die Split-Untertests innerhalb des aktiven Stimulus
- die 2ARB-Auswertung wird danach aus den waehrenddessen aufgenommenen Ruecksignal-Samples gebildet

## Ableitung

- neue Device-Module in Beispielszenarien sollen auf der Basis von `simtest/device` erstellt werden
- dieses Beispiel verwendet deshalb dieselbe gemeinsame `main.py`-/`core`-Struktur und fuegt nur ein neues Modul hinzu

## Erwartete Resultate

- erste LED-Auswertung: alle vier Werte `0`
- zweite LED-Auswertung: `15,1,25,40`
