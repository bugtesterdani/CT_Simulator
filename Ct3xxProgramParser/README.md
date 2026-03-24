# Ct3xxProgramParser

Diese Bibliothek stellt Modelle und Parser fuer CT3xx-Testprogramme und deren referenzierte Dateien bereit.

## Enthaltene Parser

- `Ct3xxProgramLoader`
  Laedt `.ctxprg`-Dateien in das Objektmodell `Ct3xxProgram`.
- `Ct3xxProgramFileParser`
  Laedt ein komplettes Programmpaket inklusive referenzierter CT3xx-Dateien.
- `SignalTableParser`
  Liest `Signaltable.ctsit`.
- Textdatei-Parser fuer typische CT3xx-Nebenformate wie:
  - `.ctarb`
  - `.ctdig`
  - `.ctict`
  - `.ctbrd`

## Typischer Einsatz

```csharp
using Ct3xxProgramParser.Programs;

var parser = new Ct3xxProgramFileParser();
var files = parser.Load(@"C:\CT3xx\testprogramme\Messung kleiner Spulen\Messung kleiner Spulen.ctxprg");

Console.WriteLine(files.Program.ProgramComment);
Console.WriteLine(files.Program.RootItems.Count);

foreach (var signalTable in files.SignalTables)
{
    Console.WriteLine($"{signalTable.FileName}: {signalTable.Table.Modules.Count} Module");
}
```

## Wichtige Typen

- `Ct3xxProgram`
  Das Hauptmodell fuer das XML-Testprogramm.
- `Ct3xxProgramFileSet`
  Kombination aus Hauptprogramm und geladenen externen CT3xx-Dateien.
- `SignalTableDocument`
  Geparste Signaltabelle.
- `ArbitraryWaveformDocument`
  Geladene `.ctarb`-Datei fuer AM2-/Waveform-Tests.

## Ziel

Die Bibliothek dient als gemeinsame Lade- und Parserbasis fuer:

- den Desktop-Simulator
- Test-Utilities
- moegliche spaetere Import-/Analysewerkzeuge

Sie zielt auf `netstandard2.0` und ist damit auch aus aelteren .NET-Anwendungen nutzbar.
