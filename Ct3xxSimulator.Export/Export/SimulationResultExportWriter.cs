// Provides Simulation Result Export Writer for the export layer export support.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Ct3xxSimulator.Export;

public static class SimulationResultExportWriter
{
    /// <summary>
    /// Executes write.
    /// </summary>
    public static void Write(string path, SimulationExportDocument document)
    {
        var steps = document.Steps.ToList();
        var logItems = document.Logs.ToList();
        var assetDirectory = PrepareAssetDirectory(path);
        var svgAssets = WriteDiagramAssets(assetDirectory, steps);

        if (Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            PdfSimulationReportWriter.Write(path, document.ConfigurationSummary, steps, logItems);
            return;
        }

        if (Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            WriteCsv(path, steps, logItems, svgAssets);
            return;
        }

        WriteJson(path, document, steps, logItems, svgAssets);
    }

    /// <summary>
    /// Executes WriteCsv.
    /// </summary>
    private static void WriteCsv(string path, IReadOnlyList<SimulationExportStep> stepResults, IReadOnlyList<SimulationExportLogEntry> logs, IReadOnlyDictionary<string, string> svgAssets)
    {
        var builder = new StringBuilder();
        builder.AppendLine("StepName,Outcome,MeasuredValue,LowerLimit,UpperLimit,Unit,Details,ConnectionDiagrams,ConnectionDiagramSvgFile");
        foreach (var step in stepResults)
        {
            builder.AppendLine(string.Join(",",
                Quote(step.StepName),
                Quote(step.Outcome),
                Quote(step.MeasuredValue),
                Quote(step.LowerLimit),
                Quote(step.UpperLimit),
                Quote(step.Unit),
                Quote(step.Details),
                Quote(ConnectionDiagramTextFormatter.FormatStepDiagrams(step)),
                Quote(svgAssets.TryGetValue(step.StepName, out var svgPath) ? svgPath : string.Empty)));
        }

        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);

        var logPath = Path.Combine(
            Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory(),
            $"{Path.GetFileNameWithoutExtension(path)}.logs.csv");
        var logBuilder = new StringBuilder();
        logBuilder.AppendLine("Timestamp,Message");
        foreach (var log in logs)
        {
            logBuilder.AppendLine($"{Quote(log.Timestamp.ToString("O"))},{Quote(log.Message)}");
        }

        File.WriteAllText(logPath, logBuilder.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// Executes WriteJson.
    /// </summary>
    private static void WriteJson(string path, SimulationExportDocument document, IReadOnlyList<SimulationExportStep> stepResults, IReadOnlyList<SimulationExportLogEntry> logs, IReadOnlyDictionary<string, string> svgAssets)
    {
        var payload = new
        {
            exportedAt = document.ExportedAt,
            configurationSummary = document.ConfigurationSummary,
            steps = stepResults.Select(step => new
            {
                step.StepName,
                step.Outcome,
                step.MeasuredValue,
                step.LowerLimit,
                step.UpperLimit,
                step.Unit,
                step.Details,
                traces = step.Traces.Select(trace => new { trace.Title, trace.Nodes }).ToList(),
                connectionDiagrams = ConnectionDiagramTextFormatter.CreateSerializableDiagrams(step),
                connectionDiagramSvg = ConnectionDiagramSvgFormatter.FormatStepSvg(step),
                connectionDiagramSvgFile = svgAssets.TryGetValue(step.StepName, out var svgPath) ? svgPath : null,
                curvePoints = step.CurvePoints.Select(point => new { point.TimeMs, point.Label, point.Value, point.Unit }).ToList()
            }).ToList(),
            logs = logs.Select(log => new { log.Timestamp, log.Message }).ToList()
        };

        File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
    }

    /// <summary>
    /// Executes Quote.
    /// </summary>
    private static string Quote(string? text)
    {
        var value = (text ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{value}\"";
    }

    /// <summary>
    /// Executes PrepareAssetDirectory.
    /// </summary>
    private static string PrepareAssetDirectory(string exportPath)
    {
        var directory = Path.Combine(
            Path.GetDirectoryName(exportPath) ?? Directory.GetCurrentDirectory(),
            $"{Path.GetFileNameWithoutExtension(exportPath)}.assets");
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    /// Executes WriteDiagramAssets.
    /// </summary>
    private static IReadOnlyDictionary<string, string> WriteDiagramAssets(string assetDirectory, IReadOnlyList<SimulationExportStep> stepResults)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < stepResults.Count; index++)
        {
            var step = stepResults[index];
            var fileName = $"{index + 1:000}-{SanitizeFileName(step.StepName)}.svg";
            var fullPath = Path.Combine(assetDirectory, fileName);
            File.WriteAllText(fullPath, ConnectionDiagramSvgFormatter.FormatStepSvg(step), Encoding.UTF8);
            result[step.StepName] = fullPath;
        }

        return result;
    }

    /// <summary>
    /// Executes SanitizeFileName.
    /// </summary>
    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string((value ?? "step").Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}
