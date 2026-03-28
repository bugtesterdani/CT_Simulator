# CSV Replay Plan

Diese Datei beschreibt, wie ein optionaler CSV-gestuetzter Analysemodus in die bestehende `Ct3xxSimulator.Desktop`-App integriert werden kann.

## Aktueller Stand

Bereits umgesetzt:

- Schritt 1: abgeschlossen
- Schritt 2: abgeschlossen
- Schritt 3: abgeschlossen
- Schritt 4: abgeschlossen
- Schritt 5: abgeschlossen
- Schritt 6: abgeschlossen
- Schritt 7: abgeschlossen
- Schritt 8: abgeschlossen
- Schritt 9: abgeschlossen
- Schritt 10: abgeschlossen

Aktuell offen:

- keine offenen CSV-Replay-Schritte mehr in diesem Plan

Bereits vorhanden im Projekt:

- neues Parser-Projekt [Ct3xxTestRunLogParser](C:/Users/hello/Desktop/CT3xx/Ct3xxTestRunLogParser)
- neues Testprojekt [Ct3xxTestRunLogParser.Tests](C:/Users/hello/Desktop/CT3xx/Ct3xxTestRunLogParser.Tests)
- Importmodell fuer historische CSV-Testlaeufe
- Parser fuer das aktuell bekannte CSV-Format aus [Test_X.csv](C:/Users/hello/Desktop/CT3xx/examples/Test_X.csv)
- Parser-Tests fuer Header, Metadaten, numerische Zeilen und grobe Zeilenklassifikation
- sichtbare Programmschritt-Extraktion aus `.ctxprg`-Dateien
- sequentieller Matcher `Programmschritt -> CSV-Zeile`
- Match-Report mit Integritaetskennzahlen und Zuverlaessigkeitsbewertung
- Desktop-Konfiguration mit optionalem CSV-Dateipfad
- CSV-Modi `Aus`, `Vergleich`, `CSV fuehrt Ergebnis`
- Preset-Persistenz fuer CSV-Datei und CSV-Modus
- CSV-Statusanzeige inklusive Matching-Zusammenfassung in der Desktop-App
- CSV-Metadaten werden pro simuliertem Schritt mitgefuehrt
- CSV-Zuordnung bleibt beim Snapshot-Speichern und -Laden erhalten
- sichtbare Ergebnisquelle `SIM`, `CSV`, `SIM/CSV` in der Testschrittliste
- Auswertungsansicht mit Reitern `Simulation`, `CSV` und `Vergleich`
- Hauptanzeige kann im Modus `CSV fuehrt Ergebnis` auf CSV-Werte umschalten, ohne Pfade und Signalansicht aus der Simulation zu verlieren

Verifiziert:

- `dotnet test Ct3xxTestRunLogParser.Tests\Ct3xxTestRunLogParser.Tests.csproj`
- `dotnet build CT3xx.sln`

Ziel:

- keine separate Software
- weiterhin dieselbe Desktop-UI
- weiterhin dieselbe Simulation fuer Pfade, Spannungen, Relais, DUT und Live-Zustand
- aber optional Auswertung und Schrittresultate aus einer importierten CSV-Datei statt aus der Simulator-Bewertung

Wichtige Grundidee:

- das Testprogramm wird weiterhin normal geladen und ausgefuehrt
- Verdrahtung, DUT-Modell und Pfadauflosung laufen weiterhin ueber den Simulator
- die CSV ist ein optionaler externer Ergebnislauf
- CSV und geladenes Testprogramm muessen fachlich zueinander passen

## Zielbild

Bei aktiviertem CSV-Modus soll die Desktop-App:

- eine CSV-Datei zusaetzlich laden koennen
- den Testlauf technisch weiter simulieren
- fuer die Schrittergebnisanzeige auf Wunsch die CSV-Daten verwenden
- im Fehlerfall weiterhin Pfade, Live-Zustand, Spannungen und Verdrahtung aus der Simulation zeigen
- klar sichtbar machen, ob ein Resultat aus der Simulation oder aus der CSV stammt

## 10 Schritte

### 1. CSV-Format fachlich festziehen

Status:

- abgeschlossen

Ziel:

- ein stabiles Importmodell fuer die vorhandene CSV definieren

Bekannte Spalten aus `examples/Test_X.csv`:

