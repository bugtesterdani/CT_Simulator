# Offene ToDos

## CSV-Replay

- Die manuell erstellte Beispiel-CSV fuer `template_splitted_am2` ist aktuell noch unvollstaendig.
- Fuer `2ARB`-/Waveform-Schritte muessen in der CSV nicht nur die sichtbaren Untertests wie `LED Abfrage` oder `Helligkeit LED` stehen, sondern auch die relevanten Waveform-Auswertungen wie `VMax`, `VMin` und weitere geloggte Kennwerte, sofern das reale CT3xx-Logging diese ebenfalls schreibt.
- Solange diese CSV-Kennwerte fehlen, ist die CSV nur eingeschraenkt als realistische Referenz fuer den Replay-/Vergleichsmodus geeignet.
- Bei der naechsten Ueberarbeitung der Beispiel-CSV pruefen:
  - welche Waveform-Metriken vom realen Testsystem wirklich in die CSV geschrieben werden
  - unter welchen Bezeichnungen sie erscheinen
  - ob dafuer zusaetzliche Matching-Regeln oder CSV-Klassifikationen im Parser noetig sind
