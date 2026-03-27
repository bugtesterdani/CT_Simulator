# simtest_template_splitted_am2

End-to-End-Beispiel fuer das reale Testprogramm `testprogramme/template_splitted_am2/template_splitted_am2.ctxprg`.

Enthalten sind:

- `wireplan/Verdrahtung.yml`
- `wireplan/simulation.yaml`
- gemeinsames Device-Framework unter `simtest/device`
- Python-Modul `simtest/device/devices/template_splitted_am2_led_analyzer.py`
- YAML-Profil `simtest/device/devices/template_splitted_am2_led_analyzer.yaml`

Die Device-Struktur ist dabei bewusst auf die gemeinsame Basis aus `simtest/device` zusammengezogen.

## Fachliches Verhalten

- Der `2ARB`-Schritt legt drei Waveform-Kanaele an.
- Diese werden im Beispiel auf drei AM2-Karteninstanzen modelliert:
  - `AM2/1`
  - `AM2/2`
  - `AM2/3`
- Das simulierte Geraet bekommt daraus drei Eingangsphasen:
  - `WAVE_IN_1`
  - `WAVE_IN_2`
  - `WAVE_IN_3`
- und spiegelt diese auf:
  - `WAVE_OUT_1`
  - `WAVE_OUT_2`
  - `WAVE_OUT_3`
- Der Stimulus bleibt waehrend des kompletten Split-Unterablaufs aktiv.
- Zuerst wird die 2ARB-Waveform angelegt, danach laufen die Untertests innerhalb des aktiven Stimulus.
- Erst danach wird die 2ARB-Auswertung aus den aufgenommenen Ruecksignal-Samples gebildet.
- Das Python-Geraetemodell liefert standardmaessig `0.2 V` als Ruecksignal.
- Wenn bei aktiver Hilfsversorgung eine Phase mindestens `0.5 V` erreicht, steigt der jeweilige Rueckkanal auf `1.3 V`.
- Im ersten Split-Abschnitt `LED Auswertung` ist `DUT_HV` noch aus:
  - Antwort der Schnittstelle: `0,0,0,0`
  - die erste `PET$`-Auswertung muss damit auf `PASS` laufen
- Danach schaltet `IOXX` `DUT_HV` ein.
- Im zweiten Split-Abschnitt `LED Auswertung` antwortet das Geraet mit:
  - `15,1,25,40`
  - die zweite `PET$`-Auswertung muss damit auf `PASS` laufen
- Der letzte `IOXX`-Schritt schaltet `DUT_HV` wieder aus.

## Verwendung

- Testprogramm-Ordner: `testprogramme/template_splitted_am2`
- Verdrahtungs-Ordner: `simtest_template_splitted_am2/wireplan`
- Simulations-Ordner: `simtest_template_splitted_am2/wireplan`
- Python-Geraeteskript: `simtest/device/devices/template_splitted_am2_led_analyzer.py`
- alternatives YAML-Geraeteprofil: `simtest/device/devices/template_splitted_am2_led_analyzer.yaml`

Die Desktop-App startet daraus automatisch die gemeinsame Bibliothek `simtest/device/main.py`, entweder mit dem Python-Modul `template_splitted_am2_led_analyzer` oder mit dem gleichwertigen YAML-Profil.

## Wichtiger Unterschied

Dieses Testprogramm verwendet einen echten Split-Ablauf innerhalb des `2ARB`-Tests. Die nachgelagerten `Group`-, `E488`-, `PET$`- und `IOXX`-Schritte sind deshalb keine Root-Schritte, sondern Untereintraege des `2ARB`-Tests.

Zusätzlich beschreibt die `.ctarb`-Datei drei Waveform-Kanaele. Der Simulator ordnet diese im Beispiel deterministisch auf drei Karteninstanzen `AM2/1`, `AM2/2`, `AM2/3` ab.

## Hinweis zur Device-Struktur

Neue Beispielszenarien sollen fuer die Geraetesimulation immer auf der gemeinsamen `simtest/device`-Basis aufbauen. Dieses Beispiel enthaelt daher nur das zusaetzliche neue Geraetemodul fuer `template_splitted_am2`, keine komplett abweichende proprietaere Device-Architektur.