- `Lauf ID`
- `Testzeit`
- `Seriennummer`
- `Bezeichnung`
- `Message`
- `Untere Grenze`
- `Wert`
- `Obere Grenze`
- `Ergebnis`

Zu klaeren:

- welche Zeilen sind echte Testschritte
- welche Zeilen sind nur Info-/Kommentarzeilen
- welche Ergebniswerte gelten als numerisch
- welche Ergebniswerte gelten als rein textuell

Ergebnis dieses Schritts:

- ein klar beschriebenes CSV-Importmodell

### 2. Eigenes Parser-Projekt anlegen

Status:

- abgeschlossen

Ziel:

- die CSV nicht direkt in der UI parsen, sondern sauber getrennt

Empfehlung:

- neues Projekt `Ct3xxTestRunLogParser`

Aufgaben:

- CSV-Datei lesen
- Header validieren
- Zeilen in DTOs umwandeln
- kultur- und formatrobuste Zahlenerkennung fuer `,` und `.`

Ergebnis dieses Schritts:

- wiederverwendbarer Parser statt UI-Hardcoding

### 3. Gemeinsames Importmodell definieren

Status:

- abgeschlossen

Ziel:

- strukturierte Repräsentation eines historischen Laufs

Empfohlene DTOs:

- `ImportedTestRun`
- `ImportedTestRunStep`
- `ImportedMeasurementValue`
- `ImportedRunMetadata`

Wichtige Felder pro Schritt:

- CSV-Zeilennummer
- Bezeichnung
- Message
- untere Grenze
- Wert
- obere Grenze
- Ergebnis
- Klassifikation
  - Info
  - Schritt
  - Messung
  - Kommentar
  - Unbekannt

Ergebnis dieses Schritts:

- stabile Datengrundlage fuer Matching und UI

### 4. Matching zwischen Testprogramm und CSV einziehen

Status:

- abgeschlossen

Ziel:

- Simulator-Schritte gegen CSV-Schritte zuordnen

Wichtig:

- dieselbe `.ctxprg`-Datei muss zum CSV-Lauf passen
- gleiche Reihenfolge ist Voraussetzung
- Schrittname allein reicht oft nicht

Empfohlene Matching-Stufen:

1. gleiche Reihenfolge
2. aehnliche Schrittbezeichnung
3. optional Message-/Wertabgleich

Zusatz:

- Integritaetspruefung einbauen
- Warnung oder Abbruch, wenn CSV und Testprogramm offensichtlich nicht zusammenpassen

Ergebnis dieses Schritts:

- belastbare Zuordnung `Simulator-Schritt -> CSV-Eintrag`

Aktueller Stand im Projekt:

- sichtbare Programmschritte werden aus dem CT3xx-Programm in Ausfuehrungsreihenfolge extrahiert
- `PET$`-Unterauswertungen werden als sichtbare synthetische Teilschritte expandiert
- relevante CSV-Zeilen werden gefiltert
- Matching nutzt Reihenfolge mit begrenztem Lookahead plus Textaehnlichkeit
- das Ergebnis liefert:
  - Matches
  - ungematchte Programmschritte
  - ungematchte CSV-Zeilen
  - Program-/CSV-Coverage
  - Durchschnittsscore
  - `IsReliable` fuer spaetere UI- und Replay-Entscheidungen

### 5. Optionalen CSV-Modus in die Desktop-Konfiguration einbauen

Status:

- abgeschlossen

Ziel:

- CSV-Datei in derselben Desktop-App auswählbar machen

UI-Erweiterung:

- Feld `CSV-Testlauf`
- `Datei...`
- optional `CSV-Modus aktiv`

Empfohlene Modi:

- `Aus`
- `Vergleich`
- `CSV fuehrt Ergebnis`

Bedeutung:

- `Aus`: heutiges Verhalten
- `Vergleich`: Simulation und CSV parallel anzeigen
- `CSV fuehrt Ergebnis`: Anzeige nutzt CSV als Ergebnisquelle

Ergebnis dieses Schritts:

- CSV-Unterstuetzung in der bestehenden UI statt in einer separaten App

Aktueller Stand im Projekt:

- die Desktop-App kann jetzt eine optionale CSV-Datei direkt im Hauptfenster auswaehlen
- der CSV-Modus ist in der UI schaltbar:
  - `Aus`
  - `Vergleich`
  - `CSV fuehrt Ergebnis`
