# Optionen fuer `simtest_template_sm2/wireplan`

## Relevante Dateien

- `Verdrahtung.yml`
- `simulation.yaml`

## Wichtige Punkte

- `REL_HV` und `GND` sind direkt vom Testsystem auf das Device gefuehrt.
- `simulation.yaml` beschreibt die testerseitige Pegelabbildung fuer `REL_HV`.
- `REL_HV` ist als `tester_output` modelliert.
- `SM2_P_24V` ist als `tester_supply` mit `24.0 V` deklariert.
