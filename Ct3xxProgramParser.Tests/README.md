# Ct3xxProgramParser.Tests

Testprojekt fuer `Ct3xxProgramParser`.

## Zweck

- Absicherung des Ladens von `.ctxprg`
- Pruefung von Signaltabellen und referenzierten CT3xx-Dateien
- Regressionstests fuer Parserverhalten

## Ausfuehrung

```powershell
dotnet test Ct3xxProgramParser.Tests\Ct3xxProgramParser.Tests.csproj
```

Viele Tests greifen auf die Beispielprogramme unter `testprogramme/` zurueck.
