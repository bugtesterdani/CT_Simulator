# Ct3xxSimulator.Tests

Regressionstests fuer den Simulationskern.

## Zweck

Dieses Projekt prueft reale Simulator-Szenarien statt nur Parser-Detailfaelle.

Abgedeckt werden unter anderem:

- Validierung des `simtest`-Standardszenarios
- Runtime-Signalaufloesung zwischen CT3xx, WireViz, Baugruppe und DUT
- End-to-End-Lauf mit deklarativem DUT-Profil `good`
- End-to-End-Lauf mit deklarativem DUT-Profil `bad`
- End-to-End-Lauf fuer das Transformator-/Stromwandler-Beispiel
- Elementverhalten fuer `relay`, `resistor`, `switch`, `fuse`, `diode`, `load`, `voltage_divider`, `sensor`, `opto`, `transistor`, `transformer`, `current_transformer`
- Tester-Ausgangslogik ueber `tester_supply` / `tester_output`
- Skript- und Dateiausfuehrung aus Testprogrammen inklusive Exit-Code-Auswertung

## Hinweise

- Die End-to-End-Tests starten die Python-DUT-Simulation ueber Named Pipes.
- Dafuer muss ein lokaler Python-Interpreter mit `pywin32` verfuegbar sein.
- Wenn kein passender Python-Interpreter gefunden wird, werden diese Tests als nicht ausfuehrbar markiert.

## Ausfuehren

```powershell
dotnet test Ct3xxSimulator.Tests\Ct3xxSimulator.Tests.csproj
```
