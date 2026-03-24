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
- Simulationsbauteile wie Relais und Widerstaende
- DUT-Modelle in Python und YAML/JSON
- Schrittresultate in der Desktop-App
- Waveform-/AM2-DUT-Anbindung ueber die Pipe

## Einstieg in der App

- Testprogramm-Ordner: `simtest/ct3xx`
- Verdrahtungs-Ordner: `simtest/wireplan`
- Simulations-Ordner: `simtest/wireplan`
- DUT-Modell: z. B. `simtest/device/devices/IKI_good.json` oder `simtest/device/devices/IKI_waveform.yaml`

Details zu den DUT-Profilen stehen in [device/README.md](c:/Users/hello/Desktop/CT3xx/simtest/device/README.md).
