# Optionen fuer `simtest_template_sm2/ct3xx`

## Relevante Dateien

- `template_SM2.ctxprg`
- `Signaltable.ctsit`
- `Interfacetable.ctifc`
- `waiter.bat`

## Wichtige Punkte

- `template_SM2.ctxprg` verwendet `waiter.bat` relativ ueber `TestProgramPath()`.
- `E488` schreibt die Antwort in die Variable `Result`.
- `PET$` wertet `Result[1]` bis `Result[4]` aus.
- `REL_HV` kommt aus der Signaltabelle und wird per `IOXX` mit `H` und `L` gesetzt.
