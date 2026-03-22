# Ct3xxSimulator WinAppDriver Tests

These end-to-end tests exercise the WPF desktop UI via [WinAppDriver](https://github.com/microsoft/WinAppDriver). They are disabled by default and require a running WinAppDriver session plus a compiled version of `Ct3xxSimulator.Desktop`.

## Prerequisites

1. Install **WinAppDriver** and start it (default URL `http://127.0.0.1:4723`).
2. Build the desktop app so the executable exists at `Ct3xxSimulator.Desktop/bin/Debug/net9.0-windows10.0.19041.0/Ct3xxSimulator.Desktop.exe`.
3. (Optional) Override the default paths:
   - `set WINAPPDRIVER_URL=http://127.0.0.1:4723`
   - `set CT3XX_APP_PATH=C:\full\path\to\Ct3xxSimulator.Desktop.exe`

## Running the tests

```powershell
cd C:\Users\hello\Desktop\CT3xx
dotnet test Ct3xxSimulator.WinAppDriverTests\Ct3xxSimulator.WinAppDriverTests.csproj
```

The provided `StartupTests` verify that the main window can be launched and that the navigation tree is present. Extend these tests to cover additional workflows (loading `.ctxprg`, verifying log output, etc.).
