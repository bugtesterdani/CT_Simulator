# Agent Notes

## Automated Desktop Simulation
- The WinAppDriver tests launch `Ct3xxSimulator.Desktop.exe` with arguments `--auto-program <ctxprg> --automation-log <log> --auto-operator --am2 1 --auto-start`. When debugging automation issues, inspect `%TEMP%\Ct3xxSimulator\automation-*.log` (or the file passed via `--automation-log`) – the WPF app writes every log entry there.
- Command-line switches and the environment variables `CT3XX_AUTO_PROGRAM`, `CT3XX_AUTOMATION_LOG`, `CT3XX_AUTO_OPERATOR`, `CT3XX_AM2`, and `CT3XX_AUTO_START` all hydrate `AppStartupOptions`. If nothing is provided, the app falls back to the sample `.ctxprg` that gets copied next to the desktop executable.

## WinAppDriver Tests
- Tests now run sequentially (`Workers = 1`) to avoid parallel WinAppDriver sessions. Ensure `WinAppDriver.exe` is running and the desktop session is unlocked before invoking `dotnet test Ct3xxSimulator.WinAppDriverTests`.
- Failures in `SimulationShouldExposeIctAndAm2TestsAndProduceResultLog` usually mean the automation log never recorded the expected line. Check the log file path printed in the test output and confirm it contains entries like `Automatisches Laden`, `Test: ICT1`, `Wellentest`, and `Simulation abgeschlossen`.

## Manual Simulation
- The WPF header shows the currently loaded `.ctxprg` and renders a status overlay for display/query instructions. All operator dialogs are mirrored to the log pane (and the automation log file), so you can replay the sequence by scanning the log.

## Before summary
- Run dotnet build to test for Syntax or other Errors on CT3xxSimulator.Desktop