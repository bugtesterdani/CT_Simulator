using System.Text.Json;
using Ct3xxAltiumWireVizExporter.Altium;
using Ct3xxAltiumWireVizExporter.Configuration;
using Ct3xxAltiumWireVizExporter.WireViz;

if (!TryParseArguments(args, out var inputPath, out var outputPath, out var configPath, out var errorMessage))
{
    Console.Error.WriteLine(errorMessage);
    PrintUsage();
    return 1;
}

try
{
    var configuration = ExportConfiguration.Load(configPath!);
    var reader = new AltiumConnectivityCsvReader();
    var records = reader.Read(inputPath!, configuration);
    var document = WireVizExportBuilder.Build(records, configuration);
    var yaml = WireVizYamlWriter.Write(document);

    var outputDirectory = Path.GetDirectoryName(outputPath!);
    if (!string.IsNullOrWhiteSpace(outputDirectory))
    {
        Directory.CreateDirectory(outputDirectory);
    }

    File.WriteAllText(outputPath!, yaml);
    Console.WriteLine($"WireViz written: {outputPath}");
    Console.WriteLine($"Connectors: {document.Connectors.Count}");
    Console.WriteLine($"Connections: {document.Connections.Count}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

static bool TryParseArguments(
    IReadOnlyList<string> args,
    out string? inputPath,
    out string? outputPath,
    out string? configPath,
    out string? errorMessage)
{
    inputPath = null;
    outputPath = null;
    configPath = null;
    errorMessage = null;

    for (var index = 0; index < args.Count; index++)
    {
        var arg = args[index];
        if (Matches(arg, "--input", "-i"))
        {
            inputPath = ReadValue(args, ref index, arg);
        }
        else if (Matches(arg, "--output", "-o"))
        {
            outputPath = ReadValue(args, ref index, arg);
        }
        else if (Matches(arg, "--config", "-c"))
        {
            configPath = ReadValue(args, ref index, arg);
        }
        else if (Matches(arg, "--help", "-h"))
        {
            errorMessage = "Help requested.";
            return false;
        }
        else
        {
            errorMessage = $"Unknown argument: {arg}";
            return false;
        }
    }

    if (string.IsNullOrWhiteSpace(inputPath))
    {
        errorMessage = "Missing required argument --input.";
        return false;
    }

    if (string.IsNullOrWhiteSpace(outputPath))
    {
        errorMessage = "Missing required argument --output.";
        return false;
    }

    if (string.IsNullOrWhiteSpace(configPath))
    {
        errorMessage = "Missing required argument --config.";
        return false;
    }

    return true;
}

static string ReadValue(IReadOnlyList<string> args, ref int index, string option)
{
    if (index + 1 >= args.Count)
    {
        throw new ArgumentException($"Missing value for {option}.");
    }

    index++;
    return args[index];
}

static bool Matches(string arg, string longName, string shortName)
{
    return string.Equals(arg, longName, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(arg, shortName, StringComparison.OrdinalIgnoreCase);
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project Ct3xxAltiumWireVizExporter -- --input <connectivity.csv> --config <export.json> --output <wireviz.yaml>");
    Console.WriteLine();
    Console.WriteLine("CSV columns:");
    Console.WriteLine("  Net, Designator, Pin, [ComponentKind], [PinName]");
    Console.WriteLine();
    Console.WriteLine("Example config:");
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        boardName = "DeviceBoard",
        connectorPrefixes = new[] { "J", "X" },
        roleMappings = new Dictionary<string, string>
        {
            ["J1"] = "device",
            ["J2"] = "harness"
        }
    }, new JsonSerializerOptions { WriteIndented = true }));
}
