# Ct3xxSimulator.Desktop Optionen

Diese Datei beschreibt die konfigurierbaren Teile der WPF-App.

## In der App auswaehlbar

- Szenario-Datei fuer Presets (`*.json`)
- Testprogramm-Ordner
- Verdrahtungs-Ordner
- Simulationsmodell-Ordner
- verpflichtende DUT-Datei
- optionaler CSV-Testlauf (`*.csv`)

Unterstuetzte DUT-Dateien:

- `*.py`
- `*.json`
- `*.yaml`
- `*.yml`

Auch rein tester-/verdrahtungsseitige Szenarien benoetigen in der Desktop-App ein Geraetemodell. Dafuer kann ein neutrales Dummy-Profil wie [noop_device.yaml](C:/Users/hello/Desktop/CT3xx/simtest/device/devices/noop_device.yaml) verwendet werden.

## Verhalten der Programmauswahl

- genau eine `*.ctxprg` im Testprogramm-Ordner:
  automatische Verwendung
- mehrere `*.ctxprg`:
  Bedienerauswahl
- nach erfolgreichem Laden wird die Testschritt-Struktur direkt im Hauptfenster aufgebaut, auch ohne Simulationsstart
- Breakpoints koennen auf Testschritte und Gruppen gesetzt werden, nicht auf reine Ergebnisunterknoten
- Breakpoints halten direkt nach dem jeweiligen Schritt oder der jeweiligen Gruppe an und warten dann auf `Weiter`, `Auto` oder eine andere Nutzeraktion
- gespeicherte Szenario-Presets enthalten auch die gesetzten Breakpoints fuer die Programmstruktur
- gespeicherte Szenario-Presets enthalten zusaetzlich Dateiname und SHA-256 der gewaehlten `.ctxprg`
- gespeicherte Szenario-Presets enthalten ausserdem den optionalen CSV-Dateipfad und den gewaehlten CSV-Modus
- ueber `Upgrade` kann ein vorhandenes Preset auf das neue Format angehoben werden
- nicht mehr eindeutig zuordenbare Breakpoints koennen dabei manuell auf aktuelle Testschritte oder Gruppen gemappt werden
- der CSV-Modus ist umschaltbar:
  - `Aus`
  - `Vergleich`
  - `CSV fuehrt Ergebnis`
- bei aktivem CSV-Modus prueft die App bereits beim Laden, ob die CSV gelesen werden kann und wie zuverlaessig das Matching zum aktuell geladenen Testprogramm ist
- CSV fuehrt Ergebnis ist nur zulaessig, wenn das Matching als ausreichend zuverlaessig eingestuft wird
- waehrend der Simulation bleibt der technische Ablauf unveraendert; CSV-Metadaten werden nur an die Schrittergebnisse und Snapshot-Sessions angeheftet
- in der Testschrittliste wird die Ergebnisquelle sichtbar als `SIM`, `CSV` oder `SIM/CSV` angezeigt
- im Modus `CSV fuehrt Ergebnis` folgt die sichtbare Hauptbewertung der CSV, waehrend Pfade, Kurven und Zustandsdaten weiterhin aus der Simulation kommen
- Schritte ohne passenden CSV-Eintrag bleiben trotzdem sichtbar; dabei wird jetzt das simulierte Ergebnis gegen die CT3xx-`LogFlags` des Programms geprueft
- fehlt ein CSV-Eintrag fuer ein Ergebnis, das laut `LogFlags` nicht geloggt werden musste, bleibt der Schritt dunkelgrau mit dem Hinweis `Kein CSV-Eintrag fuer diesen Schritt`
- fehlt ein CSV-Eintrag, obwohl die `LogFlags` fuer das aktuelle Ergebnis einen Logeintrag verlangen, wird der Schritt als CSV-Abweichung hervorgehoben
- fehlende CSV-Zeilen fuer einzelne Schritte machen das Matching deshalb nicht automatisch unzuverlaessig; kritisch bleiben vor allem falsche oder unplausible CSV-Zuordnungen der vorhandenen Logzeilen

## Presets

Die App unterstuetzt Szenario-Presets fuer die oben genannten Pfade.

Aktuell:

- Standardpfad ist `%LocalAppData%\\Ct3xxSimulatorDesktop\\scenarios.json`
- der Speicherort kann in der App explizit ausgewaehlt werden
- Laden und Speichern koennen auf verschiedene vom Bediener gewaehlte JSON-Dateien gelenkt werden

Sinnvoll fuer:

