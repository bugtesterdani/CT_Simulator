# MDA1 SC3 Szenario

Dieses Szenario laedt das Testprogramm **CT3xx Testadapter - Tutor MDA1 SC3** inkl. CTCT/SHRT/ICT.

Enthalten:

- `ct3xx/` mit dem originalen Testprogramm und allen referenzierten Dateien
- `wireplan/` mit einer direkten 1:1 Verdrahtung fuer `TP1..TP96`
- Python-DUT `mda1_sc3_device.py`, der ICT-Nennwerte liefert und SHRT ohne Kurzschluesse meldet
- YAML-DUT `mda1_sc3_device.yaml` als deklaratives Vergleichsprofil

Start in der Desktop-UI:

1. `scenarios.json` auswaehlen und das Preset laden.
2. Simulation starten.
