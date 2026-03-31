using System.Globalization;
using System.Text;
using Ct3xxTestRunLogParser.Model;

namespace Ct3xxTestRunLogParser.Parsing;

/// <summary>
/// Parses optional CSV exports of historical CT3xx test runs into a structured import model.
/// </summary>
public sealed class TestRunLogCsvParser
{
    private static readonly string[] ExpectedHeaders =
    {
        "Lauf ID",
        "Testzeit",
        "Seriennummer",
        "Bezeichnung",
        "Message",
        "Untere Grenze",
        "Wert",
        "Obere Grenze",
        "Ergebnis",
    };

    /// <summary>
    /// Parses one CSV file from disk.
    /// </summary>
    /// <param name="path">The CSV file to parse.</param>
    /// <returns>The structured imported test run.</returns>
    public ImportedTestRun ParseFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A CSV path is required.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The CSV file could not be found.", fullPath);
        }

        using var reader = new StreamReader(fullPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return Parse(reader, fullPath);
    }

    /// <summary>
    /// Parses one CSV stream.
    /// </summary>
    /// <param name="reader">The text reader that provides semicolon-separated CSV rows.</param>
    /// <param name="sourcePath">The source description used in the result.</param>
    /// <returns>The structured imported test run.</returns>
    public ImportedTestRun Parse(TextReader reader, string sourcePath = "<memory>")
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        var rows = ReadRows(reader);
        if (rows.Count == 0)
        {
            throw new InvalidDataException("The CSV file does not contain any rows.");
        }

        var header = rows[0];
        ValidateHeader(header);

        var steps = new List<ImportedTestRunStep>();
        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            steps.Add(ParseStep(rowIndex + 1, row));
        }

        var runId = steps.Select(step => step.RunId).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var serialNumber = steps.Select(step => step.SerialNumber).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return new ImportedTestRun(sourcePath, header, steps, runId, serialNumber);
    }

    /// <summary>
    /// Executes ParseStep.
    /// </summary>
    private static ImportedTestRunStep ParseStep(int rowNumber, IReadOnlyList<string> row)
    {
        var runId = NormalizeCell(row[0]);
        var testTime = NormalizeCell(row[1]);
        var serialNumber = NormalizeCell(row[2]);
        var description = NormalizeCell(row[3]) ?? string.Empty;
        var message = NormalizeCell(row[4]);
        var rawLower = NormalizeCell(row[5]);
        var rawMeasured = NormalizeCell(row[6]);
        var rawUpper = NormalizeCell(row[7]);
        var result = NormalizeCell(row[8]) ?? string.Empty;

        var lower = TryParseNullableDouble(rawLower);
        var measured = TryParseNullableDouble(rawMeasured);
        var upper = TryParseNullableDouble(rawUpper);
        var kind = ClassifyStep(description, message, lower, measured, upper);

        return new ImportedTestRunStep(
            rowNumber,
            runId,
            testTime,
            serialNumber,
            description,
            message,
            rawLower,
            rawMeasured,
            rawUpper,
            result,
            kind,
            lower,
            measured,
            upper);
    }

    /// <summary>
    /// Executes ClassifyStep.
    /// </summary>
    private static ImportedTestRunStepKind ClassifyStep(string description, string? message, double? lower, double? measured, double? upper)
    {
        if (lower.HasValue || measured.HasValue || upper.HasValue)
        {
            return ImportedTestRunStepKind.Measurement;
        }

        var normalizedDescription = description.Trim();
        var normalizedMessage = (message ?? string.Empty).Trim();

        if (normalizedDescription.IndexOf("wait", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalizedDescription.IndexOf("warte", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalizedMessage.IndexOf("waited for", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return ImportedTestRunStepKind.Wait;
        }

        if (normalizedDescription.IndexOf("information", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalizedDescription.IndexOf("ict", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalizedDescription.IndexOf("ft", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return ImportedTestRunStepKind.Information;
        }

        if (normalizedDescription.IndexOf("command", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalizedDescription.IndexOf("assigned to variable", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalizedMessage.IndexOf("assigned to variable", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalizedMessage.IndexOf("has been finished with code", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalizedDescription.IndexOf("ausgabe signal", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return ImportedTestRunStepKind.Action;
        }

        if (!string.IsNullOrWhiteSpace(normalizedDescription))
        {
            return ImportedTestRunStepKind.Step;
        }

        return ImportedTestRunStepKind.Unknown;
    }

    /// <summary>
    /// Executes ReadRows.
    /// </summary>
    private static List<string[]> ReadRows(TextReader reader)
    {
        var rows = new List<string[]>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            rows.Add(line.Split(';'));
        }

        return rows;
    }

    /// <summary>
    /// Executes ValidateHeader.
    /// </summary>
    private static void ValidateHeader(IReadOnlyList<string> header)
    {
        if (header.Count != ExpectedHeaders.Length)
        {
            throw new InvalidDataException($"Unexpected CSV column count. Expected {ExpectedHeaders.Length}, got {header.Count}.");
        }

        for (var index = 0; index < ExpectedHeaders.Length; index++)
        {
            if (!string.Equals(NormalizeCell(header[index]), ExpectedHeaders[index], StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Unexpected CSV header at column {index + 1}. Expected '{ExpectedHeaders[index]}', got '{header[index]}'.");
            }
        }
    }

    /// <summary>
    /// Executes NormalizeCell.
    /// </summary>
    private static string? NormalizeCell(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value!.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[trimmed.Length - 1] == '\'')
        {
            trimmed = trimmed.Substring(1, trimmed.Length - 2);
        }

        return trimmed.Trim();
    }

    /// <summary>
    /// Executes TryParseNullableDouble.
    /// </summary>
    private static double? TryParseNullableDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant))
        {
            return invariant;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.GetCultureInfo("de-DE"), out var german))
        {
            return german;
        }

        return null;
    }
}
