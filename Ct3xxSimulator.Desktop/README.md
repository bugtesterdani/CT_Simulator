# Ct3xxSimulator.Desktop

WPF-Oberflaeche fuer den CT3xx-Simulator.

## Funktionen

- Auswahl von:
  - Szenario-Datei (`.json`) fuer Laden und Speichern von Presets
  - Testprogramm-Ordner
  - Verdrahtungs-Ordner
  - Simulationsmodell-Ordner
  - verpflichtendem DUT-Modell (`.py`, `.json`, `.yaml`, `.yml`)
  - optionalem CSV-Testlauf (`.csv`)
- Szenario-Presets
- Validierung vor dem Start
- Start kompletter Simulationen
- Einzelschrittmodus mit `Weiter`, `Zurueck`, `Auto`, `Pause`
- Ergebnisliste pro Testschritt
- Testschritte als gruppierte, einklappbare Baumansicht entlang der CT3xx-Gruppenstruktur
- verschachtelte Unterablaeufe innerhalb eines Tests, z. B. Split-Schritte unter `2ARB`, werden ebenfalls als Unterknoten dargestellt
- Verbindungsgraph pro Schritt
- optionales Live-Zustandsfenster
- Live-Zustandsfenster mit `Kompakt`- und `Expert`-Ansicht
- Export als `PDF`, `JSON`, `CSV`
- Concurrent-Snapshot-Anzeige im Live-Zustandsfenster
- Snapshot-Timeline im Hauptfenster
- Snapshot-Session speichern und laden (`*.snapshot.json`)

## Bedienung

- Presets werden standardmaessig unter `%LocalAppData%\\Ct3xxSimulatorDesktop\\scenarios.json` gespeichert.
- Ueber das Feld `Szenario-Datei` kann eine andere JSON-Datei explizit ausgewaehlt, geladen oder per `Speichern unter` neu angelegt werden.
- Ueber das Feld `CSV-Testlauf` kann optional ein historischer CSV-Lauf in dieselbe Desktop-App geladen werden.
- Ein Geraetemodell ist immer Pflicht, auch bei tester-/verdrahtungsseitigen Szenarien. Fuer solche Faelle kann ein neutrales Dummy-Profil wie [noop_device.yaml](C:/Users/hello/Desktop/CT3xx/simtest/device/devices/noop_device.yaml) verwendet werden.
- Der CSV-Modus kann direkt im Hauptfenster auf `Aus`, `Vergleich` oder `CSV fuehrt Ergebnis` gestellt werden.
- Nach dem Laden eines Pruefprogramms ist die Testschritt-Struktur sofort sichtbar, auch ohne gestartete Simulation.
- Fuer ausgewaehlte Testschritte und Gruppen koennen Breakpoints gesetzt werden; die Simulation haelt dann direkt nach Abschluss genau dieses Schritts oder der ganzen Gruppe an und wartet auf Nutzerinteraktion.
- Breakpoints werden mit den Szenario-Presets gespeichert und beim erneuten Laden des Szenarios wiederhergestellt.
- Szenario-Presets speichern zusaetzlich Dateiname und SHA-256 der gewaehlten `.ctxprg`, damit beim erneuten Laden moeglichst genau dieselbe Programmdatei wieder aufgeloest wird.
- Szenario-Presets speichern zusaetzlich auch den optionalen CSV-Dateipfad und den ausgewaehlten CSV-Modus.
- Ueber `Upgrade` koennen bestehende Szenario-Presets auf das neue Format mit Dateiname, SHA-256 und aktueller Breakpoint-Zuordnung angehoben werden.
- Falls alte Breakpoint-Schluessel nicht mehr eindeutig in die aktuelle Programmstruktur passen, erscheint dabei eine manuelle Zuordnungsmaske.
- Wichtig: Breakpoint-Persistenz greift nur beim Szenario speichern/laden. Wenn Breakpoints gesetzt werden, das Szenario aber nicht gespeichert wird, gelten sie nur fuer die aktuelle Sitzung.
- In der Verbindungsansicht oeffnen nur echte Unterbaugruppen mit internem Pfad ein weiteres Fenster.
- Ein Testschritt wird jetzt zunaechst nur ausgewaehlt. Die Verbindungsansicht oeffnet erst per Doppelklick auf den Testschritt.
- Ueber den Button `Auswertungsanalyse` oeffnet sich fuer den aktuell ausgewaehlten Testschritt eine Auswertungsansicht mit `Ist`, `Soll Min`, `Soll Max` und vorhandenen Kurven.
- Die Testschritt-Liste selbst ist bewusst kompakt gehalten und zeigt nur noch `Struktur`, `Status`, `Ist` und `Bereich`.
- Bei AM2-/Waveform-Schritten werden in der Auswertungsansicht auch die verfuegbaren Eingangs-/Ausgangskurven des Schritts dargestellt; vorhandene Sollgrenzen werden als Linien und als zulaessiger Bereich eingeblendet.
- Die Auswertungsansicht ist fuer CSV-gestuetzte Laeufe in die Reiter `Simulation`, `CSV` und `Vergleich` aufgeteilt.
- Im Reiter `Simulation` stehen weiterhin `Diagramm` und `Details` fuer Kurven, Grenzband, tabellarische Soll-/Ist-Metriken und Rohdetails zur Verfuegung.
- Die Detailtabelle bleibt in der vom Simulator gelieferten Reihenfolge und sortiert nicht automatisch um.
- Zeilen mit `FAIL`, `ERROR`, `Zu klein` oder `Zu gross` werden in der Detailtabelle dezent hervorgehoben.
- Die Testschritte werden im Hauptfenster entlang der Gruppenstruktur des Pruefprogramms eingerueckt und klappbar dargestellt.
- Ueber `Zu letztem Snapshot dieses Schritts` springt die Timeline direkt auf den letzten bekannten Snapshot des aktuell ausgewaehlten Testschritt-Knotens inklusive seiner Untereintraege.
- Die Darstellung ist bewusst keine starre Tabelle mehr, sondern eine kompakte Baum-/Kartenansicht:
  - `Struktur`
  - `Status`
  - `Ist`
  - `Bereich`
