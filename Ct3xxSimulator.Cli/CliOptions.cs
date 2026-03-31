// Provides Cli Options for the command-line interface support code.
using System.Text;

namespace Ct3xxSimulator.Cli;

internal sealed class CliOptions
{
    /// <summary>
    /// Gets the program file.
    /// </summary>
    public string? ProgramFile { get; set; }
    /// <summary>
    /// Gets the program folder.
    /// </summary>
    public string? ProgramFolder { get; set; }
    /// <summary>
    /// Gets the wiring folder.
    /// </summary>
    public string? WiringFolder { get; set; }
    /// <summary>
    /// Gets the simulation folder.
    /// </summary>
    public string? SimulationFolder { get; set; }
    /// <summary>
    /// Gets the dut model path.
    /// </summary>
    public string? DutModelPath { get; set; }
    /// <summary>
    /// Gets the export path.
    /// </summary>
    public string? ExportPath { get; set; }
    /// <summary>
    /// Gets the preset file.
    /// </summary>
    public string? PresetFile { get; set; }
    /// <summary>
    /// Gets the preset name.
    /// </summary>
    public string? PresetName { get; set; }
    /// <summary>
    /// Gets the dut loop iterations.
    /// </summary>
    public int DutLoopIterations { get; set; } = 1;
    /// <summary>
    /// Gets the validate only.
    /// </summary>
    public bool ValidateOnly { get; set; }
    /// <summary>
    /// Gets the help requested.
    /// </summary>
    public bool HelpRequested { get; set; }

    /// <summary>
    /// Executes parse.
    /// </summary>
    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        for (var index = 0; index < args.Length; index++)
        {
            var current = args[index];
            var next = index + 1 < args.Length ? args[index + 1] : null;

            switch (current)
            {
                case "--help":
                case "-h":
                case "/?":
                    options.HelpRequested = true;
                    break;
                case "--program-file":
                    options.ProgramFile = RequireValue(current, ref index, next);
                    break;
                case "--program-folder":
                    options.ProgramFolder = RequireValue(current, ref index, next);
                    break;
                case "--wiring-folder":
                    options.WiringFolder = RequireValue(current, ref index, next);
                    break;
                case "--simulation-folder":
                    options.SimulationFolder = RequireValue(current, ref index, next);
                    break;
                case "--dut":
                    options.DutModelPath = RequireValue(current, ref index, next);
                    break;
                case "--export":
                    options.ExportPath = RequireValue(current, ref index, next);
                    break;
                case "--preset-file":
                    options.PresetFile = RequireValue(current, ref index, next);
                    break;
                case "--preset":
                    options.PresetName = RequireValue(current, ref index, next);
                    break;
                case "--dut-loop":
                    options.DutLoopIterations = int.Parse(RequireValue(current, ref index, next));
                    break;
                case "--validate-only":
                    options.ValidateOnly = true;
                    break;
                default:
                    throw new ArgumentException($"Unbekanntes Argument '{current}'.");
            }
        }

        return options;
    }

    /// <summary>
    /// Gets the usage.
    /// </summary>
    public static string GetUsage()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Ct3xxSimulator.Cli");
        builder.AppendLine();
        builder.AppendLine("Verwendung:");
        builder.AppendLine("  dotnet run --project Ct3xxSimulator.Cli -- [Optionen]");
        builder.AppendLine();
        builder.AppendLine("Direkte Pfade:");
        builder.AppendLine("  --program-file <pfad>        Einzelne .ctxprg");
        builder.AppendLine("  --program-folder <pfad>      Ordner mit .ctxprg");
        builder.AppendLine("  --wiring-folder <pfad>       Ordner mit Verdrahtung.yml");
        builder.AppendLine("  --simulation-folder <pfad>   Ordner mit simulation.yaml");
        builder.AppendLine("  --dut <pfad>                 .py, .json, .yaml oder .yml");
        builder.AppendLine();
        builder.AppendLine("Preset aus Desktop-Szenario-Datei:");
        builder.AppendLine("  --preset-file <pfad>         JSON-Datei mit Szenario-Presets");
        builder.AppendLine("  --preset <name>              Name des zu ladenden Presets");
        builder.AppendLine();
        builder.AppendLine("Optional:");
        builder.AppendLine("  --export <pfad>              Export nach .json, .csv oder .pdf");
        builder.AppendLine("  --dut-loop <n>               Anzahl DUT-Loop-Iterationen, Default 1");
        builder.AppendLine("  --validate-only              Nur validieren, nicht simulieren");
        builder.AppendLine("  --help                       Hilfe anzeigen");
        builder.AppendLine();
        builder.AppendLine("Exit-Codes:");
        builder.AppendLine("  0 = alle Schritte PASS / validiert");
        builder.AppendLine("  1 = mindestens ein Schritt FAIL");
        builder.AppendLine("  2 = Validierungsfehler oder Laufzeitfehler");
        return builder.ToString();
    }

    /// <summary>
    /// Executes RequireValue.
    /// </summary>
    private static string RequireValue(string argument, ref int index, string? next)
    {
        if (string.IsNullOrWhiteSpace(next))
        {
            throw new ArgumentException($"Fuer '{argument}' fehlt ein Wert.");
        }

        index++;
        return next;
    }
}
