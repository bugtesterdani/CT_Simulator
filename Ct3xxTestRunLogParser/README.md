# Ct3xxTestRunLogParser

Parserbibliothek fuer optionale CSV-Testlaufprotokolle, die im CSV-Replay-Modus der Desktop-App verwendet werden.

## Zweck

- CSV-Testlaufdateien lesen
- Metadaten und Schrittzeilen strukturieren
- Zahlen, Grenzen und Ergebnisse robust importieren
- sichtbare Programmschritte aus geladenen `.ctxprg`-Programmen extrahieren
- CSV-Zeilen gegen Programmschritte matchen
- eine stabile Grundlage fuer Replay-, Vergleichs- und CSV-gefuehrte Ergebnisanzeige liefern

## Aktueller Stand

Der Parser verarbeitet derzeit semikolongetrennte CSV-Dateien mit diesen Spalten:

- `Lauf ID`
- `Testzeit`
- `Seriennummer`
- `Bezeichnung`
- `Message`
- `Untere Grenze`
- `Wert`
- `Obere Grenze`
- `Ergebnis`

## Relevante Dateien

- `Model/`
  Importmodell fuer Testlaeufe und Schritte
- `Parsing/`
  CSV-Parser und Klassifikationslogik
- `Matching/`
  Extraktion sichtbarer Programmschritte und Match-Logik gegen CSV-Zeilen

## Hinweis

Die Bibliothek liefert Import-, Matching- und Integritaetsdaten fuer den CSV-gestuetzten Replay-/Vergleichsmodus in `Ct3xxSimulator.Desktop`. Sie fuehrt selbst keine Simulation aus.