- Gut-/Schlecht-/Grenzfall
- unterschiedliche DUT-Modelle
- unterschiedliche Verdrahtungsvarianten

## Verfuegbare Aktionen

- Validieren
- CSV-Datei fuer Replay-/Vergleichsmodus auswaehlen
- komplette Simulation starten
- Schrittmodus mit `Weiter`, `Zurueck`, `Auto`, `Pause`
- Laufstatus `Bereit`, `Laeuft`, `Pausiert` im Hauptfenster sehen
- gruppierte Testschritt-Baumansicht verwenden
- Breakpoint fuer den aktuell ausgewaehlten Testschritt oder die aktuell ausgewaehlte Gruppe setzen oder entfernen
- ausgewaehltes Szenario auf aktuelles Preset-Format upgraden
- Live-Zustand separat oeffnen
- Live-Zustand zwischen `Kompakt` und `Expert` umschalten
- Verbindungsansicht per Doppelklick auf einen Testschritt oeffnen
- Auswertungsansicht per Button fuer den ausgewaehlten Testschritt oeffnen
- Ergebnisse exportieren
- Snapshot-Session speichern
- Snapshot-Session laden
- zum letzten Snapshot des aktuell gewaehlten Testschritt-Knotens springen
- Concurrent-Kontext im Live-Zustandsfenster beobachten

## Verbindungsansicht

Aktuelles Bedienverhalten:

- Hauptansicht gruppiert in `Pruefsystem`, `Verdrahtung / Baugruppe`, `Geraet / Signal`
- ein einfacher Klick waehlt nur den Testschritt aus
- ueber den Button `Auswertungsanalyse` kann fuer den ausgewaehlten Testschritt die Soll-/Ist-Ansicht geoeffnet werden
- ein Doppelklick oeffnet die Verbindungsansicht des ausgewaehlten Testschritts
- Module in der Mitte zeigen `Eingang` und `Ausgang`
- nur echte Unterbaugruppen mit internem Teilpfad sind klickbar
- Relais oder andere einzelne Inline-Bauteile im Hauptpfad oeffnen kein Unterfenster
- Unterfenster uebernehmen die Signalrichtung des uebergeordneten Traces

## Auswertungsansicht

Aktuelles Bedienverhalten:

- zeigt `Ergebnis`, `Ist`, `Soll Min`, `Soll Max` und den Detailhinweis des ausgewaehlten Testschritts
- stellt vorhandene Kurvenpunkte als Diagramm dar
- mehrere Kurvensignale koennen gleichzeitig eingeblendet werden
- vorhandene numerische Sollgrenzen werden als horizontale Grenzlinien und als zulaessiges Grenzband dargestellt
- besitzt einen Reiter `Details` mit tabellarischer Aufbereitung der bekannten Metriken aus dem Testergebnis
- zeigt dort zusaetzlich die Rohdetails des Schritts an
- die Detailtabelle bleibt in der gelieferten Reihenfolge und ist nicht interaktiv sortierbar
- Grenzverletzungen und Fail-/Error-Zeilen werden dort dezent farblich hervorgehoben
- bei AM2-/Waveform-Tests koennen so sowohl Eingangs- als auch Rueckgabekurven eines Schritts sichtbar werden
- bei aktivem CSV-Match stehen zusaetzlich die Reiter `CSV` und `Vergleich` zur Verfuegung

## CSV-Beispielablauf

- Konfiguration wie fuer eine normale Simulation laden
- zusaetzlich optionale CSV-Datei auswaehlen
- Modus `Vergleich` oder `CSV fuehrt Ergebnis` setzen
- CSV-Zusammenfassung auf Warnungen oder Blockierung pruefen
- Simulation normal starten
- Ergebnisse ueber Hauptliste, Auswertungsansicht und Snapshot-Timeline analysieren
- dunkelgrau markierte `SIM`-Schritte bedeuten dabei typischerweise, dass der Schritt im historischen CSV-Lauf fuer dieses Ergebnis laut `LogFlags` nicht geloggt werden musste
- rot hervorgehobene `SIM`-Schritte ohne CSV-Treffer bedeuten dagegen, dass der CSV-Eintrag laut `LogFlags` haette vorhanden sein muessen

## Testschritt-Ansicht

