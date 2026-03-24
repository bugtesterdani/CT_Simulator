using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace Ct3xxSimulator.Export;

internal static class ConnectionDiagramTextFormatter
{
    public static string FormatStepDiagrams(SimulationExportStep step)
    {
        var builder = new StringBuilder();
        foreach (var trace in step.Traces)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine(trace.Title);
            builder.AppendLine(string.Join(" -> ", trace.Nodes));
        }

        return builder.ToString().Trim();
    }

    public static IReadOnlyList<object> CreateSerializableDiagrams(SimulationExportStep step)
    {
        return step.Traces
            .Select(trace => (object)new
            {
                trace.Title,
                nodes = trace.Nodes.ToList(),
                diagram = string.Join(" -> ", trace.Nodes)
            })
            .ToList();
    }
}
