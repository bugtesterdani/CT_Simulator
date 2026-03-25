# simtest

Hauptbeispiel fuer den CT3xx-Simulator.

## Inhalt

- `ct3xx/`
  Beispiel-Testprogramm und CT3xx-Dateien
- `wireplan/`
  Haupt-WireViz, `simulation.yaml`, Unterbaugruppen und Fault-Beispiele
- `device/`
  Python- und deklarative DUT-Modelle

## Zweck

Dieses Beispiel demonstriert zusammen:

- Hauptverdrahtung mit WireViz
- hierarchische Unterverdrahtungen ueber `assembly`
- ein Relais im Hauptverdrahtungsplan sowie weitere Bauteile in Untermodulen
- deklarative Tester-Ausgangslogik fuer `UIF_OUT1` ueber `tester_supply` / `tester_output`
- DUT-Modelle in Python und YAML/JSON
- Schrittresultate in der Desktop-App
- Waveform-/AM2-DUT-Anbindung ueber die Pipe

In der Desktop-App ist das Beispiel aktuell so gedacht:

- das Relais im Hauptverdrahtungsplan bleibt in der Haupt-Verbindungsansicht als Inline-Bauteil sichtbar
- die Baugruppe `DeviceBoard` kann als echtes Untermodul in einer Detailansicht geoeffnet werden
- die Richtung des Signals bleibt dabei zwischen Haupt- und Unteransicht konsistent

## Einstieg in der App

- Testprogramm-Ordner: `simtest/ct3xx`
- Verdrahtungs-Ordner: `simtest/wireplan`
- Simulations-Ordner: `simtest/wireplan`
- DUT-Modell: z. B. `simtest/device/devices/IKI_good.json` oder `simtest/device/devices/IKI_waveform.yaml`

Details zu den DUT-Profilen stehen in [device/README.md](c:/Users/hello/Desktop/CT3xx/simtest/device/README.md).

Weitere Format- und Optionshinweise:

- [ct3xx/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/simtest/ct3xx/OPTIONS.md)
- [wireplan/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/simtest/wireplan/OPTIONS.md)
- [device/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/simtest/device/OPTIONS.md)