- Szenario-Presets speichern:
  - CSV-Dateipfad
  - CSV-Modus
- die App laedt die CSV-Datei bereits in der Konfiguration und zeigt:
  - Ladefehler
  - Matching-Status
  - Zuverlaessigkeitszusammenfassung
- `CSV fuehrt Ergebnis` wird schon jetzt durch die Validierung blockiert, wenn das Matching nicht zuverlaessig genug ist

### 6. Simulationslauf technisch unverändert weiterlaufen lassen

Status:

- abgeschlossen

Ziel:

- Pfade, Relais, Signalzustände und Live-Zustand sollen weiter simulierbar bleiben

Wichtig:

- CSV ersetzt nicht den technischen Ablauf
- CSV ersetzt nur ganz oder teilweise die Bewertung

Das heisst:

- `Ct3xxProgramSimulator` laeuft weiter
- WireViz-Aufloesung bleibt aktiv
- DUT bleibt aktiv
- Snapshots bleiben aktiv

Ergebnis dieses Schritts:

- Fehleranalyse mit echten Simulationspfaden bleibt moeglich

Aktueller Stand im Projekt:

- der technische Simulationslauf bleibt unveraendert
- CSV-Metadaten werden pro ausgewertetem Schritt parallel an das Schrittresultat angehaengt
- Export- und Snapshot-Dokumente tragen diese CSV-Zuordnung ebenfalls mit
- der Simulationskern selbst wurde dafuer nicht auf CSV-Auswertung umgebaut

### 7. Ergebnis-Umschaltung in der UI einbauen

Status:

- abgeschlossen

Ziel:

- fuer jeden Schritt sichtbar machen:
  - Simulations-Ergebnis
  - CSV-Ergebnis

Empfohlene Anzeige:

- im Testschritt:
  - Status
  - Quelle `SIM`, `CSV`, `SIM/CSV`
- in der Auswertungsanalyse:
  - Reiter `Simulation`
  - Reiter `CSV`
  - Reiter `Vergleich`

Im Modus `CSV fuehrt Ergebnis`:

- PASS/FAIL/ERROR in der Hauptliste aus CSV ableiten
- Detailpfade und Signalansicht weiter aus der Simulation ziehen

Ergebnis dieses Schritts:

- keine Verwechslung zwischen simulierter Technik und historischem Ergebnis

Aktueller Stand im Projekt:

- die Testschrittliste zeigt jetzt die Ergebnisquelle als `SIM`, `CSV` oder `SIM/CSV`
- im Modus `CSV fuehrt Ergebnis` verwendet die Hauptanzeige CSV-Outcome sowie CSV-Min/Max/Wert als primaere Anzeige
- die Auswertungsansicht ist in `Simulation`, `CSV` und `Vergleich` aufgeteilt
- Simulation, Verdrahtung, Pfade, Snapshots und Kurven bleiben weiterhin technisch simulatorseitig

### 8. Snapshot- und Debug-Integration festlegen

Status:

- abgeschlossen

Ziel:

- CSV-Modus sauber mit Snapshot-Timeline kombinieren

Empfehlung:

- Snapshots bleiben rein simulatorseitig
- CSV erzeugt keine eigenen technischen Zustände
- beim Schritt-Snapshot wird nur zusaetzlich der zugeordnete CSV-Eintrag referenziert

Sinnvoll:

- `Zu letztem Snapshot dieses Schritts` funktioniert unverändert
- CSV-Details werden an diesem Snapshot nur eingeblendet

Ergebnis dieses Schritts:

- Debugging bleibt konsistent

Aktueller Stand im Projekt:

- Snapshots bleiben rein simulatorseitig und werden nicht aus CSV erzeugt
- Timeline-Eintraege koennen jetzt pro Snapshot die zugehoerige Ergebnisquelle und Vergleichszusammenfassung anzeigen
- beim Wechsel auf einen Snapshot wird der dazu passende Testschritt im Baum wieder selektiert
- eine offene Auswertungsansicht folgt dem ausgewaehlten Snapshot-Schritt automatisch
- `Zu letztem Snapshot dieses Schritts` bleibt unveraendert auf der simulatorseitigen Snapshot-Kette

