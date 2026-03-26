# simtest_transformer

Vollstaendiges End-to-End-Beispiel fuer einen Spannungstransformator und einen Stromwandler im echten Simulationspfad.

## Auswahl in der App

- Testprogramm-Ordner: `simtest_transformer/ct3xx`
- Verdrahtungs-Ordner: `simtest_transformer/wireplan`
- Simulations-Ordner: `simtest_transformer/wireplan`
- Geraetemodell: `simtest/device/devices/TrafoStromwandler_good.yaml`

## Fachliches Verhalten

- `T1` ist ein Transformator mit `ratio: 10.0`
- `CT1` ist ein Stromwandler mit `ratio: 2000`
- Das DUT bekommt also bei `20 V` am Primaerpfad nur `2 V` an `VT_INPUT`
- Das DUT liefert bei aktivem Eingang `LOAD_CURRENT = 2.0 A`
- Am Stromwandler-Sekundaerpfad werden daraus `0.001 A`

## Erwartete Schritte

- `Trafo OFF` -> `0.0 V`
- `Stromwandler OFF` -> `0.0 A`
- `Trafo ON` -> `2.0 V`
- `Stromwandler ON` -> `0.001 A`

## Struktur

- `ct3xx/Transformer_Current_Example.ctxprg`
- `ct3xx/Signaltable.ctsit`
- `wireplan/Verdrahtung.yml`
- `wireplan/simulation.yaml`
- gemeinsames Device-Framework unter `simtest/device`
- deklaratives Profil `simtest/device/devices/TrafoStromwandler_good.yaml`

Detailoptionen:

- [OPTIONS.md](C:/Users/hello/Desktop/CT3xx/simtest_transformer/OPTIONS.md)
- [ct3xx/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/simtest_transformer/ct3xx/OPTIONS.md)
- [wireplan/OPTIONS.md](C:/Users/hello/Desktop/CT3xx/simtest_transformer/wireplan/OPTIONS.md)
- [simtest/device/README.md](C:/Users/hello/Desktop/CT3xx/simtest/device/README.md)
