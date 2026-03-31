// Provides Pdf Simulation Report Writer for the export layer export support.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Ct3xxSimulator.Export;

internal static class PdfSimulationReportWriter
{
    private const double PageWidth = 595;
    private const double PageHeight = 842;
    private const double Margin = 36;

    /// <summary>
    /// Executes write.
    /// </summary>
    public static void Write(string path, string? configurationSummary, IEnumerable<SimulationExportStep> stepResults, IEnumerable<SimulationExportLogEntry> logs)
    {
        var pages = BuildPages(configurationSummary, stepResults.ToList(), logs.ToList());
        var writer = new MinimalPdfWriter();
        writer.Write(path, pages);
    }

    /// <summary>
    /// Executes BuildPages.
    /// </summary>
    private static List<string> BuildPages(string? configurationSummary, IReadOnlyList<SimulationExportStep> stepResults, IReadOnlyList<SimulationExportLogEntry> logs)
    {
        var pages = new List<string>();
        pages.Add(BuildSummaryPage(configurationSummary, stepResults));
        pages.AddRange(stepResults.Select(BuildStepPage));
        pages.Add(BuildLogPage(logs));
        return pages;
    }

    /// <summary>
    /// Executes BuildSummaryPage.
    /// </summary>
    private static string BuildSummaryPage(string? configurationSummary, IReadOnlyList<SimulationExportStep> stepResults)
    {
        var canvas = new PdfCanvasBuilder();
        var y = PageHeight - Margin;
        canvas.AddText(Margin, y, 18, "CT3xx Simulation Report");
        y -= 24;
        if (!string.IsNullOrWhiteSpace(configurationSummary))
        {
            canvas.AddWrappedText(Margin, y, 10, $"Konfiguration: {configurationSummary}", 520);
            y -= 36;
        }

        canvas.AddText(Margin, y, 13, "Schrittuebersicht");
        y -= 18;
        foreach (var step in stepResults)
        {
            var line = $"{step.StepName} | {step.Outcome} | Wert {step.MeasuredValue} {step.Unit} | Grenzen {step.LowerLimit} .. {step.UpperLimit}";
            canvas.AddWrappedText(Margin, y, 9, line, 520);
            y -= 14;
            if (y < 80)
            {
                break;
            }
        }

        return canvas.Build();
    }

    /// <summary>
    /// Executes BuildStepPage.
    /// </summary>
    private static string BuildStepPage(SimulationExportStep step)
    {
        var canvas = new PdfCanvasBuilder();
        var y = PageHeight - Margin;
        canvas.AddText(Margin, y, 16, step.StepName);
        y -= 22;
        canvas.AddText(Margin, y, 11, $"Ergebnis: {step.Outcome}");
        y -= 14;
        canvas.AddText(Margin, y, 11, $"Wert: {step.MeasuredValue} {step.Unit}");
        y -= 14;
        canvas.AddText(Margin, y, 11, $"Grenzen: {step.LowerLimit} .. {step.UpperLimit}");
        y -= 20;

        if (!string.IsNullOrWhiteSpace(step.Details))
        {
            canvas.AddWrappedText(Margin, y, 9, $"Hinweis: {step.Details}", 520);
            y -= 30;
        }

        canvas.AddText(Margin, y, 12, "Verbindungsbild");
        y -= 22;
        foreach (var trace in step.Traces.Take(3))
        {
            y = DrawTrace(canvas, trace, y);
            y -= 18;
        }

        if (step.CurvePoints.Count > 0)
        {
            canvas.AddText(Margin, 160, 12, "Kurvenpunkte");
            var curveY = 144d;
            foreach (var point in step.CurvePoints.Take(8))
            {
                canvas.AddText(Margin, curveY, 9, $"{point.TimeMs} ms | {point.Label} | {point.Value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-"} {point.Unit}");
                curveY -= 12;
            }
        }

        return canvas.Build();
    }

    /// <summary>
    /// Executes DrawTrace.
    /// </summary>
    private static double DrawTrace(PdfCanvasBuilder canvas, Ct3xxSimulator.Simulation.StepConnectionTrace trace, double top)
    {
        canvas.AddText(Margin, top, 10, trace.Title);
        var y = top - 18;
        var left = Margin;
        const double boxWidth = 120;
        const double boxHeight = 28;
        const double gap = 24;

        foreach (var node in trace.Nodes.Take(4))
        {
            canvas.AddRect(left, y - boxHeight, boxWidth, boxHeight);
            canvas.AddWrappedText(left + 4, y - 14, 8, node, boxWidth - 8);
            left += boxWidth + gap;
            if (left + boxWidth < PageWidth - Margin)
            {
                canvas.AddLine(left - gap + 4, y - boxHeight / 2d, left - 4, y - boxHeight / 2d);
            }
        }

        return y - boxHeight - 8;
    }

    /// <summary>
    /// Executes BuildLogPage.
    /// </summary>
    private static string BuildLogPage(IReadOnlyList<SimulationExportLogEntry> logs)
    {
        var canvas = new PdfCanvasBuilder();
        var y = PageHeight - Margin;
        canvas.AddText(Margin, y, 16, "Protokoll");
        y -= 22;
        foreach (var log in logs.Take(60))
        {
            canvas.AddWrappedText(Margin, y, 8, $"{log.Timestamp:HH:mm:ss} {log.Message}", 520);
            y -= 10;
            if (y < 50)
            {
                break;
            }
        }

        return canvas.Build();
    }

    private sealed class PdfCanvasBuilder
    {
        private readonly StringBuilder _builder = new();