### 9. Fehler- und Mismatch-Faelle robust behandeln

Status:

- abgeschlossen

Ziel:

- falsche CSV-Datei oder falsches Testprogramm sauber erkennen

Zu pruefen:

- stimmt die Anzahl relevanter Schritte grob
- passen zentrale Schrittnamen
- gibt es unzugeordnete CSV-Zeilen
- gibt es Simulator-Schritte ohne CSV-Treffer

Reaktion:

- Warnung in `Vergleich`
- harter Abbruch in `CSV fuehrt Ergebnis`, wenn die Zuordnung unzuverlaessig ist

Ergebnis dieses Schritts:

- CSV-Modus wird nicht mit fachlich falschen Daten benutzt

Aktueller Stand im Projekt:

- `CSV fuehrt Ergebnis` bleibt hart blockiert, wenn das Matching nicht zuverlaessig genug ist
- wenn ueberhaupt kein sichtbarer Programmschritt gematcht werden konnte, blockiert die Validierung den CSV-Modus ebenfalls
- im Vergleichsmodus wird ein unzuverlaessiges Matching nicht blockiert, aber klar als Warnung in der CSV-Zusammenfassung und im Simulationsprotokoll markiert

### 10. Tests, Doku und Beispielworkflow nachziehen

Status:

- abgeschlossen

Ziel:

- die Erweiterung wartbar und benutzbar machen

Noetig:

- Parser-Tests fuer CSV
- Matching-Tests
- UI-/ViewModel-Tests fuer den CSV-Modus
- README- und OPTIONS-Erweiterungen
- ein Beispielworkflow fuer:
  - Testprogramm laden
  - Verdrahtung laden
  - DUT laden
  - CSV laden
  - CSV-Modus aktivieren

Ergebnis dieses Schritts:

- die Funktion ist nicht nur implementiert, sondern reproduzierbar und dokumentiert

Aktueller Stand im Projekt:

- Parser-Tests fuer das CSV-Format und das Matching liegen in [Ct3xxTestRunLogParser.Tests](C:/Users/hello/Desktop/CT3xx/Ct3xxTestRunLogParser.Tests)
- gezielte Desktop-/ViewModel-Tests fuer CSV-Anzeigequellen und Snapshot-Roundtrip liegen in [Ct3xxSimulator.Tests](C:/Users/hello/Desktop/CT3xx/Ct3xxSimulator.Tests)
- der CSV-Beispielworkflow ist in der Desktop-README dokumentiert
- Root-README, Desktop-README, Desktop-OPTIONS und diese Plan-Datei sind auf dem aktuellen Stand

## Was diese Erweiterung leisten kann

- historischen PASS/FAIL-Verlauf in die Desktop-App holen
- trotzdem technische Pfade im Simulator analysieren
- FAIL-/ERROR-Schritte mit Verdrahtung, Relais, Spannungen und DUT-Zustand nachvollziehen

## Was diese Erweiterung nicht automatisch leisten kann

- reale historische Signalzustände aus der CSV rekonstruieren
- reale Live-Zeit aus der Vergangenheit nachbauen
- garantieren, dass die Simulation exakt dem historischen Testergebnis entspricht

Die CSV ist ein Ergebnisprotokoll, keine vollstaendige technische Zustandsaufzeichnung.

## Wichtige Annahmen

- geladenes Testprogramm und CSV beschreiben denselben Laufaufbau
- Verdrahtung und Simulationsmodell sind fuer die Analyse fachlich passend
- der CSV-Modus ist optional und darf das heutige Simulationsverhalten nicht brechen

## Empfohlene Reihenfolge fuer die Umsetzung

1. Schritt 1 bis 3
2. Schritt 4
3. Schritt 5
4. Schritt 6 und 7
5. Schritt 8 und 9
6. Schritt 10

## Historische Startempfehlung

Die erste risikoarme Ausbaustufe dieses Plans war:

- CSV laden
- Schritte matchen
- Simulationslauf normal ausfuehren
- CSV-Ergebnis parallel anzeigen
- noch keine harte Ergebnis-Umschaltung

Diese Zwischenstufe ist inzwischen ueberholt, weil die Ergebnis-Umschaltung inzwischen umgesetzt ist.

## Abschlussstand

Alle 10 Schritte dieses CSV-Replay-Plans sind aktuell umgesetzt.
