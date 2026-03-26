# simtest_template_splitted_am2 Wireplan Optionen

## Verwendete Signale

- `DUT_HV`
- `GND`
- `BNC + A-IO 3`
- `BNC + A-IO 8`

## Erwartete Bedeutung

- `DUT_HV`: digitales Testsystem-Signal fuer die Versorgung/Freigabe
- `GND`: gemeinsame Masse
- `BNC + A-IO 3`: AM2-Waveform-Stimulus aus dem `2ARB`-Test
- `BNC + A-IO 8`: AM2-Waveform-Ruecksignal fuer den Scope-Pfad

## Simulation

- `simulation.yaml` definiert `DUT_HV` als `tester_output`
- `HIGH` liefert `24 V`
- `LOW` liefert `0 V`