- `Concurrent`-Gruppen, normale `Loop`-Gruppen und der `Test Loop` werden im Baum explizit gekennzeichnet.
- Bei `Concurrent`-Gruppen wird im Knoten selbst sichtbar beschrieben, dass die darunterliegenden Schritte parallel laufen koennen.
- Gruppen starten standardmaessig aufgeklappt.
- Nach Abschluss einer Gruppe wird diese automatisch wieder eingeklappt.
- Der `Test Loop`-Ordner bleibt dauerhaft sichtbar und aufgeklappt, auch wenn sein Inhalt bereits abgeschlossen ist.
- Einzelne Inline-Bauteile wie Relais im Hauptpfad bleiben sichtbar, sind aber nicht als eigenes Untermodul klickbar.
- Unteransichten uebernehmen die Richtung des ausgewaehlten Signals aus dem uebergeordneten Fenster und drehen sie nicht mehr selbststaendig um.
- Die rechte Timeline zeigt globale Snapshot-Zustaende mit Zeit, Event und Branch-Zusammenfassung.
- Snapshot-Erzeugung ist bewusst reduziert und erzeugt standardmaessig nur noch Snapshots am Ende echter Testschritte und am Ende sichtbarer Teilauswertungen innerhalb eines Schritts, z. B. einzelner `PET$`-Auswertungen.
- Breakpoints erzeugen dabei keine eigenen Zusatz-Snapshots; sie pausieren auf Basis des regulaeren Schritt- oder Gruppenzustands.
- Ein Klick auf einen Timeline-Eintrag setzt die Live-Zustandsanzeige auf genau diesen Snapshot.
- Dabei wird der Signalverlauf im Live-Zustandsfenster auf den ausgewaehlten Snapshot-Zeitpunkt abgeschnitten und nicht mehr als kompletter Endverlauf angezeigt.
- Die Testschritt-Baumansicht wird beim Snapshot-Sprung auf den bis dahin gueltigen Ergebnisstand zurueckgesetzt und neu markiert.
- Beim Snapshot-Sprung wird ausserdem der passendste sichtbare Testschritt wieder ausgewaehlt; eine geoeffnete Auswertungsansicht folgt diesem Schritt automatisch.
- Ueber `Snapshots speichern` wird der aktuelle Analysezustand mit Timeline, Logs, Schrittergebnissen und Signalhistorie persistiert.
- Ueber `Snapshots laden` kann dieser Zustand spaeter ohne erneuten Simulationslauf wiederhergestellt werden.
- Bei `concurrent`-Gruppen zeigt das Live-Zustandsfenster jetzt:
  - aktive Concurrent-Gruppe
  - letztes Concurrent-Event
  - Branch-Liste mit Status und aktuellem Item
