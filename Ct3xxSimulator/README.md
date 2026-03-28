# Ct3xxSimulator

Simulationskern fuer CT3xx-Testprogramme.

## Aufgaben

- Ausfuehren von `.ctxprg`-Testablaeufen
- Aufloesen von Signalen gegen Signaltabelle und WireViz
- Einbinden von `simulation.yaml`
- DUT-Kommunikation ueber Python/Named Pipe
- DUT-Profile in `JSON` / `YAML`
- Auswertung von Grenzwerten und Schrittresultaten
- Fault-Injection
- Zustandssnapshots fuer die Desktop-Oberflaeche
- Concurrent-Snapshot-Metadaten fuer parallele Gruppen
- Ausfuehrung externer Testprogramm-Skripte und Exit-Code-Auswertung
- deklarative Tester-Ausgangslogik ueber `tester_supply` / `tester_output`

## Wichtige Bereiche

- `Simulation/Ct3xxProgramSimulator.cs`
  Hauptablauf der Testsimulation
- `Ct3xxSimulator.Modules`
  WireViz-Aufloesung, Fault-Injection und modulnahe Runtime
- `Ct3xxSimulator.Interfaces`
  Pipe-Client, externe DUT-Sitzung und kuenftige Interface-Laufzeiten
- `Ct3xxSimulator.Waveforms`
  gemeinsames Kurvenmodell fuer AM2-/Waveform-Tests
- `Ct3xxSimulator.TestTypes`
  vorbereitete Zielstruktur fuer testtypspezifische Handler

## Architekturhinweis

Der Simulationskern wurde fuer die weitere Umsetzung der `SHORT_ROADMAP` und `SUPPORT_MATRIX` bereits modularisiert:

- Kernablauf bleibt in `Ct3xxSimulator`
- verdrahtungs- und faultnahe Logik liegt in `Ct3xxSimulator.Modules`
- DUT-/Interface-Anbindung liegt in `Ct3xxSimulator.Interfaces`
- Waveform-Bausteine liegen in `Ct3xxSimulator.Waveforms`
- kuenftige Testtyp-Handler sollen in `Ct3xxSimulator.TestTypes` landen

Dadurch kann die weitere Testtypen-Abdeckung schrittweise aus dem monolithischen Kern in kleinere Bausteine ueberfuehrt werden.

## Unterstuetzte Schwerpunkte

- `SC2C`
- `CTCT`
- `SHRT`
- `2C2I`
- `SMUD`
- `SPIX`
- `CDMA`
- `DM30` (referenzbezogene SPI-EEPROM-Teilunterstuetzung)
- `IOXX`
- `E488`
- Datei-/Skriptausfuehrung aus Testprogrammen
- `2ARB` / AM2-Waveform
- Split-Unterablaeufe innerhalb eines Tests, z. B. verschachtelte `Group`-/`IOXX`-/`E488`-/`PET$`-Schritte unter einem `2ARB`
- generische Testschritte mit Ergebnis- und Statuspublikation

## Schnittstellen-Tests

`E488` wird als Kommunikations-Test fuer `RS232`, `IEEE-488` und `VISA` simuliert.

`2C2I` wird jetzt als I2C-Bus-Test simuliert.

`SPIX` wird jetzt als SPI-Bus-Test simuliert.
`SMUD` wird jetzt als DUT-Versorgungs- und Strommess-Test simuliert.

Wichtig:

- das CT3xx-Testsystem agiert dabei als I2C-Master
- externe DUT-Profile modellieren die angesprochenen I2C-Slaves
- fuer die Referenzprogramme `UIF I2C Test`, `EA3 I2C Test` und `EA3-R I2C Test` ist das ein `LM75`-Slave auf dem externen Bus
- I2C-Slaves behalten ihren Registerzustand jetzt ueber den kompletten Testlauf

Aktuell unterstuetzt der Simulationskern dabei:

- Laden der `EXT$`-Interface-Definitionen aus dem CT3xx-Programm
- strukturierte Verarbeitung der `2C2I`-Record-Folge unter `Parameters`
- `StartCond` / `EndCond`
- `Ack='Read'`, `Ack='Write'`, `Ack='No Ack'`
- `ToSend`, `Expected`, `Mask`, `Wait`
- per-Testlauf persistenten Register- und Pointerzustand pro I2C-Slave
- PASS bei erfolgreichem I2C-Ablauf
- FAIL bei Byte-/Maskenabweichungen
- ERROR bei Protokoll- oder Geraetefehlern wie fehlendem ACK
- gemeinsame Nutzung desselben I2C-Stacks fuer `UIF`, `EA3` und `EA3-R`

Fuer SPI gilt:

- das CT3xx-Testsystem agiert immer als SPI-Master
- externe DUT-Profile modellieren die angesprochenen SPI-Slaves
- `EXT$`-Parameter wie `Frequency`, `CLKPhase`, `CLKPolarity`, `CSActive` und Versorgung werden ausgewertet
- echte Leitungen `CS`, `CLK`, `MOSI`, `MISO` werden in Kurven und Verdrahtung sichtbar
- `CAT25128` ist als referenzierter SPI-Slave in Python-/YAML-Profilen umgesetzt
- PASS bei korrektem Readback, FAIL bei ungleichem Readback, ERROR bei Kommunikations-, Timing- oder Versorgungsfehlern
- fuer das DM30-basierte Referenzprogramm `SPI-EEPROM-93C46B` gibt es eine gezielte Teilunterstuetzung fuer den EEPROM-Testlauf

