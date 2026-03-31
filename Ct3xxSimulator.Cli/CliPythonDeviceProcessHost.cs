// Provides Cli Python Device Process Host for the command-line interface support code.
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

namespace Ct3xxSimulator.Cli;

internal sealed class CliPythonDeviceProcessHost : IDisposable
{
    private readonly Process _process;
    private readonly string _pipePath;
    private readonly StringBuilder _stdout = new();
    private readonly StringBuilder _stderr = new();

    /// <summary>
    /// Initializes a new instance of CliPythonDeviceProcessHost.
    /// </summary>
    private CliPythonDeviceProcessHost(Process process, string pipePath)
    {
        _process = process;
        _pipePath = pipePath;
    }

    /// <summary>
    /// Gets the pipe path.
    /// </summary>
    public string PipePath => _pipePath;

    /// <summary>
    /// Executes start.
    /// </summary>
    public static CliPythonDeviceProcessHost? Start(string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
        {
            return null;
        }

        var launcher = ResolvePythonLauncher()
            ?? throw new InvalidOperationException("Kein geeigneter Python-Interpreter mit pywin32 gefunden.");
        var launchSpec = ResolveDeviceLaunchSpec(scriptPath);
        var pipePath = $@"\\.\pipe\ct3xx-cli-{Guid.NewGuid():N}";

        var startInfo = new ProcessStartInfo
        {
            FileName = launcher.FileName,
            Arguments = $"{launcher.Arguments} {launchSpec.Arguments} --pipe \"{pipePath}\"".Trim(),
            WorkingDirectory = launchSpec.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = startInfo };
        var host = new CliPythonDeviceProcessHost(process, pipePath);
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

        WaitForPipe(pipePath, host);
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

    /// <summary>
    /// Executes WaitForPipe.
    /// </summary>
    private static void WaitForPipe(string pipePath, CliPythonDeviceProcessHost host)
    {
        var pipeName = pipePath.Split('\\', StringSplitOptions.RemoveEmptyEntries).Last();
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            if (host._process.HasExited)
            {
                throw new InvalidOperationException($"Python-Geraetemodell wurde beendet.{host.GetOutput()}");
            }

            try
            {
                using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
                client.Connect(200);
                return;
            }
            catch
            {
                Thread.Sleep(100);
            }
        }

        throw new TimeoutException($"Pipe '{pipePath}' wurde nicht rechtzeitig bereitgestellt.{host.GetOutput()}");
    }

    /// <summary>
    /// Executes GetOutput.
    /// </summary>
    private string GetOutput()
    {
        var stdout = _stdout.ToString().Trim();
        var stderr = _stderr.ToString().Trim();
        if (string.IsNullOrWhiteSpace(stdout) && string.IsNullOrWhiteSpace(stderr))
        {
            return string.Empty;
        }

        return $"{Environment.NewLine}stdout:{Environment.NewLine}{stdout}{Environment.NewLine}stderr:{Environment.NewLine}{stderr}";
    }

    /// <summary>
    /// Executes ResolvePythonLauncher.
    /// </summary>
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

        return candidates.FirstOrDefault(CanImportPyWin32);
    }

    /// <summary>
    /// Executes CanImportPyWin32.
    /// </summary>
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

    /// <summary>
    /// Executes SplitCommand.
    /// </summary>
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

    /// <summary>
    /// Executes ResolveDeviceLaunchSpec.
    /// </summary>
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

                return new DeviceLaunchSpec($"\"{mainPath}\" \"{Path.GetFileNameWithoutExtension(fullPath)}\"", Path.GetDirectoryName(mainPath) ?? Environment.CurrentDirectory);
            }

            return new DeviceLaunchSpec($"\"{fullPath}\"", Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory);
        }

        throw new InvalidOperationException($"Nicht unterstuetzter Geraetemodell-Typ '{extension}'.");
    }

    /// <summary>
    /// Executes FindSimtestMain.
    /// </summary>
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

    /// <summary>
    /// Executes PythonLauncher.
    /// </summary>
    private readonly record struct PythonLauncher(string FileName, string Arguments);
    /// <summary>
    /// Executes DeviceLaunchSpec.
    /// </summary>
    private readonly record struct DeviceLaunchSpec(string Arguments, string WorkingDirectory);
}