- orientiert sich an der `Group`-/DUTLoop-Struktur des geladenen CT3xx-Programms
- verwendet eine kompakte Baum-/Kartenansicht statt einer starren Tabellenzeile
- Gruppen sind einklappbar
- Gruppen starten aufgeklappt
- nach erfolgreichem Gruppenabschluss werden normale Gruppen automatisch wieder eingeklappt
- der `Test Loop`-Knoten bleibt dauerhaft sichtbar und aufgeklappt
- `Concurrent`, `Loop`, `Concurrent Loop` und `Test Loop` werden im Knoten selbst sichtbar gekennzeichnet
- `Concurrent`-Gruppen tragen einen Hinweis, dass die enthaltenen Schritte parallel ausgefuehrt werden koennen
- Testschritte sind unterhalb ihrer Gruppen eingerueckt
- die Ueberschriften sind auf die kompakte Baumansicht abgestimmt:
  - `Struktur`
  - `Status`
  - `Ist`
  - `Bereich`
- bei mehrfachen Auswertungen innerhalb eines Tests, z. B. `PET$`, entstehen unter dem Test weitere Ergebnisknoten
- Detailtexte und tiefe Soll-/Ist-Informationen sind bewusst aus der Hauptliste herausgenommen und liegen in der `Auswertungsanalyse`

## Exportformate

- `PDF`
- `JSON`
- `CSV`

## Concurrent-Snapshots

Im aktuellen Stand zeigt das Live-Zustandsfenster bei `concurrent`-Gruppen zusaetzlich:

- `Concurrent-Gruppe`
- `Concurrent-Event`
- Liste der Concurrent-Branches mit:
  - Branchname
  - Status
  - aktuellem Item
  - Details

Das ist die Datenbasis fuer die spaetere tick-/eventbasierte Concurrent-Navigation.

Zusatzlich erscheinen jetzt globale Concurrent-Events fuer:

- `branch_waiting`
- `branch_resumed`
- `interface_request`
- `interface_response`
- `process_exit`

Zusatzlich steht im Hauptfenster eine Snapshot-Timeline zur Verfuegung:

- zeigt Reihenfolge der globalen Snapshot-Zustaende
- erzeugt im aktuellen Stand standardmaessig Snapshots pro abgeschlossenem Testschritt und pro sichtbarer Teilauswertung innerhalb eines Schritts, z. B. bei einzelnen `PET$`-Ergebnisunterknoten
- Breakpoints selbst erzeugen keine zusaetzlichen Spezial-Snapshots
- zeigt Zeit, Event und Branch-Zusammenfassung
- Auswahl eines Timeline-Eintrags setzt die Anzeige auf genau diesen Snapshot
- der Verlauf im Live-Zustandsfenster wird dabei nur bis zu diesem Snapshot aufgebaut
- die Testschritt-Anzeige wird fuer diesen Snapshot-Zeitpunkt neu rekonstruiert statt den Endstand beizubehalten
- der passendste sichtbare Testschritt wird dabei wieder ausgewaehlt
- eine offene Auswertungsansicht folgt diesem Snapshot-Schritt automatisch
- `Weiter` und `Zurueck` navigieren ueber diese Snapshot-Folge
- im Schrittmodus wird an Snapshot-Punkten pausiert, nicht nur an Testenden
- bei CSV-gestuetzten Laeufen koennen Timeline-Eintraege zusaetzlich eine kurze CSV-/SIM-Vergleichsinformation anzeigen

Ansichtsmodi im Live-Zustandsfenster:

- `Kompakt`
  - fokussiert auf schnelle Uebersicht
  - gruppiert in `System`, `DUT`, `Zustaende und Concurrent`
- `Expert`
  - zeigt alle Details
  - strukturiert ueber Tabs fuer `System`, `DUT`, `Zustaende`, `Concurrent`

Snapshot-Sessions:

- werden als `*.snapshot.json` gespeichert
- enthalten Timeline, Logs, Schrittergebnisse inklusive Snapshot-Zuordnung und Signalhistorie
- koennen nur ausserhalb einer laufenden Simulation geladen oder gespeichert werden

## Relevante Umgebungsvariablen

- `CT3XX_TESTPROGRAM_ROOT`
- `CT3XX_WIREVIZ_ROOT`
- `CT3XX_SIMULATION_MODEL_ROOT`
- `CT3XX_PYTHON_EXE`
- `CT3XX_PY_DEVICE_PIPE`

## Worauf geachtet werden sollte

- Pfade muessen zusammenpassen: `ctxprg`, Signaltabelle, WireViz und `simulation.yaml`
- DUT-Modellnamen muessen zu den vom Simulator aufgeloesten Signalnamen passen
- bei YAML-DUT-Profilen muss `PyYAML` installiert sein
- fuer Python-Pipes wird `pywin32` benoetigt