Fuer `SMUD` gilt:

- es gibt genau einen Versorgungskanal pro Laufzeitinstanz
- die Versorgungsspannung wird auf einen normalen DUT-Eingang geschrieben
- die Strommessung wird ueber ein normales Ruecksignal gelesen
- Spannung wird nicht rueckgemessen
- `FAIL` entsteht bei Strom ausserhalb von `MinPermissibleCurrent .. MaxPermissibleCurrent`
- `ERROR` entsteht bei Messfehlern oder wenn der gemessene Strom ueber dem `Fuse`-Strom liegt
- die Umsetzung orientiert sich nur an den in den drei Boundary-Scan-Referenzen real vorhandenen `SMUD`-Feldern

Fuer `CTCT` gilt:

- bewertet wird der aktive DUT-Pfad zwischen den im Test gelisteten Testpunkten
- Relais, Schalter, Widerstaende und Faults aus `simulation.yaml` wirken auf die Messung
- optional zusaetzliche DUT-Widerstaende aus dem Geraetemodell (`ctct.resistances`, `ctct.groups`, `ctct.ring`)
- `K='closed'` bewertet die kleinste gefundene Pfadresistenz gegen eine Maximalgrenze
- `K='open'` bewertet Isolation bzw. fehlende Verbindung gegen eine Minimalgrenze
- `FAIL` steht fuer Toleranzverletzung oder offene Leitung
- `ERROR` steht fuer nicht aufloesbare oder nicht pruefbare Testpunkte

Fuer `SHRT` gilt:

- bewertet werden unerwartete Kurzschluesse zwischen den Testpunkten aus `STP$`
- bekannte Kurzschluesse aus `SSH$` werden ignoriert
- `FAIL` steht fuer unerwartete Shorts unterhalb des Thresholds
- `ERROR` steht fuer nicht aufloesbare Testpunkte
- Messwerte kommen aus der DUT-Simulation ueber `send_interface("SHRT", payload)`
- WireViz bestimmt die zu messenden Paare, die an das DUT uebergeben werden
- Verdrahtung dient als Pfadfilter und liefert die Traces fuer die Analyse

Aktuell unterstuetzt der Simulationskern dabei:

- Senden von Kommandos an externe DUT-Modelle ueber die Named Pipe
- optionales Ruecklesen des letzten Interface-Responses
- Rueckschreiben von Antworten in CT3xx-Variablen
- automatische Zerlegung von CSV-aehnlichen Antworten in Array-Variablen wie `Result[1] ... Result[n]`

Typischer Ablauf:

- Testprogramm sendet ein Kommando ueber `E488`
- das DUT antwortet ueber die simulierte Kommunikationsschnittstelle
- die Antwort wird in Variablen geschrieben
- nachfolgende Tests wie `PET$` werten diese Variablen aus

## Waveform-Unterstuetzung

`.ctarb`-Dateien werden eingelesen und als gemeinsames Kurvenmodell verarbeitet. Der Simulator kann:

- Stimulus-Kurven an das DUT uebergeben
- Sofortantworten vom DUT auswerten
- Response-Captures waehrend des Stimulus abfragen
- Messkurven an die UI weitergeben
- Split-Unterablaeufe innerhalb eines `2ARB` waehrend des aktiven Stimulus ausfuehren
- 2ARB-Ruecksignale ueber die Simulationszeit sampeln und erste Metriken wie `UMAX`, `UMIN`, `UAVG`, `UEFF`, `NPUL` und `AWID` auswerten

## Einbindung

Der Kern wird typischerweise aus der WPF-App verwendet, kann aber auch direkt aus Tools oder Tests aufgerufen werden.

## Concurrent-Snapshots

Fuer `concurrent`-Gruppen publiziert der Simulationskern jetzt bereits Snapshot-Metadaten als Grundlage fuer eine spaetere tick-/eventbasierte Ausfuehrung:

- aktive Concurrent-Gruppe
- globale Concurrent-Events wie `group_sync:start` oder `branch_started:*`
- Branch-Snapshots mit Status wie `queued`, `running`, `completed`
- explizite globale Snapshot-Punkte fuer:
  - Warten (`branch_waiting`, `branch_resumed`)
  - Schnittstellenkommunikation (`interface_request`, `interface_response`)
  - asynchrones Prozessende (`process_exit`)
- echte gemeinsame Simulationszeit fuer Concurrent-Branches
- Event-Fortschritt bis zum jeweils naechsten globalen Zeitpunkt statt serieller Abarbeitung
- poll-basierte Integration paralleler Prozesse in die globale Zeitfortschreibung

Die Concurrent-/Snapshot-Architektur ist projektweit inzwischen bis zur Persistenzstufe umgesetzt. Der Gesamtstatus ist in [ROADMAP.md](C:/Users/hello/Desktop/CT3xx/ROADMAP.md) zusammengefuehrt.
