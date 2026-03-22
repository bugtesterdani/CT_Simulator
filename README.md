CT3xx Visual Simulator & Parser Toolkit
======================================

Dieses Repository enthält drei komplementäre Projekte rund um CT3xx‑Testprogramme:

- `Ct3xxSimulator.Desktop` – eine WPF‑Oberfläche, mit der sich `.ctxprg`‑Programme laden, visualisieren und simulieren lassen.
- `Ct3xxSimulator` – die Simulations‑ und Interaktionslogik (Console/WPF/Tests können sie gemeinsam nutzen).
- `Ct3xxProgramParser` – eine eigenständige .NET‑Standard‑Bibliothek, die CT3xx‑Programme und Signaltabellen als wiederverwendbare, starke Modelle bereitstellt (inklusive Discovery- und Convenience-APIs).

Der Ordner `testprogramme/` liefert umfangreiche Beispieldateien (CTXPRG, CTSIT, CTIFC, CAD, …), die sowohl vom Simulator wie auch von Tests genutzt werden.

Repositorystruktur
------------------

```
CT3xx/
├─ Ct3xxProgramParser/           # Parserlibrary (netstandard2.0)
├─ Ct3xxProgramParser.Tests/     # MSTest-Suite für Parser & File-Resolver
├─ Ct3xxSimulator/               # Kern-Simulatorlogik (net9.0)
├─ Ct3xxSimulator.Desktop/       # WPF-Oberfläche (net9.0-windows)
├─ Ct3xxSimulator.WinAppDriverTests/  # UI-Automation über WinAppDriver/Appium
├─ testprogramme/                # Beispielprogramme & Begleitdateien
└─ AGENT.md                      # Zusatzhinweise für Automationsszenarien
```

Voraussetzungen
---------------

- .NET SDK 9.0 (inkl. Windows Desktop-Workload für das WPF-Projekt).
- Windows 10/11 mit aktivem WinAppDriver, falls UI-Tests (`Ct3xxSimulator.WinAppDriverTests`) ausgeführt werden sollen.
- Optional: Visual Studio 2022 (17.10+) oder JetBrains Rider für komfortable Entwicklung.

Build & Laufzeit
----------------

1. **Restore & Build (alle Projekte)**  
   ```
   dotnet build Ct3xxSimulator.sln
   ```

2. **Desktop-App starten**  
   ```
   dotnet run --project Ct3xxSimulator.Desktop
   ```
   Die Anwendung lädt beim Start den Ordner `testprogramme/` automatisch (oder verwendet `CT3XX_TESTPROGRAM_ROOT`, falls gesetzt). Über die UI lassen sich Programme wählen, die Simulation starten sowie Messwerte/Operator-Dialoge emulieren.

3. **Parser-Library verwenden**  
   Füge `Ct3xxProgramParser` als Projekt- oder Paketreferenz hinzu. Beispiel:
   ```csharp
   using Ct3xxProgramParser.Programs;

   var parser = new Ct3xxProgramFileParser();
   var files = parser.Load(@"C:\CT3xx\testprogramme\UIF I2C Test\UIF I2C Test.ctxprg");

   foreach (var table in files.SignalTables)
   {
       Console.WriteLine($"{table.FileName}: {table.Content.Modules.Count} Module");
   }
   ```

Weitere Dateitypen einbinden
----------------------------

Der Parser nutzt ein Registrierungsmodell (`Ct3xxFileParserRegistry`), in dem jede Dateiendung über eine eigene Klasse (`ICt3xxFileParser`) angehängt wird. Für neue Formate gehst du so vor:

1. **Dokumentklasse definieren**  
   Leite von `TextCt3xxFileDocument` oder `BinaryCt3xxFileDocument` ab und füge bei Bedarf strukturierte Properties hinzu.

2. **Parser implementieren**  
   Entweder `TextFileParser<T>` bzw. `BinaryFileParser<T>` ableiten oder `ICt3xxFileParser` direkt implementieren. Gib die Extension (z.B. `.ctxyz`) zurück und mappe den gelesenen Inhalt auf dein Dokument.

