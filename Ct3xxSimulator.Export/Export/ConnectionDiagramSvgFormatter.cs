// Provides Connection Diagram Svg Formatter for the export layer export support.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;

namespace Ct3xxSimulator.Export;

internal static class ConnectionDiagramSvgFormatter
{
    /// <summary>
    /// Executes format step svg.
    /// </summary>
    public static string FormatStepSvg(SimulationExportStep step)
    {
        var traces = step.Traces.Count == 0
            ? new[] { new SvgTrace(step.StepName, new[] { "Keine Verbindung verfuegbar" }) }
            : step.Traces.Select(trace => new SvgTrace(trace.Title, trace.Nodes)).ToArray();

        const double boxWidth = 180;
        const double boxHeight = 42;
        const double horizontalGap = 32;
        const double verticalGap = 54;
        const double padding = 24;

        var maxNodes = Math.Max(1, traces.Max(trace => trace.Nodes.Count));
        var width = padding * 2 + (maxNodes * boxWidth) + (Math.Max(0, maxNodes - 1) * horizontalGap);
        var height = padding * 2 + (traces.Length * (boxHeight + verticalGap));
        var builder = new StringBuilder();

        builder.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width.ToString("0", CultureInfo.InvariantCulture)}\" height=\"{height.ToString("0", CultureInfo.InvariantCulture)}\" viewBox=\"0 0 {width.ToString("0", CultureInfo.InvariantCulture)} {height.ToString("0", CultureInfo.InvariantCulture)}\">");
        builder.AppendLine("<rect x=\"0\" y=\"0\" width=\"100%\" height=\"100%\" fill=\"#fbfcfe\" />");

        for (var traceIndex = 0; traceIndex < traces.Length; traceIndex++)
        {
            var trace = traces[traceIndex];
            var top = padding + (traceIndex * (boxHeight + verticalGap));
            var titleY = top + 14;
            builder.AppendLine($"<text x=\"{padding.ToString("0.##", CultureInfo.InvariantCulture)}\" y=\"{titleY.ToString("0.##", CultureInfo.InvariantCulture)}\" font-family=\"Segoe UI\" font-size=\"14\" font-weight=\"600\" fill=\"#1f2937\">{Escape(trace.Title)}</text>");

            var nodeTop = top + 20;
            for (var nodeIndex = 0; nodeIndex < trace.Nodes.Count; nodeIndex++)
            {
                var left = padding + (nodeIndex * (boxWidth + horizontalGap));
                var centerY = nodeTop + (boxHeight / 2d);
                builder.AppendLine($"<rect x=\"{left.ToString("0.##", CultureInfo.InvariantCulture)}\" y=\"{nodeTop.ToString("0.##", CultureInfo.InvariantCulture)}\" rx=\"8\" ry=\"8\" width=\"{boxWidth.ToString("0.##", CultureInfo.InvariantCulture)}\" height=\"{boxHeight.ToString("0.##", CultureInfo.InvariantCulture)}\" fill=\"#ffffff\" stroke=\"#94a3b8\" stroke-width=\"1.4\" />");
                builder.AppendLine($"<text x=\"{(left + 10).ToString("0.##", CultureInfo.InvariantCulture)}\" y=\"{(nodeTop + 24).ToString("0.##", CultureInfo.InvariantCulture)}\" font-family=\"Segoe UI\" font-size=\"11\" fill=\"#111827\">{Escape(trace.Nodes[nodeIndex])}</text>");

                if (nodeIndex >= trace.Nodes.Count - 1)
                {
                    continue;
                }

                var arrowStart = left + boxWidth;
                var arrowEnd = left + boxWidth + horizontalGap;
                builder.AppendLine($"<line x1=\"{(arrowStart + 4).ToString("0.##", CultureInfo.InvariantCulture)}\" y1=\"{centerY.ToString("0.##", CultureInfo.InvariantCulture)}\" x2=\"{(arrowEnd - 10).ToString("0.##", CultureInfo.InvariantCulture)}\" y2=\"{centerY.ToString("0.##", CultureInfo.InvariantCulture)}\" stroke=\"#2563eb\" stroke-width=\"2\" />");
                builder.AppendLine($"<polygon points=\"{(arrowEnd - 10).ToString("0.##", CultureInfo.InvariantCulture)},{(centerY - 5).ToString("0.##", CultureInfo.InvariantCulture)} {(arrowEnd).ToString("0.##", CultureInfo.InvariantCulture)},{centerY.ToString("0.##", CultureInfo.InvariantCulture)} {(arrowEnd - 10).ToString("0.##", CultureInfo.InvariantCulture)},{(centerY + 5).ToString("0.##", CultureInfo.InvariantCulture)}\" fill=\"#2563eb\" />");
            }
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string Escape(string value)
    {
        return SecurityElement.Escape(value) ?? string.Empty;
    }

    private sealed record SvgTrace(string Title, IReadOnlyList<string> Nodes);
}
