// Provides Python Device Process Fixture for the simulator test project support code.
using System.Diagnostics;
using System.IO.Pipes;

namespace Ct3xxSimulator.Tests;

internal sealed class PythonDeviceProcessFixture : IDisposable
{
    private readonly Process _process;

    private PythonDeviceProcessFixture(Process process, string pipePath)
    {
        _process = process;
        PipePath = pipePath;
    }

    /// <summary>
    /// Gets the pipe path.
    /// </summary>
    public string PipePath { get; }

    /// <summary>
    /// Executes start profile.
    /// </summary>
    public static PythonDeviceProcessFixture StartProfile(string profileRelativePath)
    {
        var profilePath = TestData.GetPath(profileRelativePath);
        var mainPath = TestData.GetPath(@"simtest\device\main.py");
        var pipePath = $@"\\.\pipe\ct3xx-tests-{Guid.NewGuid():N}";
        var launcher = ResolvePythonLauncher()
            ?? throw new AssertInconclusiveException("Kein Python-Interpreter mit pywin32 gefunden. Tests wurden uebersprungen.");

        var startInfo = new ProcessStartInfo
        {
            FileName = launcher.FileName,
            Arguments = $"{launcher.Arguments} \"{mainPath}\" --profile \"{profilePath}\" --pipe \"{pipePath}\"".Trim(),
            WorkingDirectory = Path.GetDirectoryName(mainPath) ?? TestData.RootDirectory,
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

        process.Start();
        WaitForPipe(pipePath, process);
        Thread.Sleep(200);
        return new PythonDeviceProcessFixture(process, pipePath);
    }

    /// <summary>
    /// Executes start module.
    /// </summary>
    public static PythonDeviceProcessFixture StartModule(string mainRelativePath, string deviceModuleName)
    {
        var mainPath = TestData.GetPath(mainRelativePath);
        var pipePath = $@"\\.\pipe\ct3xx-tests-{Guid.NewGuid():N}";
        var launcher = ResolvePythonLauncher()
            ?? throw new AssertInconclusiveException("Kein Python-Interpreter mit pywin32 gefunden. Tests wurden uebersprungen.");

        var startInfo = new ProcessStartInfo
        {
            FileName = launcher.FileName,
            Arguments = $"{launcher.Arguments} \"{mainPath}\" \"{deviceModuleName}\" --pipe \"{pipePath}\"".Trim(),
            WorkingDirectory = Path.GetDirectoryName(mainPath) ?? TestData.RootDirectory,
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

        process.Start();
        WaitForPipe(pipePath, process);
        Thread.Sleep(200);
        return new PythonDeviceProcessFixture(process, pipePath);
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
                _process.WaitForExit(2000);
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

    private static void WaitForPipe(string pipePath, Process process)
    {
        var parts = pipePath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        var pipeName = parts[^1];
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                var stdOut = process.StandardOutput.ReadToEnd();
                var stdErr = process.StandardError.ReadToEnd();
                throw new AssertFailedException($"Python-DUT wurde vorzeitig beendet. stdout={stdOut} stderr={stdErr}");
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

        throw new AssertFailedException($"Python-DUT Pipe '{pipePath}' wurde nicht rechtzeitig erreichbar.");
    }

    private static PythonLauncher? ResolvePythonLauncher()
    {
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
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = launcher.FileName,
                Arguments = string.IsNullOrWhiteSpace(launcher.Arguments)
                    ? "-c \"import pywintypes, win32pipe\""
                    : $"{launcher.Arguments} -c \"import pywintypes, win32pipe\"",
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

    private readonly record struct PythonLauncher(string FileName, string Arguments);
}
