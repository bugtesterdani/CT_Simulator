using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Ct3xxProgramParser.Documents;
using Ct3xxProgramParser.FileParsers;
using Ct3xxProgramParser.Model;
using Ct3xxProgramParser.SignalTables;

namespace Ct3xxProgramParser.Parsing;

public sealed class SignalTableParser : ICt3xxFileParser
{
    private static readonly Regex ModuleRegex = new(
        "^:MODULE\\s+'([^']+?)'\\s*(?:,\\s*\"([^\"]*)\")?\\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex AssignmentRegex = new(
        "^\\s*(\\d+)\\s+\"([^\"]*)\"\\s+([^\\s\"]+)\\s+\"([^\"]*)\"\\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Extension => ".ctsit";

    public bool CanParse(string filePath) =>
        !string.IsNullOrWhiteSpace(filePath) &&
        filePath.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    public Ct3xxFileDocument Parse(string filePath, Table? tableDefinition = null)
    {
        var table = ParseTable(filePath);
        return new SignalTableDocument(filePath, tableDefinition, table);
    }

    public SignalTable ParseTable(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path must be provided.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Signal table '{filePath}' was not found.", filePath);
        }

        using var reader = File.OpenText(filePath);
        return Parse(reader, filePath);
    }

    public SignalTable Parse(TextReader reader, string? sourcePath = null)
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        var modules = new List<SignalModule>();
        string? currentName = null;
        string? currentDescription = null;
        List<SignalAssignment>? currentAssignments = null;
        var lineNumber = 0;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.Trim();
            if (trimmed.StartsWith(";", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.StartsWith(":MODULE", StringComparison.OrdinalIgnoreCase))
            {
                if (currentAssignments != null && currentName != null)
                {
                    modules.Add(new SignalModule(currentName, currentDescription, currentAssignments));
                }

                (currentName, currentDescription) = ParseModuleHeader(trimmed, lineNumber);
                currentAssignments = new List<SignalAssignment>();
                continue;
            }

            if (currentAssignments == null || string.IsNullOrWhiteSpace(currentName))
            {
                throw new InvalidDataException($"[{sourcePath ?? "<input>"}:{lineNumber}] Encountered a signal entry before any :MODULE header.");
            }

            var assignment = ParseAssignment(trimmed, lineNumber, sourcePath, currentName);
            currentAssignments.Add(assignment);
        }

        if (currentAssignments != null && currentName != null)
        {
            modules.Add(new SignalModule(currentName, currentDescription, currentAssignments));
        }

        if (modules.Count == 0)
        {
            throw new InvalidDataException($"[{sourcePath ?? "<input>"}] No :MODULE sections found in signal table.");
        }

        return new SignalTable(sourcePath, modules);
    }

    private static (string Name, string? Description) ParseModuleHeader(string line, int lineNumber)
    {
        var match = ModuleRegex.Match(line);
        if (!match.Success)
        {
            throw new InvalidDataException($"[Line {lineNumber}] Invalid MODULE header: '{line}'.");
        }

        var name = match.Groups[1].Value.Trim();
        var description = Normalize(match.Groups[2].Value);
        return (name, description);
    }

    private static SignalAssignment ParseAssignment(string line, int lineNumber, string? sourcePath, string? moduleName)
    {
        var match = AssignmentRegex.Match(line);
        if (!match.Success)
        {
            throw new InvalidDataException($"[{sourcePath ?? "<input>"}:{lineNumber}] Invalid signal entry: '{line}'.");
        }

        if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var channel))
        {
            throw new InvalidDataException($"[Line {lineNumber}] Invalid channel number: '{match.Groups[1].Value}'.");
        }

        var name = Unescape(match.Groups[2].Value);
        var board = match.Groups[3].Value.Trim();
        var comment = Unescape(match.Groups[4].Value);
        return new SignalAssignment(channel, name, board, comment, moduleName);
    }

    private static string? Normalize(string? value)
    {
        var candidate = value;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var nonNull = candidate!;
        var unescaped = Unescape(nonNull);
        return string.IsNullOrWhiteSpace(unescaped) ? null : unescaped;
    }

    private static string Unescape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\"\"", "\"").Replace("\\\"", "\"");
    }
}