        /// <summary>
        /// Adds the text.
        /// </summary>
        public void AddText(double x, double y, double fontSize, string text)
        {
            _builder.AppendLine("BT");
            _builder.AppendLine($"/F1 {fontSize.ToString("0.##", CultureInfo.InvariantCulture)} Tf");
            _builder.AppendLine($"{x.ToString("0.##", CultureInfo.InvariantCulture)} {y.ToString("0.##", CultureInfo.InvariantCulture)} Td");
            _builder.AppendLine($"({Escape(text)}) Tj");
            _builder.AppendLine("ET");
        }

        /// <summary>
        /// Adds the wrapped text.
        /// </summary>
        public void AddWrappedText(double x, double y, double fontSize, string text, double width)
        {
            var lineLength = Math.Max(20, (int)(width / (fontSize * 0.55)));
            var currentY = y;
            foreach (var line in Wrap(text, lineLength))
            {
                AddText(x, currentY, fontSize, line);
                currentY -= fontSize + 2;
            }
        }

        /// <summary>
        /// Adds the rect.
        /// </summary>
        public void AddRect(double x, double y, double width, double height)
        {
            _builder.AppendLine($"{x.ToString("0.##", CultureInfo.InvariantCulture)} {y.ToString("0.##", CultureInfo.InvariantCulture)} {width.ToString("0.##", CultureInfo.InvariantCulture)} {height.ToString("0.##", CultureInfo.InvariantCulture)} re S");
        }

        /// <summary>
        /// Adds the line.
        /// </summary>
        public void AddLine(double x1, double y1, double x2, double y2)
        {
            _builder.AppendLine($"{x1.ToString("0.##", CultureInfo.InvariantCulture)} {y1.ToString("0.##", CultureInfo.InvariantCulture)} m {x2.ToString("0.##", CultureInfo.InvariantCulture)} {y2.ToString("0.##", CultureInfo.InvariantCulture)} l S");
        }

        /// <summary>
        /// Executes build.
        /// </summary>
        public string Build() => _builder.ToString();

        /// <summary>
        /// Executes Wrap.
        /// </summary>
        private static IEnumerable<string> Wrap(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            var remaining = text.Trim();
            while (remaining.Length > maxLength)
            {
                var split = remaining.LastIndexOf(' ', Math.Min(maxLength, remaining.Length - 1));
                if (split <= 0)
                {
                    split = maxLength;
                }

                yield return remaining[..split].Trim();
                remaining = remaining[split..].Trim();
            }

            if (remaining.Length > 0)
            {
                yield return remaining;
            }
        }

        /// <summary>
        /// Executes Escape.
        /// </summary>
        private static string Escape(string text)
        {
            return (text ?? string.Empty)
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);
        }
    }

    private sealed class MinimalPdfWriter
    {
        private readonly List<string> _objects = new();

        /// <summary>
        /// Executes write.
        /// </summary>
        public void Write(string path, IReadOnlyList<string> pageContents)
        {
            _objects.Clear();
            var fontId = AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

            var pageIds = new List<int>();
            foreach (var content in pageContents)
            {
                var contentBytes = Encoding.ASCII.GetBytes(content);
                var contentId = AddObject($"<< /Length {contentBytes.Length} >>\nstream\n{content}\nendstream");
                var pageId = AddObject($"<< /Type /Page /Parent 0 0 R /MediaBox [0 0 {PageWidth.ToString("0", CultureInfo.InvariantCulture)} {PageHeight.ToString("0", CultureInfo.InvariantCulture)}] /Resources << /Font << /F1 {fontId} 0 R >> >> /Contents {contentId} 0 R >>");
                pageIds.Add(pageId);
            }

            var pagesId = AddObject($"<< /Type /Pages /Count {pageIds.Count} /Kids [{' '}{string.Join(" ", pageIds.Select(id => $"{id} 0 R"))}] >>");
            foreach (var pageId in pageIds)
            {
                ReplaceObject(pageId, _objects[pageId - 1].Replace("/Parent 0 0 R", $"/Parent {pagesId} 0 R", StringComparison.Ordinal));
            }

            var catalogId = AddObject($"<< /Type /Catalog /Pages {pagesId} 0 R >>");

            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
            writer.Write(Encoding.ASCII.GetBytes("%PDF-1.4\n"));
            var offsets = new List<long> { 0 };
            for (var index = 0; index < _objects.Count; index++)
            {
                offsets.Add(stream.Position);
                writer.Write(Encoding.ASCII.GetBytes($"{index + 1} 0 obj\n"));
                writer.Write(Encoding.ASCII.GetBytes(_objects[index]));
                writer.Write(Encoding.ASCII.GetBytes("\nendobj\n"));
            }

            var xrefPosition = stream.Position;
            writer.Write(Encoding.ASCII.GetBytes($"xref\n0 {_objects.Count + 1}\n"));
            writer.Write(Encoding.ASCII.GetBytes("0000000000 65535 f \n"));
            foreach (var offset in offsets.Skip(1))
            {
                writer.Write(Encoding.ASCII.GetBytes($"{offset:D10} 00000 n \n"));
            }

            writer.Write(Encoding.ASCII.GetBytes($"trailer << /Size {_objects.Count + 1} /Root {catalogId} 0 R >>\nstartxref\n{xrefPosition}\n%%EOF"));
        }

        /// <summary>
        /// Executes AddObject.
        /// </summary>
        private int AddObject(string content)
        {
            _objects.Add(content);
            return _objects.Count;
        }

        /// <summary>
        /// Executes ReplaceObject.
        /// </summary>
        private void ReplaceObject(int id, string content)
        {
            _objects[id - 1] = content;
        }
    }
}
