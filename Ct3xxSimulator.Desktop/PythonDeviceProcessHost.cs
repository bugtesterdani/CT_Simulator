// Provides Python Device Process Host for the desktop application support code.
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace Ct3xxSimulator.Desktop;

internal sealed class PythonDeviceProcessHost : IDisposable
{
    private readonly Process _process;
    private readonly string _pipePath;
    private readonly StringBuilder _stdout = new();
    private readonly StringBuilder _stderr = new();
    private readonly string _launchCommand;

    private PythonDeviceProcessHost(Process process, string pipePath, string launchCommand)
    {
        _process = process;
        _pipePath = pipePath;
        _launchCommand = launchCommand;
    }

    /// <summary>
    /// Gets the pipe path.
    /// </summary>
    public string PipePath => _pipePath;

    /// <summary>
    /// Executes start.
    /// </summary>
    public static PythonDeviceProcessHost? Start(string scriptPath)
    {
        if (scriptPath == null || !File.Exists(scriptPath))
        {
            return null;
        }

        var pipeName = $@"\\.\pipe\ct3xx-simtest-{Guid.NewGuid():N}";
        var launcher = ResolvePythonLauncher()
            ?? throw new InvalidOperationException("Kein geeigneter Python-Interpreter mit pywin32 gefunden. Getestet wurden CT3XX_PYTHON_EXE, py -3.13, py -3 und python.");

        var launchSpec = ResolveDeviceLaunchSpec(scriptPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = launcher.FileName,
            Arguments = $"{launcher.Arguments} {launchSpec.Arguments} --pipe \"{pipeName}\"".Trim(),
            WorkingDirectory = launchSpec.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        var host = new PythonDeviceProcessHost(process, pipeName, $"{launcher.FileName} {launcher.Arguments} {launchSpec.Arguments}".Trim());
        process.Start();
        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                host._stdout.AppendLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                host._stderr.AppendLine(args.Data);
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        WaitForPipe(pipeName, host);
        return host;
    }

    /// <summary>
    /// Executes dispose.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(1000);
            }
        }
        catch
        {
        }
        finally
        {
            _process.Dispose();
        }
    }

    private static void WaitForPipe(string pipePath, PythonDeviceProcessHost host)
    {
        var (_, pipeName) = SplitPipePath(pipePath);
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            if (host._process.HasExited)
            {
                var details = host.GetProcessOutput();
                throw new InvalidOperationException($"Python-GerÃ¤tesimulation wurde sofort beendet. Command='{host._launchCommand}' ExitCode={host._process.ExitCode}{details}");
            }

            try
            {
                using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
                client.Connect(200);
                return;
            }
            catch (TimeoutException)
            {
            }
            catch (IOException)
            {
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException($"Python-GerÃ¤tesimulation auf Pipe '{pipePath}' wurde nicht rechtzeitig gestartet. Command='{host._launchCommand}'.{host.GetProcessOutput()}");
    }

    private static (string Server, string PipeName) SplitPipePath(string pipePath)
    {
        var parts = pipePath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            throw new FormatException($"UngÃ¼ltiger Pipe-Pfad '{pipePath}'.");
        }

        return (parts[0], parts[2]);
    }

    private string GetProcessOutput()
    {
        var stdout = _stdout.ToString().Trim();
        var stderr = _stderr.ToString().Trim();
        if (string.IsNullOrWhiteSpace(stdout) && string.IsNullOrWhiteSpace(stderr))
        {
            return string.Empty;
        }

        return $"{Environment.NewLine}stdout:{Environment.NewLine}{stdout}{Environment.NewLine}stderr:{Environment.NewLine}{stderr}";
    }

    private static PythonLauncher? ResolvePythonLauncher()
    {
        var configured = Environment.GetEnvironmentVariable("CT3XX_PYTHON_EXE");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var launcher = SplitCommand(configured);
            if (CanImportPyWin32(launcher))
            {
                return launcher;
            }
        }

        var candidates = new[]
        {
            new PythonLauncher("py", "-3.13"),
            new PythonLauncher("py", "-3"),
            new PythonLauncher("python", string.Empty)
        };

        foreach (var candidate in candidates)
        {
            if (CanImportPyWin32(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool CanImportPyWin32(PythonLauncher launcher)
    {
        try
        {
            var args = string.IsNullOrWhiteSpace(launcher.Arguments)
                ? "-c \"import pywintypes, win32pipe\""
                : $"{launcher.Arguments} -c \"import pywintypes, win32pipe\"";
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = launcher.FileName,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            if (process == null)
            {
                return false;
            }

            process.WaitForExit(4000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static PythonLauncher SplitCommand(string command)
    {
        var trimmed = command.Trim();
        var firstSpace = trimmed.IndexOf(' ');
        if (firstSpace < 0)
        {
            return new PythonLauncher(trimmed, string.Empty);
        }

        return new PythonLauncher(trimmed[..firstSpace], trimmed[(firstSpace + 1)..].Trim());
    }

    private static DeviceLaunchSpec ResolveDeviceLaunchSpec(string modelPath)
    {
        var fullPath = Path.GetFullPath(modelPath);
        var extension = Path.GetExtension(fullPath);
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".yml", StringComparison.OrdinalIgnoreCase))
        {
            var mainPath = FindSimtestMain(fullPath)
                ?? throw new InvalidOperationException($"Kein simtest/device/main.py fuer Profil '{modelPath}' gefunden.");
            return new DeviceLaunchSpec($"\"{mainPath}\" --profile \"{fullPath}\"", Path.GetDirectoryName(mainPath) ?? Environment.CurrentDirectory);
        }

        if (extension.Equals(".py", StringComparison.OrdinalIgnoreCase))
        {
            var parentName = Path.GetFileName(Path.GetDirectoryName(fullPath));
            if (string.Equals(parentName, "devices", StringComparison.OrdinalIgnoreCase))
            {
                var devicesDirectory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Ungueltiger Geraetemodellpfad.");
                var mainPath = Path.Combine(Directory.GetParent(devicesDirectory)!.FullName, "main.py");
                if (!File.Exists(mainPath))
                {
                    throw new InvalidOperationException($"Kein simtest/device/main.py fuer Modul '{modelPath}' gefunden.");
                }

                var deviceName = Path.GetFileNameWithoutExtension(fullPath);
                return new DeviceLaunchSpec($"\"{mainPath}\" \"{deviceName}\"", Path.GetDirectoryName(mainPath) ?? Environment.CurrentDirectory);
            }

            return new DeviceLaunchSpec($"\"{fullPath}\"", Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory);
        }

        throw new InvalidOperationException($"Nicht unterstuetzter Geraetemodell-Typ '{extension}'.");
    }

    private static string? FindSimtestMain(string modelPath)
    {
        var directory = Path.GetDirectoryName(modelPath);
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var candidate = Path.Combine(directory, "main.py");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }

    private readonly record struct PythonLauncher(string FileName, string Arguments);
    private readonly record struct DeviceLaunchSpec(string Arguments, string WorkingDirectory);
}
