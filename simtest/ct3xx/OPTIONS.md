# simtest/ct3xx Optionen

Diese Datei beschreibt, was im CT3xx-Programmordner fuer den Simulator relevant ist, welche Dateien erwartet werden und welche Testtypen aktuell wirklich vom Simulator ausgewertet werden.

## Zweck des Ordners

Der Ordner enthaelt das eigentliche CT3xx-Testprogramm und die dazugehoerigen CT3xx-Nebenformate.

Typisch:

- `*.ctxprg`
- `Signaltable.ctsit`
- optional `*.ctarb`
- optional weitere CT3xx-Dateien wie `*.ctict`, `*.ctdig`, `*.ctbrd`

## Was Pflicht ist

Fuer einen normalen Simulationslauf sollten vorhanden sein:

- genau ein Hauptprogramm `*.ctxprg`
- eine passende `Signaltable.ctsit`

Wenn mehrere `*.ctxprg`-Dateien im Ordner liegen:

- die Desktop-App fordert den Bediener zur Auswahl auf

## Wichtige Dateien

### `*.ctxprg`

Enthaelt die Testschritte und deren Parameter.

Der Simulator wertet aktuell explizit aus:

- `SC2C`
  Scanner-/Versorgungs-Schritt
- `CDMA`
  Mess-/Auswerteschritt mit Grenzwerten
- `IOXX`
  digitale Ausgangsansteuerung
- `GSD^`
  Variablenzuweisung
- `ECLL`
  Start externer Dateien/Skripte
- `PWT$`
  Wartezeit
- `2ARB`
  Waveform-/AM2-Stimulus

Andere Testtypen werden nicht zwingend voll fachlich simuliert. Sie koennen aber trotzdem geladen und als generische Schritte ausgefuehrt werden.

### `Signaltable.ctsit`

Die Signaltabelle verbindet CT3xx-Modulkanalnamen mit den logischen Signalnamen, die der Simulator weiterverwendet.

Wichtig:

- Namen wie `VCC_Plus`, `ADC_IN`, `DIG_OUT`, `UIF_OUT1` muessen hier korrekt eingetragen sein
- diese logischen Namen muessen zu `WireViz`, `simulation.yaml` und DUT-Modell passen

## Was bei Namen beachtet werden muss

- Die Signaltabelle ist die fachliche Quelle fuer die Signalnamen.
- Der Simulator versucht diese Namen entlang von WireViz und Assembly-Pfaden zu erhalten.
- Generische Namen wie `IN`, `OUT`, `INPUT`, `OUTPUT` sind als Baugruppen-/Portnamen okay, sollten aber nicht die eigentlichen logischen Signalnamen ersetzen.

Empfehlung:

- CT3xx-Signal in `Signaltable.ctsit`: fachlicher Name
- WireViz-Testsystemseite: physischer Pinname plus passender `pinlabel`
- DUT-Seite: nur dort eigene lokale Pinnamen verwenden, wo es fachlich sinnvoll ist

## Unterstuetzte Testtyp-spezifische Hinweise

### `SC2C`

Gedacht fuer:

- Versorgung einschalten
- Scanner-/Systemsignale setzen

Darauf achten:

- das Zielsignal muss ueber Signaltabelle und Verdrahtung aufloesbar sein
- wenn eine DUT-Versorgung gesetzt werden soll, muss diese im DUT-Modell als `source` oder passendes Eingangssignal vorhanden sein

### `CDMA`

Gedacht fuer:

- analoge/digitale Messung mit Unter-/Obergrenzen

Darauf achten:

- Signalname muss zum Messpfad passen
- Einheit und Grenzen muessen fachlich zur Rueckgabe passen

### `IOXX`

Gedacht fuer:

- digitale Ansteuerung von Tester-Ausgaengen wie UIF-Outputs

Wichtige Parameter:

- `Direction='Send'`
- `ChannelName='...'`
- `Out='H'` oder `Out='L'`
- optional `RelayState='On'/'Off'`
- optional `WaitTime='...'`

Wichtig:

- `IOXX` schreibt keinen Messwert ins DUT, sondern steuert einen Ausgang des Pruefsystems
- die elektrische Interpretation von `H`/`L` sollte ueber `tester_output` in `simulation.yaml` beschrieben werden

### `GSD^`

Gedacht fuer:

- Variablen setzen

Wichtig:

- das ist keine physische Pin-Ansteuerung
- nicht verwenden, wenn fachlich ein Tester-Ausgang oder Relais geschaltet werden soll

### `ECLL`

Gedacht fuer:

- externe Datei, Script oder Executable ausfuehren

Wichtige Punkte:

- `ExeFileName` muss aufloesbar sein
- bei Exit-Code-Auswertung sollte `WaitForFinish` aktiv sein
- der Simulator kann Exit-Codes auswerten und als Schrittresultat verwenden

### `PWT$`

Gedacht fuer:

- Wartezeiten

Wichtig:

- wirkt auf die simulierte Zeit
- kann Relaisverzoegerungen, Slew, Timer oder Waveform-Captures beeinflussen

### `2ARB`

Gedacht fuer:

- AM2-/Waveform-Stimuli

Wichtig:

- referenzierte `.ctarb`-Datei muss vorhanden sein
- Stimulus wird an Python-DUT oder YAML-DUT weitergegeben
- Capture-/Response-Auswertung kann parallel mitlaufen

## Empfohlene Ordnerregeln

- pro Szenario nur ein aktives Hauptprogramm im Ordner, wenn keine manuelle Auswahl gewuenscht ist
- `Signaltable.ctsit` immer mit dem Programm zusammen versionieren
- CT3xx-Dateien nicht hart ueber absolute Pfade referenzieren, wenn das Szenario portabel bleiben soll

## Typische Fehlerquellen

- mehrere `*.ctxprg`-Dateien ohne klare Auswahl
- Signaltabellennamen passen nicht zu WireViz oder DUT
- `IOXX` wird benutzt, aber es gibt keine passende `tester_output`-Definition
- `2ARB` referenziert fehlende `.ctarb`
- `ECLL` erwartet Skriptdateien, die relativ zum Programmordner nicht gefunden werden