- Das Live-Zustandsfenster startet standardmaessig in einer kompakten Uebersicht fuer `System`, `DUT` und `Zustaende`.
- Ueber die Ansichtsumschaltung `Expert` werden alle Detaildaten tab-basiert und damit lesbarer statt in einer einzigen breiten Matrix dargestellt.
- Im Hauptfenster zeigt ein Statusindikator zusaetzlich, ob die Simulation aktuell `Bereit`, `Laeuft` oder `Pausiert` ist.
- Die Konfiguration zeigt zusaetzlich einen CSV-Status mit Lade- und Matching-Zusammenfassung an.
- Der technische Simulationslauf bleibt dabei unveraendert; CSV-Zuordnungen werden pro Schritt parallel mitgefuehrt und auch in Snapshot-Sessions gespeichert.
- Die Testschrittliste zeigt fuer CSV-gestuetzte Laeufe jetzt sichtbar die Ergebnisquelle `SIM`, `CSV` oder `SIM/CSV`.
- Im Modus `CSV fuehrt Ergebnis` folgt die Hauptanzeige fuer `Status`, `Ist` und `Bereich` den CSV-Werten; Verbindungsansicht, Pfade, Kurven und Live-Zustand bleiben simulatorseitig.
- Schritte, die im CSV-Lauf gar nicht geloggt wurden, bleiben trotzdem sichtbar und weiter analysierbar; ihre Darstellung richtet sich jetzt zusaetzlich nach den CT3xx-`LogFlags` des Programms.
- Fehlt ein CSV-Eintrag und das aktuelle simulierte Ergebnis muesste laut `LogFlags` gar nicht geloggt werden, bleibt der Schritt als `SIM` dunkelgrau markiert mit dem Hinweis `Kein CSV-Eintrag fuer diesen Schritt`.
- Fehlt ein CSV-Eintrag, obwohl die `LogFlags` fuer das aktuelle Ergebnis einen Logeintrag erwarten, wird der Schritt als CSV-Abweichung hervorgehoben und entsprechend in der Vergleichszusammenfassung beschrieben.
- Die Auswertungsansicht ist fuer CSV-gestuetzte Laeufe in die Reiter `Simulation`, `CSV` und `Vergleich` aufgeteilt.
- Die Snapshot-Timeline kann CSV-Vergleichsinformationen am jeweiligen Schritt-Snapshot einblenden.
- Unzuverlaessiges CSV-Matching wird im Vergleichsmodus sichtbar als Warnung markiert; `CSV fuehrt Ergebnis` bleibt dabei gesperrt.
- Fehlende CSV-Eintraege fuer einzelne Schritte machen den Lauf dabei nicht automatisch unzuverlaessig, solange die vorhandenen CSV-Zeilen noch belastbar zum geladenen Testprogramm passen.

