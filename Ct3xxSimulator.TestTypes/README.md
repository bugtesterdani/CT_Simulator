CT3xxSimulator.TestTypes
========================

Vorbereitete Heimat fuer testtypspezifische Simulator-Handler.

Aktuell enthaelt das Projekt nur die ersten Vertragsflaechen fuer kuenftige Handler.

Zielbild:

- pro CT3xx-Testtyp ein klarer Handler
- Dispatcher im Kern statt wachsender Monolith in `Ct3xxProgramSimulator`
- schrittweise Umsetzung entlang der `SUPPORT_MATRIX.md`

Beispiele fuer kuenftige Handler:

- `I2cTestHandler`
- `SpiTestHandler`
- `SmudTestHandler`
- `ConnectionTestHandler`
- `DigitalPatternTestHandler`
- `Am4StimulusAcquisitionHandler`
