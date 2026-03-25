using System.Text.Json;
using Ct3xxProgramParser.Programs;
using Ct3xxSimulator.Export;
using Ct3xxSimulator.Simulation;
using Ct3xxSimulator.Validation;

namespace Ct3xxSimulator.Cli;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var options = CliOptions.Parse(args);
            if (options.HelpRequested)
            {
                Console.WriteLine(CliOptions.GetUsage());
                return 0;
            }

            var configuration = ResolveConfiguration(options);
            var issues = SimulationConfigurationValidator.Validate(
                configuration.ProgramFile,
                configuration.WiringFolder,
                configuration.SimulationFolder,
                configuration.DutModelPath);

            if (issues.Count > 0)
            {
                Console.Error.WriteLine("Validierungsfehler:");
                foreach (var issue in issues)
                {
                    Console.Error.WriteLine($"- {issue}");
                }

                return 2;
            }

            if (options.ValidateOnly)
            {
                Console.WriteLine("Validierung: OK");
                return 0;
            }

            var previousWireVizRoot = Environment.GetEnvironmentVariable("CT3XX_WIREVIZ_ROOT", EnvironmentVariableTarget.Process);
            var previousSimulationRoot = Environment.GetEnvironmentVariable("CT3XX_SIMULATION_MODEL_ROOT", EnvironmentVariableTarget.Process);
            var previousPipe = Environment.GetEnvironmentVariable("CT3XX_PY_DEVICE_PIPE", EnvironmentVariableTarget.Process);
            CliPythonDeviceProcessHost? pythonHost = null;

            try
            {
                Environment.SetEnvironmentVariable("CT3XX_WIREVIZ_ROOT", configuration.WiringFolder, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("CT3XX_SIMULATION_MODEL_ROOT", configuration.SimulationFolder, EnvironmentVariableTarget.Process);

                if (!string.IsNullOrWhiteSpace(configuration.DutModelPath))
                {
                    pythonHost = CliPythonDeviceProcessHost.Start(configuration.DutModelPath!);
                    if (pythonHost == null)
                    {
                        throw new InvalidOperationException("Das DUT-Modell konnte nicht gestartet werden.");
                    }

                    Environment.SetEnvironmentVariable("CT3XX_PY_DEVICE_PIPE", pythonHost.PipePath, EnvironmentVariableTarget.Process);
                }

                var parser = new Ct3xxProgramFileParser();
                var fileSet = parser.Load(configuration.ProgramFile!);
                var observer = new CliSimulationObserver();
                var simulator = new Ct3xxProgramSimulator(new ConsoleInteractionProvider(), observer, new NullSimulationExecutionController());
                simulator.Run(fileSet, options.DutLoopIterations);

                var exitCode = observer.GetExitCode();
                Console.WriteLine($"CLI beendet mit Exit-Code {exitCode}.");

                if (!string.IsNullOrWhiteSpace(options.ExportPath))
                {
                    var document = observer.CreateExportDocument(BuildConfigurationSummary(configuration, options));
                    SimulationResultExportWriter.Write(options.ExportPath!, document);
                    Console.WriteLine($"Export geschrieben: {options.ExportPath}");
                }

                return exitCode;
            }
            finally
            {
                pythonHost?.Dispose();
                Environment.SetEnvironmentVariable("CT3XX_WIREVIZ_ROOT", previousWireVizRoot, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("CT3XX_SIMULATION_MODEL_ROOT", previousSimulationRoot, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("CT3XX_PY_DEVICE_PIPE", previousPipe, EnvironmentVariableTarget.Process);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static ResolvedConfiguration ResolveConfiguration(CliOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.PresetFile) || !string.IsNullOrWhiteSpace(options.PresetName))
        {
            if (string.IsNullOrWhiteSpace(options.PresetFile) || string.IsNullOrWhiteSpace(options.PresetName))
            {
                throw new ArgumentException("Fuer Presets werden --preset-file und --preset zusammen benoetigt.");
            }

            var presets = JsonSerializer.Deserialize<List<CliScenarioPreset>>(
                              File.ReadAllText(options.PresetFile!),
                              new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
                          new List<CliScenarioPreset>();
            var preset = presets.FirstOrDefault(item => string.Equals(item.Name, options.PresetName, StringComparison.OrdinalIgnoreCase))
                         ?? throw new InvalidOperationException($"Preset '{options.PresetName}' nicht gefunden.");

            return new ResolvedConfiguration(
                ResolveProgramFile(options.ProgramFile, preset.TestProgramFolderPath),
                preset.WiringFolderPath,
                preset.SimulationModelFolderPath,
                preset.PythonScriptPath);
        }

        return new ResolvedConfiguration(
            ResolveProgramFile(options.ProgramFile, options.ProgramFolder),
            options.WiringFolder,
            options.SimulationFolder,
            options.DutModelPath);
    }

    private static string? ResolveProgramFile(string? programFile, string? programFolder)
    {
        if (!string.IsNullOrWhiteSpace(programFile))
        {
            return Path.GetFullPath(programFile);
        }

        if (string.IsNullOrWhiteSpace(programFolder) || !Directory.Exists(programFolder))
        {
            return null;
        }

        var programs = Directory.GetFiles(programFolder, "*.ctxprg", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return programs.Count switch
        {
            0 => null,
            1 => programs[0],
            _ => throw new InvalidOperationException("Im Programmordner liegen mehrere .ctxprg-Dateien. Bitte --program-file verwenden oder ein Preset mit eindeutigem Ordner nutzen.")
        };
    }

    private static string BuildConfigurationSummary(ResolvedConfiguration configuration, CliOptions options)
    {
        return
            $"Programm={configuration.ProgramFile}; Verdrahtung={configuration.WiringFolder}; Simulation={configuration.SimulationFolder}; DUT={configuration.DutModelPath}; DutLoop={options.DutLoopIterations}";
    }

    private sealed record ResolvedConfiguration(
        string? ProgramFile,
        string? WiringFolder,
        string? SimulationFolder,
        string? DutModelPath);
}
