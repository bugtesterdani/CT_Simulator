# Ct3xxProgramParser

Diese Bibliothek stellt wiederverwendbare Modelle und Parser fuer CT3xx-Programmpakete bereit. Sie enthaelt unter anderem:

- Ct3xxProgramLoader zum Deserialisieren von *.ctxprg Dateien in starke Typen (Ct3xxProgram).
- SignalTableParser zum Einlesen der textbasierten Signaltable.ctsit Dateien inklusive Modul- und Kanaldefinitionen.
- Ct3xxProgramFileParser als Convenience-API, um ein komplettes Testprogramm zu laden und alle referenzierten Signaltabellen samt Pfad/Metadaten aufzufinden.
- TestProgramDiscovery um den 	estprogramme-Stammordner automatisch zu ermitteln.

## Beispiel

`csharp
using Ct3xxProgramParser.Programs;

var parser = new Ct3xxProgramFileParser();
var files = parser.Load(@"C:\\CT3xx\\testprogramme\\UIF I2C Test\\UIF I2C Test.ctxprg");

Console.WriteLine($"Programm: {files.Program.ProgramComment}");
foreach (var table in files.SignalTables)
{
    Console.WriteLine($"Signaltable {table.FileName}: {table.Content.Modules.Count} Module");
}
`

Die Library zielt auf 
etstandard2.0 ab und kann dadurch in .NET Framework- sowie modernen .NET-Anwendungen verwendet werden. Tests und Beispielprogramme greifen auf die Dateien unter 	estprogramme/ zurueck.
