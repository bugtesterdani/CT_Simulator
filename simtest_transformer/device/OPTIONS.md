# simtest_transformer/device Optionen

Dieser Ordner enthaelt das DUT-Modell fuer das Transformator-/Stromwandler-Beispiel.

## Typische Dateien

- `main.py`
- `devices/TrafoStromwandler_good.yaml`

## Erwartete Signale

Beispielhaft:

- Spannungspfad fuer den Transformator-Eingang
- Ausgangssignal fuer `LOAD_CURRENT`

## Worauf geachtet werden sollte

- das DUT-Profil muss die durch den Trafo skalierten Spannungen korrekt erwarten
- der Stromwandler liefert sekundaerseitig einen skalierten Wert, nicht den Primaerstrom direkt
