# Optionen fuer `simtest_template_sm2`

## Ordnerstruktur

- `ct3xx/`: Testprogramm und CT3xx-Tabellen
- `wireplan/`: Hauptverdrahtung und Simulationsmodell
- `simtest/device/`: gemeinsame Python-/Profil-Bibliothek fuer DUT-Modelle

## Erwartete Einstellungen

- Das Testprogramm muss `template_SM2.ctxprg` verwenden.
- Der Verdrahtungs- und Simulationsordner zeigen beide auf `wireplan/`.
- In der Desktop-App wird als Python-Datei `simtest/device/devices/template_sm2_led_analyzer.py` ausgewaehlt.
- Intern startet die App dafuer automatisch `simtest/device/main.py` mit dem Modul `template_sm2_led_analyzer`.

## Wichtige Punkte

- `E488` verwendet den Interface-Namen `Interface LED Analyzer`.
- `E488` ist hier als Kommunikationsschritt zu verstehen, nicht als direkter Messschritt.
- `REL_HV` wird als testerseitiger Digital-Ausgang ueber `tester_output` auf `24 V` oder `0 V` abgebildet.
- `waiter.bat` liegt im Testprogrammordner, weil das CT3xx-Programm es relativ ueber `TestProgramPath()` startet.
