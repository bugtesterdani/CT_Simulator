# simtest_template_sm2

Vollstaendiges End-to-End-Beispiel fuer das vorhandene Testprogramm `template_SM2`.

Enthalten sind:

- `ct3xx/template_SM2.ctxprg`
- `wireplan/Verdrahtung.yml`
- `wireplan/simulation.yaml`
- gemeinsames Device-Framework unter `simtest/device`
- Python-Modul `simtest/device/devices/template_sm2_led_analyzer.py`

## Fachliches Verhalten

- `REL_HV = LOW`:
  - LED-Analyzer antwortet mit `0,0,0,0`
  - die vier `PET$`-Auswertungen muessen damit auf `PASS` laufen
- `REL_HV = HIGH`:
  - LED-Analyzer antwortet mit `15,1,25,40`
  - die vier `PET$`-Auswertungen muessen damit auf `PASS` laufen

## Verwendung in der Desktop-App

- Testprogramm-Ordner: `simtest_template_sm2/ct3xx`
- Verdrahtungs-Ordner: `simtest_template_sm2/wireplan`
- Simulations-Ordner: `simtest_template_sm2/wireplan`
- Python-Geraeteskript: `simtest/device/devices/template_sm2_led_analyzer.py`

Die Desktop-App startet daraus automatisch die gemeinsame Bibliothek `simtest/device/main.py` mit dem Modul `template_sm2_led_analyzer`.

## Ablauf

Das Testprogramm schaltet `REL_HV` per `IOXX`, startet zusaetzlich `waiter.bat` per `ECLL`, wartet mit `PWT$` und fragt den LED-Analyzer per `E488` mit `LSENS3,2,200?` ab.

Wichtig dabei:

- `E488` ist hier die Kommunikationsabfrage der Schnittstelle `Interface LED Analyzer`
- die Rueckantwort wird in `Result` geschrieben
- danach wertet `PET$` `Result[1]` bis `Result[4]` aus

Die LED-Helligkeit selbst wird also nicht direkt von `E488` gemessen, sondern ueber die Kommunikationsantwort der simulierten Schnittstelle beschrieben.