## CSV-Beispielworkflow

1. Testprogramm-Ordner, Verdrahtungs-Ordner, Simulations-Ordner und DUT-Modell wie gewohnt laden.
2. Optionalen `CSV-Testlauf` auswaehlen.
3. CSV-Modus waehlen:
   - `Aus`
   - `Vergleich`
   - `CSV fuehrt Ergebnis`
4. CSV-Zusammenfassung pruefen:
   - bei Warnung im Vergleichsmodus kann weiter analysiert werden
   - bei gesperrtem `CSV fuehrt Ergebnis` zuerst Matching-Problem beheben
5. Simulation normal starten.
6. In der Testschrittliste auf Ergebnisquelle `SIM`, `CSV` oder `SIM/CSV` achten.
   - ein dunkelgrau markierter `SIM`-Schritt bedeutet in diesem Modus meist, dass der Schritt im historischen CSV-Lauf fuer dieses Ergebnis laut `LogFlags` nicht geloggt werden musste
   - ein rot markierter `SIM`-Schritt ohne CSV-Treffer bedeutet dagegen, dass laut `LogFlags` ein Logeintrag erwartet worden waere
7. Fuer einen Schritt `Auswertungsanalyse` oeffnen und zwischen `Simulation`, `CSV` und `Vergleich` wechseln.
8. Ueber Snapshot-Timeline oder `Zu letztem Snapshot dieses Schritts` den gewuenschten Analysezeitpunkt ansteuern.
- Bei `concurrent`-Gruppen erscheinen jetzt auch explizite globale Events fuer:
  - Warten
  - Interface-Request / Interface-Response
  - asynchrones Prozessende
- Die zugrundeliegenden Snapshot-Zeiten kommen jetzt aus einer echten gemeinsamen Concurrent-Simulationsuhr statt aus seriell aufaddierter Branch-Zeit.
- `Weiter` und `Zurueck` verwenden jetzt die Snapshot-Folge statt eines Replay-Neustarts.
- Snapshot-Pausen im Schrittmodus passieren an globalen Snapshot-Punkten statt nur nach kompletten Testschritten.
- Stufe 1 bis Stufe 5 der Concurrent-Snapshot-Architektur sind damit in Kern, Desktop-Oberflaeche und Persistenz umgesetzt.

## Relevante Dateien

- `MainWindow.xaml`
  Hauptoberflaeche
- `MainWindow.xaml.cs`
  Steuerung, Simulationstart und Observer-Anbindung
- `MainWindow.Configuration.cs`
  Pfade, Presets und Programmauswahl
- `MainWindow.Simulation.cs`
  Simulationslauf, Start des DUT-Prozesses und Observer-Anbindung
- `MainWindow.Timeline.cs`
  Schrittmodus und Snapshot-Navigation
- `Views/ConnectionGraphWindow.*`
  Verbindungsansicht pro Testschritt
- `Views/EvaluationDetailsWindow.*`
  Auswertungsansicht fuer Soll-/Ist-Grenzen, Kurven und tabellarische Detailmetriken
- `Views/LiveStateWindow.*`
  optionale Live-Zustandsanzeige
- `Configuration/`
  Presets

Export und tiefe Modellvalidierung liegen inzwischen in den separaten Projekten:

- `Ct3xxSimulator.Export`
- `Ct3xxSimulator.Validation`

## Start

```powershell
dotnet run --project Ct3xxSimulator.Desktop
```

Konfigurationshinweise stehen in [OPTIONS.md](C:/Users/hello/Desktop/CT3xx/Ct3xxSimulator.Desktop/OPTIONS.md).

## Hinweis

Die App enthaelt bewusst keine eigene Hardcode-Simulationslogik fuer DUT-Verhalten. Bauteilverhalten und DUT-Reaktionen sollen ueber den Simulationskern, `simulation.yaml` und die DUT-Modelle beschrieben werden.