3. **Parser registrieren**  
   Entweder `Ct3xxFileParserRegistry.CreateDefault().Add(new MyParser())` beim Erzeugen des `Ct3xxProgramFileParser` verwenden oder – falls du das Default-Verhalten erweitern willst – die Registrierung direkt im Code anpassen:

   ```csharp
   var registry = Ct3xxFileParserRegistry.CreateDefault()
       .Add(new MyCustomFileParser());

   var parser = new Ct3xxProgramFileParser(registry: registry);
   var fileSet = parser.Load(programPath);
   var customDocs = fileSet.GetDocuments<MyCustomDocument>();
   ```

Auf diese Weise bleiben neue Dateitypen gekapselt, und bestehende Verbraucher können gezielt nach `fileSet.GetDocuments<T>()` filtern, ohne zentrale Klassen anfassen zu müssen.

Tests
-----

- **Parser & Discovery**  
  ```
  dotnet test Ct3xxProgramParser.Tests
  ```
  Diese Tests greifen auf echte Dateien im Ordner `testprogramme/` zu und stellen sicher, dass multi-modulare Signaltabellen korrekt geparst sowie Table-Verweise aus `.ctxprg` aufgelöst werden.

- **Simulator-Unit-Tests**  
  Weitere Tests befinden sich im Projekt `Ct3xxSimulator` (z.B. Expression-Evaluator); sie laufen automatisch mit `dotnet test` auf der Lösung.

- **WinAppDriver UI-Tests**  
  ```
  WinAppDriver.exe   # separat starten
  dotnet test Ct3xxSimulator.WinAppDriverTests
  ```
  Voraussetzungen:
  - Desktop-App muss gebaut sein (Standard-Pfad: `Ct3xxSimulator.Desktop/bin/Debug/net9.0-windows.../Ct3xxSimulator.Desktop.exe`).
  - Windows-Desktop darf während des Runs nicht gesperrt sein.
  - Die Tests lesen Log-Dateien unter `%TEMP%\Ct3xxSimulator\automation-*.log` (siehe `AGENT.md`).

Wichtige Umgebungsvariablen
---------------------------

- `CT3XX_TESTPROGRAM_ROOT` – überschreibt den automatisch gefundenen `testprogramme/`-Pfad (für Parser, Desktop-App und Tests).
- `CT3XX_APP_PATH` – Pfad zur kompilierten WPF-Anwendung (für WinAppDriver-Tests, falls vom Standard abweichend).
- `WINAPPDRIVER_URL` – falls WinAppDriver nicht auf `http://127.0.0.1:4723` lauscht.

Publishing-Hinweise
-------------------

- **GitHub Actions / CI:** verwende `dotnet build` + `dotnet test`. Für UI-Tests empfiehlt sich ein separater Workflow mit Windows-Runner und manuellem Start von WinAppDriver.
- **NuGet-Paket (optional):** `Ct3xxProgramParser` enthält bereits Paket-Metadaten (`PackageId`, `Description`). Mit `dotnet pack Ct3xxProgramParser/Ct3xxProgramParser.csproj -c Release` lässt sich ein eigenständiges Paket erzeugen.
- **Lizenz & Issues:** Ergänze bei Bedarf noch eine `LICENSE` sowie Issue-/PR-Vorlagen, bevor du das Repo öffentlich machst.

Support / Weiterentwicklung
---------------------------

- Parser um weitere Dateiformate erweitern (`*.ctifc`, `*.ctbrd`, Logfiles).
- Simulator UI: zusätzliche Visualisierung (z.B. Baumansicht, Messwertverlauf).
- Automationsszenarien ausbauen (z.B. weitere Sampleprogramme, Negative Tests).

Viel Erfolg beim Veröffentlichen! Ergänze README-Abschnitte gerne um Screenshots oder GIFs, sobald die Desktop-App visuell präsentiert werden soll.
